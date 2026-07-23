# Contract: Điều hướng bộ tài liệu canon

## Mục đích

Định nghĩa contract điều hướng của bộ tài liệu sau feature `001-docs-domain-canon`, để người đọc và AI agent biết phải tìm thông tin ở đâu và **không** dùng loại file nào như nguồn sự thật runtime.

## Cửa vào bắt buộc

- `docs/README.md` là cửa vào cấp 1 cho toàn bộ tài liệu sản phẩm.

## Phân vai tài liệu

| Nhóm | Đường dẫn | Vai trò | Không được dùng để làm gì |
|------|-----------|---------|---------------------------|
| Domain living | `docs/domain/*.md` | Luật sản phẩm theo chủ đề | Không thay code runtime; không là archive |
| AIUP | `docs/use_cases/*`, `docs/entity_model.md` | Phân tích, dictionary, use case | Không phải bảng cổng runtime |
| Architecture | `docs/architecture.md` | Bản đồ kiến trúc / pipeline lớn | Không ôm chi tiết mọi ngưỡng domain |
| Archive | `docs/_archive/*` | Lịch sử / proposal / audit / path cũ | Không được coi là luật sống |
| Agent maps | `CLAUDE.md`, `.continue/rules/*` | Điều hướng nhanh cho agent/dev | Không nhân đôi bảng cổng đầy đủ |
| Governance | `.specify/memory/constitution.md`, `specs/*` | Quy trình thay đổi và ý định feature | Không thay code runtime |

## Contract tìm thông tin

1. Muốn biết luật nghiệp vụ hiện tại của một chủ đề → vào `docs/README.md` → chọn file `docs/domain/*.md` phù hợp.
2. Muốn hiểu use case hay mô hình khái niệm → xem artifact AIUP.
3. Muốn biết file cũ/proposal/audit → xem `docs/_archive/`.
4. Muốn đổi thiết kế cổng/điểm/pipeline → mở `specs/NNN-...` liên quan và constitution.

## Contract viết mới

- Không tạo file markdown “đang đúng” mới ngoài `docs/domain/*` cho các chủ đề đã có canon.
- Khi đổi cổng trọng yếu, phải cập nhật file domain living tương ứng trong cùng change set.
- Nếu tạo tài liệu mang tính một lần (audit/proposal/retro), đặt ngay trong `_archive/` hoặc feature folder có nhãn rõ.

## Dấu hiệu vi phạm contract

- Cùng một chủ đề domain có từ 2 file trở lên đều tự nhận là “nguồn sự thật hiện tại”
- `CLAUDE.md` hoặc rule agent lại chứa bảng cổng đầy đủ thay vì link canon
- Link chính còn trỏ path cũ đã archive
