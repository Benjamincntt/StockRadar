# Deploy StockRadar lên server Gdata (cùng máy PhuKienTuiLoc)

StockRadar chạy **song song** với AnTea — API riêng port **5281**, frontend riêng, database **`StockRadarDb`**.

| App | Port nội bộ | URL public (mặc định) |
|-----|-------------|------------------------|
| PhuKienTuiLoc API | 5280 | `https://baobiantea.com/api/` |
| StockRadar API | 5281 | `https://stock.baobiantea.com/api/` |
| StockRadar UI | — | `https://stock.baobiantea.com/` |

---

## Bước 1 — SSH vào server

| Loại | IP |
|------|-----|
| Public (Floating) | `103.226.248.6` |
| LAN (cùng mạng Gdata) | `192.168.200.20` |

```bash
ssh root@103.226.248.6
# hoặc trong LAN:
ssh root@192.168.200.20
```

> Server yêu cầu **SSH key** (không password). Thêm public key vào `/root/.ssh/authorized_keys` trên server.

---

## Bước 2 — Cài lần đầu (tự động)

Server đã có .NET, Node, nginx, SQL từ PhuKienTuiLoc thì chỉ cần:

```bash
git clone https://github.com/Benjamincntt/StockRadar.git /var/www/StockRadar
cd /var/www/StockRadar

DOMAIN=stock.baobiantea.com \
SQL_USER=hieunl \
SQL_PASSWORD='MAT_KHAU_SQL_CUA_BAN' \
bash scripts/gdata-stockradar-setup.sh
```

Test bằng IP LAN trước khi có subdomain:

```bash
DOMAIN=103.226.248.6 bash scripts/gdata-stockradar-setup.sh
```

---

## Bước 3 — Sửa cấu hình Production

```bash
nano /var/www/StockRadar/backend/StockRadar.Api/appsettings.Production.json
```

Bắt buộc sửa:

- `ConnectionStrings:DefaultConnection` — user/password SQL (cùng server PhuKienTuiLoc)
- `Jwt:Secret` — chuỗi bí mật ≥ 32 ký tự
- `MarketData:SyncApiKey` — khóa sync job (giữ bí mật)

Sau đó:

```bash
cd /var/www/StockRadar
bash deploy.sh be
```

Migration DB tự chạy khi API khởi động (`DatabaseInitializer`).

---

## Bước 4 — DNS + HTTPS

1. Trỏ A record `stock.baobiantea.com` → IP public server Gdata
2. SSL:

```bash
certbot --nginx -d stock.baobiantea.com
```

3. Deploy lại:

```bash
cd /var/www/StockRadar && git pull && bash deploy.sh all
```

---

## Deploy cập nhật hàng ngày

```bash
cd /var/www/StockRadar
git pull
bash deploy.sh all          # frontend + backend
# hoặc:
# bash deploy.sh fe
# bash deploy.sh be
```

Từ Windows (push + deploy):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\deploy-remote.ps1 -Action all
```

**JUICE mobile (Flutter):** không build trên server (`deploy.sh apk` / `mobile` — RAM thấp, dễ fail). Build APK trên máy dev:

```powershell
cd mobile
flutter build apk --release
# APK: mobile\build\app\outputs\flutter-apk\app-release.apk
```

API production: `http://103.226.248.6/api/v1` (mặc định trong app).

---

## Auto deploy (GitHub Actions)

Mỗi lần **push lên `master`**, workflow `.github/workflows/deploy.yml` SSH vào server và chạy `deploy.sh`:

| Thay đổi trong commit | Deploy |
|------------------------|--------|
| `frontend/**` | `fe` |
| `backend/**` hoặc `deploy.sh` | `be` |
| Cả hai | `all` |
| Chỉ `mobile/`, `docs/`, … | Bỏ qua |

Chạy tay: GitHub → **Actions** → **Deploy production** → **Run workflow**.

### Bước 1 — SSH key deploy (đã tạo sẵn trên server)

Key dùng cho GitHub Actions nằm tại `/root/.ssh/gh_actions_deploy` trên server (tạo tự động lần đầu chạy script bên dưới).

Nếu muốn tạo tay trên server:

```bash
ssh root@103.226.248.6
ssh-keygen -t ed25519 -f /root/.ssh/gh_actions_deploy -N "" -C "github-actions-stockradar"
cat /root/.ssh/gh_actions_deploy.pub >> ~/.ssh/authorized_keys
chmod 600 /root/.ssh/gh_actions_deploy ~/.ssh/authorized_keys
```

### Bước 2 — Cài GitHub CLI + đăng nhập + đặt secrets (một lệnh)

Trên Windows (cần đăng nhập GitHub trong trình duyệt khi được hỏi):

```powershell
winget install GitHub.cli
cd D:\Source\StockRadar
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\setup-github-deploy-secrets.ps1
```

Script tự: đăng nhập `gh` (device code) → đọc private key từ server → ghi 3 secrets (`SSH_HOST`, `SSH_USER`, `SSH_PRIVATE_KEY`).

Hoặc đặt secrets thủ công trên GitHub (Settings → Secrets → Actions):

| Secret | Giá trị |
|--------|---------|
| `SSH_HOST` | `103.226.248.6` |
| `SSH_USER` | `root` |
| `SSH_PRIVATE_KEY` | Nội dung `ssh root@103.226.248.6 cat /root/.ssh/gh_actions_deploy` |

### Bước 3 — Kích hoạt

```powershell
cd D:\Source\StockRadar
git add .github/workflows/deploy.yml
git commit -m "Add GitHub Actions auto deploy on push to master"
git push origin master
```

Lần push đầu sẽ kích hoạt deploy (nếu secrets đã cấu hình). Xem log: **Actions** tab trên GitHub.

> Server cần `git fetch` được từ GitHub (repo public hoặc đã cấu hình credential trên server).

### Lỗi billing GitHub Actions

Nếu job fail sau **~3 giây**, log ghi:

> *The job was not started because your account is locked due to a billing issue.*

→ Vào **GitHub → Settings → Billing** và xử lý (thẻ hết hạn, vượt quota, v.v.). Cho đến khi mở khóa, **Actions không chạy**.

**Giải pháp thay thế (không cần Actions):** cron trên server — sau mỗi `git push`, server tự `pull` + `deploy.sh` trong vài phút:

```bash
ssh root@103.226.248.6
cd /var/www/StockRadar && git pull
bash scripts/setup-server-auto-deploy.sh
tail -f /var/log/stockradar-auto-deploy.log
```

Mặc định kiểm tra mỗi **5 phút**. Push xong → đợi tối đa 5 phút là lên production.

---

## Kiểm tra sau deploy

```bash
systemctl status stockradar
journalctl -u stockradar -n 50 --no-pager
curl -s http://127.0.0.1:5281/api/v1/market | head
curl -I http://stock.baobiantea.com/
```

| URL | Kỳ vọng |
|-----|---------|
| `/` | HTML SPA |
| `/api/v1/market` | JSON VNINDEX |
| `/hubs/market` | WebSocket negotiate 101 |

---

## Xử lý lỗi

| Triệu chứng | Cách sửa |
|-------------|----------|
| API 502 | `systemctl restart stockradar` → xem `journalctl` |
| DB lỗi | Sửa connection string, đảm bảo `StockRadarDb` tồn tại |
| Log SQL phình | `bash scripts/set-db-recovery-simple.sh` — **RECOVERY SIMPLE** + shrink log (~512MB) |
| Realtime không kết nối | Kiểm tra nginx block `/hubs/` + WebSocket |
| Port trùng | PhuKienTuiLoc dùng 5280 — StockRadar **phải** dùng 5281 |

---

## Chạy Job 1 / pipeline (tùy chọn)

Từ máy có quyền gọi API (header `X-Sync-Key`):

```bash
curl -X POST -H "X-Sync-Key: YOUR_SYNC_KEY" \
  https://stock.baobiantea.com/api/v1/market/jobs/history
```

Hoặc từ repo: `scripts/run-backfill.ps1` (sửa `pipeline-config.json` trỏ `api_base_url` tới domain production).
