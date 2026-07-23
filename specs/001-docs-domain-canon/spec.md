# Đặc tả tính năng: Canon tài liệu domain

**Nhánh tính năng**: `[001-docs-domain-canon]`

**Ngày tạo**: 2026-07-23

**Trạng thái**: Nháp

**Đầu vào**: Mô tả người dùng: "Thiết lập một bộ tài liệu living duy nhất (canon) cho luật sản phẩm StockRadar để mọi thay đổi sau này bám theo một nguồn sự thật (docs-domain-canon). Tạo docs/README làm mục lục vào duy nhất. Gom luật đang phân mảnh (Buy Score/cổng Top, MA stack và pha thị trường, flatBox/nền giá, pipeline jobs, ReversalBounce như hệ song song) vào docs/domain/*.md — ghi hành vi as-is và nêu rõ khoảng trống/mâu thuẫn. CLAUDE.md và Continue rules chỉ còn bản đồ ngắn trỏ tới canon và constitution — không nhân đôi bảng cổng. Ngoài phạm vi: đổi hành vi chấm điểm/cổng runtime. Ngoài phạm vi: redesign UI mobile/web. Dùng artifact AIUP reverse-engineer (use case + entity model) làm đầu vào phân tích."

## Kịch bản người dùng & kiểm thử *(bắt buộc)*

### User Story 1 - Tìm một cửa vào luật sản phẩm (Ưu tiên: P1)

Là developer hoặc AI agent làm việc trên StockRadar, tôi mở một mục lục tài liệu và biết ngay đâu là luật sản phẩm sống, đâu là ghi chú lưu trữ, đâu là artifact phân tích AIUP — để không còn đoán từ lịch sử chat hay markdown mâu thuẫn.

**Vì sao ưu tiên này**: Không có cửa vào duy nhất thì mọi file domain sau vẫn cạnh tranh với docs rải rác và CLAUDE.md.

**Kiểm thử độc lập**: Chỉ mở `docs/README.md`; người mới nêu được thư mục living canon và thứ không được coi là sự thật runtime trong vòng 2 phút.

**Kịch bản chấp nhận**:

1. **Given** repo đã giao tính năng này, **When** người đọc mở `docs/README.md`, **Then** thấy mục lục rõ: domain living, artifact AIUP, Spec Kit/constitution, quy tắc archive.
2. **Given** cùng mục lục, **When** tìm luật Buy Score / MA / hộp phẳng / pipeline / sóng hồi, **Then** mỗi chủ đề trỏ đúng **một** tài liệu domain living (không còn ba file “đang đúng” cạnh tranh).

---

### User Story 2 - Đọc luật tăng trưởng và MA/pha tại một chỗ (Ưu tiên: P1)

Là developer sửa Top cơ hội hoặc Buy Decision, tôi đọc một tài liệu domain living nêu cổng hiện tại, chế độ MA stack theo pha thị trường, và các khoảng trống/mâu thuẫn đã biết (gồm uptrend một phiên buộc Full stack), để không bịa luật từ checklist cũ.

**Vì sao ưu tiên này**: Đây là vùng đã chứng minh phân mảnh và Top trống; phải chuẩn hóa trước.

**Kiểm thử độc lập**: Chỉ dùng doc growth + MA/pha (và code nếu đối chiếu gap), giải thích vì sao blue-chip có thể fail MA stack dù index xanh mạnh.

**Kịch bản chấp nhận**:

1. **Given** doc domain cho buy decision/Top và MA stack & pha TT, **When** người đọc lần “Favorable → Full”, **Then** doc nêu mapping as-is và cách trend/pha đang được phân loại.
2. **Given** các doc đó, **When** tìm mâu thuẫn, **Then** gap được gắn nhãn rõ (as-is vs mong muốn) và **không** giả vờ đã sửa trên production.

---

### User Story 3 - Tách rõ mô hình sóng hồi và tăng trưởng (Ưu tiên: P2)

Là developer làm sóng hồi, tôi có tài liệu domain living coi regime sóng hồi song song với pha tăng trưởng, khớp UC-003 vs UC-004 của AIUP, để không gộp hai hệ chấm điểm.

**Vì sao ưu tiên này**: Trộn regime/pha là lỗi khái niệm đắt; tài liệu phải chặn trước khi đụng code.

**Kiểm thử độc lập**: Chỉ từ docs, liệt kê 4 regime sóng hồi và 3 pha tăng trưởng và khẳng định chúng độc lập.

**Kịch bản chấp nhận**:

1. **Given** doc domain sóng hồi và doc growth/MA, **When** so sánh regime vs pha, **Then** docs cấm coi chúng là một khái niệm.
2. **Given** UC-003 và UC-004 AIUP, **When** viết domain docs, **Then** tham chiếu mục tiêu đó, không chép nguyên UC thành luật thứ hai cạnh tranh.

---

### User Story 4 - Thu nhỏ bản đồ agent thành con trỏ (Ưu tiên: P2)

Là AI agent, tôi đọc `CLAUDE.md` / Continue rules như bản đồ ngắn trỏ constitution và `docs/README`, không phải bảng cổng nhân đôi — để context dài không còn đánh nhau với canon.

**Vì sao ưu tiên này**: Nhân đôi bảng cổng trong map tái tạo phân mảnh ngay.

**Kiểm thử độc lập**: `CLAUDE.md` không còn bảng mode MA đầy đủ hay essay implementation sóng hồi nhiều trang; thay bằng link tới canon.

**Kịch bản chấp nhận**:

1. **Given** map agent đã cập nhật, **When** tìm bảng cổng Buy Score chi tiết, **Then** chúng nằm dưới `docs/domain/` (hoặc living doc được link), không phải thân chính `CLAUDE.md`.
2. **Given** nguyên tắc IV của constitution, **When** cập nhật map, **Then** map nhắc: đổi cổng trọng yếu phải qua Spec Kit + cập nhật canon cùng lúc.

---

### User Story 5 - Lưu trữ nhiễu mà không xóa lịch sử (Ưu tiên: P3)

Là người bảo trì, tôi phân biệt được markdown nào là audit/proposal lịch sử và nào là luật sống, để báo cáo phase-0a cũ không bị hiểu như hợp đồng sản phẩm hiện tại.

**Vì sao ưu tiên này**: Làm sau khi đã có index và domain cốt lõi.

**Kiểm thử độc lập**: Mở `docs/_archive/` (hoặc tương đương) và xác nhận proposal/audit có nhãn superseded + link living.

**Kịch bản chấp nhận**:

1. **Given** markdown proposal/audit cũ, **When** tính năng hoàn tất, **Then** file được chuyển archive hoặc đóng dấu superseded và trỏ bản living.
2. **Given** AIUP `docs/use_cases*` và `docs/entity_model.md`, **When** lập mục lục, **Then** giữ làm artifact phân tích (không xóa), gắn nhãn lớp BA/AIUP nuôi Spec Kit — không phải bảng cổng runtime.

### Trường hợp biên

- Doc cũ và domain mới lệch nhau? Domain living chỉ “thắng” sau khi đã đối chiếu code; nếu chưa, mục gap phải ghi “code khác — tin code”.
- Chủ đề chưa extract (vd. ML ranker)? Index ghi “TODO living doc”, không bịa luật.
- Trộn Việt/Anh? Living docs theo ngôn ngữ sản phẩm hiện có (tiếng Việt cho nhãn nghiệp vụ; giữ tên symbol/engine ổn định).

## Yêu cầu *(bắt buộc)*

### Yêu cầu chức năng

- **FR-001**: Dự án PHẢI có `docs/README.md` làm mục lục vào duy nhất phân vai trò tài liệu (domain living, artifact AIUP, Spec Kit/constitution, archive, ghi chú ops).
- **FR-002**: Dự án PHẢI có tài liệu domain living dưới `docs/domain/` gồm tối thiểu: buy decision / cổng Top; MA stack và pha thị trường; hộp phẳng / phá nền; pipeline jobs; sóng hồi (ReversalBounce) như hệ song song.
- **FR-003**: Mỗi tài liệu domain living PHẢI mô tả hành vi sản phẩm **as-is** đã đối chiếu ý định code hiện tại, và PHẢI có mục riêng liệt kê gap/mâu thuẫn đã biết (không “sửa ngầm” trong doc).
- **FR-004**: Tài liệu domain living KHÔNG ĐƯỢC dùng tính năng này để ra lệnh đổi hành vi runtime; đổi hành vi phải là Spec Kit feature sau, cập nhật cùng canon trong cùng lần giao.
- **FR-005**: `CLAUDE.md` và `.continue/rules/stockradar.md` PHẢI thu về bản đồ ngắn trỏ constitution + `docs/README` / domain canon và PHẢI ngừng sở hữu bảng cổng/điểm đầy đủ.
- **FR-006**: Proposal và audit một lần PHẢI được archive hoặc đóng dấu superseded để không trình bày như luật sống.
- **FR-007**: Output AIUP reverse-engineer (`docs/use_cases.puml`, `docs/use_cases/UC-*.md`, `docs/entity_model.md`) PHẢI giữ và được index như đầu vào phân tích khớp mục tiêu UC-001…UC-008.
- **FR-008**: Bộ tài liệu PHẢI nêu rõ: khi docs và code lệch, **code trên disk** vẫn là sự thật runtime — khớp constitution.
- **FR-009**: Người dùng canon (dev/agent) PHẢI tìm được trang thẩm quyền cho một chủ đề cổng chỉ với một bước từ `docs/README.md`.

### Thực thể chính

- **Mục lục Canon**: Cửa vào duy nhất phân loại tài liệu theo vai trò (living / AIUP / archive / map).
- **Tài liệu Domain Living**: Trang luật sản phẩm theo chủ đề; gồm as-is và danh sách gap.
- **Artifact Phân tích AIUP**: Sơ đồ UC, đặc tả UC, mô hình thực thể từ code; nuôi planning, không phải runtime.
- **Bản đồ Agent**: File định hướng ngắn (`CLAUDE.md`, Continue rules) trỏ canon và constitution.
- **Tài liệu Archive**: Proposal/audit đã superseded, giữ lịch sử kèm con trỏ bản living.

## Tiêu chí thành công *(bắt buộc)*

### Kết quả đo được

- **SC-001**: Contributor mới xác định được living doc cho Buy Score/Top, MA/pha, hộp phẳng, pipeline, sóng hồi trong 5 phút chỉ với `docs/README.md`.
- **SC-002**: Ít nhất năm chủ đề domain trong FR-002 có trang riêng và mỗi trang có mục “Khoảng trống / mâu thuẫn”.
- **SC-003**: `CLAUDE.md` không còn bảng cổng MA / essay implementation sóng hồi dài; các đoạn nhân đôi được thay bằng link (mục tiêu: chỉ còn map định hướng).
- **SC-004**: Không còn hai tài liệu “đang đúng” cho cùng chủ đề cổng mà thiếu nhãn superseded/archive hoặc ghi chú chuyển hướng.
- **SC-005**: 100% mâu thuẫn rủi ro cao đã biết (Uptrend một phiên → Favorable → MA Full so với ý định đa phiên) xuất hiện như gap đã ghi, không như luật đã viết lại ngầm.
- **SC-006**: Kỳ vọng đã viết (trong index hoặc link constitution): mọi đổi cổng trọng yếu sau này cập nhật `docs/domain/*` trong cùng change set với Spec Kit.

## Giả định

- Chấm điểm runtime, ngưỡng MA stack, và UI **không đổi** trong tính năng này; chỉ cấu trúc và nội dung tài liệu.
- Artifact AIUP ngày 2026-07-23 đủ dùng làm đầu vào; có thể cross-link nhẹ, không bắt buộc viết lại toàn bộ ở bước plan (đã Việt hóa cùng đợt tài liệu).
- File cũ như `docs/opportunity-scan-rules.md`, `docs/base-price-engine.md`, `docs/pipeline-jobs.md`, docs sóng hồi sẽ được **gộp vào hoặc superseded bởi** `docs/domain/*` (move / stub chuyển hướng / archive) — chi tiết file là việc của plan/tasks.
- Tiếng Việt là ngôn ngữ living docs mặc định.
- `docs/architecture.md` có thể giữ làm tổng quan kiến trúc, link từ index; không thay canon cổng domain.
- Constitution Spec Kit v1.0.0 vẫn là thẩm quyền quy trình; tính năng này hiện thực phần workflow tài liệu domain.
