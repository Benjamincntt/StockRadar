# Feature Specification: Nhật ký giao dịch thật

**Feature Branch**: `010-trade-journal`

**Created**: 2026-10-21

**Status**: Draft (phạm vi đã duyệt N1–N4 ngày 2026-10-21; chưa plan)

**Input**: Chủ sản phẩm: "tôi cũng muốn có 1 màn ghi nhật ký giao dịch real. vì hiện tại hệ thống chưa ổn định nên tôi không giao dịch thật theo tín hiệu app mà vẫn đang training thôi."

## Bối cảnh

Hệ thống chỉ phát tín hiệu (Telegram, app), không nối với sàn hay công ty chứng khoán, nên không biết lệnh thật của người dùng có khớp không và khớp ở giá nào. Các số liệu hiện có đều đo **tín hiệu**:

- V2: `KetQuaKichBan` + Pha 3 (spec 009).
- V1 VIP: "Lợi nhuận thực" (`docs/domain/realized-pnl.md`) — tên dễ nhầm nhưng vẫn là giá **tại tín hiệu** bán, không phải lệnh thật.

Chủ sản phẩm cần một nơi ghi lệnh **thật** của tài khoản mình để (1) tính lãi/lỗ thật, (2) so với tín hiệu app để biết app sát thực tế đến đâu.

Spec này độc lập với spec 009. Spec 009 C-34 (tín hiệu chỉ dùng giá đã khớp) giữ số liệu tín hiệu đúng; spec này đo tài khoản thật.

## Quyết định đã chốt

| # | Quyết định | Ai / khi nào |
|---|---|---|
| N1 | Một dòng nhật ký có thể gắn với một tín hiệu của app (chọn từ tin MUA/BÁN — tự điền mã, mức kế hoạch) hoặc nhập tự do mọi mã | Chủ sản phẩm, 2026-10-21 (duyệt đề xuất) |
| N2 | Nhập: mã, mua/bán, ngày giờ, giá khớp, khối lượng, phí; ghi chú; lý do vào/ra | như trên |
| N3 | Tính lãi/lỗ và tỷ lệ thắng của tài khoản thật; có cột "lệch so tín hiệu" (giá khớp thật − giá tín hiệu) cho lệnh gắn tín hiệu | như trên |
| N4 | Làm trên mobile trước | như trên |
| N6 | Nhiều người dùng: mỗi người dùng app có nhật ký riêng, chỉ xem/sửa được nhật ký của mình (dùng đăng nhập sẵn có: `AuthController`, `UsersController`, JWT) | Chủ sản phẩm, 2026-10-21 (sửa lại câu trả lời trước) |
| N7 | Sự kiện quyền (chia tách, cổ tức): không tự điều chỉnh giá vốn; người dùng tự nhập lệnh điều chỉnh | như trên |
| N8 | Giá tín hiệu chụp theo **chiều lệnh**. Mua: giá vào điểm 1; lệnh mua thứ hai gắn cùng chu kỳ → giá khớp điểm 2. Bán: lệnh bán đầu tiên gắn chu kỳ → giá bán nửa (nếu tín hiệu có bán nửa), lệnh bán sau / tín hiệu không bán nửa → giá bán hết. Hệ thống tự hiểu theo thứ tự lệnh, người dùng không phải chọn. Tín hiệu chưa có giá tương ứng (ví dụ chưa bán) → không có giá chụp, lệch để trống. V1 VIP áp cùng nguyên tắc | Chủ sản phẩm, 2026-10-22 (duyệt đề xuất) |
| N5 | Đã đọc `docs/domain/realized-pnl.md`: tính năng đó đo giá tín hiệu VIP V1, không phải lệnh thật → không dùng chung dữ liệu; có thể dùng lại công thức phí/thuế (`RealizedPnlMath`) | Đánh giá của trợ lý, 2026-10-21 — chờ xác nhận ở bước plan |

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ghi lệnh mua/bán thật (Priority: P1)

Người dùng vừa đặt lệnh trên app chứng khoán, mở StockRadar, ghi lại lệnh: mã, mua hay bán, thời điểm, giá khớp, khối lượng, phí, ghi chú, lý do.

**Why this priority**: Không có dữ liệu thì không tính được gì.

**Independent Test**: Thêm một lệnh mua và một lệnh bán cho cùng mã; danh sách hiển thị đúng hai dòng, sửa và xoá được.

**Acceptance Scenarios**:

1. **Given** màn nhật ký, **When** nhập lệnh mua đủ trường bắt buộc, **Then** lệnh được lưu và hiện trong danh sách.
2. **Given** thiếu giá khớp hoặc khối lượng, **When** lưu, **Then** báo lỗi, không lưu.
3. **Given** một lệnh đã lưu, **When** sửa giá khớp, **Then** lãi/lỗ liên quan tính lại theo giá mới.
4. **Given** lệnh bán khối lượng lớn hơn số đang giữ, **When** lưu, **Then** cảnh báo, không lưu.

---

### User Story 2 - Gắn lệnh với tín hiệu của app (Priority: P1)

Khi nhận tin MUA/BÁN, người dùng chọn tín hiệu đó để ghi lệnh: mã, giá tín hiệu, mức kế hoạch được điền sẵn; chỉ cần nhập giá khớp thật, khối lượng, phí.

**Why this priority**: Mục tiêu chính là biết app sát thực tế đến đâu.

**Independent Test**: Chọn một tín hiệu MUA V2, ghi lệnh với giá khớp khác giá tín hiệu → dòng nhật ký hiện "lệch so tín hiệu" đúng.

**Acceptance Scenarios**:

1. **Given** danh sách tín hiệu gần đây (V2 và V1 VIP), **When** chọn một tín hiệu, **Then** form điền sẵn mã, chiều, giá tín hiệu.
2. **Given** lệnh gắn tín hiệu, **Then** hiển thị lệch = giá khớp thật − giá tín hiệu (giá trị và %).
3. **Given** tín hiệu có lệnh "không khớp" (người dùng đánh dấu bỏ qua / không khớp), **Then** lưu được trạng thái không khớp để thống kê tỷ lệ khớp.

---

### User Story 3 - Lãi/lỗ và tỷ lệ thắng tài khoản thật (Priority: P2)

Người dùng xem: vị thế đang giữ (giá vốn bình quân, khối lượng), lệnh đã đóng (lãi/lỗ ròng sau phí và thuế), tỷ lệ thắng, tổng lãi/lỗ theo kỳ; so với kết quả tín hiệu tương ứng.

**Independent Test**: Mua 1000 @10, bán 500 @11, bán 500 @9 → lãi/lỗ ròng đúng theo công thức phí/thuế; vị thế đóng; tỷ lệ thắng cập nhật.

**Acceptance Scenarios**:

1. **Given** mua nhiều lần một mã, **Then** giá vốn = bình quân có trọng số theo khối lượng (có phí mua).
2. **Given** bán hết, **Then** vị thế đóng, lãi/lỗ ròng tính sau phí bán và thuế.
3. **Given** lệnh gắn tín hiệu đã đóng, **Then** hiện song song kết quả thật và kết quả tín hiệu (V2: `KetQuaDoLuong`; V1: realized).

### Edge Cases

- Bán khi chưa có mua (nhập sai thứ tự) → cảnh báo.
- Một tín hiệu được gắn nhiều lệnh (chia nhỏ lệnh) → cho phép; lệch tính theo giá khớp bình quân.
- Tín hiệu bị xoá hoặc huỷ sau khi đã gắn → lệnh vẫn giữ, giữ ảnh chụp giá tín hiệu lúc gắn.
- Mã chia tách / quyền làm đổi giá → không tự điều chỉnh; người dùng nhập lệnh điều chỉnh (N7), ví dụ một dòng "điều chỉnh" đổi khối lượng / giá vốn kèm ghi chú.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Người dùng PHẢI thêm, sửa, xoá được lệnh thật (mã, chiều, thời điểm, giá khớp, khối lượng, phí, ghi chú, lý do).
- **FR-002**: Lệnh PHẢI có thể gắn với một tín hiệu V2 hoặc V1 VIP; lưu ảnh chụp giá tín hiệu tại thời điểm gắn.
- **FR-003**: Hệ thống PHẢI cho đánh dấu một tín hiệu là "không khớp / bỏ qua".
- **FR-004**: Hệ thống PHẢI tính vị thế đang giữ (khối lượng, giá vốn bình quân) theo từng mã.
- **FR-005**: Hệ thống PHẢI tính lãi/lỗ ròng của lệnh đã đóng sau phí mua, phí bán, thuế bán (mặc định theo cấu hình phí hiện có, người dùng ghi đè được phí thật của từng lệnh).
- **FR-006**: Hệ thống PHẢI hiển thị tỷ lệ thắng, tổng lãi/lỗ theo kỳ, và lệch trung bình so tín hiệu, tỷ lệ tín hiệu được khớp.
- **FR-007**: Dữ liệu nhật ký KHÔNG được ảnh hưởng số liệu tín hiệu (V2 Pha 3, V1 realized, T+2.5).
- **FR-008**: Mobile trước; API đủ để web làm sau.
- **FR-009**: Mỗi người dùng đăng nhập có nhật ký riêng (N6); API chỉ trả và chỉ cho sửa lệnh của chính người dùng đó. Số liệu tổng hợp (tỷ lệ thắng, lệch so tín hiệu) tính theo từng người dùng.
- **FR-010**: Người dùng PHẢI nhập được lệnh "điều chỉnh" (đổi khối lượng và/hoặc giá vốn, kèm ghi chú) cho sự kiện quyền; hệ thống không tự điều chỉnh (N7).

### Key Entities

- **Lệnh thật**: mã, chiều, thời điểm, giá khớp, khối lượng, phí, ghi chú, lý do, tín hiệu gắn kèm (nếu có) + giá tín hiệu chụp lại.
- **Đánh dấu tín hiệu**: tín hiệu nào được khớp / không khớp.
- **Vị thế thật** (tính từ lệnh): mã, khối lượng giữ, giá vốn bình quân, đóng/mở.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Ghi một lệnh gắn tín hiệu trong dưới 30 giây trên mobile.
- **SC-002**: Lãi/lỗ ròng khớp với sao kê công ty chứng khoán trong phạm vi chênh phí làm tròn (≤ 0.01% giá trị lệnh) khi người dùng nhập phí thật.
- **SC-003**: Với mọi lệnh gắn tín hiệu, xem được lệch so tín hiệu; có số tổng hợp lệch trung bình và tỷ lệ khớp.
- **SC-004**: Số liệu tín hiệu (V2, V1) không đổi trước và sau khi có dữ liệu nhật ký.

## Assumptions

- Nhập tay; không kết nối API công ty chứng khoán.
- Phí/thuế mặc định lấy theo cấu hình hiện có (`RealizedPnl`: mua 0.15%, bán 0.25%, thuế 0.1%).
- Web làm sau, ngoài phạm vi spec này.
