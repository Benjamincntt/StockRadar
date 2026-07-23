# Mô hình dữ liệu: Canon tài liệu domain

## 1. DocumentationIndex

Thực thể khái niệm đại diện cho cửa vào duy nhất của toàn bộ hệ tài liệu.

### Trường chính

| Trường | Mô tả | Ràng buộc |
|--------|-------|-----------|
| title | Tiêu đề bộ tài liệu | Bắt buộc |
| living_sections | Danh sách nhóm tài liệu living | Ít nhất 1 |
| aiup_sections | Danh sách artifact AIUP | Bắt buộc nếu AIUP đã có |
| archive_section | Link vào khu archive | Bắt buộc |
| governance_links | Link tới constitution / specs | Bắt buộc |
| update_rules | Quy tắc khi nào phải cập nhật docs | Bắt buộc |

### Quan hệ

- `DocumentationIndex` tham chiếu nhiều `DomainDocument`
- `DocumentationIndex` tham chiếu nhiều `AiupArtifact`
- `DocumentationIndex` tham chiếu một `ArchiveCollection`
- `DocumentationIndex` tham chiếu nhiều `AgentMap`

## 2. DomainDocument

Tài liệu living theo một chủ đề nghiệp vụ.

### Trường chính

| Trường | Mô tả | Ràng buộc |
|--------|-------|-----------|
| slug | Định danh file (vd. `ma-stack-and-market-phase`) | Duy nhất |
| title | Tên chủ đề | Bắt buộc |
| purpose | Mục đích tài liệu | Bắt buộc |
| source_entries | Danh sách code/doc entry dùng để đối chiếu as-is | Ít nhất 2 |
| as_is_rules | Luật hiện tại đã xác nhận | Bắt buộc |
| gaps | Khoảng trống / mâu thuẫn đã biết | Có thể rỗng nhưng phải có mục |
| related_docs | Link sang doc khác / AIUP / archive | Tùy chọn |
| owner_scope | Phạm vi trách nhiệm (growth, rebound, pipeline...) | Bắt buộc |

### Quan hệ

- Nhiều `DomainDocument` được nhóm bởi `DocumentationIndex`
- `DomainDocument` có thể tham chiếu nhiều `AiupArtifact`
- `DomainDocument` có thể supersede một hay nhiều `ArchivedDocument`

## 3. AiupArtifact

Artifact phân tích từ AIUP/Tessl, không phải luật runtime.

### Trường chính

| Trường | Mô tả | Ràng buộc |
|--------|-------|-----------|
| type | `use-case-diagram`, `use-case-spec`, `entity-model` | Bắt buộc |
| path | Đường dẫn file | Duy nhất |
| scope | Phạm vi nghiệp vụ artifact bao phủ | Bắt buộc |
| role | Vai trò trong workflow (analysis input) | Bắt buộc |
| linked_domain_topics | Chủ đề domain liên quan | Tùy chọn |

### Quan hệ

- Một `AiupArtifact` có thể nuôi nhiều `DomainDocument`
- Nhiều `AiupArtifact` được index bởi `DocumentationIndex`

## 4. AgentMap

Tài liệu định hướng ngắn cho agent/dev, không sở hữu luật chi tiết.

### Trường chính

| Trường | Mô tả | Ràng buộc |
|--------|-------|-----------|
| path | Đường dẫn file map (`CLAUDE.md`, `.continue/rules/...`) | Duy nhất |
| purpose | Vai trò định hướng | Bắt buộc |
| canonical_links | Link tới `docs/README.md`, constitution, entry files | Bắt buộc |
| forbidden_content | Loại nội dung không nên nhân đôi (bảng cổng chi tiết...) | Bắt buộc |

## 5. ArchivedDocument

Tài liệu lịch sử, proposal, audit hoặc path cũ đã superseded.

### Trường chính

| Trường | Mô tả | Ràng buộc |
|--------|-------|-----------|
| original_path | Đường dẫn cũ | Bắt buộc |
| archive_path | Đường dẫn trong `_archive/` | Bắt buộc |
| archive_reason | Lý do archive | Bắt buộc |
| replacement | Tài liệu living thay thế (nếu có) | Tùy chọn |
| status | `historical`, `proposal`, `audit`, `renamed` | Bắt buộc |

## 6. ArchiveCollection

Thư mục / lớp tập hợp tài liệu không còn là nguồn sự thật.

### Trường chính

| Trường | Mô tả | Ràng buộc |
|--------|-------|-----------|
| root_path | Đường dẫn gốc (`docs/_archive/`) | Bắt buộc |
| readme | Tài liệu giải thích vai trò archive | Bắt buộc |
| categories | Nhóm con: audit, proposal, path cũ... | Ít nhất 1 |

## 7. Chuyển trạng thái

### DomainDocument

- `draft-outline` → đã xác định chủ đề nhưng chưa gom luật
- `as-is-verified` → đã đối chiếu code entry và ghi luật hiện tại
- `canon-ready` → đã có as-is + gaps + cross-links
- `superseded` → bị thay bằng tài liệu domain mới hơn

### ArchivedDocument

- `detected` → đã xác định là không còn dùng làm living truth
- `moved` → đã vào `_archive/`
- `linked` → archive đã có link thay thế / index phù hợp

## 8. Ghi chú thiết kế

- Feature này là **tái cấu trúc tài liệu**, không thêm schema runtime.
- Dữ liệu trên đây là mô hình khái niệm để tổ chức tasks và kiểm chứng deliverable.
- Quan hệ quan trọng nhất là: `DocumentationIndex` → `DomainDocument` và `DomainDocument` → `AiupArtifact` / `ArchivedDocument`.
