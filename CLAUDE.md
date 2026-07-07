# StockRadar (JUICE) — context cố định cho AI

Monorepo: **.NET API** + **Flutter mobile** + **React web**. Production API: `http://103.226.248.6/api/v1`, dev `:5280`.

## Cấu trúc (chỉ mở khi cần)

| Vùng | Path | Ghi chú |
|------|------|---------|
| API | `backend/StockRadar.{Api,Application,Domain,Infrastructure}/` | Controllers, services, domain engines |
| Mobile | `mobile/lib/` | GoRouter, screens, `api_client.dart` |
| Web | `frontend/src/` | Vite + React |
| Scripts | `scripts/` | deploy, build APK, **`ship-all.ps1`** (commit+push+deploy+jobs) |

## Pipeline dữ liệu

1. **Job 1** — KBS listing/history → lọc universe (KL, IPO, giá) → `Stocks` active
2. **Job 2** — append phiên T (KBS bảng giá) + Darvas alert; phân tích/criteria/monitor chỉ đọc universe Job 1
3. **OpportunityRanker** (Phase 2) — logistic regression T+2.5 sort Top list; train `POST /ml/train/t25-ranking`
3. **Daily analysis** — `DailyAnalysisRunner` → Top cơ hội (SmartMoney strict → fallback relaxed)
4. **Criterion scoring** — `DailyCriterionScoringRunner` → tab Phân tích chỉ báo
5. **Backtest** — `GET /api/v1/backtest/smartmoney` (`SmartMoneyBacktestRunner`)

Ship production: `.\scripts\ship-all.ps1 -Message "..."` (commit, push, deploy, pipeline jobs trừ Job 1).

## Quyết định mua / điểm

- **Buy Score**: `BuyDecisionEngine.cs` — 9 tiêu chí + gates (FOMO, phân phối, MA stack, breakout…)
- **Nền giá**: `docs/base-price-engine.md` → `BaseQualityEvaluator.cs` (VCP / Darvas / Spring parallel gates)
- **Phá hộp phẳng**: `DarvasBreakoutAnalyzer.cs` + `DarvasBreakoutAlertPublisher.cs` — `SignalType.DarvasBreakout` (UI: *Phá vỡ hộp tích lũy phẳng có xác nhận dòng tiền*); tách khỏi `Breakout` 20 phiên
- **Top strict**: `SmartMoneyOpportunitySelector.cs` — wrapper qua BuyDecision + `MinPassScore`
- **Chỉ báo kỹ thuật**: `TechnicalIndicatorAnalyzer`, bundles RSI/EMA/VWAP… — **khác** backtest; đo reliability từng chỉ báo

## Quy ước khi sửa

- Backend xong → tự restart: `backend/restart-api.ps1` (không nhắc user)
- **Ship production:** `.\scripts\ship-all.ps1 -Message "..."` — commit, push, deploy FE+BE, pipeline jobs (trừ Job 1)
- Mobile API: `--dart-define=API_BASE=...` hoặc default production trong `api_config.dart`
- **Tiết kiệm token**: Grep/SemanticSearch trước; đọc 3–5 file; không quét `build/`, `node_modules/`, `bin/`, `obj/`
- **Kiến trúc sâu**: dùng Understand-Anything (`/understand`, graph trong `.understand-anything/`)
- **Phân tích toàn repo**: chỉ khi cần — `scripts/repomix-pack.ps1` (không attach mặc định)

## Entry files thường dùng

`Program.cs`, `MarketService.cs`, `DailySessionSyncRunner.cs`, `DailyAnalysisRunner.cs`, `DarvasBreakoutAnalyzer.cs`, `BuyDecisionEngine.cs`, `BaseQualityEvaluator.cs`, `SignalAnalyzer.cs`, `mobile/lib/core/navigation/app_router.dart`
