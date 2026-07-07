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

1. **Job 1** — KBS listing/history → universe → `Stocks` active
2. **Job 2** — append phiên T + Darvas alert (`DarvasBreakoutAlertPublisher`)
3. **Daily analysis** — `DailyAnalysisRunner` → Top cơ hội (**11:30** + **~15:05** VN nếu `MorningRunEnabled`); sort `IOpportunityRanker`
4. **Đo T+2.5** — cuối analysis / weekly review → North Star (`GET /performance/north-star`)
5. **Criterion scoring** — `DailyCriterionScoringRunner`
6. **Backtest** — `GET /api/v1/backtest/smartmoney`
7. **HPO tuần (T6 15:30)** — sau weekly review: `HyperparameterTuningRunner` + `tune-optuna.py` → Telegram (không auto-apply)

**ML (Phase 2–3):** `MlController` — dataset/train/backfill/ranker status; train khi ≥30 mẫu đo; `scripts/monitor-ranker-weekly.ps1`. **HPO (Phase 0–1):** `POST /ml/tune/evaluate` + `scripts/tune-optuna.py` (Optuna TPE, Sync-Key).

**Deploy:** `.\scripts\ship-all.ps1` (SSH, **không** GitHub Actions). API job: `POST /api/v1/market/jobs/daily` (không `/jobs/daily-pipeline`).

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

`Program.cs`, `MarketService.cs`, `DailySessionSyncRunner.cs`, `DailyAnalysisRunner.cs`, `MlController.cs`, `OpportunityRankerTrainingService.cs`, `DarvasBreakoutAnalyzer.cs`, `BuyDecisionEngine.cs`, `mobile/lib/core/navigation/app_router.dart`
