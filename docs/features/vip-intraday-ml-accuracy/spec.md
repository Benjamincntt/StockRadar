# Spec — Nâng độ chính xác noti VIP + AI phụ trợ trong phiên

**Status:** Implemented (Phase 1–4) — train Phase 3 khi đủ ≥30 fire đã đo  
**Scope:** Đường bắn noti intraday (`OpportunityIntradayMonitorRunner` → `TopOpportunityVipAlert*`) + đo lường + đưa `OpportunityRanker` vào tầng trong phiên.  
**KHÔNG đổi:** Buy Score, Top selection, luật bán, SessionRadar.  
**Related:** [`vip-buy-trigger-open-pullback/spec.md`](../vip-buy-trigger-open-pullback/spec.md), [`../../domain/buy-decision.md`](../../domain/buy-decision.md), [`win-rate-overhaul/spec.md`](../win-rate-overhaul/spec.md).

### Đã ship trong code
- Bảng `VipAlertFires` + repo + ghi feature lúc bắn BuyPoint
- Watermark high/low trong phiên + đo intraday sau 14:45
- `GET /api/v1/performance/vip-alert-accuracy?days=N`
- ML gate `MlGateEnabled` + `MinMlProbToFire` theo pha (fail-open khi model/feature thiếu)
- Log `VIP rejected_ml` khi bị chặn
- **Phase 3:** `VipIntradayFeatures` + train `POST /ml/train/vip-intraday` (fail soft nếu thiếu mẫu); ensemble min(daily, intraday)
- **Phase 4:** calibration file + dynamic MinMlProb bump + anti-spam biên (foreign≥0, không VSA xả)
- Status: `GET /ml/vip-intraday/status`, recalibrate: `POST /ml/vip-intraday/recalibrate`

---

## Vấn đề

1. **ML không dùng trong phiên.** `OpportunityRanker` (logistic T+2.5, đã nâng cấp AUC + atr/dist_ma20) chỉ chạy ở daily analysis để sort Top. Quyết định bắn BuyPoint (`EvaluateMasterSignal`) thuần rule.
2. **Không đo được từng noti.** BuyPoint có đăng ký `SetupTrack` (sourceType master → đo T+2.5 qua `OpportunityPerformanceRunner`) nhưng **không lưu feature lúc bắn** (P(hit), gainFromOpen, orderflow, MA, nhánh trigger, paced vol) và **không đo outcome trong phiên**.
3. **Orderflow trong phiên bị bỏ phí.** `SessionFlow` (foreign/prop net lũy kế, pressure), VSA (`TradeEventDetector`), book imbalance đã tính nhưng không tham gia quyết định noti.

---

## Hạ tầng sẵn có (tái dùng, không dựng lại)

| Thành phần | Vai trò |
|------------|---------|
| `IOpportunityRanker.PredictWinProbability(OpportunityRankInput)` + `IsModelActive` | ML P(hit) T+2.5 — gọi được ngay trong phiên |
| `RegisterMasterTrackAsync` → `SetupTrack` (master) | Đã tạo track cho mỗi BuyPoint → có T+2.5 |
| `OpportunityPerformanceRunner` | Đo T+2.5 + MFE/MAE cho SetupTrack |
| `VipPullbackMaCache` | MA10/20/50 + uptrend (đã prefetch cho pullback) |
| `SessionFlowTracker` / `TradeEventDetector` | Orderflow + VSA point-in-time trong phiên |
| `DailyOpportunityRecord` | BuyScore, PredictedHit, VolumeRatio, SetupDna, TradeState, MarketPhase, AverageDailyVolume |
| `KbsPriceBoardClient.KbsBoardRow` | Open/High/Low/Close/Volume + foreign/prop + bid/ask |

---

## Data availability & anti-fake

| Feature lúc bắn | Nguồn | Có sẵn? | Anti-fake |
|-----------------|--------|---------|-----------|
| gainFromOpen | `row.Open` | ✅ | `Open>0` (đã guard) |
| pacedVolumeRatio | monitor tính | ✅ | giữ `PassesVolumeGate` |
| P(hit) ML | ranker | ✅ | Nếu `!IsModelActive` → **không gate bằng ML**, fallback rule cũ (không chặn oan) |
| rs5d / atr% / dist_ma20% | history prefetch + live close | ✅ (khi đủ ≥50 bar) | thiếu history → feature = trung tính, KHÔNG bịa; ghi `featuresComplete=false` |
| MA10/20/50, uptrend | `VipPullbackMaCache` | ✅/❌ | fail-closed (đã có) |
| foreign/prop net, pressure, VSA | `SessionFlow`/scan | ✅ | có thể null đầu phiên → lưu như optional, không coi 0 = tín hiệu |
| Outcome trong phiên | giá cuối phiên vs giá bắn | ❌ chưa đo | Phase 1 thêm; nếu chưa tới cuối phiên → pending, không gán 0 |
| Outcome T+2.5 | SetupTrack master | ✅ | dùng nguyên |

**Nguyên tắc:** thiếu dữ liệu → đánh dấu incomplete + fail-open cho ML gate (không chặn noti khi model/feature không đủ), fail-closed cho nhánh pullback (giữ như spec trước).

---

## Phase 1 — Đo lường noti (bắt buộc, làm trước)

### 1.1 Lưu feature lúc bắn

Mở rộng bản ghi mỗi khi bắn BuyPoint (không đổi hành vi bắn):

- Thêm cột JSON `FireContextJson` vào SetupTrack master (hoặc bảng phụ `VipAlertFires` nếu không muốn đụng SetupTrack schema — **ưu tiên bảng phụ** để tách concern).
- Nội dung: `symbol, sessionDate, firedAtUtc, signal (BuyPoint1/2), branch (breakout/pullback), firePrice, open, gainFromOpen, pacedVolumeRatio, mlProbAtFire, mlModelActive, buyScore, predictedHit, marketPhase, rs5d, atrPct, distMa20, ma10, ma20, ma50, uptrendLong, foreignNet, propNet, sessionPressure, vsaLabel, featuresComplete`.

### 1.2 Đo outcome trong phiên

- Cuối phiên (job có sẵn cuối ngày hoặc bước trong monitor lần chạy cuối): với mỗi fire, tính `intradayReturn = (closeCuốiPhiên - firePrice)/firePrice`, `intradayMaxFavorable`, `intradayMaxAdverse` (dùng cache tick/board trong phiên nếu có; nếu không, tối thiểu close cuối phiên).
- Ghi vào bản ghi Phase 1.1.

### 1.3 KPI endpoint

- `GET /api/v1/performance/vip-alert-accuracy?days=N` trả: hit-rate theo `branch`, theo `marketPhase`, theo bucket P(hit); avg intradayReturn; T+2.5 win-rate (join SetupTrack master).
- Mục tiêu: có baseline để đánh giá Phase 2+.

### Files (Phase 1)

- `backend/StockRadar.Application/Abstractions/IPerformanceServices.cs` — record + repo cho VipAlertFire (nếu bảng phụ).
- `backend/StockRadar.Infrastructure/Persistence/` — entity + migration + repo (`VipAlertFires`).
- `backend/StockRadar.Infrastructure/Notifications/TopOpportunityVipAlertPublisher.cs` — ghi FireContext trong `ProcessQuoteAsync` sau khi dispatch buy.
- `backend/StockRadar.Infrastructure/MarketData/OpportunityIntradayMonitorRunner.cs` — hook đo intraday cuối phiên (hoặc runner đo riêng).
- `backend/StockRadar.Api/Controllers/PerformanceController.cs` (hoặc tương đương) — KPI endpoint.

---

## Phase 2 — ML gate cho noti (đòn bẩy cao, rủi ro thấp)

### Hành vi

Trong `EvaluateMasterSignal` (hoặc ngay trước dispatch trong `ProcessQuoteAsync`), sau khi rule xác định `masterSignal ∈ {BuyPoint1, BuyPoint2}`:

```
mlProb = ranker.PredictWinProbability(rankInputLive)   // build từ opp + live
if ranker.IsModelActive && mlProb < MinMlProbToFire[phase]:
    → KHÔNG bắn (đếm rejected_ml), vẫn giữ confirm-ticks state
else:
    → bắn như hiện tại
```

- `rankInputLive` = `OpportunityRankInput.FromEvaluation(opp.BuyScore, opp.PredictedHitPercent, sectorRankParseFromDna, rs5dLive, opp.VolumeRatio, tradeState, opp.SetupDna, phase, atrLive, distMa20Live)`.
- rs5d/atr/distMa20 tính từ history prefetch + live close (tái dùng helper `ComputeAtrAndDistMa20` — cân nhắc chuyển sang shared util).
- **Fail-open:** `!IsModelActive` hoặc feature incomplete → bỏ gate, bắn theo rule (không chặn oan khi model chưa sẵn sàng).

### Config (`MasterAlerts`)

```json
"MlGateEnabled": true,
"MinMlProbToFire": { "Favorable": 45, "Neutral": 52, "Unfavorable": 60 }
```

(giá trị P(hit) thang 0–100, đồng bộ đơn vị `PredictWinProbability`).

### Files (Phase 2)

- `MasterAlertOptions.cs` + `appsettings*.json` — `MlGateEnabled`, `MinMlProbToFire`.
- `TopOpportunityVipAlertEvaluator.cs` — nhận `mlProb` + ngưỡng, trả lý do reject.
- `TopOpportunityVipAlertPublisher.cs` — build `rankInputLive`, gọi ranker, truyền vào evaluator; log rejected_ml; ghi `mlProbAtFire` (Phase 1).
- (dùng lại) `VipPullbackMaCache` cho MA; helper ATR/dist.

---

## Phase 3 — Model orderflow trong phiên

- Dataset = `VipAlertFires` đã đo + label intradayReturn ≥ ngưỡng.
- Feature: `VipIntradayFeatures` (gain open, paced vol, daily P(hit), atr, dist MA20, uptrend, foreign/prop/pressure, VSA xả).
- Train: `POST /api/v1/ml/train/vip-intraday?days=90` — nếu &lt; `IntradayMinSamplesToTrain` → trả message, **không** bịa data.
- Gate: ưu tiên intraday; nếu cả hai active + `IntradayEnsembleWithDaily` → `min(daily, intraday)`.

---

## Phase 4 — Calibration + ngưỡng động + anti-spam

- Calibration tái dùng `HitCalibrationBuilder` → file `IntradayCalibrationPath`.
- Dynamic threshold: hit-rate &lt; `DynamicHitRateFloorPercent` → +`DynamicThresholdBump` vào MinMlProb.
- Anti-spam: khi P trong band `[min, min+AntiSpamBorderBandPercent]` → chặn nếu foreignNet&lt;0 hoặc VSA xả; thiếu orderflow → fail-open.
- `POST /ml/vip-intraday/recalibrate` rebuild calibration + threshold (không train lại).

**Chưa làm được cho tới khi Phase 1 tích lũy đủ label — không train trên data bịa.**

---

## Acceptance

**Phase 1**
- [ ] Mỗi BuyPoint bắn ra ghi 1 fire record đầy đủ feature + `featuresComplete`
- [ ] Cuối phiên có `intradayReturn` cho các fire trong ngày
- [ ] `GET /performance/vip-alert-accuracy` trả hit-rate theo branch/phase/bucket P(hit)
- [ ] Không đổi điều kiện/hành vi bắn ở Phase 1

**Phase 2**
- [ ] Model active + P(hit) < ngưỡng pha → noti bị chặn, có log `rejected_ml`
- [ ] Model không active hoặc feature thiếu → bắn như cũ (fail-open)
- [ ] `mlProbAtFire` được lưu cho mọi fire
- [ ] Ngưỡng theo pha đọc từ config

---

## Rủi ro & quyết định thiết kế

- **Bảng phụ `VipAlertFires`** thay vì nhồi SetupTrack: tránh phá schema/đo T+2.5 hiện có; join khi cần KPI.
- **Fail-open ML gate**: ưu tiên không bỏ sót khi model chưa chín; siết dần khi KPI chứng minh.
- **Đơn vị P(hit)**: bám `PredictWinProbability` (0–100). Kiểm tra thực tế giá trị trả về trước khi chốt ngưỡng.
- **Không** dùng orderflow=0 như tín hiệu (đầu phiên chưa có delta).

---

## Out of scope

- Đổi Buy Score / Top hygiene / bán / SessionRadar
- Retrain daily ranker (đã có auto-retrain riêng)
- Thay đổi mobile/web

---

## Notes cho agent thực thi

- Làm **Phase 1 trước, Phase 2 sau** (Phase 2 phụ thuộc `mlProbAtFire` để đánh giá).
- Tái dùng `LogisticRegressionTrainer`, `HitCalibrationService`, `OpportunityPerformanceRunner` — không viết lại.
- Migration DB: theo pattern `backend/StockRadar.Infrastructure/Migrations/`.
- Sau backend: `backend/restart-api.ps1`; ship theo `scripts/ship-all.ps1` khi user yêu cầu.
- Kiểm chứng đơn vị `PredictWinProbability` (đọc `OpportunityRankerService`) trước khi set `MinMlProbToFire`.
