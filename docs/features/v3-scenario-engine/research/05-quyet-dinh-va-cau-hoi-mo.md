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
| 2026-11-02 | Owner: "Giữ 20%, làm tiếp" — giữ tỉ trọng KB4 = V/5 (20%) và làm tròn lô gần nhất sau khi xem PH-23; làm tiếp dòng khối lượng gợi ý trong tin mua | Chốt |
| 2026-11-02 | Danh mục V3 theo lệnh thật (spec 014): "chỉ có 1 danh mục thôi. Thay"; "nếu không có nhật ký update thì lấy lệnh cũ. có update thì lấy theo Nhật ký giao dịch. Hạn cuối update là cuối phiên chiều."; vốn = `V3:VonBanDau`; lệnh không theo tín hiệu V3: "Không được phép có lệnh đó. bỏ" | Chốt hướng |
| 2026-11-02 | Spec 014 C1–C3, owner: "Đồng ý cả 3" (chấp nhận đề xuất của AI): một tài khoản danh mục trong `V3:TaiKhoanDanhMuc`; hạn nhập nhật ký = trước nhịp 16:00 của phiên khớp, sau hạn không đối chiếu lại; lệnh gắn tín hiệu V3 không có vị thế vẫn cho nhập, danh mục bỏ qua | Chốt |
| 2026-11-02 | Plan 014 P1–P3: vị thế FPT có sẵn — "bạn tự sửa dữ liệu fpt db server luôn" (sửa tay, không làm lần bù trong code); bán một phần: bản đầu chỉ áp khi bán hết (a); tài khoản danh mục ghi bằng Guid (b) | Chốt |
| 2026-11-03 | Trần tỉ trọng khi chỉ số dưới MA50 (PH-25): owner "Giữ nguyên luật" — không đặt trần, V3 vẫn dùng hết tiền theo tỉ trọng V/5 (×1,5 cho KB1/KB2/N1) | Chốt |
| 2026-11-02 | Spec 014, owner: "Nới chiều bán" — bán thật muộn hơn ngày hệ thống bán vẫn được đối chiếu ở nhịp 16:00 của ngày bán thật, nếu phần bán chưa đối chiếu; ngày thoát ghi theo ngày bán thật (FR-010a) | Chốt |
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
| Q4 | Rủi ro đuôi của KB4 (tệ nhất −31%, mất 5,7% danh mục vốn 40 triệu) | Đã thử hai hướng, cả hai **không giảm sụt tối đa** của danh mục: giảm tỉ trọng KB4 (PH-23) và dừng lỗ realtime có điều kiện thị trường (PH-24, 2026-11-02 — dừng chỉ bớt đuôi lệnh −5,7% → −3,7…−4,6%, đổi lại mất 7–49 điểm lãi và số lệnh lỗ ≥10% tăng). Chưa có luật dừng nào đáng dùng. Hướng còn lại: đo cắt lỗ bằng **nến giờ/phút** thật (Q7) khi dữ liệu tích lũy đủ. **2026-11-06 (PH-32):** đo 104 ô / 17 họ cắt lỗ cho cả V3. Không ô nào hơn BASE ngoài mẫu. Đợt sụt tối đa là cú sập 04/2025, sau đó mọi lệnh hồi, nên cắt lỗ từng lệnh không bớt được sụt. Riêng KB4, "cắt 10%" kéo lệnh tệ nhất từ −31% về −14%, đổi lại mất 12 điểm lãi |
| Q5 | N3 quý 4 chỉ thắng 48,5% | Quý 4 là quý định áp dụng |
| Q6 | Chạy thật (forward) có khớp backtest không | Cần đo bằng dữ liệu V3 thật sau khi chạy |
| Q7 | Nến giờ chỉ có từ 28/04/2026 | Không mô phỏng được trong phiên cho giai đoạn dài; nên lưu nến phút/giờ từ nay. **2026-11-06 (đo thử KBS `data_1P/5P/15P/60P`, cửa sổ 1 tuần lùi theo mốc 2023→10/2026):** không mã nào có nến trong phiên trước 05/2026. HPG có cả 4 khung từ tuần 04/05/2026 (tuần 20/04 rỗng). FPT, VCB, SSI, MWG, DGC: tuần 01/06 và tuần 03/08 (FPT) rỗng, tuần 07/09/2026 đã có — độ sâu **khác nhau theo mã**, chỉ vài tháng. Chất lượng không đều: DGC chỉ 85 nến phút/tuần (đủ phải ~1.130), MWG 877–1.098. Kết luận: không backtest trong phiên từ 2023 bằng KBS được; script `kbs_probe/probe.py` (scratchpad, chưa đưa vào `scripts/`). **FireAnt (owner cho chạy, 2026-11-06, token khách như `FireAntLichChotQuyenClient`, mã FPT, 3 tuần 03/2023, 03/2025, 10/2026):** `symbols/FPT/historical-quotes` trả nến **ngày** đủ cả 3 tuần (có thêm `priceAverage`); ba endpoint trong phiên do AI đoán tên (`symbols/FPT/intraday`, `tradingview/history` 1 và 5 phút) đều 404 **kể cả tuần gần nhất** → sai tên endpoint, chưa kết luận được FireAnt có nến phút lịch sử hay không (`kbs_probe/fireant_probe.py`). Dò file JS công khai của fireant.vn (owner cho dò, cùng ngày): biểu đồ lấy nến qua **SignalR** `streaming.fireant.vn`, hàm `GetBars(symbol, resolution, from, to)` (khung "1", "5"…, "D") và `GetIntradayBars(symbol)` cho phiên hiện tại, **không qua REST**. Bảng gói của FireAnt ghi mục "Dữ liệu trong phiên (intraday)": cổ phiếu "không giới hạn" ở gói thấp nhất trong bảng (phái sinh 1 tháng; dài hơn cần gói Chuyên nghiệp/Cao cấp). Đây là lời quảng cáo (`kbs_probe/fireant_endpoints.py`). **Gọi thử `GetBars` (owner cho, cùng ngày; hub `tradestation.fireant.vn/quote-lite`, WebSocket, giao thức JSON, token khách; 12 lệnh):** FPT khung 1 phút có đủ ~1.130 nến/tuần ở các tuần 01/2026, 03/2026, 04/2026, 05/2026, 10/2026; **rỗng** ở 09/2025, 03/2025, 03/2023; HPG 03/2024 rỗng; khung 5, 15, 60 phút ở 2023–2025 cũng rỗng. → FireAnt sâu hơn KBS (FPT có từ ≤ 01/2026 so với 09/2026 ở KBS) nhưng **không có từ 2023**; mốc đầu nằm giữa 09/2025 và 01/2026, chưa dò chính xác, chưa biết là cửa sổ trượt hay mốc cố định. Mới đo 2 mã (`kbs_probe/fireant_getbars.py`) |
| Q8 | Phân ngành `Stocks.Sector` có đủ đúng cho `SECR20` không | KB4, N1 phụ thuộc trung vị ngành |
| Q9 | Lệch thời điểm dữ liệu (nến giữ chỗ lúc 08:30, nến dở dang trong phiên) | Làm tín hiệu lệch một ngày nếu không xử lý |
| Q10 | Thiên lệch chọn tham số | Bộ lọc N3, tỉ trọng ×1,5, chọn MA10/MA20 đều tìm từ chính dữ liệu này |
| Q11 | V3 chưa có cổng chặn quanh ngày chốt quyền / chia cổ tức như V1 | Phiên rớt giá do quyền có thể bị đếm thành phiên sàn (N1) hoặc giảm sâu (KB4). Backtest cùng dữ liệu nên số vẫn nhất quán, nhưng nên xem xét |
| Q12 | Lợi thế phai dần khi giao dịch tự động / nhiều người dùng chung chiến lược | Dễ phai nhất: N3 (hiệu ứng đầu tháng) và quy luật qua đêm. Ít phai hơn: KB1/KB2/N1 (ăn vào bán ép). Ngoài mẫu 07/2025–10/2026 chưa thấy giảm (KB1 82%, KB2 86%). Theo dõi bằng màn Hiệu quả V3 và trượt giá trong nhật ký |
| Q13 | Thời gian chạy thật của từng nhịp V3 trên production | Đã ghi log `"V3 …: nhịp chạy hết {Ms} ms"` ở cả 4 nhịp + nhịp nạp chuỗi. Local: 16:00 2,6 giây, ATC 1,4 giây. Production chưa đo, đọc log sau deploy |
| Q14 | Vốn ban đầu `V3:VonBanDau` (mặc định 1 tỷ) cần đặt đúng số vốn thật | Tin Telegram tính "Quy mô" theo số này |
| Q15 | Có đặt trần tỉ trọng khi chỉ số dưới MA50 không (PH-25) | Ô ổn định: chỉ số < MA50 −3%/−5%, trần 80% — bớt ~2–3 điểm sụt, mất ~30–50 điểm lãi. **Đã trả lời 2026-11-03: giữ nguyên luật** (không đặt trần) |
| Q16 | BREADTH50 của bộ nghiên cứu (`bt.py`) lệch production (PH-28: 0,179 so với 0,2198 ngày 02/11) đúng ở ngưỡng KB1 0,20 | **Đã tìm ra nguyên nhân (2026-11-03): lỗi ở `bt.py`, production đúng.** `MA[50] = C.rolling(50).mean()` trên bảng giá đã pivot cho ra NaN khi mã có phiên trống trong 50 phiên gần nhất → mã đó bị đếm là "không trên MA50". Ngày 02/11: 17/184 mã UNI bị NaN; `bt.py` 33/184 = 0,1793, tính MA50 theo chuỗi nến riêng của từng mã (như production) 39/184 = **0,2120** — khớp đúng snapshot nhịp 08:30 ngày 03/11 (TCB/PNJ/GMD/BID: 0,2120, 184 mã). Số 0,2198 là nhịp ATC 14:25 (giá trong phiên). Hệ quả: backtest đếm độ rộng **thấp hơn** production → KB1 (< 0,20), KB4 (< 0,35), hoảng loạn sâu (< 0,15) bật nhiều hơn thực tế; các mức `C.shift().rolling(n-1)` cho ngưỡng bán cũng có thể NaN với mã có phiên trống. Chưa đo ảnh hưởng lên các số PH đã ghi |
| Q17 | Phí + thuế thật của tài khoản owner | Mọi backtest trừ 0,4% khứ hồi (ước phí môi giới ~0,15%/chiều + thuế bán 0,1%). Owner sẽ cập nhật biểu phí thật sau (2026-11-03); mỗi 0,1% phí thêm làm mỗi lệnh mất đúng 0,1% |
| Q18 | Cột "giá cắt lỗ" trên Home cho mã V3 đang giữ: có hiện không, và hiện theo luật nào (PH-32) | V3 không có luật cắt lỗ, và backtest không ủng hộ thêm luật tự bán. Nếu hiện thì chỉ nên là **mức tham khảo**, ghi rõ T+2.5 + sàn trắng có thể làm giá khớp thấp hơn nhiều. Hai ứng viên ít tốn nhất: I6 "5 phiên chưa hồi +3% → 8%" (−7 điểm lãi, sụt −14,4%) và "chỉ KB4 10%" (−12 điểm lãi). **Chờ owner quyết**; đổi luật bán phải qua Spec Kit |
