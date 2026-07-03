# StockRadar (JUICE) — context cố định cho AI

Monorepo: **.NET API** + **Flutter mobile** + **React web**. Production API: `http://103.226.248.6/api/v1`, dev `:5280`.

## Cấu trúc (chỉ mở khi cần)

| Vùng | Path | Ghi chú |
|------|------|---------|
| API | `backend/StockRadar.{Api,Application,Domain,Infrastructure}/` | Controllers, services, domain engines |
| Mobile | `mobile/lib/` | GoRouter, screens, `api_client.dart` |
| Web | `frontend/src/` | Vite + React |
| Scripts | `scripts/` | deploy, build APK |

## Pipeline dữ liệu

1. **Job 1** — universe + backfill OHLCV (KBS)
2. **Job 2** — sync phiên
3. **Daily analysis** — `DailyAnalysisRunner` → Top cơ hội (SmartMoney strict → fallback relaxed)
4. **Criterion scoring** — `DailyCriterionScoringRunner` → tab Phân tích chỉ báo
5. **Backtest** — `GET /api/v1/backtest/smartmoney` (`SmartMoneyBacktestRunner`)

## Quyết định mua / điểm

- **Buy Score**: `BuyDecisionEngine.cs` — 9 tiêu chí + gates (FOMO, phân phối, MA stack, breakout…)
- **Top strict**: `SmartMoneyOpportunitySelector.cs` — wrapper qua BuyDecision + `MinPassScore`
- **Chỉ báo kỹ thuật**: `TechnicalIndicatorAnalyzer`, bundles RSI/EMA/VWAP… — **khác** backtest; đo reliability từng chỉ báo

## Quy ước khi sửa

- Backend xong → tự restart: `backend/restart-api.ps1` (không nhắc user)
- Mobile API: `--dart-define=API_BASE=...` hoặc default production trong `api_config.dart`
- **Tiết kiệm token**: Grep/SemanticSearch trước; đọc 3–5 file; không quét `build/`, `node_modules/`, `bin/`, `obj/`
- **Kiến trúc sâu**: dùng Understand-Anything (`/understand`, graph trong `.understand-anything/`)
- **Phân tích toàn repo**: chỉ khi cần — `scripts/repomix-pack.ps1` (không attach mặc định)

## Entry files thường dùng

`Program.cs`, `MarketService.cs`, `DailyAnalysisRunner.cs`, `SmartMoneyBacktestRunner.cs`, `BuyDecisionEngine.cs`, `mobile/lib/core/navigation/app_router.dart`, `mobile/lib/core/api/api_client.dart`
