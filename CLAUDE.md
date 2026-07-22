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
3. **Daily analysis** — `DailyAnalysisRunner` → Top strict; relaxed fallback nếu 0 mã; criterion scoring **T+2.5** (Horizon=2); cuối bước tính **Market Breadth + Regime** (`MarketBreadthRunner` → `MarketBreadthSnapshots`, regime 4-state `Panic/Stabilizing/ReboundConfirmed/Normal` cho ReversalBounce — song song, KHÔNG đụng `MarketWyckoffPhase`), rồi **quét ReversalBounce** (`ReversalBounceAnalysisRunner` → `ReversalCandidateSnapshots`)
4. **Đo T+2.5** — cuối analysis / weekly review → North Star (`GET /performance/north-star`)
5. **Criterion scoring** — `DailyCriterionScoringRunner`
6. **Backtest** — `GET /api/v1/backtest/smartmoney`
7. **HPO tuần (T6 15:30)** — sau weekly review: `HyperparameterTuningRunner` + `tune-optuna.py` → Telegram (không auto-apply)

**ML (Phase 2–3):** `MlController` — dataset/train/backfill/ranker status; train khi ≥30 mẫu đo; `scripts/monitor-ranker-weekly.ps1`. **HPO (Phase 0–1):** `POST /ml/tune/evaluate` + `scripts/tune-optuna.py` (Optuna TPE, Sync-Key).

**Deploy:** `.\scripts\ship-all.ps1` (SSH, **không** GitHub Actions). API job: `POST /api/v1/market/jobs/daily` (không `/jobs/daily-pipeline`).

## Quyết định mua / điểm

- **Buy Score**: `BuyDecisionEngine.cs` — 9 tiêu chí + gates (FOMO, phân phối, MA stack theo pha, RS percentile Unfavorable, breakout…). Hiển thị 0–100: list + detail dùng **snapshot** `DailyOpportunity.BuyScore` khi mã nằm Top (`StockService.GetDetailAsync`); Setup DNA không còn bucket `· Điểm xx`. Mobile ẩn PredictedHit/`Tiềm năng ranking` cạnh Buy Score; nhãn mức giá **Giá vào** (không “Điểm mua”)
- **Master Alert VIP**: mua qua Top trong phiên; vị thế SQL `MasterAlertPositions`; bán chỉ từ **T+3** (`MinTradingSessionsToSell=3`); T+0…T+2 chỉ `RiskWarningIntraday` (không chữ Bán)
- **Lịch sử lệnh T+2.5**: `GET /api/v1/performance/alert-history?kind=buy` (+ `from`/`to`); trends `GET /api/v1/performance/alert-history/trends?period=week|month|quarter` — Win ≥1% (thuế phí), Flat [0,1%), Lose &lt;0%; mobile tab **Hiệu quả** `/performance`
- **Nền giá**: `docs/base-price-engine.md` → `BaseQualityEvaluator.cs` (VCP / Darvas / Spring parallel gates)
- **Top cơ hội / quét strict**: `docs/opportunity-scan-rules.md` → `DailyAnalysisRunner` + `BuyDecisionEngine`
- **MA stack theo pha**: Favorable=Full / Neutral=Medium / Unfavorable=Loose(+RS percentile ≥80 & rs5>0); mã Loose nhưng thiếu RS → `GET /api/v1/early-recovery`
- **Phá hộp phẳng**: `DarvasBreakoutAnalyzer.cs` + `DarvasBreakoutAlertPublisher.cs` — `SignalType.DarvasBreakout` (UI: *Phá vỡ hộp tích lũy phẳng có xác nhận dòng tiền*); tách khỏi `Breakout` 20 phiên
- **Top strict**: `SmartMoneyOpportunitySelector.cs` — wrapper qua BuyDecision + `MinPassScore`
- **Chỉ báo kỹ thuật**: `TechnicalIndicatorAnalyzer`, bundles RSI/EMA/VWAP… — **khác** backtest; đo reliability từng chỉ báo
- **Sóng hồi / bắt đáy (counter-trend)**: `docs/features/reversal-bounce.md` (spec tổng) + `docs/features/reversal-bounce-implementation-spec.md` (spec 0C/0D) + `docs/features/phase0a-audit-reports/` (audit). **0B**: `MarketBreadthAnalyzer` + `MarketRegimeClassifier` (Domain `Services/ReversalBounce/`), `MarketBreadthRunner` (Infra), regime `GET /api/v1/reversal-bounce/market-regime`. **0C xong**: `ReversalBounceAnalyzer` (stage None→Capitulating→Stabilizing→Confirmed/Invalidated + 6 trục điểm §5, stateless) + `CounterTrendDecisionEngine` (hard gate theo regime + trade plan) — cả 2 ở Domain; `ExchangePriceBand` (floor/ceiling runtime); `ReversalBounceAnalysisRunner` (Infra) chạy cuối `DailyAnalysisRunner` SAU breadth → snapshot idempotent `ReversalCandidateSnapshots` (unique `TradingDate,Symbol,StrategyVersion,SetupId`; SetupId deterministic MD5). Endpoints: `GET /reversal-bounce/candidates`, `/candidates/{symbol}`. Options `ReversalBounce.*` (analyzer/gate/trade) + `.ToSettings()`. Tests: `backend/StockRadar.Tests/ReversalBounce/`. **0D xong**: `ReversalBounceFillSimulator` (Domain, thuần: fill Open(T+1), gap-cancel, bán từ T+3, floor-lock defer/force, slippage+phí, Win≥1%/Flat/Lose≤-0.5%) + `ReversalBounceBacktestRunner` (Infra `MarketData/Backtest/`, regime tính lại on-the-fly qua breadth/classifier, RS percentile=50 tạm) + `POST /reversal-bounce/backtest/run` (`ReversalBounceBacktestRequest`→`ReversalBounceBacktestReport`); options `ReversalBounceBacktest.*`. **Phase 1 (Shadow mode)**: `ReversalBounceShadowEvaluator` (Domain, thuần: đo outcome tín hiệu production đã lưu qua fill simulator, Win≥1%/Flat/Lose≤-0.5% + breakdown theo regime) + `ReversalBounceShadowReportService` (App, on-the-fly, KHÔNG persist/migration ở MVP) + `GET /reversal-bounce/shadow-report?from&to&allowDefensiveEarlyExit`. RS `VsSector` + NearSupplyCluster tạm để 0 (MVP). **Phase 2a (mobile)**: Home (`screens/home_screen.dart`) — trong khối **"Cơ hội tốt nhất"**, ngay dưới label là **cần gạt** (sliding toggle `_listToggle`, mặc định trái, KHÔNG persist) chọn **1 trong 2 list dùng chung vị trí** (không đồng thời): trái **"Top cơ hội"** (logic cũ) ↔ phải **"Top đánh sóng hồi"** (`_reversalInlineBody`) = list mã `IsActionable=true` (sắp theo TotalScore) qua `getReversalCandidates(actionableOnly:true, pageSize:40)` + 1 dòng regime (`getReversalMarketRegime`), empty-state khi regime Normal, ErrorBanner nếu load lỗi. Khối **"Tín hiệu mới nhất"** (radar) LUÔN hiển thị bên dưới, không phụ thuộc cần gạt. Card rút gọn inline `_reversalTile` (mã + stage badge + 1-2 bằng chứng pass + 1 dòng trade plan Vào/Cắt/Đích + ScorePill) → tap `/stocks/:symbol`. **Chi tiết cổ phiếu** (`stock_detail_screen.dart`): cần gạt **"Theo tăng trưởng"** ↔ **"Theo sóng hồi"** thay TOÀN BỘ body (lazy load; không chồng UI nặng); sóng hồi = `StockReversalDetailBody` + `GET /reversal-bounce/candidates/{symbol}` (on-demand `AnalyzeSymbolLiveAsync` kể cả Stage=None nếu chưa có snapshot). Reversal tải qua `_loadReversal()` (try riêng). Models `ReversalCandidate*`/`MarketRegimeInfo`/`ReversalCandidateDetail`; nhãn trung tính `core/labels/reversal_bounce_labels.dart`

## Quy ước khi sửa

- Backend xong → tự restart: `backend/restart-api.ps1` (không nhắc user)
- **Ship production:** `.\scripts\ship-all.ps1 -Message "..."` — commit, push, deploy FE+BE, pipeline jobs (trừ Job 1)
- Mobile API: `--dart-define=API_BASE=...` hoặc default production trong `api_config.dart`
- **Tiết kiệm token**: Grep/SemanticSearch trước; đọc 3–5 file; không quét `build/`, `node_modules/`, `bin/`, `obj/`
- **Kiến trúc sâu**: dùng Understand-Anything (`/understand`, graph trong `.understand-anything/`)
- **Phân tích toàn repo**: chỉ khi cần — `scripts/repomix-pack.ps1` (không attach mặc định)

## Entry files thường dùng

`Program.cs`, `MarketService.cs`, `DailySessionSyncRunner.cs`, `DailyAnalysisRunner.cs`, `MlController.cs`, `OpportunityRankerTrainingService.cs`, `DarvasBreakoutAnalyzer.cs`, `BuyDecisionEngine.cs`, `mobile/lib/core/navigation/app_router.dart`

**Kiến trúc tổng hợp (review production):** `docs/system-architecture.md`
