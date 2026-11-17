# Feature Specification: Kịch bản "Mùa Tết — mã khỏe" — chọn giỏ 10 mã và bắn tin (đợt 1)

**Feature Branch**: `020-mua-tet` (làm trên `master` như spec 012–019)

**Created**: 2026-11-17

**Status**: Đã làm xong đợt 1 — **owner chỉ đạo làm ngay 17/11/2026, bỏ quy trình Spec Kit đầy đủ** (không có plan/tasks/checklist riêng); spec này ghi lại luật đã cài để đợt 2 có mốc so.

**Input**: Owner (17/11/2026): "Kịch bản 'Mùa Tết — mã khỏe' — tính giỏ 10 mã và bắn tin trong hôm nay (đợt 1, không đổi DB)". Nguồn luật: [`docs/features/v4-research/02-phat-hien.md`](../../docs/features/v4-research/02-phat-hien.md) mục **P4-19**.

## Luật sản phẩm (đúng như backtest P4-19)

Nguồn dữ liệu: nến ngày trong `Stocks.HistoryJson`, **chỉ mã active**, **không dùng nến của phiên đang chạy** (trước 15:00 ngày giao dịch thì cắt ở phiên trước).

1. **Vũ trụ**: mã có ≥ 250 phiên; giá đóng cửa phiên cuối ≥ 3 (nghìn đồng); giá trị giao dịch TB 20 phiên `TV20 = Σ(close × volume) / n / 10⁶` (tỷ đồng) ≥ 1. Giữ **100 mã TV20 cao nhất**.
2. **Điểm `gan_dinh`** = giá đóng cửa phiên cuối ÷ giá cao nhất (`high`) của 250 phiên gần nhất, tính cả phiên cuối.
3. **Xếp hạng**: `gan_dinh` giảm dần, lấy lần lượt (**BUSINESS-RULE: tối đa 2 mã/ngành** theo `Stocks.Sector`), đủ **10 mã** thì dừng.
4. **Chặn bẫy đơn vị**: loại hẳn mã có giá ≥ 500 trong khi **trung vị giá đóng cửa của toàn bộ lịch sử (chỉ nến < 500)** < 100 — tức mã lưu lẫn đơn vị (970 thay vì 0,97). Trung vị phải tính trên toàn lịch sử, không phải riêng 250 phiên cuối: mã "lên đơn vị đồng" trọn 250 phiên cuối vẫn bị bắt.
5. **Không** dùng FireAnt/KBS để chọn giỏ — luật được kiểm trên nến ngày đã lưu.

**Giả định lô 100 (AI đề xuất, chờ owner chốt):** vốn `V3:MuaTet:VonBanDau` (mặc định 40.000.000 đ), 1 chỗ = vốn ÷ 10. Mã có 1 lô (100 cổ × giá) > 1,5 × 1 chỗ **vẫn ở trong giỏ**, chỉ gắn cờ `vuot_cho = true` và ghi "1 lô = X đ". Số lô gợi ý = làm tròn (1 chỗ ÷ (giá × 100)), tối thiểu 1.

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
| Test | `backend/StockRadar.Tests/MuaTet/MuaTetChonGioTests.cs` — top 100 thanh khoản, tối đa 2 mã/ngành, đủ 10 mã, loại lẫn đơn vị, không dùng nến phiên đang chạy, cờ `vuot_cho` + số lô |

Không tạo bảng, không migration, không Quartz job, không đổi luật/job V3 đang chạy.

## Đợt 2 — chưa làm (cần owner duyệt riêng vì có migration)

- Lưu giỏ đã mua (bảng mới + migration).
- Theo dõi giá trị giỏ hằng ngày.
- Tự bắn tin "bán cả giỏ" khi giá trị ≤ 90% vốn.
- Tin nhắc bán giữa tháng 3.

## Kiểm chứng

- `dotnet build StockRadar.slnx` sạch; `dotnet test StockRadar.Tests` 562 pass / 0 fail (trước đợt này 555).
- Đối chiếu bắt buộc: giỏ tham chiếu bằng script nghiên cứu (dữ liệu xuất 16/11, nến tới **13/11/2026**): MSB, NAB, BAF, VPI, VIC, ORS, SBT, PET, PHP, FRT. Chạy hàm C# trên bộ dữ liệu xuất đó → **trùng đúng 10 mã, đúng thứ tự** (test tạm, đã xoá).
- Chạy trên DB local (dữ liệu tới 16/11, có nến 17/11 đang chạy): 9/10 mã trùng; **VTZ vào, PHP ra** — vì DB local để `PHP.IsActive = 0` và nến PHP dừng ở 11/11. Đây là khác dữ liệu, không phải khác luật.
- Giới hạn như P4-19: 10 mùa, kỳ vọng thực tế +10 … +15%. Kết quả backtest, không phải lời khuyên đầu tư.
