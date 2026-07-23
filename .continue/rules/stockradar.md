# StockRadar — bản đồ repo (Continue Agent)

Governance: `.specify/memory/constitution.md`.  
Canon: `docs/README.md` → `docs/domain/*`. Bản đồ này không thay code / Spec Kit.

Khi docs lệch code → tin code trên disk. Đổi cổng trọng yếu → Spec Kit + cập nhật `docs/domain/*` cùng change set.

Monorepo: API .NET + Flutter mobile + React web. API `/api/v1`, dev `5280`.

## Cấu trúc

- `backend/StockRadar.{Api,Application,Domain,Infrastructure}/`
- `mobile/lib/` — `app_router.dart`, `api_client.dart`, `screens/`
- `frontend/src/` — `pages/`, `components/`
- `scripts/ship-all.ps1`

## Domain living (đọc trước khi sửa luật)

| Chủ đề | Doc |
|--------|-----|
| Buy / Top / VIP | `docs/domain/buy-decision.md` |
| MA / pha | `docs/domain/ma-stack-and-market-phase.md` | Favorable = MA20+FTD+HL |
| flatBox | `docs/domain/base-price-flatbox.md` |
| Pipeline | `docs/domain/pipeline-jobs.md` |
| Sóng hồi | `docs/domain/reversal-bounce.md` |

Entry code: `DailyAnalysisRunner`, `BuyDecisionEngine`, `DarvasBreakoutAnalyzer`, `SmartMoneyOpportunitySelector`.

Khi sửa feature, chỉ mở package liên quan — không quét toàn repo.
