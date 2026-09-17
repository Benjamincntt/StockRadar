# Feature Specification: Gỡ bỏ hoàn toàn ReversalBounce (sóng hồi)

**Feature Branch**: `008-remove-reversal-bounce`

**Created**: 2026-08-25

**Status**: Approved (owner chọn phương án C — xóa sạch)

**Input**: User description: "màn hình sóng hồi và luồng sóng hồi thực sự không có 1 tác dụng gì hết. xóa bỏ hoàn toàn màn hình sóng hồi và logic liên quan"

## Bối cảnh

Chiến lược counter-trend ReversalBounce được xây theo `docs/_archive/reversal-bounce-implementation-spec.md`
(breadth/regime 0B → analyzer 0C → shadow mode → backtest). Sau thời gian vận hành, owner
xác nhận luồng này **không tạo giá trị quyết định** — không ai dùng tab "Top đánh sóng hồi",
tín hiệu không được đưa vào Buy Score, và pipeline vẫn tốn thời gian quét toàn universe mỗi phiên.

Owner đã cân nhắc phương án tắt cờ (`ReversalBounce:Enabled=false`) và **chọn xóa sạch** để
loại bỏ code chết, giảm bề mặt bảo trì và rút ngắn daily pipeline.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Người dùng mobile không còn thấy sóng hồi (Priority: P1)

Người dùng mở Home. Trước đây có thanh chuyển 2 tab "Top cơ hội / Top đánh sóng hồi".
Sau thay đổi, chỉ còn danh sách Top cơ hội, hiển thị thẳng, không còn thanh chuyển tab.

**Why this priority**: Đây là bề mặt người dùng nhìn thấy — phần duy nhất họ cảm nhận được.

**Independent Test**: Chạy app mobile, mở Home, xác nhận không còn thanh segment và
không còn gọi API `/reversal-bounce/*` (kiểm tra qua network log).

**Acceptance Scenarios**:

1. **Given** app mobile bản mới, **When** mở Home, **Then** thấy ngay danh sách Top cơ hội,
   không có thanh chuyển tab "Top đánh sóng hồi".
2. **Given** app mobile bản mới, **When** Home load xong, **Then** không có request nào
   tới `/api/v1/reversal-bounce/*`.
3. **Given** app mobile bản mới, **When** mở chi tiết một mã, **Then** màn chi tiết hiển thị
   bình thường, không còn phần thân "sóng hồi".

---

### User Story 2 - Daily pipeline chạy nhanh hơn, không còn bước sóng hồi (Priority: P1)

Job phân tích hằng ngày (`DailyAnalysisRunner`) bỏ hẳn 2 bước: quét breadth/regime và
quét ReversalBounce. Các bước còn lại (Top → criterion → sector wave → monitor) không đổi hành vi.

**Why this priority**: Đây là lý do chính owner muốn xóa — pipeline đang tốn công vô ích.

**Independent Test**: Chạy daily analysis cho một ngày giao dịch, so log trước/sau.
Không còn dòng `ReversalBounce: quét ...`; Top selection và criterion score không đổi.

**Acceptance Scenarios**:

1. **Given** pipeline bản mới, **When** chạy daily analysis full, **Then** log không còn
   nhắc breadth/ReversalBounce và job kết thúc thành công.
2. **Given** cùng một ngày giao dịch, **When** so danh sách Top trước và sau thay đổi,
   **Then** kết quả **giống hệt** (sóng hồi chưa từng ảnh hưởng Top).
3. **Given** pipeline bản mới, **When** chạy intraday refresh (light), **Then** hành vi không đổi.

---

### User Story 3 - API không còn endpoint sóng hồi (Priority: P2)

Toàn bộ route dưới `api/v1/reversal-bounce` biến mất khỏi Swagger và trả 404.

**Why this priority**: Dọn hợp đồng API; không client nào còn gọi sau khi P1 xong.

**Independent Test**: Mở Swagger, xác nhận không còn nhóm `ReversalBounce`.

**Acceptance Scenarios**:

1. **Given** API bản mới, **When** GET `/api/v1/reversal-bounce/candidates`, **Then** trả 404.
2. **Given** API bản mới, **When** mở Swagger UI, **Then** không còn nhóm ReversalBounce.

---

### User Story 4 - Cơ sở dữ liệu không còn bảng sóng hồi (Priority: P3)

Hai bảng `ReversalCandidateSnapshots` và `MarketBreadthSnapshots` bị drop qua migration.

**Why this priority**: Dọn cuối cùng; làm sau khi P1–P3 đã chạy ổn trên production.

**Independent Test**: Chạy migration, xác nhận 2 bảng biến mất, API/app vẫn chạy bình thường.

**Acceptance Scenarios**:

1. **Given** migration mới đã apply, **When** truy vấn schema, **Then** 2 bảng không còn tồn tại.
2. **Given** migration mới đã apply, **When** chạy daily analysis, **Then** thành công.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Home mobile PHẢI hiển thị Top cơ hội trực tiếp, không còn thanh chuyển tab sóng hồi.
- **FR-002**: `DailyAnalysisRunner` PHẢI không còn gọi breadth/regime và ReversalBounce analyzer.
- **FR-003**: API PHẢI không còn expose bất kỳ route nào dưới `api/v1/reversal-bounce`.
- **FR-004**: Cấu hình PHẢI không còn section `ReversalBounce` và `ReversalBounceBacktest`.
- **FR-005**: Migration PHẢI drop `ReversalCandidateSnapshots` và `MarketBreadthSnapshots`.
- **FR-006**: Tài liệu canon PHẢI được cập nhật để không còn mô tả sóng hồi như tính năng sống.

### Ràng buộc bảo toàn (KHÔNG được đụng)

- **NR-001**: `MarketPhaseClassifier` / pha thị trường (Favorable/Neutral) là hệ **độc lập** —
  giữ nguyên hoàn toàn. Chỉ gỡ câu comment nhắc tới MarketRegime.
- **NR-002**: `MarketBreadthStats` + `GetBreadthStatsAsync` (trong `IRepositories.cs`,
  `EfStockRepository`, `CachedRepositories`) phục vụ **Home market overview** —
  trùng tên nhưng KHÔNG liên quan sóng hồi. Giữ nguyên.
- **NR-003**: ~~Giữ `PlaybookId.ReversalBounce`~~ → **Đảo lại: xóa hoàn toàn.**
  Giả định ban đầu (có dữ liệu `CriterionPlaybook` lịch sử cần bảo tồn) đã được kiểm chứng và **sai**.
  Xác minh: `hasReversalBounceSignal: true` chỉ từng xuất hiện trong file test; caller production duy nhất
  (`DailyCriterionScoringRunner:267`) luôn gọi `Classify(eval)` không truyền cờ → giá trị này
  **chưa từng được gán cho bất kỳ dòng nào**. Ordinal enum cũng không phải rủi ro vì DB lưu string id.
  Xóa kèm: entry `reversal-bounce` trong `CriterionAccuracyOptions.PlaybookOutcomes` và `appsettings.json`,
  nhãn + thứ tự hiển thị ở màn Tiêu chí (mobile).
- **NR-004**: Buy Score, cổng Top, MA stack, flatBox/Darvas, sector wave — không đổi hành vi.
  Kết quả Top của cùng một phiên phải giống hệt trước/sau.

### Key Entities

- **MarketBreadthSnapshot** (Domain, sóng hồi) — xóa. Khác `MarketBreadthStats` ở NR-002.
- **MarketRegime** (Normal/Stabilizing/ReboundConfirmed/Panic) — xóa. Khác `MarketWyckoffPhase` ở NR-001.
- **ReversalCandidateSnapshot** — xóa.

## Success Criteria *(mandatory)*

- **SC-001**: `dotnet build` sạch, `dotnet test` xanh sau khi gỡ 6 file test ReversalBounce.
- **SC-002**: Top selection của một phiên mẫu giống hệt trước và sau thay đổi.
- **SC-003**: Daily analysis full chạy thành công, log không còn nhắc ReversalBounce.
- **SC-004**: Không còn **bất kỳ** tham chiếu `ReversalBounce` / `reversal-bounce` nào trong
  `backend/`, `mobile/lib/`, `docs/domain/`, `CLAUDE.md` (trừ migration lịch sử và `docs/_archive/`).
- **SC-005**: App mobile build được và Home hiển thị đúng theo FR-001.

## Rollback

- Code: `git revert` change set.
- DB: migration drop bảng là **không hoàn tác được dữ liệu**. Trước khi apply lên production,
  backup 2 bảng (hoặc chấp nhận mất — dữ liệu chỉ phục vụ tính năng đã bỏ).
  Owner quyết định thời điểm apply migration; không tự chạy lên production.
