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

## Đợt 2 — chưa làm

- Lưu giỏ đã mua vào bảng riêng + migration (thay cho cấu hình `GioDaMua`).
- Tin nhắc bán giữa tháng 3.
- Đối chiếu tự động tỷ lệ giỏ với backtest trên nhiều mùa.

## Kiểm chứng

- `dotnet build StockRadar.slnx` sạch; `dotnet test StockRadar.Tests` 582 pass / 0 fail (đợt 1: 562, đợt 2a thêm 19 test).
- Đối chiếu bắt buộc: giỏ tham chiếu bằng script nghiên cứu (dữ liệu xuất 16/11, nến tới **13/11/2026**): MSB, NAB, BAF, VPI, VIC, ORS, SBT, PET, PHP, FRT. Chạy hàm C# trên bộ dữ liệu xuất đó → **trùng đúng 10 mã, đúng thứ tự** (test tạm, đã xoá).
- Chạy trên DB local (dữ liệu tới 16/11, có nến 17/11 đang chạy): 9/10 mã trùng; **VTZ vào, PHP ra** — vì DB local để `PHP.IsActive = 0` và nến PHP dừng ở 11/11. Đây là khác dữ liệu, không phải khác luật.
- Giới hạn như P4-19: 10 mùa, kỳ vọng thực tế +10 … +15%. Kết quả backtest, không phải lời khuyên đầu tư.
