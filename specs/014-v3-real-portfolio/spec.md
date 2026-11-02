# Feature Specification: Danh mục V3 chạy theo lệnh thật trong Nhật ký giao dịch

**Feature Branch**: `014-v3-real-portfolio` (làm trên `master` như spec 012, 013)

**Created**: 2026-11-02

**Status**: Draft — owner đã chốt 4 hướng chính và 3 câu làm rõ C1–C3 (02/11/2026), chờ lập plan

**Input**: Owner (02/11/2026), sau khi thấy màn Hiệu quả ghi tiền mặt 29,36 triệu trong khi chỉ mua thật 100 cp FPT: "Cho danh mục chạy theo lệnh thật anh nhập". Trả lời 4 câu hỏi của AI:
1. "chỉ có 1 danh mục thôi. Thay"
2. "nếu không có nhật ký update thì lấy lệnh cũ. có update thì lấy theo Nhật ký giao dịch. Hạn cuối update là cuối phiên chiều."
3. "V3:VonBanDau"
4. "Không được phép có lệnh đó. bỏ" (lệnh không theo tín hiệu V3)

## Bối cảnh

- Danh mục V3 hiện là danh mục giả lập: hệ thống tự chọn khối lượng (`V3DanhMucXepHang.Chon` → `V3LenhMua.KhoiLuongTheoLo`), tự ghi giá vào/giá thoát vào `ViTheV3`. Tiền mặt, giá trị danh mục, chỗ trống đều tính từ `ViTheV3` (`V3Engine.LayTrangThaiDanhMucAsync`, `V3HieuQuaService.TinhDanhMucAsync`).
- Ví dụ thật: vị thế đầu tiên FPT · KB4 (30/10/2026) — hệ thống ghi 200 cp × 53,2 = 10,64 triệu; owner mua thật 100 cp. Màn Hiệu quả vì vậy ghi tiền mặt 29,36 triệu thay vì 34,68 triệu.
- Nhật ký giao dịch (spec 010, FR-040) đã cho gắn lệnh thật với tín hiệu V3: `NhatKyGiaoDich.NguonTinHieu = "V3"`, `MaTinHieu = TinHieuV3.Id`. Mỗi vị thế V3 trỏ đúng một tín hiệu (`ViTheV3.TinHieuV3Id`) → nối được lệnh thật với vị thế qua mã tín hiệu. Nhật ký cũng có bảng "bỏ qua tín hiệu" (`NhatKyTinHieuBoQua`).
- Nhật ký là **theo từng tài khoản**; danh mục V3 là **một danh mục chung**.

## User Scenarios & Testing

### User Story 1 — Lệnh mua thật thay khối lượng và giá vào của hệ thống (Priority: P1)

Hệ thống bắn tin mua FPT · KB4 gợi ý 200 cp. Owner mua thật 100 cp giá 53,4, nhập vào Nhật ký, gắn tín hiệu FPT · KB4. Trước hạn cuối, danh mục V3 dùng 100 cp × 53,4: tiền mặt, giá trị danh mục, ngưỡng bán, và cỡ lệnh của tín hiệu kế tiếp đều tính theo số thật.

**Why this priority**: đây là sai lệch owner đang thấy; mọi số khác (tiền mặt, cỡ lệnh sau, hiệu quả) đều phụ thuộc vào nó.

**Independent Test**: trên DB local có 1 vị thế V3 đang giữ (200 cp × 53,2), nhập 1 lệnh mua thật 100 cp × 53,4 gắn tín hiệu đó, chạy nhịp đối chiếu → `ViTheV3` ghi 100 cp × 53,4; màn Hiệu quả ghi tiền mặt = vốn − 5,34 triệu.

**Acceptance Scenarios**:

1. **Given** vị thế V3 hệ thống ghi 200 cp × 53,2, **When** owner nhập trước hạn lệnh mua 100 cp × 53,4 gắn tín hiệu đó, **Then** vị thế dùng 100 cp, giá vào 53,4; tiền mặt và giá trị danh mục tính lại theo số này.
2. **Given** giá vào đổi theo lệnh thật, **When** đối chiếu xong, **Then** ngưỡng bán tính lại theo giá vào thật (lệnh thường: phần giá vào × 1,004; hoảng loạn sâu: giá vào × 1,15).
3. **Given** owner mua thật 2 lần cho cùng tín hiệu (60 cp + 40 cp), **When** đối chiếu, **Then** vị thế dùng tổng 100 cp, giá vào bình quân theo khối lượng.
4. **Given** owner không nhập gì cho tín hiệu đó, **When** qua hạn, **Then** vị thế giữ nguyên khối lượng và giá hệ thống đã ghi ("lấy lệnh cũ").

---

### User Story 2 — Lệnh bán thật thay giá thoát của hệ thống (Priority: P1)

Hệ thống bắn tin bán FPT ở 55,0. Owner bán thật ở 55,2, nhập vào Nhật ký. Vị thế đóng theo giá 55,2; lãi %, kết quả Thắng/Thua và tiền thu về dùng số thật.

**Why this priority**: không có phần này thì lãi/lỗ và tiền mặt sau khi bán vẫn lệch.

**Independent Test**: vị thế đã đóng ở 55,0 (hệ thống), nhập lệnh bán thật 100 cp × 55,2 gắn cùng tín hiệu, chạy đối chiếu → `GiaThoat` = 55,2, `PhanTramLoiNhuan` và `KetQua` tính lại.

**Acceptance Scenarios**:

1. **Given** hệ thống đã đóng vị thế ở 55,0, **When** owner nhập trước hạn lệnh bán thật 100 cp × 55,2, **Then** giá thoát = 55,2; lãi %, kết quả, tiền mặt tính lại.
2. **Given** owner không nhập lệnh bán, **When** qua hạn, **Then** giữ giá thoát hệ thống đã ghi.

---

### User Story 3 — Bỏ qua tín hiệu thì vị thế bị huỷ (Priority: P2)

Owner không mua theo tín hiệu và đánh dấu "bỏ qua" trong Nhật ký. Vị thế V3 tương ứng bị huỷ, tiền và chỗ trống trả lại danh mục, tín hiệu không tính vào thống kê theo kịch bản.

**Why this priority**: cần để "một danh mục" đúng với thực tế; nhưng US1–US2 dùng được khi owner luôn mua theo tín hiệu.

**Acceptance Scenarios**:

1. **Given** vị thế V3 đang giữ, **When** owner đánh dấu bỏ qua tín hiệu đó trước hạn, **Then** vị thế chuyển sang bị huỷ, không bắn tin bán cho vị thế này, tiền mặt cộng lại giá trị lệnh.

---

### User Story 4 — Màn Hiệu quả ghi rõ số nào là thật (Priority: P3)

Trên màn Hiệu quả (mobile + web), mỗi vị thế ghi rõ nguồn số: "theo nhật ký" hay "theo hệ thống (chưa nhập nhật ký)".

**Acceptance Scenarios**:

1. **Given** vị thế FPT đã đối chiếu với nhật ký, **When** mở màn Hiệu quả, **Then** dòng FPT có nhãn "theo nhật ký".

### Edge Cases

- Lệnh nhật ký gắn tín hiệu V3 nhưng tín hiệu đó không có vị thế (bị cắt vì danh mục đủ chỗ, giá trần, hết tiền): vẫn cho nhập, danh mục V3 bỏ qua (C3).
- Lệnh nhật ký **không** gắn tín hiệu V3 (owner tự mua): không tính vào danh mục V3 (owner: "bỏ").
- Bán một phần (mua 100, bán 50): vị thế còn 50 cp, chưa đóng; tiền thu về của 50 cp cộng vào tiền mặt. *Đề xuất của AI, owner chưa chốt.*
- Owner bán thật trước khi hệ thống ra tín hiệu bán: vị thế đóng theo lệnh bán thật; hệ thống không bắn tin bán nữa. *Đề xuất của AI.*
- Hệ thống đã ra tín hiệu bán nhưng owner chưa bán và không nhập gì: giữ giá thoát hệ thống ("lấy lệnh cũ").
- Owner bán thật **muộn hơn** ngày hệ thống bán (ví dụ tin bán thứ Hai, bán thật thứ Ba): vị thế lấy giá và ngày thoát thật, đối chiếu ở nhịp 16:00 của ngày bán thật (FR-010a).
- Owner sửa hoặc xoá lệnh nhật ký sau hạn: không đối chiếu lại, số của danh mục giữ nguyên (C2).
- Lệnh thật khiến tiền mặt âm (mua nhiều hơn tiền mặt còn): vẫn ghi theo thật, màn Hiệu quả hiện tiền mặt âm, không chặn. *Đề xuất của AI.*

## Requirements

### Functional Requirements

**Một danh mục**

- **FR-001**: Danh mục V3 MUST chỉ có một. Số trên `ViTheV3` (khối lượng, giá vào, giá thoát, giá trị lệnh) MUST là số đã đối chiếu với nhật ký; không giữ bản giả lập song song (owner: "chỉ có 1 danh mục thôi. Thay").
- **FR-002**: Vốn ban đầu của danh mục MUST vẫn là `V3:VonBanDau` (owner chốt).

**Đối chiếu với nhật ký**

- **FR-003**: Lệnh nhật ký MUST được nối với vị thế V3 qua mã tín hiệu (`NguonTinHieu = "V3"`, `MaTinHieu = ViTheV3.TinHieuV3Id`), chỉ lấy nhật ký của **một** tài khoản chỉ định trong cấu hình `V3:TaiKhoanDanhMuc` (C1). Chưa cấu hình → không đối chiếu, danh mục giữ số hệ thống, log cảnh báo.
- **FR-004**: Có lệnh mua thật gắn tín hiệu → vị thế MUST dùng tổng khối lượng mua thật và giá vào bình quân theo khối lượng; giá trị lệnh = khối lượng × giá vào thật.
- **FR-005**: Có lệnh bán thật gắn tín hiệu → vị thế MUST dùng giá thoát bình quân theo khối lượng bán thật; lãi %, kết quả Thắng/Thua/Hoà vốn, tiền thu về tính lại theo số này (cùng công thức phí và ngưỡng 0,05% đang dùng).
- **FR-006**: Không có lệnh nhật ký gắn tín hiệu → vị thế MUST giữ nguyên số hệ thống đã ghi (owner: "không có nhật ký update thì lấy lệnh cũ").
- **FR-007**: Giá vào đổi theo lệnh thật → ngưỡng bán `NguongChamMa` MUST tính lại theo giá vào thật, cùng luật hiện hành (`V3Engine.TinhNguongBan`); cờ hoảng loạn sâu giữ nguyên.
- **FR-008**: Tín hiệu bị đánh dấu "bỏ qua" trong nhật ký → vị thế MUST chuyển sang bị huỷ, trả tiền và chỗ trống, không bắn tin bán cho vị thế đó.
- **FR-009**: Lệnh nhật ký không gắn tín hiệu V3, hoặc gắn tín hiệu V3 không có vị thế, MUST không ảnh hưởng danh mục V3; Nhật ký vẫn cho nhập, không chặn (owner: "bỏ"; C3).
- **FR-010**: Việc đối chiếu MUST chạy ở nhịp 16:00. Hạn nhập/sửa nhật ký cho lệnh khớp ở phiên D là trước nhịp 16:00 của phiên D (C2). Lệnh nhập/sửa/xoá sau hạn MUST không được đối chiếu lại; số của vị thế đã đối chiếu giữ nguyên.
- **FR-010a** (owner "Nới chiều bán", 02/11/2026): vị thế hệ thống đã đóng ở phiên trước mà phần bán **chưa** đối chiếu MUST nhận lệnh bán thật khớp ở phiên sau đó. Hạn vẫn theo FR-010: lệnh bán khớp ở phiên D phải nhập trước nhịp 16:00 phiên D, và chỉ áp khi các lệnh bán khớp **trong phiên D** đủ hết khối lượng (bán một phần vẫn không áp). Ngày thoát ghi theo ngày bán thật.
- **FR-011**: Cỡ lệnh của tín hiệu mới (`V3DanhMucXepHang.Chon`) MUST dùng tiền mặt và giá trị danh mục đã đối chiếu.

**Hiển thị**

- **FR-012**: API danh sách vị thế V3 MUST trả thêm cờ nguồn số (nhật ký / hệ thống); mobile và web hiện nhãn tương ứng (US4).

### Key Entities

- **ViTheV3** (đã có): thêm cờ "đã đối chiếu với nhật ký" và thời điểm đối chiếu. Cần migration (thêm cột, không đổi cột cũ).
- **NhatKyGiaoDich**, **NhatKyTinHieuBoQua** (đã có, spec 010): chỉ đọc, không đổi bảng.

## Đã làm rõ (owner: "Đồng ý cả 3", 02/11/2026 — chấp nhận đề xuất của AI)

- **C1 — Tài khoản**: chỉ nhật ký của **một** tài khoản chỉ định trong cấu hình `V3:TaiKhoanDanhMuc` được đối chiếu.
- **C2 — Hạn cuối**: trước **nhịp 16:00 của chính phiên khớp lệnh** (lệnh ATC khớp lúc 14:45 nên hạn 14:45 không nhập kịp). Nhập/sửa sau hạn không được đối chiếu lại.
- **C3 — Lệnh gắn tín hiệu V3 không có vị thế**: Nhật ký **vẫn cho nhập**, danh mục V3 **bỏ qua** (phương án b; AI đã nêu là nghiêng về b khi hỏi).

## Success Criteria

### Measurable Outcomes

- **SC-001**: Với vị thế FPT hiện tại, sau khi nhập lệnh mua thật 100 cp, màn Hiệu quả ghi tiền mặt = `V3:VonBanDau` − 100 × giá thật (sai ≤ 1 nghìn đồng).
- **SC-002**: 100% vị thế có lệnh nhật ký gắn tín hiệu, nhập trước hạn, dùng khối lượng và giá thật sau nhịp đối chiếu (kiểm bằng truy vấn so `ViTheV3` với tổng nhật ký theo tín hiệu = 0 dòng lệch).
- **SC-003**: Vị thế không có nhật ký giữ nguyên số hệ thống (0 dòng bị đổi).
- **SC-004**: Thời gian nhịp 16:00 tăng không quá 1 giây. *Đề xuất của AI.*

## Assumptions

- Giữ nguyên luật chọn mã, luật bán, tỉ trọng (KB4 20% — owner chốt 02/11/2026, PH-23), thời điểm các nhịp và nội dung tin Telegram/đẩy app.
- Tin bán vẫn bắn theo vị thế hệ thống đang theo dõi; nhật ký chỉ sửa số sau khi owner nhập.
- Hệ quả của "chỉ 1 danh mục" (AI nêu, owner chưa xác nhận): thống kê "Theo kịch bản" trên màn Hiệu quả dùng số đã đối chiếu, nên không còn so thẳng được với backtest.
- Không đối chiếu ngược các vị thế đã đóng trước ngày ship, trừ khi owner yêu cầu.
