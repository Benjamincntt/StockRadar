# StockRadar (JUICE) — bản đồ agent ngắn

**Governance:** [`.specify/memory/constitution.md`](./.specify/memory/constitution.md) (Spec Kit).  
**Canon tài liệu:** [`docs/README.md`](./docs/README.md) → [`docs/domain/`](./docs/domain/).

Đổi cổng / điểm / MA·pha / flatBox / pipeline / ngữ nghĩa ReversalBounce **trọng yếu** → Spec Kit (`/speckit-specify`…) **và** cập nhật `docs/domain/*` **cùng change set**. Bản đồ này **không** thay constitution hay code.

Khi docs lệch code → **tin code trên disk**.

## Giao thức mặc định — LUẬT BẮT BUỘC

**1. Hỏi trước, đừng sửa ngay.**  
Khi user hỏi, mô tả lỗi, yêu cầu phân tích, hoặc trao đổi hướng xử lý mà chưa ra lệnh rõ ràng:

- Chỉ đưa: nguyên nhân, 1–3 phương án, ưu/nhược, rủi ro, cách test.
- **Không sửa code, không tạo file, không chạy lệnh đổi codebase trong lượt đó.**
- Đọc code để phân tích thì được, nhưng không dùng kết quả để implement ngay.

Chỉ bắt đầu sửa khi user xác nhận rõ: **"làm đi"**, **"fix đi"**, **"implement"**, **"apply phương án X"**. Không chắc → hỏi lại.

Xem chi tiết tại `.specify/memory/bug-fix-constitution.md` và `.specify/memory/constitution.md` (Nguyên tắc III).

**2. Thay đổi tối thiểu xâm lấn.**  
Chỉ sửa bề mặt tối thiểu để đạt ý định đã duyệt. Không refactor, đổi tên, hay dọn code lân cận.

**3. Gate trọng yếu → Spec Kit.**  
Đổi Buy Score, cổng Top, MA stack, pha, flatBox, pipeline job, route API mới, hay ngữ nghĩa ReversalBounce → dừng và dùng `/speckit-specify` trước. Không implement thẳng từ chat.

Monorepo: **.NET API** + **Flutter mobile** + **React web**. Production API: `http://103.226.248.6/api/v1`, dev `:5280`.

## Cấu trúc (chỉ mở khi cần)

| Vùng | Path |
|------|------|
| API | `backend/StockRadar.{Api,Application,Domain,Infrastructure}/` |
| Mobile | `mobile/lib/` |
| Web | `frontend/src/` |
| Scripts | `scripts/` (`ship-all.ps1`) |

## Pipeline (tóm tắt)

Job 1 universe → Job 2 append + Darvas alert → Daily analysis (Top → criterion → **breadth/regime** → **ReversalBounce**; **intraday 15'** 9:00–11:30 & 13:00–14:45 chỉ refresh Top) → monitor VIP → ML/HPO theo lịch.

Chi tiết: [`docs/domain/pipeline-jobs.md`](./docs/domain/pipeline-jobs.md).

## Luật sản phẩm → đọc domain

| Chủ đề | Living |
|--------|--------|
| Buy Score / Top / VIP / hiển thị | [`docs/domain/buy-decision.md`](./docs/domain/buy-decision.md) · LLM veto: [`docs/features/vip-deepseek-veto/spec.md`](./docs/features/vip-deepseek-veto/spec.md) · Chỉ báo theo playbook (đo riêng, không vào Buy Score): [`docs/features/indicator-playbooks/spec.md`](./docs/features/indicator-playbooks/spec.md) · Sóng ngành + kiểu điểm vào: [`docs/features/sector-wave-entry-patterns/spec.md`](./docs/features/sector-wave-entry-patterns/spec.md) |
| MA stack & pha tăng trưởng | [`docs/domain/ma-stack-and-market-phase.md`](./docs/domain/ma-stack-and-market-phase.md) | Favorable = MA20+FTD+HL |
| flatBox / Darvas | [`docs/domain/base-price-flatbox.md`](./docs/domain/base-price-flatbox.md) |
| Sóng hồi (≠ Buy Score; **cùng pha TT** với Top) | [`docs/domain/reversal-bounce.md`](./docs/domain/reversal-bounce.md) |
| Lợi nhuận thực (Realized P&L, song song T+2.5) | [`docs/domain/realized-pnl.md`](./docs/domain/realized-pnl.md) |
| Win-rate overhaul (Top hygiene + ML + HPO) | [`docs/features/win-rate-overhaul/spec.md`](./docs/features/win-rate-overhaul/spec.md) |
| Điều chỉnh giá theo quyền | [`specs/006-paid-rights-adjust/spec.md`](./specs/006-paid-rights-adjust/spec.md) · seed `Data/su-kien-quyen.json` | %/RS/FOMO lúc chấm điểm (kể cả quyền mua); last/chart thô |
| VNINDEX Home overview + pha | `GET /api/v1/market/vnindex/chart` · `VnIndexMarketCard` |

Kiến trúc: [`docs/architecture.md`](./docs/architecture.md). AIUP: [`docs/use_cases/`](./docs/use_cases/), [`docs/entity_model.md`](./docs/entity_model.md).

## Quy ước khi sửa

- Backend xong → `backend/restart-api.ps1`
- Ship: `.\scripts\ship-all.ps1 -Message "..."` — user tự chạy
- Token: Grep → đọc 3–5 file; không quét `build/` / `node_modules/` / `bin/` / `obj/`
- Entry thường dùng: `Program.cs`, `DailyAnalysisRunner.cs`, `BuyDecisionEngine.cs`, `DarvasBreakoutAnalyzer.cs`, `app_router.dart`

## Skill có sẵn — `.claude/skills/`

Skill được nạp tự động khi gọi đúng tên. Nạp skill **trước** khi viết code.

### Skill nền (nạp cho MỌI task implement/fix)

| Skill | Dùng khi |
|-------|----------|
| `engineering-principles` | mọi lần sửa code |
| `sr-build-run-test` | trước khi build/test/debug/reproduce |

### Theo nhóm task

| Task | Skill |
|------|-------|
| **Fix bug / lỗi runtime** | `debugging-error-recovery` |
| **Code cũ, không rõ owner** | `legacy-code-change` |
| **Migration EF Core** | `efcore-migration-review` |
| **SQL script / data fix** | `sql-data-review` |
| **API / endpoint / DTO** | `api-contract-review` |
| **Hiệu năng / N+1 / JSON scan** | `performance-review` |
| **Background job / SignalR / outbound** | `integration-event-design` · `observability-instrumentation` |
| **Frontend web (React/Vite)** | `frontend-web-dev` |
| **UI/UX review** | `ui-ux-review` |
| **Browser QA / smoke** | `browser-qa-execution` |
| **Viết / review test xUnit** | `test-automation-dotnet` |
| **Trước khi ship** | `release-deploy-gate` |
| **Config / secrets / appsettings** | `secrets-config-review` |
| **Quyết định rủi ro cao** | `doubt-driven-review` |
| **Cập nhật docs sau code** | `doc-update-after-code` |
| **Tạm dừng / tiếp phiên** | `context-handoff` |
