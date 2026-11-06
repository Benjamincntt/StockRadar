# Feature Specification: V3 chấm điểm và đặt ngưỡng bán trên giá đã điều chỉnh theo quyền

**Feature Branch**: `015-v3-rights-adjusted-prices` (làm trên `master` như spec 012–014)

**Created**: 2026-11-05

**Status**: Draft — owner chấp nhận đề xuất của AI cho Q1–Q5 ("Đồng ý cả 5", 05/11/2026); plan đã lập cho US1–US5. Thêm US6 (cổng sắp chốt quyền chuyển từ V1, 05/11/2026), chờ owner trả lời Q6–Q7

**Input**: Owner (05/11/2026), sau khi AI phát hiện tín hiệu KB2 TCB ngày 02/11 sinh ra từ giá chưa điều chỉnh sau ngày chia thưởng: "Viết spec phương án B" — phương án B do AI đề xuất: cho V3 dùng giá đã điều chỉnh theo quyền (MA, RSI, ret20, floor20, ngưỡng bán), sự kiện quyền lấy tự động thay cho file nhập tay, rồi chạy lại backtest.

## Bối cảnh

- V3 tính mọi chỉ báo trên nến **thô** của `Stocks.HistoryJson` (`V3Engine.LayCacMaAsync` → `V3ChiSoToanThiTruong`, `V3BoKichBan`, ngưỡng bán `NguongChamMa` lưu ở nhịp 16:00). Bộ điều chỉnh giá theo quyền (`BoDieuChinhGiaTheoQuyen.TaoDayGiaDieuChinh`, spec 005/006) **chỉ** được V1 dùng (`SignalAnalyzer`); không file V3 nào gọi tới.
- Nguồn sự kiện quyền của bộ điều chỉnh là file nhập tay `Data/su-kien-quyen.json` (`FileNguonSuKienQuyen`). Hệ thống đã có nguồn lịch chốt quyền tự động toàn thị trường từ FireAnt (`INguonLichChotQuyen` / `FireAntLichChotQuyenClient`, `events/search`, kiểu 1 tiền mặt, 2 cổ phiếu, 3 quyền mua) nhưng nguồn này chỉ phục vụ cổng "loại mã sắp chia" của V1, trả mô tả dạng chữ, chưa đưa vào điều chỉnh giá.
- Bằng chứng (đọc DB production 05/11/2026, chỉ đọc):
  - TCB GDKHQ **29/10/2026** thưởng cổ phiếu 50% (CafeF 23/10). Đóng cửa 28/10 = 33,50, mở cửa 29/10 = 22,65. File quyền **không có** TCB.
  - Tín hiệu KB2 TCB 02/11 (`TinHieuV3` Id 3, hạng 1) — snapshot: RSI14 **13,48**, ret20 **−34,2%**, floor20 **1**. Tính lại trên giá đã điều chỉnh: RSI14 **39,7**, ret20 **−1,2%**, floor20 0 → nằm sau PNJ (22,2), GMD (25,4), BID (27,9); KB2 xếp theo RSI14 tăng dần lấy top 5 nên gần như chắc chắn **không** lọt.
  - Vị thế TCB (`ViTheV3` Id 3, 600 cp × 21,40): `NguongChamMa` = **27,07** (MA10 trộn giá cũ ~33 và giá mới ~22); MA10 đã điều chỉnh tới 02/11 ≈ 22,4, tới 04/11 ≈ 22,1. Luật bán chạm MA10 bị vô hiệu tới khoảng 11/11.
  - Cùng lỗi từ 09/2026: GMD (07/10, thưởng 2:1, mở cửa −32%; đã ra KB2 02/11 và 03/11), HDB (09/10, cổ tức 25% + thưởng 5%, −23%), TPB (02/10, cổ tức cổ phiếu 15% + 500 đ, −15%), KDH (07/10, cổ tức 10%, −8%), TCX (17/09, cổ tức 20%, −17%).
- Chỉ số thị trường V3 (`ChiSoNgayV3.ChiSo`) cộng lợi nhuận ngày của các mã UNI nên phiên GDKHQ của mã lớn kéo chỉ số và độ rộng (`Breadth50`, mã dưới MA50) xuống giả — hai số này là cổng của KB1/KB2.
- Bộ nghiên cứu (`docs/features/v3-scenario-engine/research/`, `bt.py`) cũng dùng giá thô: chỉ bỏ lệnh khi phiên đang giữ có bước nhảy vượt biên độ (`BAD`), **không** chặn tín hiệu sinh ra sau ngày quyền. Các PH-01…PH-29 vì vậy lẫn tín hiệu giả kiểu TCB; mức ảnh hưởng chưa đo.

## User Scenarios & Testing

### User Story 1 — Tín hiệu V3 không bật vì khoảng trống cơ học của ngày quyền (Priority: P1)

Mã vừa chia thưởng / trả cổ tức cổ phiếu giảm mạnh ở phiên GDKHQ nhưng cổ đông không mất tiền. V3 chấm KB1/KB2/KB4/N1/N3 trên dãy giá đã điều chỉnh, nên mã đó chỉ bật tín hiệu khi thật sự bị bán ép.

**Why this priority**: đây là lỗi đã gây một lệnh mua thật (TCB 600 cp) theo tín hiệu giả.

**Independent Test**: dựng lại dữ liệu 02/11/2026 với sự kiện TCB 29/10 (pha loãng 1,5) và GMD 07/10 (pha loãng 1,5), chạy chấm KB2 → TCB không còn hạng 1 với RSI14 13,5; RSI14 TCB ≈ 39,7 (±0,5).

**Acceptance Scenarios**:

1. **Given** TCB có sự kiện thưởng 50% GDKHQ 29/10/2026, **When** chấm KB2 phiên 02/11, **Then** RSI14 ≈ 39,7, ret20 ≈ −1,2%, floor20 = 0 (sai số RSI ±0,5, ret20 ±0,5 điểm %).
2. **Given** phiên GDKHQ của một mã, **When** đếm phiên sàn (`floor20`, `laSan`), **Then** phiên đó không bị tính là phiên sàn chỉ vì khoảng trống quyền.
3. **Given** mã không có sự kiện quyền nào, **When** chấm điểm, **Then** mọi chỉ báo trùng khớp kết quả hiện tại (hồi quy).

---

### User Story 2 — Ngưỡng bán của vị thế đang giữ dùng MA đã điều chỉnh (Priority: P1)

Vị thế đang giữ có sự kiện quyền trong cửa sổ MA (trước hoặc trong lúc giữ). Ngưỡng bán `NguongChamMa` tính trên MA đã điều chỉnh, nên luật bán chạm MA vẫn chạy đúng ngay sau ngày quyền.

**Why this priority**: TCB hiện có ngưỡng 27,07 so với giá 21,95 — luật bán bị tắt im lặng.

**Independent Test**: vị thế TCB mua 03/11 giá 21,40; nhịp 16:00 ngày 04/11 tính `NguongChamMa` = max(MA10 đã điều chỉnh, 21,40 × 1,004) ≈ 22,1, không phải 27,07.

**Acceptance Scenarios**:

1. **Given** sự kiện quyền nằm trong 10 phiên trước ngày tính, **When** nhịp 16:00 lưu `NguongChamMa`, **Then** MA dùng dãy đã điều chỉnh.
2. **Given** vị thế mua **trước** GDKHQ và còn giữ qua ngày đó, **When** tới phiên GDKHQ, **Then** giá vào, khối lượng và ngưỡng bán được quy đổi theo Q3 (giá vào theo giá tham chiếu, khối lượng × hệ số pha loãng), và luật bán không bị kích hoạt hoặc tắt chỉ vì khoảng trống quyền.

---

### User Story 3 — Chỉ số thị trường và độ rộng V3 không bị kéo bởi ngày quyền (Priority: P1)

`ChiSoNgayV3.ChiSo`, MA50 của chỉ số, độ lệch so MA50 và `Breadth50` tính trên lợi nhuận ngày đã điều chỉnh, nên KB1/KB2 không bật hoặc tắt chỉ vì một mã lớn vừa chia thưởng.

**Why this priority**: hai số này là cổng thị trường của KB1/KB2 (Q16 cho thấy độ rộng lệch 0,01 đã đổi KB1 bật hay không).

**Independent Test**: tính lại `ChiSoNgayV3` phiên 29/10/2026 có và không có sự kiện TCB; lợi nhuận ngày của TCB dùng trong chỉ số phiên 29/10 là 22,35 / (33,50 / 1,5) − 1 ≈ +0,1%, không phải −33%.

**Acceptance Scenarios**:

1. **Given** TCB thuộc UNI và có sự kiện 29/10, **When** tính chỉ số phiên 29/10, **Then** lợi nhuận ngày của TCB đóng góp ≈ +0,1%.
2. **Given** chuỗi `ChiSoNgayV3` đã lưu từ trước (từ 01/2023, `v3-nap-chi-so`), **When** có sự kiện quyền mới được ghi nhận, **Then** các phiên bị ảnh hưởng 50 phiên gần nhất được tính lại ở nhịp 16:00 cùng ngày (Q4).

---

### User Story 4 — Sự kiện quyền được lấy tự động, thiếu thì có cảnh báo (Priority: P2)

Người vận hành không phải nhập tay từng đợt chia thưởng. Hệ thống lấy sự kiện quyền (ngày GDKHQ, tiền mặt, tỷ lệ cổ phiếu, quyền mua và giá phát hành) từ nguồn tự động; file nhập tay vẫn dùng được để sửa hoặc bổ sung. Mã có bước nhảy giá vượt biên độ sàn mà không khớp sự kiện nào thì bị cảnh báo.

**Why this priority**: file tay thiếu TCB, GMD, HDB, TPB, KDH, TCX trong 2 tháng — điều chỉnh mà không có sự kiện thì vô dụng. Nhưng US1–US3 chạy được với file tay nếu bổ sung đủ.

**Independent Test**: với cửa sổ 09–10/2026, nguồn tự động trả đủ 6 sự kiện TCB, GMD, HDB, TPB, KDH, TCX với hệ số khớp tin CafeF; một mã có bước nhảy −30% không có sự kiện → log cảnh báo nêu mã và ngày.

**Acceptance Scenarios**:

1. **Given** FireAnt có sự kiện thưởng TCB 50%, **When** nhịp nạp sự kiện chạy, **Then** hệ thống ghi sự kiện TCB với ngày GDKHQ 29/10/2026 và hệ số pha loãng 1,5.
2. **Given** cùng một mã và ngày có cả bản ghi file tay và bản ghi tự động, **When** điều chỉnh, **Then** dùng bản file tay (người vận hành sửa được nguồn sai).
3. **Given** mã có mở cửa so với đóng cửa phiên trước vượt biên độ sàn + 1 điểm % và không có sự kiện quyền ngày đó, **When** nhịp 16:00 chạy, **Then** log cảnh báo kèm mã, ngày, % nhảy; mã đó không được vào tín hiệu V3 trong 20 phiên kể từ phiên nhảy (Q2).
4. **Given** nguồn tự động lỗi mạng hoặc đổi định dạng, **When** nạp, **Then** giữ sự kiện đã lưu, log lỗi, không xoá sự kiện cũ.

---

### User Story 5 — Đo lại backtest V3 trên giá đã điều chỉnh (Priority: P3)

Bộ nghiên cứu dùng cùng dãy giá đã điều chỉnh để biết các con số PH-01…PH-29 lệch bao nhiêu vì tín hiệu giả kiểu TCB.

**Why this priority**: không chặn luồng production, nhưng cần để biết luật hiện hành có còn đúng.

**Independent Test**: chạy lại danh mục luật production (`kb4size.py` / `run_kb4`) 2023–2026 trên giá thô và giá đã điều chỉnh, ghi PH mới với số tín hiệu bị bỏ / thêm và chênh lệch lãi, sụt.

**Acceptance Scenarios**:

1. **Given** dữ liệu xuất từ production kèm danh sách sự kiện quyền, **When** chạy bộ đo, **Then** sổ nghiên cứu có mục mới ghi số tín hiệu đổi và chênh lệch danh mục.

---

### User Story 6 — Cổng sắp chốt quyền chuyển từ V1 sang V3 (Priority: P2)

Owner (05/11/2026, khi quyết gỡ V1 theo spec 016): "Cổng sắp chốt quyền giữ hoặc chuyển sang cho v3." V1 có một cổng loại mã đang trong cửa sổ chốt quyền khỏi Top; V3 chưa có. Story này đưa cổng đó vào V3 sau khi đã đo.

Cổng của V1 hiện tại (để chuyển nguyên trạng hoặc làm biến thể):

- Nguồn: `FireAntLichChotQuyenClient.LayMaSapChotQuyenAsync(homNay, LookaheadDays)`, FireAnt `events/search` toàn thị trường, kiểu 1 (tiền mặt), 2 (cổ phiếu), 3 (quyền mua); lấy sự kiện có `recordDate` trong [hôm nay − 45 ngày, hôm nay + `FireAnt:LookaheadDays` (= 7)].
- Chặn khi mã nằm trong khoảng **từ 7 ngày trước `recordDate` (= GDKHQ, theo khảo sát R1) tới ngày thực hiện quyền** (`executionDate`; không có thì tới `recordDate`). Cổ tức tiền mặt thường có ngày thực hiện cách GDKHQ vài tuần, nên cửa sổ chặn có thể dài.
- Đặt ở đầu chuỗi cổng (`DailyAnalysisRunner` → `SmartMoneyMarketContext.CoChiaQuyenSapDen`); nguồn lỗi thì mở cổng (fail-open).

**Why this priority**: lỗi TCB cho thấy V3 cần một lớp phòng vệ quanh ngày quyền; nhưng US1–US3 (giá đã điều chỉnh) đã xử lý gốc của lỗi, nên cổng này cần đo xem có thêm giá trị không hay chỉ cắt bớt lệnh tốt.

**Independent Test**: backtest 2023–2026 danh mục V3 luật production có và không có cổng (các biến thể ở Q6), trên giá đã điều chỉnh (US5), ghi PH mới; chỉ bật trên production biến thể owner chọn sau khi xem số.

**Acceptance Scenarios**:

1. **Given** mã có GDKHQ trong cửa sổ chặn owner chọn, **When** V3 chấm ở nhịp 08:30, 14:25 hoặc 16:00, **Then** mã không thành ứng viên của bất kỳ kịch bản nào; log ghi lý do kèm ngày và loại sự kiện.
2. **Given** FireAnt lỗi hoặc tắt, **When** V3 chấm, **Then** cổng mở (không chặn cả bảng) và log cảnh báo.
3. **Given** vị thế đang giữ, **When** mã vào cửa sổ chặn, **Then** cổng không ép bán (bán vẫn theo luật bán và quy đổi FR-007), trừ khi owner chọn khác ở Q6.

### Edge Cases

- Phiên GDKHQ không có nến (mã không giao dịch): áp hệ số cho phiên giao dịch kế tiếp, như spec 006.
- Nhiều sự kiện cùng ngày (cổ tức tiền + cổ tức cổ phiếu + thưởng, như HDB, TPB): gộp thành một bản ghi; không cộng chồng.
- Sự kiện ghi nhận muộn (sau GDKHQ vài phiên): các chỉ báo và ngưỡng tính từ lúc ghi nhận trở đi dùng dãy mới; tín hiệu đã bắn trước đó giữ nguyên, không bắn lại hoặc huỷ tự động. *Đề xuất của AI.*
- Quyền mua trả tiền: chỉ điều chỉnh từ ngày GDKHQ theo công thức 006, dù owner có thực hiện quyền hay không.
- Giá live trong phiên (nhịp mỗi phút): là giá thô sau quyền; dãy điều chỉnh lùi về quá khứ nên MA hiện tại cùng thang với giá live — không nhân hệ số lên giá live.
- Sự kiện chỉ có tiền mặt nhỏ (vd 500 đ trên giá 30.000 đ): vẫn điều chỉnh theo công thức chung.
- Giá lỗi đơn vị (VPG, DDG, TCD, DFF, QBS, HHG: 1,00 ↔ 900–990): không phải sự kiện quyền; ngoài phạm vi spec này, chỉ không được để bộ cảnh báo US4 coi là thiếu sự kiện rồi im lặng — ghi log riêng. *Đề xuất của AI.*

## Requirements

### Functional Requirements

**Dãy giá dùng cho V3**

- **FR-001**: Mọi chỉ báo giá của V3 MUST tính trên dãy đã điều chỉnh theo quyền (`BoDieuChinhGiaTheoQuyen.TaoDayGiaDieuChinh`, công thức 005/006): MA5/10/20/50, RSI14, ret20, secr20, RS63, RS126, floor20, laSan, đỉnh/đáy dùng cho các kịch bản.
- **FR-002**: TV20 (giá trị giao dịch) và giá live MUST giữ nguyên số thô.
- **FR-003**: `NguongChamMa` lưu ở nhịp 16:00 MUST dùng MA đã điều chỉnh; luật bán `V3LuatBanThucTe` (max(chạm MAn, giá vào × 1,004)) giữ nguyên.
- **FR-004**: Chỉ số thị trường V3 (`ChiSo`, `ChiSoMa50`, `ChiSoDoLechMa50PhanTram`) và `Breadth50` MUST tính trên lợi nhuận ngày và MA50 đã điều chỉnh của từng mã.
- **FR-005**: Nến lưu kho (`Stocks.HistoryJson`), giá trên thẻ mã và chart MUST không bị ghi đè (như 005/006).
- **FR-006**: Mã không có sự kiện quyền MUST cho kết quả trùng khớp hiện tại (hồi quy bằng test).

**Vị thế đang giữ qua ngày quyền**

- **FR-007**: Vị thế V3 còn giữ qua phiên GDKHQ MUST được quy đổi tại phiên đó: giá vào theo công thức giá tham chiếu 005/006, khối lượng × hệ số pha loãng; cổ tức tiền cộng vào lãi % khi đóng (Q3).

**Nguồn sự kiện quyền**

- **FR-008**: Hệ thống MUST lấy sự kiện quyền tự động (ứng viên: FireAnt `events/search`, kiểu 1/2/3) và lưu lại; tỷ lệ và tiền mặt MUST quy về `tienMat`, `heSoPhaLoang`, `soCoCu`/`soCoMoi`/`giaPhatHanh` của 006. Nguồn là FireAnt; cách lấy ngày GDKHQ và tỷ lệ theo Q1. V1 dùng chung nguồn này (Q5).
- **FR-009**: File nhập tay MUST vẫn dùng được và thắng bản tự động khi trùng mã + ngày.
- **FR-010**: Nguồn tự động lỗi MUST không xoá sự kiện đã lưu; log lỗi để vận hành thấy.
- **FR-011**: Nhịp 16:00 MUST log cảnh báo cho mỗi mã UNI có bước nhảy mở cửa vượt biên độ sàn + 1 điểm % mà không khớp sự kiện quyền nào; mã đó MUST không được vào tín hiệu V3 trong 20 phiên kể từ phiên nhảy, trừ khi sự kiện được ghi nhận (Q2).

**Dữ liệu đã lưu**

- **FR-012**: Chuỗi `ChiSoNgayV3` đã lưu MUST được tính lại khi bật tính năng (chạy lại `v3-nap-chi-so` trên dãy đã điều chỉnh) và mỗi nhịp 16:00 tính lại 50 phiên gần nhất khi có sự kiện mới cho mã thuộc UNI (Q4). Không tạo lại tín hiệu hay vị thế cũ.

**Nghiên cứu**

- **FR-013**: Bộ nghiên cứu MUST có cách nạp sự kiện quyền và chạy lại danh mục luật production trên giá đã điều chỉnh; kết quả ghi thành PH mới (US5).

**Cổng sắp chốt quyền (US6)**

- **FR-014**: Trước khi bật cổng, bộ nghiên cứu MUST đo danh mục V3 2023–2026 có và không có cổng, theo từng biến thể ở Q6, trên giá đã điều chỉnh; ghi PH mới. Cần lịch sự kiện FireAnt 2023–2026 (gọi `events/search` theo khoảng ngày; hỏi owner trước khi tải hàng loạt).
- **FR-015**: Khi bật, V3 MUST loại mã trong cửa sổ chặn khỏi ứng viên mọi kịch bản ở cả ba nhịp chấm; dùng lại `INguonLichChotQuyen`/`FireAntLichChotQuyenClient` (spec 016 giữ client này); fail-open khi nguồn lỗi; ngưỡng cửa sổ lấy từ cấu hình, không hardcode.
- **FR-016**: Tín hiệu bị loại vì cổng MUST được ghi lại (log, hoặc trạng thái tín hiệu nếu plan chọn) để đối chiếu sau.

### Key Entities

- **Sự kiện quyền** (đã có dạng giá trị `SuKienQuyen` + file seed): cần nơi lưu bền cho bản lấy tự động (bảng mới hoặc ghi vào file — quyết ở plan), kèm nguồn (tay / tự động) và thời điểm ghi nhận.
- **ChiSoNgayV3**, **ViTheV3**, **TinHieuV3** (đã có): không đổi cột ở P1; số liệu tính lại theo FR-003, FR-004, FR-012.

## Đã làm rõ (owner: "Đồng ý cả 5", 05/11/2026 — chấp nhận đề xuất của AI)

- **Q1 — Nguồn sự kiện**: dùng FireAnt `events/search`. Plan phải khảo sát FireAnt có trường ngày GDKHQ và tỷ lệ dạng số không; nếu không thì suy GDKHQ = phiên giao dịch liền trước ngày chốt (`recordDate`) và đọc tỷ lệ từ tiêu đề. Trước khi bật, đối chiếu kết quả với tin CafeF cho 6 mã ở Bối cảnh (SC-003). *Kết quả khảo sát 05/11 (plan R1–R2)*: `recordDate` của FireAnt chính là ngày GDKHQ (không lùi phiên); tỷ lệ chỉ có trong tiêu đề; khớp 6/6 sự kiện CafeF.
- **Q2 — Mã nhảy giá không rõ sự kiện**: mã UNI có bước nhảy mở cửa vượt biên độ sàn + 1 điểm % mà không khớp sự kiện nào thì **không** được vào tín hiệu V3 trong 20 phiên kể từ phiên nhảy, trừ khi sự kiện được ghi nhận trước đó; kèm log cảnh báo.
- **Q3 — Vị thế giữ qua ngày quyền**: tại phiên GDKHQ, giá vào chia cho hệ số pha loãng (sau khi trừ tiền mặt theo công thức 005/006), khối lượng nhân hệ số pha loãng; cổ tức tiền cộng vào lãi % khi đóng vị thế.
- **Q4 — Tính lại `ChiSoNgayV3`**: mỗi nhịp 16:00, khi có sự kiện mới được ghi nhận cho mã thuộc UNI, tính lại 50 phiên gần nhất của chuỗi chỉ số.
- **Q5 — Phạm vi V1**: V1 dùng chung nguồn sự kiện tự động (file tay vẫn thắng khi trùng); luật V1 không đổi. *Cập nhật 05/11/2026*: V1 sẽ bị gỡ theo spec 016, nên phần này hết hiệu lực khi spec 016 xong.

## Câu hỏi mở cho US6 (chưa chốt)

- **Q6 — Cửa sổ chặn và phạm vi.** Đề xuất của AI: đo ba biến thể rồi owner chọn. (a) Y như V1: từ 7 ngày trước GDKHQ tới ngày thực hiện, cả 3 loại sự kiện. (b) Chỉ quanh GDKHQ: từ 7 ngày trước tới GDKHQ + 1 phiên, cả 3 loại. (c) Như (b) nhưng chỉ sự kiện cổ phiếu và quyền mua (loại 2, 3), bỏ cổ tức tiền vì khoảng trống giá nhỏ và US1 đã điều chỉnh. Cổng chỉ chặn mua mới, không ép bán vị thế đang giữ.
- **Q7 — Tải lịch sự kiện FireAnt 2023–2026 để đo.** Đề xuất của AI: tải một lần theo từng tháng (khoảng 46 lần gọi, chỉ đọc), lưu cache trong thư mục nghiên cứu.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Dựng lại 02/11/2026 với sự kiện TCB + GMD: TCB không nằm trong top 5 KB2; RSI14 TCB 39,7 ± 0,5.
- **SC-002**: Ngưỡng bán TCB tính ngày 04/11/2026 ≈ 22,1 (± 0,1), không phải 27,07.
- **SC-003**: Với 6 sự kiện ở Bối cảnh, nguồn tự động cho hệ số pha loãng khớp tin CafeF (sai số 0).
- **SC-004**: Toàn bộ test V3 hiện có vẫn xanh; mã không có sự kiện cho chỉ báo trùng khớp trước thay đổi.
- **SC-005**: Sổ nghiên cứu có mục PH mới so danh mục 2023–2026 giữa giá thô và giá đã điều chỉnh.

## Assumptions

- Công thức điều chỉnh 005/006 (tiền + pha loãng + quyền mua trả tiền) đủ cho các loại sự kiện ở Việt Nam; spec này không đổi công thức.
- Dãy điều chỉnh lùi về quá khứ (giá gần nhất giữ nguyên) như 005, nên giá live và ngưỡng bán cùng thang.
- Không đổi luật kịch bản, ngưỡng, xếp hạng hay tỉ trọng của V3 — chỉ đổi dữ liệu đầu vào.
- Đổi dữ liệu đầu vào cổng chấm điểm là thay đổi trọng yếu: cập nhật `docs/domain/*` và Phụ lục spec 012 cùng change set khi implement.
- Vị thế TCB đang giữ (Id 3) xử lý tay, ngoài phạm vi spec; spec chỉ áp cho nhịp chạy sau khi deploy.
