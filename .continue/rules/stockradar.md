# StockRadar — bản đồ repo (Continue Agent)

Monorepo: API .NET + Flutter mobile + React web.

## Backend (`backend/`)

- `StockRadar.Api/` — controllers, `Program.cs`, SignalR hubs
- `StockRadar.Application/` — DTOs, services, options
- `StockRadar.Domain/` — entities, `BuyDecisionEngine`, `SmartMoneyOpportunitySelector`, scorers
- `StockRadar.Infrastructure/` — EF, `DailyAnalysisRunner`, `SmartMoneyBacktestRunner`, market jobs

API base: `/api/v1`. Dev port: `5280`.

## Mobile (`mobile/lib/`)

- `core/navigation/app_router.dart` — GoRouter, shell vs pushed routes
- `core/api/api_client.dart` — REST client
- `screens/` — màn hình
- `widgets/` — UI components

## Frontend (`frontend/src/`)

- `pages/` — React pages
- `components/` — UI

## Jobs / pipeline

- Job 1: universe + backfill OHLCV
- Job 2: sync phiên + alert phá hộp tích lũy phẳng (`DarvasBreakoutAlertPublisher`)
- Daily analysis → Top cơ hội (`DailyAnalysisRunner`)
- Criterion scoring → tab Phân tích chỉ báo (`DailyCriterionScoringRunner`)
- Backtest on-demand: `GET /api/v1/backtest/smartmoney` (`SmartMoneyBacktestRunner`)

## Điểm chấm / quyết định mua

- **Buy Score**: `BuyDecisionEngine.cs` — gates + breakdown 9 tiêu chí; breakout gồm `Breakout` (20 phiên) và `DarvasBreakout` (hộp phẳng)
- **Nền giá + breakout hộp**: `docs/base-price-engine.md` → `BaseQualityEvaluator.cs`, `DarvasBreakoutAnalyzer.cs`
- **Top cơ hội strict**: `SmartMoneyOpportunitySelector.cs`
- **Chỉ báo kỹ thuật**: `TechnicalIndicatorAnalyzer`, `DailyCriterionScoringRunner`

Khi sửa feature, chỉ mở package liên quan — không quét toàn repo.
