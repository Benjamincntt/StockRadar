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
| Realtime không kết nối | Kiểm tra nginx block `/hubs/` + WebSocket |
| Port trùng | PhuKienTuiLoc dùng 5280 — StockRadar **phải** dùng 5281 |

---

## Chạy Job 1 / pipeline (tùy chọn)

Từ máy có quyền gọi API (header `X-Sync-Key`):

```bash
curl -X POST -H "X-Sync-Key: YOUR_SYNC_KEY" \
  https://stock.baobiantea.com/api/v1/market/jobs/history
```

Hoặc dùng script `data-sync/` trên server với `config.json` trỏ `api_base_url` tới domain StockRadar.
