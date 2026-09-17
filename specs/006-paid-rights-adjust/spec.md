# Feature Specification: Điều chỉnh giá khi quyền mua trả tiền

**Feature Branch**: `006-paid-rights-adjust`

**Created**: 2026-08-22

**Status**: Implemented

**Input**: User description: "Phương án 3: mở rộng hợp đồng điều chỉnh giá theo quyền để mô tả đúng quyền mua cổ phiếu trả tiền (không xấp xỉ như thưởng miễn phí). Case neo HCM: cổ tức 400đ + quyền mua 4:1 giá 10.000đ cùng GDKHQ 16/07/2026; đợt cổ tức tiền 400đ GDKHQ 05/02/2026. Giữ nguyên SSI thưởng 5:1 + cổ tức 1.000đ."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Gap quyền mua không còn bị hiểu là bán tháo (Priority: P1)

Nhà đầu tư / hệ thống chấm điểm nhìn HCM (và mã tương tự) vừa **trả cổ tức tiền** vừa **chào bán thêm cho cổ đông hiện hữu** cùng ngày không hưởng quyền. Trên sàn, giá giảm vì cổ đông phải nộp tiền mua cổ mới và nhận tiền mặt — đó là khoảng trống cơ học, không phải dump. Công thức hiện tại chỉ biết cổ tức tiền và pha loãng thưởng/tách miễn phí: ghi 400đ thì gần như không chỉnh; ghi pha loãng 1.25 thì coi như cổ mới **không mất tiền**, giá tham chiếu thấp hơn thực tế.

Sau thay đổi, phép đo lợi suất / RS / FOMO / hộp dùng dãy đã điều chỉnh theo **giá lý thuyết đủ ba thành phần**: tiền mặt, cổ mới miễn phí (nếu có), và cổ mới **mua với giá phát hành**.

**Why this priority**: Đợt 2 HCM 16/07/2026 gap thật sự do quyền mua 4:1 giá 10.000đ, không do 400đ. Không có tham số giá phát hành thì %/RS/FOMO mã này vẫn sai trong cửa sổ nhiều tháng.

**Independent Test**: Với Close phiên liền trước GDKHQ HCM 16/07 = 26.95, tính giá tham chiếu theo lô 4 cổ cũ (đã trừ 0.4) cộng 1 cổ mới giá 10, chia 5 cổ; lợi suất từ giá tham chiếu tới Close phiên giao dịch kế tiếp (25.4 ngày 17/07, không có nến 16/07) **không** còn ≈ −6% như Close thô 26.95→25.4.

**Acceptance Scenarios**:

1. **Given** HCM, `giaTruocQuyen` = 26.95, cổ tức 0.4, quyền mua 4:1 giá 10.0, **When** tính giá tham chiếu, **Then** kết quả = `(4 × (26.95 − 0.4) + 10) / 5` = **23.24** (sai số cho phép ±0.02).
2. **Given** Close 17/07 = 25.4 so với 23.24, **When** engine đo lợi suất qua ngày quyền trên dãy điều chỉnh, **Then** khoảng **+9% ± 1.5 điểm phần trăm**, không phải −5.8% thô, cũng không phải ~+19% nếu coi 4:1 là thưởng miễn phí `(26.95 − 0.4) / 1.25`.
3. **Given** SSI 17/08 (không quyền mua trả tiền), **When** tính giá tham chiếu, **Then** vẫn `(24.5 − 1.0) / 1.2 ≈ 19.58` như spec 005 — hồi quy không đổi.

---

### User Story 2 - Người vận hành ghi được quyền mua, không chỉ cổ tức / thưởng (Priority: P1)

Người vận hành mở màn sự kiện quyền từ chi tiết mã, nhập **ngày GDKHQ** (không phải ngày đăng ký cuối hay ngày thanh toán), cổ tức tiền, pha loãng thưởng/tách, **và** tỷ lệ quyền mua n:m kèm giá phát hành khi HOSE công bố chào bán thêm. Cùng ngày vừa tiền vừa quyền mua = **một** bản ghi, không hai dòng cộng chồng.

**Why this priority**: Không ghi được tham số thì US1 không chạy trên production.

**Independent Test**: Tra sự kiện HCM 16/07/2026: tiền 0.4, không thưởng (pha loãng 1), tỷ lệ mua 4:1, giá phát hành 10.0. Tra HCM 05/02/2026: chỉ tiền 0.4, không quyền mua. Form từ chối cổ tức ghi 400 / 1000.

**Acceptance Scenarios**:

1. **Given** HCM GDKHQ 16/07/2026, **When** xem sự kiện đã ghi, **Then** có cổ tức 0.4, pha loãng 1, quyền mua 4 cổ được mua 1 cổ mới, giá phát hành 10.0.
2. **Given** HCM GDKHQ 05/02/2026, **When** xem sự kiện đã ghi, **Then** chỉ cổ tức 0.4; không quyền mua (tỷ lệ mua = 0).
3. **Given** bản ghi không có quyền mua (thiếu tỷ lệ hoặc số cổ mới = 0), **When** tính giá tham chiếu, **Then** trùng công thức 005: `(giaTruocQuyen − tienMat) / heSoPhaLoang`.
4. **Given** người dùng nhập cổ tức ≥ 100 (nhầm 400đ thành 400), **When** lưu, **Then** bị từ chối; không áp hệ số.

---

### User Story 3 - Last / chart / nến kho vẫn giá sàn (Priority: P2)

Nhà đầu tư mở chi tiết HCM: giá last và nến khớp lệnh vẫn đúng sàn. Điều chỉnh chỉ lúc so sánh % / chỉ báo phụ thuộc % giá — giống 005.

**Why this priority**: Tránh nhầm “điều chỉnh Yahoo” với giá đặt lệnh.

**Independent Test**: Cùng thời điểm, last HCM trên thẻ = giá nguồn quote; nến ngày 17/07 trên chart vẫn Close 25.4 thô.

**Acceptance Scenarios**:

1. **Given** đã ghi sự kiện quyền mua HCM, **When** xem giá last / nến phiên đang khớp, **Then** không nhân hệ số lên giá live.
2. **Given** Job lưu OHLCV, **When** đọc nến lịch sử đã persist, **Then** Close 15/07 vẫn 26.95 thô.

---

### Edge Cases

- Cùng GDKHQ: cổ tức + quyền mua (HCM 16/07) = một sự kiện; cổ tức + thưởng miễn phí (SSI) = một sự kiện, không quyền mua.
- Chỉ quyền mua, không tiền: `tienMat` = 0.
- Chỉ thưởng/tách, không quyền mua: `tyLeQuyenMua` = 0 → công thức 005.
- Ngày GDKHQ không có nến (HCM 16/07 vắng phiên): dùng Close thô **phiên giao dịch liền trước**; nến phiên kế tiếp đã là thang sau quyền.
- Ngày đăng ký cuối / ngày thanh toán **không** phải ngày áp hệ số. HCM: ĐKCC 06/02 → GDKHQ 05/02; ĐKCC 17/07 → GDKHQ 16/07.
- Quyền mua không đăng ký hết / quyền chuyển nhượng: v1 dùng **giá lý thuyết giả định nhận đủ quyền** (chuẩn điều chỉnh chỉ số), không mô phỏng tỷ lệ đăng ký thực.
- ESOP / phát hành riêng lẻ không phải quyền cho mọi cổ đông hiện hữu: **ngoài** feature này.
- Không cam kết HCM lọt Top sau khi chỉnh — cổng phá nền / kích hoạt giữ nguyên.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Mọi sự kiện quyền PHẢI dùng **một** công thức giá tham chiếu (bảng dưới). Thiếu quyền mua → đúng spec 005. Có quyền mua → cộng giá trị cổ mới nộp tiền vào tử, cộng tỷ lệ cổ mới vào mẫu.
- **FR-002**: Người vận hành PHẢI ghi được, trên màn sự kiện quyền, tỷ lệ quyền mua **n cổ cũ : m cổ mới** và **giá phát hành** cùng thang Close. Không quyền mua thì để trống / 0.
- **FR-003**: Case neo HCM PHẢI có trong danh sách sự kiện: GDKHQ **2026-02-05** (`tienMat` 0.4, không quyền mua); GDKHQ **2026-07-16** (`tienMat` 0.4, pha loãng 1, quyền mua 4:1, giá phát hành 10.0).
- **FR-004**: Case neo SSI 17/08/2026 PHẢI giữ nguyên kết quả 005 (hồi quy).
- **FR-005**: `tienMat` cùng thang Close (400đ = **0.4**). Giá phát hành 10.000đ = **10.0**. Từ chối cổ tức ≥ 100 như hiện tại.
- **FR-006**: Ngày áp dụng = **ngày không hưởng quyền**, không phải ĐKCC hay ngày thanh toán.
- **FR-007**: Nến kho / last / chart phiên khớp PHẢI thô (không nới FR-003/004 của spec 005).
- **FR-008**: Phép đo % giá (RS, FOMO hộp, % phiên, sóng ngành đầu vào) PHẢI dùng dãy điều chỉnh đã gồm quyền mua — không đổi ngưỡng cổng Top / sóng ngành.
- **FR-009**: Sự kiện quyền mua thiếu giá phát hành, hoặc n ≤ 0 khi m > 0, hoặc giá phát hành < 0, PHẢI không được áp dụng (fail rõ).
- **FR-010**: Cùng mã + cùng ngày GDKHQ = thay bản ghi (một sự kiện), không nhân đôi hệ số.

### Công thức (hợp đồng sản phẩm)

Gọi `tyLeQuyenMua` = số cổ mới được mua / số cổ cũ (4:1 → **0.25**). Không quyền mua → 0.

| Tên | Công thức |
|-----|-----------|
| `giaThamChieu` | `(giaTruocQuyen − tienMat + tyLeQuyenMua × giaPhatHanh) / (heSoPhaLoang + tyLeQuyenMua)` |
| `heSoNgayQuyen` | `giaThamChieu / giaTruocQuyen` — nhân vào nến **trước** ngày quyền |

Khi `tyLeQuyenMua` = 0, rút gọn đúng 005: `(giaTruocQuyen − tienMat) / heSoPhaLoang`.

**Kiểm số HCM 16/07:** `giaTruocQuyen` = 26.95 → `(26.95 − 0.4 + 0.25 × 10) / (1 + 0.25)` = **23.24**. Tương đương lô `(4 × (26.95 − 0.4) + 10) / 5`.

**Không dùng:** pha loãng 1.25 cho quyền mua trả tiền (bỏ qua 10.000đ cổ đông phải nộp).

### Key Entities

- **SuKienQuyen**: một đợt GDKHQ của một mã — ngày không hưởng quyền, cổ tức tiền, hệ số pha loãng thưởng/tách, tỷ lệ quyền mua (n:m), giá phát hành (cùng thang Close). Không có trường “loại” riêng; thành phần nào không có thì 0 / 1.
- **GiaLyThuyetSauQuyen**: giá tham chiếu cùng phần kinh tế sau tiền + thưởng + quyền mua.
- **DayGiaDieuChinh**: OHLC trước quyền nhân `heSoNgayQuyen` lũy kế — chỉ để so % / chỉ báo.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Với HCM, `giaTruocQuyen` 26.95, sự kiện 16/07 đủ tiền + quyền mua 4:1 giá 10, giá tham chiếu = **23.24 ± 0.02**.
- **SC-002**: Lợi suất điều chỉnh từ giá tham chiếu 23.24 tới Close 17/07 25.4 nằm trong **+9% ± 1.5 điểm phần trăm**; lợi suất thô 26.95→25.4 **không** được dùng cho RS/FOMO cửa sổ qua ngày đó.
- **SC-003**: Xấp xỉ thưởng miễn phí `(26.95 − 0.4) / 1.25 ≈ 21.24` **không** được dùng; lệch giá tham chiếu so với 23.24 phải ≥ 1.5 điểm giá.
- **SC-004**: SSI 17/08: giá tham chiếu vẫn ≈ 19.58; lợi suất điều chỉnh 1 phiên qua 17/08 vẫn trong +1% ± 1 điểm phần trăm (005).
- **SC-005**: Giá last HCM trên thẻ chi tiết khớp nguồn quote cùng thời điểm (lệch 0.00 do nhân hệ số).
- **SC-006**: Người vận hành ghi xong sự kiện quyền mua trên màn sự kiện quyền của mã trong **một lần nhập** (ngày, tiền, pha loãng, tỷ lệ n:m, giá phát hành) mà không sửa file tay bắt buộc.

## Assumptions

- Đơn vị giá hệ thống: nghìn đồng. 400đ = `tienMat` 0.4; 10.000đ = `giaPhatHanh` 10.0.
- Tỷ lệ HOSE “4:1” = 4 cổ cũ được mua 1 cổ mới (`tyLeQuyenMua` = 0.25), không phải 4 cổ mới trên 1 cổ cũ.
- HCM Đợt 1: chỉ tiền, GDKHQ 05/02/2026 (ĐKCC 06/02). HCM Đợt 2: tiền + quyền mua cùng GDKHQ 16/07/2026 (ĐKCC 17/07). Ngày thanh toán không dùng.
- Không có nến 16/07/2026 trên lịch sử hiện tại: `giaTruocQuyen` = Close 15/07 = 26.95; phiên sau quyền quan sát được = 17/07 Close 25.4.
- Giả định nhận đủ quyền khi tính giá lý thuyết (không mô phỏng bỏ quyền).
- ESOP / private placement ngoài scope.
- Không đổi ngưỡng Buy Score / Top / sóng ngành; không cam kết HCM vào Top (HCM đang dưới nền 26.7–29.0).
- Nguồn sự kiện vẫn nhập tay (màn sự kiện quyền + danh sách versioned); không crawler HOSE.
- Khối lượng không điều chỉnh.
- Công thức 005 là trường hợp đặc biệt của công thức này, không song song hai engine.
