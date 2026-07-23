# Feature Specification: Xác nhận Uptrend thị trường (ClassifyMarket)

**Feature Branch**: `002-confirmed-market-uptrend`

**Created**: 2026-07-23

**Status**: Draft

**Input**: User description: Đánh giá Favorable hiện tại quá nhạy (1 phiên xanh / ChangePercent > 0.5 → MA Stack Full). Cần ClassifyMarket “lỳ”: Close > MA20 + Follow-Through Day (+ cấu trúc Higher Low); tách Correction / Attempted Rally / Favorable; chỉ khi Favorable mới bật MA Full; UX không báo “Chưa đạt MA” khi thị trường chưa xác nhận.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Không gọi Favorable khi chỉ là nhịp nảy kỹ thuật (Priority: P1)

Nhà giao dịch / vận hành thấy thị trường xanh 1–3 phiên sau nhịp giảm (nảy T+2.5 / bull trap). Hệ thống **không** xếp pha **TT thuận (Favorable)** và **không** đòi cổ phiếu xếp đủ MA dài hạn Full chỉ vì index xanh ngắn hạn.

**Why this priority**: Đây là root cause Top hiện “Chờ kích hoạt — Chưa đạt MA stack…” hàng loạt dù DNA ghi TT thuận.

**Independent Test**: Với chuỗi index chỉ nảy ngắn, dưới hoặc quanh MA20, chưa có ngày bùng nổ theo đà → pha ≠ Favorable; cổng MA không ở mức Full chỉ vì % phiên > 0.5.

**Acceptance Scenarios**:

1. **Given** index vừa có 1–3 phiên tăng nhưng Close vẫn dưới MA20, **When** hệ thống phân loại pha thị trường cho Top tăng trưởng, **Then** pha là Correction (hoặc tương đương giảm/điều chỉnh), **không** phải Favorable.
2. **Given** index có vài phiên xanh nhưng **chưa** thỏa ngày bùng nổ theo đà (FTD), **When** phân loại pha, **Then** pha là Attempted Rally (nỗ lực hồi phục), **không** phải Favorable.
3. **Given** pha không phải Favorable, **When** áp độ chặt MA cho cổng Top, **Then** hệ thống **không** yêu cầu bộ lọc MA Full (MA20>50>100>200) như điều kiện Favorable đã xác nhận.

---

### User Story 2 - Favorable chỉ khi uptrend được xác nhận (Priority: P1)

Khi thị trường thực sự đủ điều kiện nắm giữ dài hơn một nhịp nảy, hệ thống mới gắn **Favorable / TT thuận** và lúc đó mới siết MA Full để bắt siêu cổ phiếu xếp đủ xu hướng dài hạn.

**Why this priority**: Khớp nguyên tắc “đã Favorable + Full thì thị trường phải xứng đáng với sự khắt khe đó”.

**Independent Test**: Chỉ khi đủ bộ điều kiện xác nhận → Favorable → MA Full; thiếu một điều kiện cứng → không Favorable.

**Acceptance Scenarios**:

1. **Given** index Close > MA20, độ dốc MA20 không hướng xuống, **và** đã có Follow-Through Day trong cửa sổ nỗ lực hồi phục, **và** đã có ít nhất một Higher Low trên index theo quy tắc đã công bố, **When** phân loại pha, **Then** pha = Favorable.
2. **Given** pha = Favorable, **When** chọn độ chặt MA cho Top tăng trưởng, **Then** chế độ MA = Full (đòi xếp MA dài hạn).
3. **Given** thiếu FTD hoặc Close ≤ MA20 hoặc chưa có Higher Low, **When** phân loại, **Then** **không** Favorable dù phiên gần nhất tăng mạnh (>0.5% hay cao hơn).

---

### User Story 3 - Attempted Rally: Top nới + nhãn đúng ngữ cảnh (Priority: P2)

Trong pha nỗ lực hồi phục (chưa xác nhận), hệ thống vẫn có thể đưa mã vào danh sách theo dõi / Top nới (dòng tiền sớm, breakout ngắn), nhưng giao diện **không** đổ lỗi “Chưa đạt MA stack / xu hướng dài hạn” như thể đã Favorable; thay bằng thông điệp kiểu **chờ xác nhận thị trường chung**.

**Why this priority**: Sửa hiểu nhầm UX trên Home mà vẫn giữ khả năng quan sát mã sớm.

**Independent Test**: Snapshot/list ở Attempted Rally hiển thị lý do gắn thị trường chưa xác nhận, không gắn câu MA Full giả định Favorable.

**Acceptance Scenarios**:

1. **Given** pha = Attempted Rally và mã fail MA Full nhưng đủ điểm / điều kiện list nới, **When** người dùng xem Top cơ hội, **Then** trạng thái/lý do phản ánh **chờ xác nhận thị trường chung** (hoặc tương đương đã thống nhất), **không** dùng câu “Chưa đạt MA stack / xu hướng dài hạn” như thông điệp chính.
2. **Given** pha = Favorable và mã fail MA Full, **When** xem lý do cổng, **Then** vẫn được phép báo chưa đạt MA / xu hướng dài hạn (vì lúc này Full là đúng ngữ cảnh).

---

### User Story 4 - Correction: quản trị rủi ro (Priority: P2)

Khi index còn dưới MA20 (điều chỉnh / downtrend), hệ thống không tuyên bố TT thuận; Top strict theo MA Full không được kích hoạt như Favorable.

**Why this priority**: Tránh bull trap chọn mã “siêu trend” trong thị trường còn dưới MA20.

**Independent Test**: Chuỗi index Close < MA20 → Correction; không Favorable; chính sách Top/MA khớp bảng pha đã công bố.

**Acceptance Scenarios**:

1. **Given** index Close < MA20, **When** phân loại, **Then** pha = Correction.
2. **Given** pha = Correction, **When** áp chính sách chọn Top tăng trưởng, **Then** không dùng bộ lọc Favorable+Full; cho phép chế độ thận trọng / list nới cực kỳ hạn chế theo chính sách đã ghi (xem Assumptions).

---

### User Story 5 - Tách khỏi sóng hồi (Priority: P3)

Thay đổi pha tăng trưởng **không** đổi hay ghi đè hệ regime sóng hồi (Panic / Stabilizing / ReboundConfirmed / Normal).

**Why this priority**: Hiến pháp: hai hệ song song.

**Independent Test**: Sau khi đổi ClassifyMarket tăng trưởng, ngữ nghĩa regime sóng hồi và điểm ReversalBounce giữ nguyên hợp đồng hiện có.

**Acceptance Scenarios**:

1. **Given** hệ thống đã áp pha Favorable mới cho Top tăng trưởng, **When** xem regime sóng hồi cùng ngày, **Then** regime sóng hồi vẫn độc lập, không bị map 1-1 từ Favorable/Attempted/Correction.

---

### Edge Cases

- Index thiếu lịch sử để tính MA20 / volume TB20: không nâng Favorable; pha an toàn thấp hơn (Correction hoặc Attempted theo dữ liệu còn lại) và ghi nhận trong vận hành.
- Có FTD nhưng sau đó index thủng lại dưới MA20: rời Favorable → Correction (hoặc Attempted nếu vẫn đủ điều kiện nỗ lực — ưu tiên Close vs MA20).
- Nhiều phiên tăng liên tiếp nhưng volume thấp (không đủ FTD): vẫn Attempted Rally, không Favorable.
- FTD xuất hiện ngoài cửa sổ ngày 4–7 của đợt nỗ lực: không tính là FTD hợp lệ cho lần xác nhận đó.
- Higher Low chưa hình thành dù đã có FTD và Close > MA20: không Favorable (đủ “lỳ”).
- List nới (fallback) khi Correction: không spam Top lớn; giới hạn số mã / ngưỡng điểm theo chính sách đã công bố trong plan (không mở Top “giả Favorable”).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Hệ thống MUST phân loại pha thị trường cho chiến lược tăng trưởng (Top / Buy / MA) thành đúng **ba** trạng thái nghiệp vụ: **Correction**, **Attempted Rally**, **Favorable** (nhãn UI tiếng Việt tương ứng, ví dụ Điều chỉnh / Nỗ lực hồi phục / TT thuận — chốt copy ở plan).
- **FR-002**: Hệ thống MUST NOT gắn Favorable chỉ dựa trên biến động % một phiên của index (bỏ tiêu chí kiểu “đổi % phiên > 0.5 ⇒ Uptrend/Favorable” như điều kiện đủ).
- **FR-003**: Điều kiện **bắt buộc** để Favorable MUST gồm đồng thời: (a) index Close > MA20, (b) độ dốc MA20 không hướng xuống, (c) đã có **Follow-Through Day** hợp lệ trong cửa sổ xác nhận đợt hồi phục, (d) đã có **Higher Low** trên cấu trúc index theo quy tắc Assumptions.
- **FR-004**: Follow-Through Day MUST được định nghĩa lượng hóa: phiên tăng index ≥ **1.2%**, volume phiên đó **cao hơn phiên liền trước** và **cao hơn trung bình volume 20 phiên**, và ngày đó rơi vào **ngày thứ 4 đến thứ 7** kể từ phiên bắt đầu đợt nỗ lực hồi phục (quy tắc đánh dấu ngày 1 — Assumptions).
- **FR-005**: Khi Close index < MA20, pha MUST là Correction.
- **FR-006**: Khi không Favorable và không Correction theo FR-005, nhưng đang có nỗ lực hồi phục / dao động quanh quá trình lấy lại MA20 mà chưa đủ FR-003, pha MUST là Attempted Rally.
- **FR-007**: Mapping độ chặt MA MUST: Favorable → **Full**; Attempted Rally → **không Full** (Medium hoặc Loose theo plan, mặc định Medium); Correction → **Loose** hoặc Off/list thận trọng theo plan (mặc định Loose), MUST NOT dùng Full.
- **FR-008**: Khi pha là Attempted Rally (và tương tự khi list nới vì thị trường chưa Favorable), thông điệp trạng thái chính trên Top MUST NOT đổ lỗi “Chưa đạt MA stack / xu hướng dài hạn”; MUST dùng thông điệp chờ xác nhận thị trường chung (copy chốt ở plan).
- **FR-009**: Khi pha là Favorable, cổng fail MA Full vẫn được phép hiển thị lý do liên quan MA / xu hướng dài hạn.
- **FR-010**: Thay đổi pha tăng trưởng MUST NOT ghi đè hoặc hợp nhất với regime / điểm chiến lược sóng hồi.
- **FR-011**: Domain living (`docs/domain/ma-stack-and-market-phase.md` và liên quan) MUST được cập nhật as-is trong cùng change set triển khai; gap G-MA-1 được đóng hoặc đánh dấu đã resolve.
- **FR-012**: Sau triển khai, vận hành MUST có cách đối chiếu nhanh: với cùng dữ liệu index, kết quả pha Favorable / không Favorable giải thích được bằng bộ điều kiện FR-003 (không còn “vì phiên xanh 0.5%”).

### Key Entities

- **Pha thị trường tăng trưởng**: Correction | Attempted Rally | Favorable — đầu vào cho độ chặt MA và chính sách Top.
- **Follow-Through Day (FTD)**: Sự kiện xác nhận đà trên index (biên độ giá + volume + vị trí trong cửa sổ ngày 4–7).
- **Đợt nỗ lực hồi phục**: Chuỗi phiên được đánh dấu từ điểm bắt đầu nỗ lực (sau áp lực giảm / từ vùng thấp) để đếm ngày FTD.
- **Higher Low (index)**: Đáy sau cao hơn đáy trước trên cấu trúc giá index trong lookback đã công bố.
- **Chính sách MA theo pha**: Full / Medium / Loose (hoặc Off) gắn từng pha.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Trên tập kịch bản index “chỉ nảy 1–3 phiên / chưa FTD / dưới hoặc chưa vững trên MA20”, **100%** trường hợp **không** được gắn Favorable.
- **SC-002**: Trên tập kịch bản đủ Close > MA20 + FTD hợp lệ + Higher Low, **100%** được gắn Favorable và áp MA Full.
- **SC-003**: Sau khi bật luật mới, trên Home Top trong pha Attempted Rally: **0** thẻ Top dùng câu “Chưa đạt MA stack / xu hướng dài hạn” làm lý do chính khi nguyên nhân thực sự là thị trường chưa xác nhận (thay bằng thông điệp chờ xác nhận TT).
- **SC-004**: Người vận hành đọc domain living cập nhật giải thích được vì sao Favorable/không Favorable trong **≤ 5 phút** không cần đoán từ % một phiên.
- **SC-005**: Regime / danh sách sóng hồi không đổi hành vi hợp đồng vì feature này (kiểm chứng hồi quy: cùng đầu vào → cùng regime sóng hồi như trước feature).
- **SC-006**: Gap nghiệp vụ “Favorable quá sớm → Full quá sớm” (G-MA-1) được coi là **đã xử lý** trên living doc sau ship.

## Assumptions

- Ba trạng thái map vào hợp đồng sản phẩm hiện có (Favorable / Neutral / Unfavorable) hoặc mở rộng nhãn: **Attempted Rally ≈ thay Neutral** trong ngữ cảnh Top/MA; **Correction ≈ Unfavorable**; giữ tên hiển thị “TT thuận” cho Favorable.
- FTD ngưỡng giá mặc định **1.2%** (trong dải 1.2–1.5% user nêu); volume > prev và > TB20.
- Cửa sổ FTD: ngày **4–7** của đợt nỗ lực; **ngày 1** = phiên đầu tiên Close tăng sau chuỗi áp lực / sau đáy ngắn hạn trong lookback **20 phiên** (chi tiết thuật toán đánh dấu ngày 1 chốt ở plan, không đổi ý định nghiệp vụ).
- Higher Low: trong lookback **60 phiên**, có ít nhất một cặp đáy swing (đáy sau > đáy trước); định nghĩa swing tối thiểu (vd. pivot 5 phiên) chốt ở plan.
- Slope MA20 “không hướng xuống”: MA20 hôm nay ≥ MA20 cách **3 phiên** (cùng tinh thần slope đang dùng cho mã).
- Attempted Rally: MA mặc định **Medium**; Correction: **Loose**; list nới (fallback) ưu tiên bật ở Attempted Rally; Correction thì fallback rất hạn chế (số mã thấp / điểm cao hơn) — số liệu chính xác ở plan.
- Không đổi chiến lược ReversalBounce / `MarketRegime`.
- Không yêu cầu đổi luật T+2.5 Master Alert bán trong feature này.
- Đo North Star trước/sau là khuyến nghị vận hành ở plan/tasks, không chặn acceptance FR pha.

## Out of Scope

- Đổi công thức Buy Score 9 tiêu chí (trừ chỗ đọc pha / MA strictness).
- Gộp pha tăng trưởng với regime sóng hồi.
- Tự động trade / tự đặt lệnh.
- Viết lại toàn bộ VIP Telegram (trừ khi đọc nhãn pha từ context mới — chỉ nếu plan chỉ ra bắt buộc).
- Thay đổi ngưỡng FTD sang chuẩn nước ngoài khác VN nếu chưa có Spec Kit follow-up.

## Dependencies

- Living canon: `docs/domain/ma-stack-and-market-phase.md`, `docs/domain/buy-decision.md`.
- Hiến pháp: Spec trước khi đổi cổng/pha; code là sự thật runtime; hai hệ pha/regime tách.
- Phụ thuộc dữ liệu: lịch sử OHLCV (và volume) của **index** đủ dài để MA20, TB volume 20, FTD, Higher Low.
