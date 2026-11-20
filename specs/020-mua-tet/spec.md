# Feature Specification: Kịch bản "Mùa Tết — mã khỏe" — chọn giỏ 10 mã và bắn tin (đợt 1)

**Feature Branch**: `020-mua-tet` (làm trên `master` như spec 012–019)

**Created**: 2026-11-17

**Status**: Đã làm xong đợt 1 (17/11/2026) và đợt 2a — cảnh báo cắt lỗ (17/11/2026) — **owner chỉ đạo làm ngay, bỏ quy trình Spec Kit đầy đủ** (không có plan/tasks/checklist riêng); spec này ghi lại luật đã cài để đợt sau có mốc so.

**Input**: Owner (17/11/2026): "Kịch bản 'Mùa Tết — mã khỏe' — tính giỏ 10 mã và bắn tin trong hôm nay (đợt 1, không đổi DB)". Nguồn luật: [`docs/features/v4-research/02-phat-hien.md`](../../docs/features/v4-research/02-phat-hien.md) mục **P4-19**.

## Luật sản phẩm (đúng như backtest P4-19)

Nguồn dữ liệu: nến ngày trong `Stocks.HistoryJson`, **chỉ mã active**, **không dùng nến của phiên đang chạy** (trước 15:00 ngày giao dịch thì cắt ở phiên trước).

1. **Vũ trụ**: mã có ≥ 250 phiên; giá đóng cửa phiên cuối ≥ 3 (nghìn đồng); giá trị giao dịch TB 20 phiên `TV20 = Σ(close × volume) / n / 10⁶` (tỷ đồng) ≥ 1. Giữ **100 mã TV20 cao nhất**.
2. **Điểm `gan_dinh`** = giá đóng cửa phiên cuối ÷ giá cao nhất (`high`) của 250 phiên gần nhất, tính cả phiên cuối.
3. **Xếp hạng**: `gan_dinh` giảm dần, lấy lần lượt (**BUSINESS-RULE: tối đa 2 mã/ngành** theo `Stocks.Sector`), đủ **10 mã** thì dừng.
4. **Chặn bẫy đơn vị**: loại hẳn mã có giá ≥ 500 trong khi **trung vị giá đóng cửa của toàn bộ lịch sử (chỉ nến < 500)** < 100 — tức mã lưu lẫn đơn vị (970 thay vì 0,97). Trung vị phải tính trên toàn lịch sử, không phải riêng 250 phiên cuối: mã "lên đơn vị đồng" trọn 250 phiên cuối vẫn bị bắt.
5. **Không** dùng FireAnt/KBS để chọn giỏ — luật được kiểm trên nến ngày đã lưu.

**Lô 100 (owner chốt 17/11/2026):** vốn `V3:MuaTet:VonBanDau` (mặc định 40.000.000 đ), 1 chỗ = vốn ÷ 10. Mã có 1 lô (100 cổ × giá) > 1,5 × 1 chỗ **bị bỏ khỏi giỏ**, lấy mã kế tiếp theo điểm (mã bị bỏ không chiếm suất 2 mã/ngành); danh sách mã bị bỏ trả ở `biBoVuotCho` và in trong tin. Số lô gợi ý = làm tròn (1 chỗ ÷ (giá × 100)), tối thiểu 1.

**Luật mua/bán (hiển thị trong tin, chưa tự động hoá):** mua một lần giữa tháng 11, giữ tới giữa tháng 3 năm sau; bán **cả giỏ** khi giá trị giỏ ≤ 90% vốn ban đầu.

## Phạm vi đợt 1 (đã làm)

| Phần | File |
|---|---|
| Hàm chọn giỏ thuần (không DB, không mạng) | `backend/StockRadar.Domain/Services/MuaTetChonGio.cs` |
| Options / DTO / interface | `Application/Options/MuaTetOptions.cs`, `Application/DTOs/MuaTetDtos.cs`, `Application/Abstractions/IMuaTetService.cs` |
| Service đọc DB + dựng tin | `Infrastructure/MarketData/MuaTetRunner.cs`, `Infrastructure/Notifications/MuaTetTinNhan.cs` |
| Endpoint đọc giỏ | `GET /api/v1/v3/mua-tet/gio` (`V3KichBanController`) |
| Endpoint bắn tin (admin hoặc `X-Sync-Key`) | `POST /api/v1/market/jobs/mua-tet-bao-tin` (`MarketJobsController`) → `IGuiThongBao.GuiHeThongAsync` (Telegram + push như tin V3) |
| Cấu hình | `V3:MuaTet:VonBanDau` (mặc định 40.000.000) |
| Test | `backend/StockRadar.Tests/MuaTet/MuaTetChonGioTests.cs` — top 100 thanh khoản, tối đa 2 mã/ngành, đủ 10 mã, loại lẫn đơn vị, không dùng nến phiên đang chạy, bỏ mã vượt chỗ (không chiếm suất ngành) + số lô |

Không tạo bảng, không migration, không Quartz job, không đổi luật/job V3 đang chạy.

## Đợt 2a — cảnh báo cắt lỗ (đã làm, không đổi DB)

**Quyết định owner 17/11/2026:** "Làm cảnh báo cắt lỗ trước rồi mới mua" — owner chỉ mua giỏ khi app tự canh được giá trị giỏ và báo lúc phải bán.

**Không bảng, không migration.** Giỏ ĐÃ MUA do owner ghi vào cấu hình `V3:MuaTet` sau khi mua:

```json
"MuaTet": {
  "NgayMua": "2026-11-18",
  "GioDaMua": [ { "Symbol": "AAA", "GiaVao": 12.5, "SoCo": 400 } ],
  "NguongCanhBao": 0.95,
  "NguongBan": 0.90,
  "TheoDoiBat": true
}
```

- `GiaVao` tính bằng **nghìn đồng** (cùng đơn vị `Stocks.LastClose`); vốn đã mua = Σ `GiaVao` × `SoCo` × 1000 (đồng). Chưa điền giỏ hoặc `TheoDoiBat = false` → job chỉ ghi log, không gửi tin.
- **Giá hiện tại** = cột `Stocks.LastClose` (KBS sync mỗi 60 giây trong phiên, Job 2 chốt sau phiên). **Không** gọi KBS thêm, **không** đọc `HistoryJson` (lúc 14:20 nến hôm nay chưa chắc đã ghi).
- **Mức báo**: giá trị giỏ ≤ 95% vốn → tin **CẢNH BÁO SỚM**; ≤ 90% → tin **BÁN CẢ GIỎ**. Mỗi mức tối đa 1 tin/ngày (cache trong bộ nhớ, khoá theo ngày + mức); API khởi động lại có thể gửi lại một lần — chấp nhận được vì không có bảng trạng thái.
- **Hai mốc kiểm** (Quartz, giờ VN, thứ Hai–thứ Sáu, ngày giao dịch): **14:20** (giá trong phiên, owner kịp đặt ATC đúng như backtest bán ở giá đóng cửa) và **15:40** (sau Job 2 15:05/15:15/15:30 — giá đóng cửa chính thức). Job đăng ký **ngoài** `ConfigureV3Jobs` nên chạy dù `V3:Enabled = false` (V3 tắt trên production tới giữa tháng 3/2027).
- **Thiếu giá** (mã ngừng giao dịch, `LastClose ≤ 0`): dùng giá vào, đánh cờ `ThieuGia`; nếu vì thế mà tỷ lệ ≤ 90% thì **hạ xuống mức cảnh báo sớm**, không kết luận bán trên dữ liệu khuyết.
- Tin gửi qua `IGuiThongBao.GuiHeThongAsync` (cùng kênh đợt 1): mức báo, giá trị giỏ / vốn (triệu đồng), % so vốn, từng mã (giá vào, giá hiện tại, % lãi lỗ), mã kéo giỏ xuống nhiều nhất, kèm câu "Kết quả theo luật backtest, không phải lời khuyên đầu tư."

| Phần | File |
|---|---|
| Hàm thuần tính vốn/giá trị/tỷ lệ/mức | `backend/StockRadar.Domain/Services/MuaTetTheoDoi.cs` |
| Service đọc `LastClose` + gửi tin | `Infrastructure/MarketData/MuaTetTheoDoiRunner.cs`, `Infrastructure/MarketData/MuaTetChanLap.cs` (cache chặn lặp), `Infrastructure/Notifications/MuaTetTheoDoiTinNhan.cs` |
| Job Quartz + 2 trigger | `Infrastructure/Scheduling/Jobs/MuaTetTheoDoiJob.cs`, đăng ký trong `QuartzSchedulingExtensions` (`mua-tet-theo-doi-trong-phien` 14:20, `mua-tet-theo-doi-sau-phien` 15:40, `VietnamTimeZone`) |
| Endpoint đọc trạng thái (không gửi tin) | `GET /api/v1/v3/mua-tet/theo-doi` (`V3KichBanController`) |
| Endpoint kiểm + gửi (bỏ qua chặn lặp, để owner thử) | `POST /api/v1/market/jobs/mua-tet-kiem-lo` (`MarketJobsController`, quyền như `mua-tet-bao-tin`) |
| Test | `backend/StockRadar.Tests/MuaTet/MuaTetTheoDoiTests.cs` — mức báo ở 100/96/95/90,5/90/85%, mã thiếu giá, vốn theo số cổ, giỏ trống, chặn lặp, nội dung tin |

## Đợt 2b — thẻ "Mùa Tết" trên Home mobile (đã làm, không đổi DB)

**Yêu cầu owner 20/11/2026:** đưa giỏ đã mua lên màn hình chính app mobile, mỗi mã và cả danh mục có % so giá mua, % trong ngày và tiền tăng/giảm. Owner chọn cách thêm số liệu vào API (không để app tự tính).

- `GET /api/v1/v3/mua-tet/theo-doi` thêm trường (chỉ thêm, client cũ không ảnh hưởng):
  - từng mã: `laiLoDong` (giá trị hiện tại − vốn), `giaThamChieu`, `thayDoiTrongNgayPhanTram`, `laiLoTrongNgayDong`;
  - cả giỏ: `laiLoDong`, `ngayPhien`, `laiLoTrongNgayDong`, `thayDoiTrongNgayPhanTram` (= tổng lãi/lỗ trong ngày ÷ giá trị giỏ theo giá tham chiếu).
- **Giá tham chiếu** = giá đóng cửa của nến liền trước nến mới nhất trong `HistoryJson` (nến mới nhất cùng phiên với `LastClose`). `ngayPhien` = ngày nến mới nhất. Trước khi KBS ghi nến hôm nay, cả giá lẫn % trong ngày đều là của phiên trước — app ghi rõ "phiên dd/MM".
- Mã thiếu giá (`ThieuGia`) không có số trong ngày và không cộng vào tổng trong ngày.
- **Giới hạn đã biết:** giá lưu chưa điều chỉnh quyền — đúng ngày giao dịch không hưởng quyền của một mã, % trong ngày của mã đó sẽ sai (ví dụ TCB 29/10/2026 sẽ hiện khoảng −33%). Số so giá mua không bị ảnh hưởng.
- Cảnh báo 95% / 90% và nội dung tin Telegram **không đổi**.
- Mobile: thẻ "Mùa Tết" giữa VNINDEX và "Đang giữ" (`mobile/lib/screens/home_screen.dart`, model `mobile/lib/core/models/mua_tet_theo_doi.dart`), tự làm mới cùng nhịp 2 phút của Home; giỏ trống hiện "Chưa có giỏ đã mua.".

| Phần | File |
|---|---|
| Tính số trong ngày (hàm thuần) | `backend/StockRadar.Domain/Services/MuaTetTheoDoi.cs` — tham số `giaThamChieu` |
| Đọc giá tham chiếu | `IGiaHienTaiRepository.LayGiaThamChieuAsync`, `GiaThamChieuPhien`, `EfStockRepository` |
| Test | `MuaTetTheoDoiTests.cs` (4 test số trong ngày / tiền), `GiaThamChieuRepositoryTests.cs` (2 test) |

## Đợt 2c — giỏ đã mua lấy từ Nhật ký lệnh, sửa được trên app

**Yêu cầu owner 20/11/2026:** có nút và màn hình sửa giỏ trên app, vì giá khớp thật sẽ khác giá dự kiến. Owner chọn cách **dùng Nhật ký lệnh có sẵn** (không thêm bảng riêng), rồi bảo "sửa luôn đi" khi ba câu Q1–Q3 chưa trả lời — AI làm theo phương án mặc định ghi ở mục "Mặc định AI chọn", owner đổi được sau.

### Luật

1. **Ghi lệnh:** lệnh Mua/Bán của giỏ ghi vào Nhật ký lệnh (spec 010) với `nguonTinHieu = "MUATET"`, `maTinHieu` = mã mùa (cấu hình `V3:MuaTet:MaMua`, mặc định `2026-27`). Giá khớp, khối lượng, phí là số thật owner nhập.
   - Không có bảng tín hiệu cho Mùa Tết → **bỏ bước kiểm "tín hiệu còn tồn tại"** với nguồn này; `GiaTinHieuChup` để trống, nên không có số "lệch so tín hiệu".
   - Không đổi cột: `NguonTinHieu` dài tối đa 8 ký tự, `MUATET` có 6. **Không migration, không route mới.**
2. **Tài khoản của giỏ:** cấu hình `V3:MuaTet:TaiKhoan` (Guid người dùng). Cần vì Nhật ký lưu theo từng người dùng, còn job cảnh báo 14:20 / 15:40 chạy không có người đăng nhập. Giống `V3:TaiKhoanDanhMuc` của spec 014.
3. **Giỏ đã mua** = các mã của tài khoản đó có **ít nhất một lệnh Mua nguồn MUATET, đúng mã mùa** và **khối lượng còn giữ > 0**.
   - Giá vào = **giá vốn bình quân** Nhật ký đang tính (đã gồm phí mua). Số cổ = khối lượng còn giữ. Bán bớt thì tự giảm, bán hết thì mã rời giỏ.
   - Ngày mua = ngày khớp (giờ Việt Nam) của lệnh Mua MUATET sớm nhất trong mùa.
   - Giá vốn tính theo **toàn bộ lệnh của mã đó** (Nhật ký gộp theo mã). Cùng mã mà cũng giữ theo nguồn khác thì bị gộp chung — chấp nhận.
4. **Dự phòng:** chưa cấu hình `TaiKhoan` (hoặc sai dạng Guid) hoặc tài khoản chưa có lệnh MUATET của mùa → dùng giỏ trong cấu hình `GioDaMua` như cũ.
5. **Không đổi:** ngưỡng 95% / 90%, chặn lặp tin, nội dung tin Telegram, số trong ngày (đợt 2b), luồng V3 và đối chiếu Nhật ký của V3 (`V3DoiChieuNhatKy` chỉ đọc nguồn V3).
6. **Thống kê Nhật ký:** lệnh MUATET **không** tính vào "số tín hiệu đã gắn / tỷ lệ khớp tín hiệu"; "lệch so tín hiệu" tự bỏ vì không có giá tín hiệu.

### Mặc định AI chọn (owner chưa trả lời)

- Q1 mã mùa: **cố định trong cấu hình** `V3:MuaTet:MaMua` (mặc định `2026-27`), không suy từ ngày.
- Q2: khi tài khoản đã có lệnh MUATET của mùa, **bỏ qua hẳn** `GioDaMua` trong cấu hình.
- Q3: `GET /v3/mua-tet/theo-doi` **giữ không cần đăng nhập** như hiện nay (không làm hỏng thẻ khi app chưa đăng nhập). Ghi lệnh vẫn bắt đăng nhập.

### App mobile

- Thẻ "Mùa Tết": mỗi mã có nút bút (`Icons.edit_outlined`). Đã có lệnh Mua MUATET của mã → mở sửa lệnh gần nhất; chưa có → mở form ghi lệnh Mua điền sẵn mã, nguồn MUATET, mã mùa, số cổ, giá.
- Đầu thẻ có nút **"Ghi lệnh mua"** để thêm mã (form điền sẵn nguồn và mã mùa).
- Chưa đăng nhập → nút đưa sang màn đăng nhập, như thẻ "Đang giữ". Lưu xong thì thẻ tải lại ngay.
- Form Nhật ký hiện nguồn "Mùa Tết · 2026-27".
- API trả thêm `maMua` và `nguonGio` (`NhatKy` / `CauHinh`) để app biết giỏ đang lấy từ đâu.

## Đợt 2d — giỏ gợi ý 5 mã, mỗi ngành 1 mã; danh sách gợi ý trên app

**Owner chốt 20/11/2026:** "5 mã, làm đi" — giỏ gợi ý đổi từ 10 mã / tối đa 2 mã một ngành sang **5 mã / mỗi ngành 1 mã**; app phải có danh sách gợi ý để owner sửa giá rồi ghi lệnh.

- Cấu hình mới `V3:MuaTet:SoMa` (mặc định code 10) và `V3:MuaTet:ToiDaMoiNganh` (mặc định code 2). `appsettings.json` đặt **5 / 1**. Vốn chia đều cho số mã: 40 triệu ÷ 5 = **8 triệu một chỗ**, nên ngưỡng "1 lô vượt 1,5 lần một chỗ" là 12 triệu (giá ≤ 120).
- `MuaTetChonGio.Chon` nhận `soMa`, `toiDaMoiNganh`; `GET /v3/mua-tet/gio` trả thêm `soMa`, `toiDaMoiNganh`.
- Tin Telegram: tiêu đề "Mùa Tết — giỏ N mã", dòng luật và dòng backtest theo cấu hình. Số backtest giỏ 5 mã / 1 mã ngành (10 mùa, cắt cả giỏ 90%): mua 16/11 TB +22,6%, trung vị +18,9%, 9/10 mùa có lãi, tệ nhất −13,3%; mua 20/11 TB +17,6%. Cấu hình khác chưa đo thì tin ghi "Chưa có backtest cho cấu hình giỏ này".
- Giới hạn: giỏ 5 mã phụ thuộc nhiều vào việc chọn trúng mã (rút ngẫu nhiên 5 trong 10 mã: TB +13 … +25%); với giỏ 10 mã, mỗi ngành 1 mã lại kém hơn 2 mã/ngành — kết quả "1 mã/ngành tốt hơn" chưa ổn định ở mọi số mã. Tin cậy thấp–trung bình.
- App: thẻ "Mùa Tết" có phần **Gợi ý** — các mã trong giỏ gợi ý chưa có trong giỏ đã mua, mỗi mã có ngành, giá đóng cửa, số cổ gợi ý (số lô × 100) và nút bút mở form ghi lệnh Mua điền sẵn. Ghi xong mã đó chuyển sang phần đã mua.
- Test: `MuaTetChonGioTests.Gio_5_ma_moi_nganh_1_ma_va_chia_von_cho_5`.

## Đợt 2 — chưa làm

- Lưu giỏ đã mua vào bảng riêng + migration (thay cho cấu hình `GioDaMua`).
- Tin nhắc bán giữa tháng 3.
- Đối chiếu tự động tỷ lệ giỏ với backtest trên nhiều mùa.

## Kiểm chứng

- `dotnet build StockRadar.slnx` sạch; `dotnet test StockRadar.Tests` 582 pass / 0 fail (đợt 1: 562, đợt 2a thêm 19 test). Đợt 2b: 588 pass / 0 fail (thêm 6 test); `flutter analyze` 3 file đổi: không lỗi.
- Đối chiếu bắt buộc: giỏ tham chiếu bằng script nghiên cứu (dữ liệu xuất 16/11, nến tới **13/11/2026**): MSB, NAB, BAF, VPI, VIC, ORS, SBT, PET, PHP, FRT. Chạy hàm C# trên bộ dữ liệu xuất đó → **trùng đúng 10 mã, đúng thứ tự** (test tạm, đã xoá).
- Chạy trên DB local (dữ liệu tới 16/11, có nến 17/11 đang chạy): 9/10 mã trùng; **VTZ vào, PHP ra** — vì DB local để `PHP.IsActive = 0` và nến PHP dừng ở 11/11. Đây là khác dữ liệu, không phải khác luật.
- Giới hạn như P4-19: 10 mùa, kỳ vọng thực tế +10 … +15%. Kết quả backtest, không phải lời khuyên đầu tư.
