# Hiến pháp sửa bug

**Mục đích**: Đảm bảo AI sửa bug theo kiểu phẫu thuật, an toàn, và do user kiểm soát

**Version**: 1.0.1 | **Created**: 2026-07-23 | **Last Amended**: 2026-07-23

**Cha**: `.specify/memory/constitution.md` — Nguyên tắc III

---

## Nguyên tắc cốt lõi

### I. Phẫu thuật tối thiểu xâm lấn (BẮT BUỘC)

**Chỉ sửa chỗ hỏng. Không đụng chỗ khác.**

- Đổi số dòng TỐI THIỂU cần để hết bug
- KHÔNG refactor code quanh đó “tiện tay”
- KHÔNG đổi tên biến trừ khi chính nó gây bug
- KHÔNG thêm tính năng hay “nice to have”
- KHÔNG sắp lại import/format/cấu trúc trừ khi bắt buộc cho fix

**Lý do**: Mỗi dòng đổi là rủi ro bug mới trong điểm, cổng hoặc cảnh báo.

---

### II. Giữ pattern hiện có (BẮT BUỘC)

**Bám đúng style codebase. Bạn là khách, không phải người cải tạo.**

- Cùng naming, xử lý lỗi, thư viện như file xung quanh
- KHÔNG thêm dependency trừ khi thật sự bắt buộc
- KHÔNG “hiện đại hóa” style (async, DI, folder) trong một bug fix

**Lý do**: Nhất quán hơn sở thích cá nhân.

---

### III. Ranh giới thay đổi tường minh (BẮT BUỘC)

**Trước khi viết code, nêu RÕ sẽ đổi gì.**

1. File sẽ sửa
2. Ý định đổi (từ gì → thành gì)
3. Vì sao hết bug
4. Rủi ro nếu sai
5. Cách test (bước + kết quả kỳ vọng)

Nếu user chỉ hỏi phân tích: dừng ở tái hiện → nguyên nhân → phương án; KHÔNG sửa đến khi họ xác nhận.

---

### IV. Không giả định mù (BẮT BUỘC)

- KHÔNG bịa type, field DTO hay helper không thấy trong context
- Đọc engine/runner sở hữu trước khi đổi hành vi cổng hoặc điểm
- Liệt kê giả định tường minh trong Change Plan

---

### V. Leo thang ngữ nghĩa cổng

Nếu “fix” sẽ đổi Buy Score, cổng Top, mapping MA stack/pha, luật flatBox, hoặc thời điểm bán cảnh báo → **dừng** và leo thang lên full Spec Kit (`/speckit-specify` …). Đó không còn là bug fix phẫu thuật theo file này.
