# SPEC: Nâng win rate StockRadar — 4 hạng mục

> Handoff spec để agent khác thực thi. Tự chứa — đọc là làm được.
> Nguồn chẩn đoán: production API `http://103.226.248.6/api/v1`, đo ngày 2026-08-10.
>
> **Trạng thái thực thi (2026-08-10):** HM1–HM4 code đã land local; HM2 backfill + train manual đã chạy trên prod (`modelActive=true`, 137 mẫu, accuracy 82.5% **in-sample** — cần ship holdout rồi train lại). `AutoRetrainEnabled` vẫn `false`. HPO venv server đã có; `HyperparameterTuning.Enabled=true` + walk-forward `--folds 3` trong script.

---

## 0. Bối cảnh bắt buộc đọc (chẩn đoán từ data thật)

Production, 30 ngày, 125 lệnh đã đo T+2.5 (`GET /api/v1/performance/north-star?days=30`):

- Hit rate Top10 = **18.4%**, avg return T+2.5 = **−2.11%**, MFE/MAE = +2.34% / **−7.53%**.
- Weekly review tự gắn cờ `recommendedAction: "Overhaul"` (9/11 hỏng tuần 2026-07-27).

**3 nguyên nhân gốc (theo `tradeStateBuckets`):**

1. **Regime mismatch:** hầu hết pick có `setupDna` chứa `"TT bất lợi"` + `Breakout` → breakout trong downtrend, MAE −7.5%.
2. **`AwaitingTrigger` (Chờ kích hoạt) đầu độc Top:** 59/125 mẫu (47%), hit chỉ **10.2%**, avg **−3.81%**.
3. **Relaxed fallback ép pick yếu:** `FallbackMinScore:45` + `FallbackMinResults:3` lấp Top bằng mã score 51–59 trong pha xấu.

**Nguyên tắc:** sửa nguồn ứng viên (HM1) TRƯỚC khi train ML (HM3), vì ML học từ rổ pick hiện tại → rổ tệ ⇒ model tệ.

### Số liệu tham chiếu — tradeStateBuckets (30 ngày)

| Trạng thái | Số mẫu | Hit rate | Avg return T+2.5 |
|-----------|--------|----------|------------------|
| Chờ kích hoạt (AwaitingTrigger) | 59 (47%) | 10.2% | −3.81% |
| Vào ngay (Actionable) | 21 | 19.0% | −0.65% |
| Tránh (Avoid) | 15 | 13.3% | −2.09% |
| Chưa ghi nhận (Unknown) | 27 | 40.7% | +0.46% |

---

## 1. Guardrails chung (không được vi phạm)

- **KHÔNG phá chế độ intraday light.** `DailyAnalysisRunner.RunAsync` có 2 cờ: `runPostProcessing`, `includeStructureAndTracking`. Intraday refresh 15' gọi với cả hai = `false`. Mọi thay đổi phần chọn Top phải áp dụng cho cả full run và intraday.
- **KHÔNG auto-apply HPO** khi chưa có walk-forward (HM4).
- Backend xong chạy `backend/restart-api.ps1`. Ship qua `.\scripts\ship-all.ps1 -Message "..."`.
- Đổi thiết kế pipeline/scoring → cập nhật `docs/domain/buy-decision.md` + `CLAUDE.md` cùng change set (theo constitution).
- Mọi thay đổi hành vi phải có config cờ để rollback nhanh (không hardcode).

---

## 2. HẠNG MỤC 1 — Làm sạch nguồn ứng viên Top (ưu tiên cao nhất)

**Mục tiêu:** loại 3 nguyên nhân gốc. Đây là thứ nâng win rate rõ rệt nhất.

**File chính:** `backend/StockRadar.Infrastructure/MarketData/DailyAnalysisRunner.cs`

- Luồng chọn Top strict: dòng ~87–133 (`candidates` → `ordered` → `Take(MaxResults)`).
- Fallback: `BuildRelaxedCandidates` / `CollectRelaxedCandidates` dòng ~386–450.

**Enum liên quan:** `MarketWyckoffPhase { Favorable, Neutral, Unfavorable }`; `StockTradeState { Actionable, AwaitingTrigger, Avoid, Watchlist, ... }`.

### 1A. Gate theo pha thị trường

Trong pha `Unfavorable`:

- **Không** để setup `Breakout` (đọc `decision.Entry.Type` / `SetupDna`) vào Top trừ khi `tradeState.State == Actionable` **và** BuyScore ≥ ngưỡng cao (config `UnfavorableMinBuyScore`, mặc định 75).
- Ưu tiên các setup không phụ thuộc breakout (Shakeout/pullback đã kích hoạt).
- Trong pha `Neutral`: siết nhẹ (chỉ chặn breakout chưa Actionable).

### 1B. Lọc `AwaitingTrigger` khỏi Top

- Thêm cờ config `DailyAnalysis.ExcludeAwaitingTriggerFromTop` (mặc định `true`).
- Khi bật: loại các item `tradeState.State == AwaitingTrigger` khỏi danh sách ghi `DailyOpportunities` **và** khỏi `SetupTracks` seed (dòng ~183–193). Cho phép giữ nếu số Top < `MinResults` (đỡ rỗng hoàn toàn), nhưng đánh dấu rõ.
- **Lý do:** bucket này hit 10.2%, avg −3.81% — kéo tụt toàn bộ win rate.

### 1C. Kiểm soát relaxed fallback theo pha

- Trong `Unfavorable`: tắt relaxed fallback (thà Top rỗng/ít còn hơn Top rác). Thêm config `DailyAnalysis.RelaxedFallbackDisabledPhases` = `["Unfavorable"]`.
- Giữ `FallbackMinResults` chỉ áp dụng khi pha `Favorable/Neutral`.

### 1D. Config cần thêm

File `backend/StockRadar.Api/appsettings.json`, mục `MarketJobs.DailyAnalysis`:

```json
"UnfavorableMinBuyScore": 75,
"ExcludeAwaitingTriggerFromTop": true,
"RelaxedFallbackDisabledPhases": ["Unfavorable"]
```

Thêm property tương ứng vào `DailyAnalysisJobOptions` (tìm class trong `backend/StockRadar.Application/Options/`).

### 1E. Acceptance (HM1)

- Chạy `POST /api/v1/market/jobs/daily` (full). Kiểm tra log: pha hiện tại + số item bị loại vì AwaitingTrigger/breakout-in-downtrend.
- `GET /performance/north-star?days=30` sau ≥5 phiên: tỉ lệ AwaitingTrigger trong Top → ~0; hit rate Top10 tăng so baseline 18.4%.
- Intraday refresh 15' vẫn chạy, không lỗi (kiểm tra `analysisRuns` vẫn cập nhật trong khung giờ).

---

## 3. HẠNG MỤC 2 — Backfill SetupTracks (tăng mẫu train)

**Mục tiêu:** hiện chỉ 125 mẫu / 23 positive → overfit cao. Backfill lịch sử để tăng.

**Endpoint có sẵn:** `POST /api/v1/ml/backfill/setup-tracks?days=365` (header `X-Sync-Key`).

- Service: `SetupTrackBackfillService.BackfillFromDailyOpportunitiesAsync` — dựng SetupTracks từ `DailyOpportunities` lịch sử + đo T+2.5.

**Blocker cần xử lý:** endpoint `/ml/*` yêu cầu `X-Sync-Key` = giá trị prod (`MarketData.SyncApiKey` trên server, **khác** `dev-sync-key-change-me`). Hai cách:

1. Lấy key prod từ `appsettings.Production.json` trên server rồi gọi API prod.
2. SSH vào server chạy script/job trực tiếp.

**Lưu ý thứ tự:** Backfill dựng từ `DailyOpportunities` **lịch sử** (đã ghi rồi), nên có thể chạy độc lập/song song với HM1. Nhưng nhãn train nên ưu tiên dữ liệu sau HM1 khi đánh giá cuối.

**Acceptance:** `GET /ml/ranker/status` → không đổi (chưa train); `GET /ml/dataset/t25-ranking?days=365` → `rowCount` tăng đáng kể so 125, `positiveLabels` > 40 lý tưởng.

---

## 4. HẠNG MỤC 3 — Train ML OpportunityRanker + auto-retrain

**Trạng thái hiện tại (đã verify 2026-08-10):** `GET /ml/ranker/status` → `modelActive:false, trainingSamples:0`. Model **chưa từng train** → đang fallback heuristic. 9 feature: `buy_score_norm, predicted_hit_norm, sector_inv_rank, rs5d_norm, volume_ratio_norm, is_actionable, dna_breakout, dna_shakeout, market_favorable`.

**Bước:**

1. Train tay lần đầu: `POST /api/v1/ml/train/t25-ranking?days=365` (hoặc script `scripts/train-opportunity-ranker.ps1 -ApiBase <prod> -SyncKey <prod-key>`).
   - Điều kiện train: `MinSamplesForRetrain:30`, `MinPositiveLabelsForRetrain:1` (đã đủ với 125/23, tốt hơn sau backfill).
2. Xem kết quả: accuracy, samples. Nếu accuracy < ~55% → **chưa đủ tin**, cần thêm data (quay lại HM2) hoặc thêm feature.
3. Bật auto-retrain **chỉ khi** accuracy đạt: `OpportunityRanker.AutoRetrainEnabled = true` (appsettings). Auto-retrain chạy trong `WeeklyOpportunityReviewJob` và chỉ promote nếu vượt `MinAccuracyToPromote:55` (đã có gate + versioning + revert).

**Chống overfit (bắt buộc thêm):**

- `OpportunityRankerTrainingService.TrainInternalAsync`: hiện đánh giá accuracy in-sample (`TrainFromRows` train rồi đo trên chính data đó). **Thêm holdout split** (ví dụ train 80% theo thời gian, đo 20% mới nhất — time-based, không random) và dùng accuracy holdout để quyết promote. Đây là điều kiện tiên quyết trước khi bật auto-retrain.

**Acceptance:** `GET /ml/ranker/status` → `modelActive:true`, `trainingAccuracy` (holdout) ghi nhận; `DailyAnalysisRunner` log `"OpportunityRanker ML active"`.

---

## 5. HẠNG MỤC 4 — HPO (Optuna) + walk-forward

**Trạng thái:** `HyperparameterTuning.Enabled:false`, thiếu `.venv-tune` trên server. Có sẵn: `scripts/setup-tune-venv.sh`, `scripts/tune-optuna.py`, `scripts/tune-optuna-requirements.txt`, endpoint headless `POST /ml/tune/evaluate` (fitness cho Optuna, không ghi DB), `HyperparameterTuningRunner` (weekly, **không auto-apply** — chỉ xuất `Data/weekly-tuning-result.json` + Telegram).

**Bước:**

1. **Ops (trên server Linux prod):** chạy `bash scripts/setup-tune-venv.sh` tạo `.venv-tune`. Xác nhận `PythonPath` khớp `HyperparameterTuning.PythonPath` (`/var/www/StockRadar/.venv-tune/bin/python`).
2. Bật `HyperparameterTuning.Enabled:true`. Verify job weekly chạy: đọc `backend/logs` hoặc file `weekly-tuning-result.json`.
3. **Walk-forward validation (bắt buộc trước auto-apply):** hiện HPO tune trên `Days:60` một cửa sổ → dễ overfit regime. Sửa/bổ sung trong `tune-optuna.py` + `TuneEvaluate`:
   - Chia lịch sử thành ≥3 fold thời gian liên tiếp (train fold k → validate fold k+1).
   - Fitness = trung bình out-of-sample các fold, không phải in-sample.
4. **Auto-apply CÓ KIỂM SOÁT (tùy chọn, cuối cùng):** chỉ đề xuất áp cấu hình mới nếu fitness walk-forward vượt cấu hình hiện tại một biên an toàn (config ngưỡng). Giữ human-in-the-loop mặc định (chỉ Telegram đề xuất, không tự ghi).

**Acceptance:** `weekly-tuning-result.json` có kết quả walk-forward (nhiều fold); không tự đổi tham số production trừ khi vượt ngưỡng + được duyệt.

---

## 6. Thứ tự thực thi & phụ thuộc

```
HM1 (regime gate + Top hygiene)  ──►  deploy, chạy ≥5 phiên lấy baseline mới
        │
HM2 (backfill SetupTracks)  ──►  song song được, không phụ thuộc HM1
        │
        ▼
HM3 (train ML + holdout + auto-retrain)  ──►  cần HM2 xong (đủ data) + HM1 (rổ sạch)
        │
        ▼
HM4 (HPO + walk-forward)  ──►  cuối cùng; cần Python server; không auto-apply
```

**Quan trọng:** HM3 nên train **sau** khi HM1 đã chạy vài phiên, để nhãn phản ánh rổ ứng viên đã làm sạch. Nếu cần baseline ML ngay, train trên data cũ nhưng đánh dấu là "pre-cleanup baseline", train lại sau.

---

## 7. Verification tổng (KPI)

Đo bằng `GET /api/v1/performance/north-star?days=30` trước/sau:

| KPI | Baseline hiện tại | Mục tiêu |
|-----|-------------------|----------|
| Hit rate Top10 | 18.4% | ≥ 30% |
| Avg return T+2.5 | −2.11% | ≥ 0% |
| % AwaitingTrigger trong Top | 47% | ≤ 5% |
| Avg MAE | −7.53% | thu hẹp (≥ −5%) |

---

## 8. Prerequisite chung (giải quyết trước khi bắt đầu HM2–4)

- **Prod `X-Sync-Key`:** cần lấy từ server (`appsettings.Production.json` → `MarketData:SyncApiKey`). Không dùng được `dev-sync-key-change-me` (đã test, 401).
- **Truy cập SSH server** `root@103.226.248.6` (key `D:\ssh\id_rsa`) cho HM2 (backfill), HM4 (venv).
- Backup `Data/opportunity-ranker-model.json` + versions trước khi train/promote (đã có revert nhưng nên backup).

---

## 9. Bản đồ file/endpoint tham chiếu nhanh

| Vùng | Đường dẫn / endpoint |
|------|----------------------|
| Chọn Top + ML rank | `backend/StockRadar.Infrastructure/MarketData/DailyAnalysisRunner.cs` |
| Selector pha/ngành/RS | `backend/StockRadar.Domain/Services/SmartMoneyOpportunitySelector.cs` |
| Buy Score / entry | `backend/StockRadar.Domain/Services/BuyDecisionEngine.cs` |
| Trade state resolve | `TradeStateResolver` (Domain/Services) |
| ML ranker + train | `backend/StockRadar.Application/Services/OpportunityRankerTrainingService.cs`, `OpportunityRankingDatasetService.cs`, `OpportunityRankerService.cs` |
| ML infra (model store) | `backend/StockRadar.Infrastructure/Ml/OpportunityRankerInfrastructure.cs` |
| ML controller | `backend/StockRadar.Api/Controllers/MlController.cs` |
| Performance / north-star | `backend/StockRadar.Api/Controllers/PerformanceController.cs` |
| Backfill setup tracks | `SetupTrackBackfillService` |
| HPO | `backend/StockRadar.Infrastructure/MarketData/HyperparameterTuningRunner.cs`, `scripts/tune-optuna.py`, `scripts/setup-tune-venv.sh` |
| Config | `backend/StockRadar.Api/appsettings.json` (`MarketJobs.DailyAnalysis`, `OpportunityRanker`, `HyperparameterTuning`) |
| Options | `backend/StockRadar.Application/Options/OpportunityRankerOptions.cs`, `HyperparameterTuningOptions.cs` |
