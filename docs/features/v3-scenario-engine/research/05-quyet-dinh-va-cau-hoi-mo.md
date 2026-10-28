# 05 — Quyết định của owner và câu hỏi mở

## Quyết định (ghi đúng lời owner, có ngày)

| Ngày | Quyết định | Mức |
|---|---|---|
| 2026-10-23 | Nghiên cứu phải dùng DB production, không dùng DB local | Chốt |
| 2026-10-23 | Không bó buộc khuôn khổ cũ; nếu chưa ưng thì tự sáng tạo kịch bản theo dữ liệu | Chỉ đạo |
| 2026-10-26 | Chọn hướng **P1 — ưu tiên lãi** (không chọn P3 ưu tiên tỉ lệ thắng) | Chốt |
| 2026-10-26 | Chọn **P1++**, bỏ tất cả phần không dùng của V2, làm thành **V3** | Chốt |
| 2026-10-26 | Luật bán phải **realtime trên giá khớp**; bác "xét giá đóng cửa rồi bán ATO phiên sau" | Chốt |
| 2026-10-26 | V3 **bắn Telegram thay thế luôn** V2 (V2 đã cũ) | Chốt |
| 2026-10-26 | Ghi lại toàn bộ nghiên cứu vào repo để liên tục bổ sung | Chốt |
| 2026-10-26 | Nhật ký (spec 010, FR-040): phần "gắn lệnh với tín hiệu" **trỏ sang tín hiệu V3** (`TinHieuV3.Id`), endpoint đổi hẳn thành `nhat-ky/tin-hieu/v3` | Chốt (owner gửi AI thực hiện) |
| 2026-10-26 | Duyệt spec 012 bản 2: tỉ trọng KB1/KB2/N1 = V/5 × 1,5 (30% danh mục); KB1/KB2 tín hiệu từ giá đóng cửa phiên T, tin bắn 08:30 phiên T+1, mua ngay đầu phiên T+1; lệnh bán chỉ giải phóng chỗ/tiền sau khi khớp; bắn thêm tin nhắc ATO cho N3 lúc 08:30 phiên `TDM` = 2 | Chốt |
| 2026-10-26 | Dữ liệu nhật ký đang gắn tín hiệu V2: **giữ nguyên, chỉ đọc** (phương án A). Không xoá, không gỡ liên kết | Chốt (owner gửi AI thực hiện) |
| 2026-10-26 | Chưa xoá bảng `KetQuaKichBan`. Việc xoá làm bằng migration riêng, sau khi xuất lưu lịch sử V2 trên production (`scripts/export-db.ps1`) | Chốt (owner gửi AI thực hiện) |
| 2026-10-26 | DB local: giữ migration `AddV3Tables` đã tự áp khi chạy API local | Chốt (owner gửi AI thực hiện) |
| 2026-10-26 | Không "chạy giả" V3 cho các ngày đã qua để tạo vị thế; V3 bắt đầu với danh mục trống (5 chỗ, tiền mặt = vốn ban đầu `V3:VonBanDau`) | Chốt (owner gửi AI thực hiện) |
| 2026-10-26 | Sắp lại tải dữ liệu V3 (xem mục "Kiến trúc tải dữ liệu" dưới) và nạp sẵn chuỗi chỉ số thị trường từ 01/2023 vào `ChiSoNgayV3` khi deploy | Chốt (owner gửi AI thực hiện) |
| 2026-10-27 | Không đổi luật bán sang chốt cố định +3% (owner: "chốt mỏng thì khó ăn lãi những cổ tăng nhiều lần như đợt sau sự kiện tháng 3 năm 2025") | Chỉ đạo |
| 2026-10-27 | Owner: "Đưa luật PH-21 vào V3, soạn spec và tin giao việc cho AI kia." → spec 012 bổ sung v3 (FR-015a, FR-016a, SC-008) | Chốt (owner gửi AI thực hiện) |
| 2026-10-28 | Lệnh không đủ 1 lô: owner chọn "backtest trước rồi chọn", sau đó chọn **làm tròn tới lô gần nhất, ít nhất 1 lô, không vượt tiền mặt; không đủ 1 lô thì bỏ mã, xét mã kế** (PH-22) → spec 012 FR-020a | Chốt (owner gửi AI thực hiện) |
| 2026-10-28 | Lệnh chờ mua không khớp trong phiên mua: **huỷ cuối phiên mua** → spec 012 FR-014a | Chốt (owner gửi AI thực hiện) |
| 2026-10-28 | Owner: "Giữ tin HUỶ" — bắn tin Telegram khi lệnh chờ mua bị huỷ (không đủ 1 lô lúc khớp / hết phiên mua) → spec 012 Phụ lục E | Chốt |

## Hiện trạng triển khai V3 (cập nhật 2026-10-27)

- Spec: [`specs/012-v3-scenario-engine/spec.md`](../../../../specs/012-v3-scenario-engine/spec.md), bản 2 đã duyệt.
- Code V3 và gỡ V2: AI thực hiện đã làm xong, **chưa commit, chưa deploy**. Theo báo cáo của AI thực hiện: build 0 lỗi, **260/260 test pass**, `flutter analyze` và `npm run build` sạch, grep khoá V2 = 0.
- Đối chiếu trực tiếp code với tài liệu này (AI nghiên cứu kiểm, 26–27/10): luật 5 kịch bản, tỉ trọng, luật bán realtime, xử lý nến giữ chỗ **khớp**; các sửa 1, 3 và ngưỡng bán tính sẵn ở danh sách dưới đã có trong code. Số test, thời gian chạy và số dòng DB là **AI thực hiện tự báo**, chưa đo lại độc lập.
- Đã sửa xong lượt 26/10/2026 (trước khi commit):
  1. T+2.5: đúng phiên T+2 chỉ được bán từ 13:00 (code cũ cho bán cả buổi sáng T+2) — đã có test sáng/chiều phiên T+2.
  2. Chỉ số bình quân đều tính theo `UNI` **của từng phiên trước** (`V3ChiSoToanThiTruong.TinhDiemTheoTruc`, dùng chung cho `Tinh` và `TinhChuoi`) — đã có test.
  3. Mã không có ngành không đủ điều kiện KB4/N1 (`SECR20 = null`, không lấy `RET20` của chính mã) — đã có test.
  4. Sắp lại tải dữ liệu (mục dưới): 16:00 nạp đầy đủ duy nhất, 08:30/14:25 chỉ ~200 mã `UNI` đã lưu, mỗi phút chỉ quote mã đang giữ theo ngưỡng đã lưu.
  5. Nhịp ATC ghép nến hôm nay bằng khối lượng khớp tích luỹ, không phải 0.
  6. Log thời gian chạy từng nhịp (`Stopwatch`) — trả lời Q13 ở dưới (số liệu production cần xem log sau deploy).
  7. Endpoint nạp một lần khi deploy: `POST /api/v1/market/jobs/v3-nap-chi-so?tuNgay=` (`V3:TuNgayNapChiSo`, mặc định 01/01/2023).
- Số đo trên DB local (AI thực hiện báo, 26/10): nạp sẵn chuỗi chỉ số 933 phiên (03/01/2023 → 23/10/2026) mất 2,9 giây, `UNI` cuối 181 mã; nhịp 16:00 mất 2,6 giây (179 mã `UNI` cho phiên 27/10); nhịp ATC 1,4 giây (nạp 181 mã); nhịp mỗi phút 1 ms (ngoài giờ, bỏ qua). Chỉ số tính theo nhịp ATC và theo nhịp 16:00 lệch nhau 0,005%. Nạp sẵn không tạo tín hiệu hay vị thế nào.
- Hai hành vi biên đã chấp nhận:
  - Ngưỡng bán lưu = 0 (chưa tính được MA) → **giữ**, không bán theo giá; ngưỡng trống (NULL) → chỉ cần giá ≥ giá vào + phí. Backtest xử lý như trường hợp 0. Thực tế gần như không xảy ra, vì mã trong `UNI` đã có ≥ 20 phiên.
  - Chưa có danh sách `UNI` đã lưu (DB mới, chưa nạp sẵn) → nhịp nhẹ quay về quét đầy đủ, không mất tín hiệu.
- Migration `AddV3Tables` chỉ tạo 3 bảng mới (đã thêm cột `ViTheV3.GiaThamChieu`, `ViTheV3.Exchange`, `ChiSoNgayV3.DanhSachUni`). API production tự áp migration khi khởi động (`DatabaseInitializer.cs`), nên deploy là bảng V3 có ngay.
- File snapshot của EF đã được tạo lại cho khớp các bảng V3. Hệ quả: entity V2 không còn trong model, nên **migration xoá `KetQuaKichBan` sau này phải viết tay**.
- Việc phải làm khi deploy, **trước 08:30 phiên kế tiếp**:
  1. Kiểm API khởi động và đã áp `AddV3Tables`.
  2. Chạy một lần `POST /api/v1/market/jobs/v3-nap-chi-so`.
  3. Đặt `V3:VonBanDau` trong `appsettings.Production.json` trên server bằng vốn thật, restart service.
  4. Xem log thời gian chạy nhịp 08:30 và ~14:25 để trả lời Q13.

## Kiến trúc tải dữ liệu V3 (chốt 2026-10-26)

Lý do: bản đầu nạp toàn bộ ~1.100 mã kèm `HistoryJson` ở mọi nhịp, kể cả nhịp mỗi phút. Đo trên DB local: đọc hết 526 mã (103 MB) mất 3,2 giây; production ~1.100 mã ước 5–10 giây/lần (chưa đo trên server). Cổng 60–70 mã mà V2 từng có là sơ tuyển của V2, đã gỡ; cổng của V3 là `UNI` (~170–210 mã).

| Nhịp | Việc | Số mã nạp |
|---|---|---|
| 16:00 sau phiên | Quét đầy đủ một lần: danh sách `UNI` cho phiên sau; giá trị chỉ số bình quân đều hôm nay → `ChiSoNgayV3`; ngưỡng bán (TB 9 / 19 giá đóng cửa trước) cho vị thế đang giữ → `ViTheV3` | ~1.100, ngoài giờ |
| 08:30 | KB1/KB2 từ chỉ số, MA50, độ rộng đã lưu; xếp hạng trong `UNI` | ~200 |
| ~14:25 ATC | Giá realtime + tính lại độ rộng, `SECR20`, RS chỉ trong `UNI` | ~200 |
| Mỗi phút | So giá khớp với ngưỡng bán đã lưu | ≤ 5 |

Ngưỡng bán "chạm MAn" cố định suốt phiên (chỉ dùng giá đóng cửa trước hôm nay), nên tính sẵn được: nhịp 16:00 ghi vào `ViTheV3.NguongChamMa` (kèm `GiaThamChieu` + `Exchange` để kiểm trần/sàn mà không cần lịch sử), nhịp mỗi phút gọi `V3LuatBanThucTe.XetTheoNguongDaLuu`. Khi deploy chỉ nạp sẵn **số liệu thị trường** (chuỗi chỉ số, `UNI` theo ngày từ 01/2023 vào `ChiSoNgayV3.DanhSachUni`), không nạp tín hiệu hay vị thế.

## Câu hỏi mở / việc nên nghiên cứu tiếp

| # | Câu hỏi | Vì sao quan trọng |
|---|---|---|
| Q1 | Trượt giá thực tế khi mua ATC / mở cửa các mã 10–30 tỷ/phiên? | Chưa tính trong mọi số liệu |
| Q2 | So với VNINDEX thật | DB chỉ có VNINDEX từ 25/06/2026 |
| Q3 | Lãi thật của "tiền nhàn rỗi sinh lời" rút được ngay | Số danh mục dùng giả định 3–4,5% |
| Q4 | Rủi ro đuôi của KB4 (tệ nhất −31%, mất 6,2% tổng vốn) | Chưa chặn được; nghĩ thêm cách khác cắt lỗ cứng |
| Q5 | N3 quý 4 chỉ thắng 48,5% | Quý 4 là quý định áp dụng |
| Q6 | Chạy thật (forward) có khớp backtest không | Cần đo bằng dữ liệu V3 thật sau khi chạy |
| Q7 | Nến giờ chỉ có từ 28/04/2026 | Không mô phỏng được trong phiên cho giai đoạn dài; nên lưu nến phút/giờ từ nay |
| Q8 | Phân ngành `Stocks.Sector` có đủ đúng cho `SECR20` không | KB4, N1 phụ thuộc trung vị ngành |
| Q9 | Lệch thời điểm dữ liệu (nến giữ chỗ lúc 08:30, nến dở dang trong phiên) | Làm tín hiệu lệch một ngày nếu không xử lý |
| Q10 | Thiên lệch chọn tham số | Bộ lọc N3, tỉ trọng ×1,5, chọn MA10/MA20 đều tìm từ chính dữ liệu này |
| Q11 | V3 chưa có cổng chặn quanh ngày chốt quyền / chia cổ tức như V1 | Phiên rớt giá do quyền có thể bị đếm thành phiên sàn (N1) hoặc giảm sâu (KB4). Backtest cùng dữ liệu nên số vẫn nhất quán, nhưng nên xem xét |
| Q12 | Lợi thế phai dần khi giao dịch tự động / nhiều người dùng chung chiến lược | Dễ phai nhất: N3 (hiệu ứng đầu tháng) và quy luật qua đêm. Ít phai hơn: KB1/KB2/N1 (ăn vào bán ép). Ngoài mẫu 07/2025–10/2026 chưa thấy giảm (KB1 82%, KB2 86%). Theo dõi bằng màn Hiệu quả V3 và trượt giá trong nhật ký |
| Q13 | Thời gian chạy thật của từng nhịp V3 trên production | Đã ghi log `"V3 …: nhịp chạy hết {Ms} ms"` ở cả 4 nhịp + nhịp nạp chuỗi. Local: 16:00 2,6 giây, ATC 1,4 giây. Production chưa đo, đọc log sau deploy |
| Q14 | Vốn ban đầu `V3:VonBanDau` (mặc định 1 tỷ) cần đặt đúng số vốn thật | Tin Telegram tính "Quy mô" theo số này |
