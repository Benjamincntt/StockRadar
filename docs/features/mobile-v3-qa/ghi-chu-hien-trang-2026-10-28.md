# Ghi chú hiện trạng — sửa lỗi UI/UX mobile V3 (QA 28/10/2026)

> File này để **tiếp tục làm việc ngày hôm sau**. Trạng thái ghi nhận lúc 17:40, 28/10/2026.
> Repo: `D:\Source\StockRadar`, nhánh `master` @ `dc936d2`. **Chưa commit gì.**

## 1. Đang dừng ở đâu

- Đợt 1 (BUG-01→05), Đợt 2 (P2 BUG-06→18), Đợt 3 (P3 gộp) đã sửa xong trong code.
- SQL dữ liệu thử đã **owner duyệt có điều kiện** → đã chạy phần chèn, test máy, rồi chạy phần xoá. DB local **sạch** (0/0 dòng thử).
- Test máy lượt A đã xong trên bản debug cài lúc **17:32** (chứa toàn bộ sửa đổi, kể cả sửa layout phát sinh khi test).
- **DỪNG chờ owner: lệnh commit.** Không push, không build APK ship, không đụng production.

## 2. Việc còn lại của ngày mai (theo thứ tự ưu tiên)

1. **Sửa trước khi commit (chặn ship):** `_fmtPrice` ở `hieu_qua_screen.dart` làm tròn giá về số nguyên — sai với dữ liệu thật (xem mục 6, mục 13).
2. Owner quyết định commit: 2 commit ở mục 8; commit `[FIX]` còn thiếu mã lỗi.
3. Kiểm nốt 6 mục chưa kiểm được (mục 5) — cần **tài khoản test để đăng nhập app** cho BUG-13 và BUG-15.
4. Nếu owner đồng ý: chạy `dotnet test` + `flutter analyze` + `flutter test` lần cuối trước commit (kết quả gần nhất ở mục 7).

## 3. Kết quả nghiệm thu SQL (ba lần đếm, DB local `StockRadarDb`)

| Thời điểm | TinHieuV3 | ViTheV3 |
|---|---|---|
| Trước chèn | 0 | 0 |
| Sau chèn (dòng 1→`GO` đầu, dòng 93) | 2 | 2 |
| Sau xoá (chạy riêng dòng 95→109) | 0 | 0 |

- API in đúng: `Da chen 2 tin hieu + 2 vi the thu (TestQA-2026-10-28).`
- Không chạy cả file một lần (phần xoá nằm chung file).
- Kiểm server trong chuỗi kết nối = `localhost` trước khi chạy; không in chuỗi đầy đủ.

## 4. Kết quả test trên máy (device `10AE4G1ZE2000K8`, app debug 17:32)

Ảnh lưu tại `docs/features/mobile-v3-qa/evidence/2026-10-28/fix/`.

| BUG / mục | Kết quả | Ảnh |
|---|---|---|
| BUG-03 "Giữ N phiên" | HPG "Giữ 20 phiên", VNM "Giữ 9 phiên" | `SAU-HQ-03-04-05-hpg-vnm.png` |
| BUG-04 lý do thoát tiếng Việt | "Thoát: Chốt +15% hoảng loạn sâu", "Thoát: Chạm MA" | `SAU-HQ-03-04-05-hpg-vnm.png` |
| BUG-05 cờ hoảng loạn sâu | Badge "Hoảng loạn sâu"; thẻ Chi tiết HPG: "Bán realtime: giá khớp ≥ giá vào +15% · Giữ tối đa 40 phiên" | `SAU-HQ-03-04-05-hpg-vnm.png`, `SAU-SD-05-hoangloan-banline.png` |
| BUG-11 màu theo token | Thắng/+14.8% xanh (`AppColors.positive`), Thua/−1.3% đỏ (`scheme.error`), Chờ đo tím | `SAU-HQ-11-12-testrows.png` |
| HQ-12 tên kịch bản Việt | "KB1 · Hoảng loạn", "N1 · Giải chấp cạn kiệt" | `SAU-HQ-11-12-testrows.png` |
| BUG-12 Sự kiện quyền | Ngày "20/10/2026" (dd/MM/yyyy), label từng trường, PushedPageScaffold | `SAU-SKQ-12-ngay-ddmmyyyy.png`, `SAU-SKQ-12-validation.png` |
| SD-05 bỏ "RS" | "SỨC MẠNH TƯƠNG ĐỐI −3.50%" | `SAU-SD-05-suc-manh-tuong-doi.png` |
| HM-06 nhãn Việt | "Radar trong phiên", "KL≥1 triệu cp" | `SAU-HM-06-home.png` |
| NKL-01 PushedPageScaffold | Form "Thêm lệnh" có nút "Quay lại" chuẩn | chỉ verify qua `uiautomator dump`, **chưa lưu ảnh** |
| NKP-01 / NKP-05 | Picker "Chọn tín hiệu" dùng PushedPageScaffold; lỗi tải hiện message + nút "Thử lại" | `SAU-NKP-13-picker.png` (chụp lúc spinner; nút "Thử lại" xác nhận qua dump) |
| Regression phát sinh khi test | Tên kịch bản dài làm tràn hàng ("RIGHT OVERFLOWED BY 61 PIXELS") → đã tách "Mã kịch bản" ra hàng riêng + `Flexible`/ellipsis, dòng "Thoát: …" cho 2 dòng | trước: `SAU-HQ-12-11-testrows.png` (bản 16:56) · sau: `SAU-HQ-11-12-testrows.png` |

## 5. Chưa kiểm được + lý do + cách kiểm

| Mục | Lý do | Cách kiểm khi làm tiếp |
|---|---|---|
| BUG-13 nhãn Việt trong danh sách picker, NKP-04 màu chip | **đã kiểm 29/10** sau khi owner đăng nhập — xem mục 14 | — |
| BUG-15 tư cách watchlist khi mở Chi tiết | **đã kiểm 29/10** — thêm rồi gỡ HPG, Watchlist về trống | — |
| BUG-11 phần lọc `tu/den` | **Dừng theo luật**: `/v3/hieu-qua/lich-su` chỉ nhận `page/size/status` | Cần backend thêm tham số trước (thay đổi API, ngoài phạm vi đợt này) |
| SH-05 (chạm 48dp + tooltip), SB-04 (tooltip nút xoá) | Tooltip Android không xuất hiện trong screencap qua adb | Quan sát bằng mắt trên máy, hoặc thêm `Semantics`/label để đọc qua dump |
| LG-08 gợn sóng khi đổi tab | Cần mắt quan sát lúc chạm | Xem trực tiếp trên máy |
| HQ-01 spinner ~20s | Không repro trên local (dữ liệu nhỏ) | Cần đo trên production |

## 6. Câu hỏi cần owner chốt

- ~~Giá < 1.000~~ — **đã sửa 29/10** (mục 14): `_fmtPrice` gọi `formatPrice`, giá lẻ hiện đủ 2 chữ số. Không còn là câu hỏi.
- **NKL-01 ảnh:** ~~có cần chụp lại~~ — đã chụp 29/10: `evidence/2026-10-29/fix/SAU-NKL-01-form-day-du.png`.

## 7. Validate đã chạy (kết quả gần nhất)

| Lệnh | Kết quả |
|---|---|
| `dotnet build` (backend) | 0 lỗi |
| `dotnet test` | **284/284 pass** (281 mốc + 3 test BUG-05 mới) |
| `flutter analyze` | No issues found |
| `flutter test` | **9/9 pass** |
| `npm run build` (frontend) | sạch |

## 8. Hai commit đã chốt (chưa chạy)

1. `[FIX][mobile][<mã lỗi>] sửa lỗi UI/UX các màn V3 theo báo cáo QA 28/10/2026` — **chờ owner cho mã lỗi** (luật commit: `[FIX]` bắt buộc có mã); thêm mã có thể phải rút gọn câu cho ≤ 72 ký tự.
   — gồm `mobile/lib`, `mobile/test`, `docs/features/mobile-v3-qa/`; **không** gồm `evidence/`.
2. `[FEAT][v3] tín hiệu V3 trả thêm cờ hoảng loạn sâu từ vị thế`
   — gồm `backend/StockRadar.Application/DTOs/V3Dtos.cs`, `backend/StockRadar.Infrastructure/MarketData/V3HieuQuaService.cs`, `backend/StockRadar.Tests/V3/V3TinHieuHoangLoanSauTests.cs`, `frontend/src/types/index.ts`.

**Loại trừ khỏi mọi commit:** `specs/003-regime-aware-sell-exits/spec.md`, `mobile/screenshots/v2/`, `throne-room-mockup.html`, và toàn bộ `evidence/`.

## 9. Cách dựng lại môi trường test (lệnh đã dùng)

```powershell
# 1) API local (cùng OS, cổng 5280)
cd d:\Source\StockRadar\backend\StockRadar.Api
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet exec bin\Debug\net10.0\StockRadar.Api.dll

# 2) Cầu cổng cho thiết bị
adb reverse tcp:5280 tcp:5280

# 3) Chạy app bản sửa trên máy (không đổi pubspec)
cd d:\Source\StockRadar\mobile
flutter run -d 10AE4G1ZE2000K8 --dart-define=API_BASE=http://localhost:5280/api/v1
#   hoặc: flutter build apk --debug --dart-define=API_BASE=http://localhost:5280/api/v1
#         adb install -r build\app\outputs\flutter-apk\app-debug.apk

# 4) Thu bằng chứng (ảnh qua file + pull, KHÔNG dùng '>' redirect của PowerShell)
adb shell screencap -p /sdcard/s.png ; adb pull /sdcard/s.png <duong-dan>.png
adb shell uiautomator dump /sdcard/ui.xml ; adb pull /sdcard/ui.xml <duong-dan>-tree.xml

# 5) SQL: chạy từng phần, bắt buộc có -I
$env:SQLCMDPASSWORD='<mat-khau-trong-appsettings.Development.json>'
sqlcmd -S localhost -d StockRadarDb -U <tai-khoan-sql-local> -I -f 65001 -i <phan-chen.sql>
sqlcmd -S localhost -d StockRadarDb -U <tai-khoan-sql-local> -I -f 65001 -i <phan-xoa.sql>
```

## 10. Cạm bẫy đã gặp (đọc trước khi làm tiếp)

- **`sqlcmd` mặc định tắt `QUOTED_IDENTIFIER`** → INSERT vào `TinHieuV3` fail (bảng có filtered index), báo `Msg 1934`. Phải thêm `-I`. May là `BEGIN TRAN` rollback nên DB không bẩn.
- **RAM máy là nút thắt:** build fail `Could not start thread DartWorker` / crash `kernel_snapshot_program` khi RAM trống < ~1,5GB. Đã hết sau khi owner giải phóng RAM. SQL Server local + API + Gradle + IDE cùng chạy là nguyên nhân; API local có thể timeout DB ("Connection Timeout Expired" ở post-login) khi máy căng.
- **Cài APK qua `--no-streaming` bị HyperOS chặn** (`INSTALL_FAILED_ABORTED: User rejected permissions`) → dùng `adb install -r` (streamed) và bấm cho phép trên máy.
- **`flutter run` kẹt ở bước "Installing APK"** khi app còn sống → `adb shell am force-stop com.baobiantea.juice` rồi `adb install -r` thủ công nhanh hơn.
- **Ảnh chụp không được lưu bằng `adb exec-out screencap -p > file.png`** trong PowerShell (redirect làm hỏng nhị phân); dùng screencap ra `/sdcard` rồi `adb pull`.
- **Toạ độ chạm:** ảnh screencap được scale về 1080 rộng trong khi thiết bị 1260 → luôn lấy bounds từ `uiautomator dump`, đừng đoán từ ảnh.
- **Đường dẫn API:** controller V3 là `V3KichBanController` với route `api/v1/v3`; **không có** endpoint `hieu-qua/tat-ca` (chỉ `tom-tat`, `lich-su`, `vi-the-dang-giu`). Picker tín hiệu dùng `/nhat-ky/tin-hieu/v3`.
- **Tên field:** backend trả `SoPhienDaGi` (không có "u"); Dart giữ tên `soPhienDaGiu` và parse `json['soPhienDaGi']` — **đúng, không phải lỗi** (bẫy #6).
- **`price_levels_card.dart` không đụng** theo chỉ đạo owner.

## 11. Danh sách file thay đổi hiện tại (`git status`)

**Sửa (backend + web):** `V3Dtos.cs`, `V3HieuQuaService.cs`, `frontend/src/types/index.ts`.
**Sửa (mobile):** `app.dart`, `core/api/api_client.dart`, `core/services/app_services.dart`, `core/theme/app_colors.dart`, `screens/hieu_qua_screen.dart`, `screens/home_screen.dart`, `screens/login_screen.dart`, `screens/nhat_ky_lenh_screen.dart`, `screens/nhat_ky_screen.dart`, `screens/nhat_ky_tin_hieu_picker.dart`, `screens/stock_detail_screen.dart`, `screens/su_kien_quyen_screen.dart`, `screens/watchlist_screen.dart`, `widgets/app_shell.dart`, `widgets/chart_widgets.dart`, `widgets/pushed_page_scaffold.dart`, `widgets/scenario_widgets.dart`, `widgets/stock_search_bar.dart`, `widgets/vnindex_market_card.dart`.
**Thêm (untracked):** `backend/StockRadar.Tests/V3/V3TinHieuHoangLoanSauTests.cs`, `mobile/lib/core/utils/` (`v3_parsers.dart`), `mobile/test/`, `docs/features/mobile-v3-qa/`.
**Ngoài phạm vi (không commit):** `specs/003-regime-aware-sell-exits/spec.md`, `mobile/screenshots/v2/`, `throne-room-mockup.html`.

## 12. Dọn dẹp đã làm cuối phiên

- `adb reverse --remove-all` (danh sách reverse trống).
- API local đã dừng — không còn listener ở cổng 5280.
- DB local: 0/0 dòng thử (`TestQA-2026-10-28`).
- File tạm APK trong `%TEMP%` đã xoá.
- App trên máy đang là bản debug 17:32 (trỏ `http://localhost:5280/api/v1`) — **muốn dùng bình thường trở lại thì cài lại bản ship hoặc build mới trỏ production**.
- Log phiên chạy mobile: `evidence/2026-10-28/flutter-run-B.log`.

## 13. Tổng hợp công việc ngày 28/10/2026 và rà soát của owner

Ghi lúc sáng 29/10/2026, sau khi đọc lại báo cáo cuối của AI làm sửa lỗi. Mục này đè lên các mục trên nếu lệch.

### Việc đã làm trong ngày

| Việc | Kết quả | Trạng thái |
|---|---|---|
| Nghiên cứu luật bán, kịch bản tín hiệu dày (PH-17→PH-20) | Không kịch bản dày nào thắng N3; chốt lời mỏng (+3%) bị loại | commit `f4b84cf` |
| Luật bán riêng cho lệnh mua lúc hoảng loạn sâu (PH-21): chốt +15%, giữ tối đa 40 phiên | Đưa vào V3 + spec 012 | commit `9002d71`, owner đã ship |
| Khối lượng theo lô cho vốn nhỏ (PH-22): làm tròn lô gần nhất, huỷ lệnh chờ mua quá phiên, tin HUỶ | Nghiên cứu + sửa backend | commit `d8e4248`, `dc936d2` — đã đưa lệnh ship, chưa ghi nhận xác nhận ship |
| Kịch bản test UI/UX toàn bộ màn mobile V3 | `kich-ban-test-ui-ux.md` | chưa commit |
| QA trên máy thật, lượt A (API local) + lượt B (production, chỉ đọc) | `bao-cao-2026-10-28.md`: 29 lỗi (3 P0, 3 P1, 11 P2, 11 P3) | chưa commit |
| Sửa đợt 1 (P0/P1), đợt 2 (P2), đợt 3 (P3) + backend BUG-05 | Mục 1–12 ở trên | chưa commit |

### Owner đã chốt trong ngày

- BUG-03: giữ cách đọc `25.5` = 25,5. **Đúng**, vì giá lưu theo nghìn đồng → 25,5 = 25.500 đ. (Ghi chú cảnh báo trước đó "lưu thành 25,5 đồng" là sai, bỏ.)
- BUG-05: cho sửa backend — `TinHieuV3Dto` thêm `LaHoangLoanSau`, chỉ cộng thêm, không migration.
- Đi tiếp đợt 2 + 3; ngoài phạm vi giữ nguyên: chính sách nhãn thô XC-10, biểu đồ khối 2 màn Hiệu quả, lọc `tu/den` (cần đổi API).
- SQL dữ liệu thử: duyệt có điều kiện (chạy từng phần, chỉ DB local) — đã chạy, đã xoá, 0/0.
- `price_levels_card.dart`: không đụng. Rà lại thấy **không màn nào dùng file này** (chữ "Cắt lỗ" không hiện trên app) — xoá hay giữ là việc riêng, owner quyết sau.

### Rà soát báo cáo cuối — chỗ lệch

1. **Lỗi chặn ship — định dạng giá màn Hiệu quả** (mục 6). Giá thật sẽ hiện sai (20,75 → "21"). Phải sửa trước khi commit.
2. **Thiếu mã lỗi cho commit `[FIX]`** (mục 8). Chờ owner cho mã.
3. Mục 9 trước đây ghi tên đăng nhập SQL local — đã thay bằng chỗ giữ chỗ để file commit được.
4. `evidence/2026-10-28/B-HM-top-1400.png` là ảnh chụp nhầm app Zalo — đã xoá sáng 29/10. Toàn bộ `evidence/` không vào git.
5. AI kia báo "đã lưu một kinh nghiệm vào memory" (lỗi `Msg 1934`) — nằm ở memory của công cụ bên đó, chưa kiểm được nội dung. Cạm bẫy này đã có ở mục 10.

### Thứ tự làm tiếp

1. Sửa `_fmtPrice` → dùng `formatPrice`; chạy `flutter analyze` + `flutter test`; xem lại màn Hiệu quả trên máy với dữ liệu có giá lẻ.
2. Owner cho mã lỗi → chốt 2 commit message.
3. Chạy lại `dotnet test` (mốc 284) + `npm run build` lần cuối.
4. Commit tay 2 commit (không để `ship-all.ps1` tự commit — nó gom cả `docs/`, sẽ cuốn theo `evidence/`).
5. Ship: `.\scripts\ship-all.ps1 -SkipCommit` (owner tự chạy). Backend chỉ thêm trường, không migration.
6. Build APK release `.\mobile\build-apk.ps1` (owner tự chạy) — máy đang là bản debug trỏ `localhost`, mở app sẽ không có dữ liệu cho tới khi cài bản mới.
7. Khi có tài khoản test: kiểm nốt BUG-13, NKP-04, BUG-15 (mục 5). → **đã kiểm xong 29/10, xem mục 14.**

## 14. Kết quả 29/10/2026 — sửa định dạng giá + kiểm các mục cần đăng nhập

### Đã sửa

- `hieu_qua_screen.dart` `_fmtPrice`: bỏ đoạn tự làm tròn + nhóm nghìn, gọi `formatPrice` (`widgets/score_pill.dart`); giữ quy ước null/0 → `—`. Bỏ luôn comment mô tả hành vi sai.
- `nhat_ky_lenh_screen.dart`: `giá tín hiệu ${_giaTinHieu!.toStringAsFixed(0)}` → `formatPrice(_giaTinHieu!)`.

### Kiểm trên máy (app debug cài 09:2x, API local, ảnh `evidence/2026-10-29/fix/`)

| Kiểm | Kết quả |
|---|---|
| Giá màn Hiệu quả với dữ liệu thử | HPG "Vào: 15.50 · Thoát: 17.85" (+14.8%, Giữ 20 phiên, badge Hoảng loạn sâu, "Thoát: Chốt +15% hoảng loạn sâu"); VNM "Vào: 75.20 · Thoát: 74.50" (−1.3%, Giữ 9 phiên, "Thoát: Chạm MA"); không tràn hàng |
| Giá trên Chi tiết CK | HPG "20.75" — đúng giá lẻ, không bị làm tròn |
| BUG-05 dòng bán | "Bán realtime: giá khớp ≥ giá vào +15% · Giữ tối đa 40 phiên" |
| BUG-13 nhãn Việt trong picker | "V3 · N1 · Giải chấp cạn kiệt · 02/01/2020 · kết quả: Thua" |
| NKP-04 màu theo token | icon ⊘ `scheme.error` (hàng chưa bỏ qua), `scheme.primary` khi đã bỏ qua |
| Giá sau khi gắn tín hiệu vào form | "giá tín hiệu 15.50" (trước khi sửa: "16") |
| NKL-01 | ảnh `SAU-NKL-01-form-day-du.png` (bổ sung cho mục 6) |
| BUG-15 watchlist | Đăng nhập → Chi tiết HPG → "+ Thêm vào Watchlist" → đổi thành "Đã thêm Watchlist" (mờ); HPG xuất hiện ở tab Watchlist; đã vuốt gỡ, trở về "Chưa có mã nào" |

### Chỗ làm tròn giá còn lại — NGOÀI diff, chờ owner quyết

Trong diff chỉ còn `su_kien_quyen_screen.dart` `v.round()` (số cp được chia — đúng). Ngoài diff, cùng lớp lỗi:

- `nhat_ky_tin_hieu_picker.dart` `_formatGia` — dòng picker hiện "HPG · 16 đ" (giá thật 15,5 nghìn), "VNM · 75 đ" (75,2). Vừa sai làm tròn vừa sai đơn vị "đ".
- `nhat_ky_lenh_screen.dart` hai chỗ `_giaCtrl.text = ...toStringAsFixed(0)` (dòng 53 khi sửa lệnh, dòng 119 khi chọn tín hiệu) — bằng chứng trên máy: ô "Giá khớp (VND/cp)" tự điền **16** cho tín hiệu 15,5.
- `nhat_ky_screen.dart` `_formatVnd` dùng cho `giaKhop` ở 2 chỗ (cũng dùng cho khối lượng và tiền VND — chỉ sai nhánh giá).

Câu hỏi cần chốt trước khi sửa: ô nhập "Giá khớp (VND/cp)" kỳ vọng **VND thật** (20.750) hay **nghìn đồng** (20,75)? `_parse`/`parseSoVn` hiện chấp nhận cả hai ("25.5" → 25,5; "25.500" → 25.500).

### Chưa kiểm được

- SH-05, SB-04 (tooltip): screencap qua adb không chứa tooltip. Ghi chú thêm: ở Watchlist, xoá mã bằng **vuốt trái** (Dismissible), không có nút xoá riêng.
- HQ-01 spinner ~20s: không repro trên local.
- Lọc `tu/den`: vẫn ngoài phạm vi (cần backend thêm tham số).

### Số liệu và dọn dẹp

- Đếm dữ liệu thử: trước chèn **0/0** → sau chèn **2/2** → sau xoá **0/0**.
- `adb reverse --remove-all` (danh sách trống), API local đã dừng (cổng 5280 không còn listener), Watchlist đã về trống.
- Validate: `flutter analyze` 0 vấn đề · `flutter test` 9/9 · `dotnet test` 284/284 · `npm run build` sạch.
- **Chưa commit.** Hai message chốt: `[FIX][mobile] sửa lỗi UI/UX các màn V3 theo báo cáo QA 28/10/2026` và `[FEAT][v3] tín hiệu V3 trả thêm cờ hoảng loạn sâu từ vị thế`.
