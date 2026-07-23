# StockRadar (JUICE) — bản đồ agent ngắn

**Governance:** [`.specify/memory/constitution.md`](./.specify/memory/constitution.md) (Spec Kit).  
**Canon tài liệu:** [`docs/README.md`](./docs/README.md) → [`docs/domain/`](./docs/domain/).

Đổi cổng / điểm / MA·pha / flatBox / pipeline / ngữ nghĩa ReversalBounce **trọng yếu** → Spec Kit (`/speckit-specify`…) **và** cập nhật `docs/domain/*` **cùng change set**. Bản đồ này **không** thay constitution hay code.

Khi docs lệch code → **tin code trên disk**.

Monorepo: **.NET API** + **Flutter mobile** + **React web**. Production API: `http://103.226.248.6/api/v1`, dev `:5280`.

## Cấu trúc (chỉ mở khi cần)

| Vùng | Path |
|------|------|
| API | `backend/StockRadar.{Api,Application,Domain,Infrastructure}/` |
| Mobile | `mobile/lib/` |
| Web | `frontend/src/` |
| Scripts | `scripts/` (`ship-all.ps1`) |

## Pipeline (tóm tắt)

Job 1 universe → Job 2 append + Darvas alert → Daily analysis (Top → criterion → **breadth/regime** → **ReversalBounce**) → monitor VIP → ML/HPO theo lịch.

Chi tiết: [`docs/domain/pipeline-jobs.md`](./docs/domain/pipeline-jobs.md).

## Luật sản phẩm → đọc domain

| Chủ đề | Living |
|--------|--------|
| Buy Score / Top / VIP / hiển thị | [`docs/domain/buy-decision.md`](./docs/domain/buy-decision.md) |
| MA stack & pha tăng trưởng | [`docs/domain/ma-stack-and-market-phase.md`](./docs/domain/ma-stack-and-market-phase.md) | Favorable = MA20+FTD+HL |
| flatBox / Darvas | [`docs/domain/base-price-flatbox.md`](./docs/domain/base-price-flatbox.md) |
| Sóng hồi (≠ Buy Score / ≠ Wyckoff pha) | [`docs/domain/reversal-bounce.md`](./docs/domain/reversal-bounce.md) |

Kiến trúc: [`docs/architecture.md`](./docs/architecture.md). AIUP: [`docs/use_cases/`](./docs/use_cases/), [`docs/entity_model.md`](./docs/entity_model.md).

## Quy ước khi sửa

- Backend xong → `backend/restart-api.ps1`
- Ship: `.\scripts\ship-all.ps1 -Message "..."`
- Token: Grep/SemanticSearch → đọc 3–5 file; không quét `build/` / `node_modules/` / `bin/` / `obj/`
- Entry thường dùng: `Program.cs`, `DailyAnalysisRunner.cs`, `BuyDecisionEngine.cs`, `DarvasBreakoutAnalyzer.cs`, `app_router.dart`
