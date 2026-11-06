# V1 lưu trữ — 03. Xếp hạng ML, ngành, hygiene và Top 5

Phạm vi: từ danh sách ứng viên đã qua cổng đến Top 5 trang Home. Gồm: xếp hạng ML, cộng điểm ngành, hygiene, giới hạn mã mỗi ngành, số mã tối đa, làm mới Top trong phiên, và cách huấn luyện model + HPO.

Đọc từ code tại commit da75683 (05/11/2026). Lưu ý: git ghi commit này ngày 03/11/2026; 05/11/2026 là ngày đọc.

Quy ước đường dẫn: `Domain/...` = `backend/StockRadar.Domain/Services/OpportunityRanking/`; `App/...` = `backend/StockRadar.Application/`; `Infra/...` = `backend/StockRadar.Infrastructure/`; `Runner` = `Infra/MarketData/DailyAnalysisRunner.cs`.

---

## 1. Luồng sau cổng (thứ tự thật trong code)

| Bước | Việc làm | Nguồn |
|---|---|---|
| 1 | Với mỗi ứng viên: tính `tradeState`, ATR%, khoảng cách MA20, dựng `OpportunityRankInput` | `Runner:183-200` |
| 2 | `mlProb = opportunityRanker.PredictWinProbability(...)` | `Runner:201` |
| 3 | `rankedScore = mlProb + bonusNgành / 100` | `Runner:203` |
| 4 | Sắp: `RankedScore` giảm dần, rồi `Eval.Score`, rồi bậc sóng ngành, rồi `RelativeStrength5d` (đều giảm dần), cuối cùng `Symbol` tăng dần | `Runner:207-211` |
| 5 | Hygiene (`ApplyTopHygiene`) | `Runner:214`, `Runner:510-593` |
| 6 | Giới hạn mã mỗi ngành (`ApplySectorCap`, `MaxPerSector`) | `Runner:216`, `Runner:478-497` |
| 7 | Cắt `MaxResults` (nếu > 0) | `Runner:227-228` |
| 8 | Ghi `DailyOpportunity` (thay cả ngày) | `Runner:283` |
| 9 | Chỉ khi chạy đầy đủ: ghi `SetupTracks` để sau này đo kết quả | `Runner:297-303` |

Thứ tự quan trọng: hygiene chạy TRƯỚC sector cap, sector cap chạy TRƯỚC cắt Top 5. Cắt Top 5 là bước cuối.

Cấu hình thật trong repo: `MaxResults=5`, `MaxPerSector=2` (`appsettings.json:127,130`). Mặc định trong class là 30 và 2 (`App/Options/MarketJobsOptions.cs:114,123`). Nhớ file trên server có thể khác repo (xem phần 9).

---

## 2. Mười một đặc trưng (thứ tự cố định)

Nguồn tên và thứ tự: `Domain/OpportunityRankFeatures.cs:8-21`. Công thức: `OpportunityRankFeatures.cs:30-45` (hàm `Vectorize`).

| # | Tên | Nguồn dữ liệu khi xếp hạng | Công thức chuẩn hoá | Dòng |
|---|---|---|---|---|
| 0 | `buy_score_norm` | `decision.BuyScore` | `BuyScore / 100` | 32 |
| 1 | `predicted_hit_norm` | `decision.PredictedHitPercent` | `clamp(PredictedHitPercent, 0, 100) / 100` | 33 |
| 2 | `sector_wave_inv` | Token sóng ngành trong `SetupDna`; nếu không đọc được thì `decision.SectorWave.WaveRank` | `1 / (1 + max(1, rank))` | 34 |
| 3 | `rs5d_norm` | `decision.RelativeStrength5d` (%) | `clamp(RS5d / 15, -1, 1)` | 35 |
| 4 | `volume_ratio_norm` | `decision.VolumeRatio` | `clamp(VolumeRatio / 3, 0, 2)` | 36 |
| 5 | `is_actionable` | `tradeState.State` | 1 nếu `Actionable`, ngược lại 0 | 37 |
| 6 | `dna_breakout` | Phần đầu của `SetupDna` | 1 nếu chứa "Breakout", ngược lại 0 | 38 |
| 7 | `dna_shakeout` | Phần đầu của `SetupDna` | 1 nếu chứa "Shakeout" hoặc "Divergence", ngược lại 0 | 39 |
| 8 | `market_favorable` | `context.MarketPhase` hoặc phần pha trong `SetupDna` | 1 nếu pha là `Favorable` HOẶC DNA ghi "thuận", ngược lại 0 | 27-28, 40 |
| 9 | `atr_norm` | ATR14 / Close × 100 (%) | `clamp(ATR% / 5, 0, 1)` | 42 |
| 10 | `dist_ma20_norm` | (Close − SMA20) / SMA20 × 100 (%) | `clamp(DistMa20% / 10, -1, 1)` | 44 |

Chi tiết thêm:

| Điểm | Giải thích | Nguồn |
|---|---|---|
| Đọc `SetupDna` | Chuỗi chia bằng ký tự `·`: phần 0 = kiểu vào lệnh, phần 1 = pha thị trường, phần 2 = sóng ngành | `OpportunityRankFeatures.cs:72-76` |
| Token sóng ngành | "Sóng ngành mạnh" = 1; "Chớm sóng" = 2; "chưa có sóng" = 3; không khớp thì tìm `#n` (DNA cũ dạng "Ngành #n"); không có gì = 99 | `OpportunityRankFeatures.cs:81-92` |
| Giá trị `sector_wave_inv` | rank 1 → 0,5; rank 2 → 0,333; rank 3 → 0,25; rank 99 → 0,01 | tính từ dòng 34 |
| Pha trong DNA | chứa "thuận" = Favorable; chứa "bất lợi" = Unfavorable; còn lại Neutral | `OpportunityRankFeatures.cs:110-117` |
| ATR14 | Trung bình đơn giản của True Range 14 phiên (không phải Wilder). Lấy từ `IndicatorMath.Atr` | `Domain/Services/IndicatorMath.cs:27-42, 245` |
| SMA20 | Trung bình đóng cửa 20 phiên, `IndicatorMath.SmaAt` | `Runner:649-673` |
| Hàm `FromTrack` (dùng khi huấn luyện) | Tự điền RS5d = 0, VolumeRatio = 1 rồi bị dataset ghi đè bằng số tính lại | `OpportunityRankFeatures.cs:48-65` |

---

## 3. Model đang chạy trên production (bản 25/09/2026)

Nguồn: file `ranker_model.json` đã tải về từ server, nằm ngoài repo ở
`C:\Users\nguye\AppData\Local\Temp\claude\6cd2803dd17193e6\488a7dd0-be70-4c8f-8b76-9d041b14c592\scratchpad\v1replay\ranker_model.json`. Dữ liệu này KHÔNG có trong repo, nên chép đủ ở đây.

### 3.1. Thông tin chung

| Trường | Giá trị |
|---|---|
| `version` | `logistic-v2` (hồi quy logistic, tự viết, không dùng ML.NET) |
| `trainedAtUtc` | `2026-09-25T08:30:21.541228Z` (= 15:30 giờ VN, thứ Sáu) |
| `trainingSamples` | 45 |
| `trainingAccuracy` | 100 (xem 3.4: đây là AUC, không phải độ chính xác) |
| `isTrained` | true |
| `intercept` | 0.34428738974613504 |

### 3.2. Trọng số ghép với tên đặc trưng (đúng thứ tự trong code)

| # | Đặc trưng | Trọng số |
|---|---|---|
| 0 | `buy_score_norm` | 0.07791520698105994 |
| 1 | `predicted_hit_norm` | -0.05487227447743847 |
| 2 | `sector_wave_inv` | -0.6180919964625153 |
| 3 | `rs5d_norm` | 1.4063622834589635 |
| 4 | `volume_ratio_norm` | -0.010463136757546563 |
| 5 | `is_actionable` | -0.06055956510765263 |
| 6 | `dna_breakout` | -0.06055956510765263 |
| 7 | `dna_shakeout` | 0 |
| 8 | `market_favorable` | 0 |
| 9 | `atr_norm` | -1.4242198271028519 |
| 10 | `dist_ma20_norm` | 0.8047444763104681 |

Cách đọc (suy ra từ số liệu, chưa có kiểm chứng thêm):

| Quan sát | Suy ra |
|---|---|
| `is_actionable` và `dna_breakout` có trọng số giống hệt nhau tới từng chữ số | Trong 45 mẫu học, hai cột này giống nhau hoàn toàn (mẫu nào Breakout thì cũng Actionable và ngược lại) |
| `dna_shakeout` và `market_favorable` đúng bằng 0 | Hai cột này luôn bằng 0 trong mẫu học (thuật toán khởi đầu từ 0, cột toàn 0 thì không bao giờ có gradient). Model chưa từng học gì về hai đặc trưng này |
| `sector_wave_inv` âm | Ngành có sóng mạnh (giá trị 0,5) bị trừ nhiều hơn ngành không sóng (0,25): ngược trực giác. Chỉ là kết quả học từ 45 mẫu, không có lý do nghiệp vụ |
| Hai trọng số lớn nhất: `atr_norm` (−1,42) và `rs5d_norm` (+1,41) | Model chủ yếu phạt biến động cao và thưởng RS 5 phiên mạnh |

### 3.3. Cách tính xác suất

Nguồn: `Domain/OpportunityRankerModel.cs:18-28, 32`.

| Bước | Công thức |
|---|---|
| 1 | `z = intercept + Σ (trọng số[i] × đặc trưng[i])`, i = 0..10 |
| 2 | `p = 1 / (1 + e^(−z))` (sigmoid) |
| 3 | Trả `NaN` nếu model chưa học xong hoặc số đặc trưng lệch số trọng số |
| 4 | Ở service: `mlProb = clamp(round(p × 100, 1), 5, 95)` (`App/Services/OpportunityRankerService.cs:34`) |

`mlProb` là PHẦN TRĂM (5,0 đến 95,0), làm tròn 1 chữ số thập phân. Đây là điểm mấu chốt cho phần 4.

Điều kiện "model đang học xong": có trọng số, số trọng số bằng số tên đặc trưng, và `TrainingSamples >= 30` (`OpportunityRankerModel.cs:13-16`).

### 3.4. "Độ chính xác 100" thực chất là gì

| Điểm | Giải thích | Nguồn |
|---|---|---|
| Trường `TrainingAccuracy` thật ra là AUC (0-100), không phải tỉ lệ đoán đúng | `Train` gán `TrainingAccuracy = auc` | `Domain/LogisticRegressionTrainer.cs:67-72, 84-117` |
| Khi có tách holdout, giá trị lưu là AUC trên phần holdout | `modelToSave.TrainingAccuracy = promoteAccuracy` | `App/Services/OpportunityRankerTrainingService.cs:108-116` |
| Suy luận ra cỡ mẫu: 45 mẫu học = n − floor(n/5) nên n = 56, holdout = 11 mẫu | Tính từ dòng 76-78 của training service. Đây là suy luận, chưa kiểm trên DB | `OpportunityRankerTrainingService.cs:76-78` |
| Vậy "100" = AUC hoàn hảo trên khoảng 11 mẫu holdout | Quá ít để kết luận bất cứ điều gì | suy luận |

---

## 4. Điểm xếp hạng cuối và cộng điểm ngành

Công thức: `rankedScore = mlProb + bonusNgành / 100` (`Runner:203`).

Vì `mlProb` tính theo phần trăm (5-95), phép chia 100 làm bonus rất nhỏ:

| Nhóm ngành | Bonus khai báo | Cộng thật vào `rankedScore` | Nguồn |
|---|---|---|---|
| Ngành trọng yếu | +10 | +0,10 điểm phần trăm (đúng 1 nấc làm tròn của `mlProb`) | `Runner:455-462, 474` |
| Ngành phụ trợ | +5 | +0,05 điểm phần trăm (nửa nấc) | `Runner:465-471, 475` |
| Còn lại | 0 | 0 | `Runner:476` |

Hệ quả: bonus gần như chỉ phá thế hoà. Nó không đảo được thứ tự giữa hai mã lệch nhau từ 0,2 điểm phần trăm trở lên. Chưa kiểm được ý định của người viết: comment trong code chỉ nói "cộng thẳng vào điểm xếp hạng" (`Runner:202`).

### 4.1. Danh sách ngành

| Nhóm | Ngành (chuỗi khớp `SectorCatalog`, không phân biệt hoa thường) |
|---|---|
| +10 (`NganhTrongYeu`) | "Ngân hàng"; "Dịch vụ tài chính" (chứng khoán); "Thép"; "Bất động sản"; "Dầu khí" |
| +5 (`NganhPhuTro`) | "Xây dựng và Vật liệu"; "Vật liệu xây dựng"; "Bán lẻ"; "Thực phẩm và đồ uống" |

Nguồn: `Runner:455-471`.

Danh sách ngành được viết cứng trong code. Không đọc từ config, không đổi theo ngày. So khớp theo `Stock.Sector` của mã (`Runner:203`, truyền `c.Stock.Sector`).

### 4.2. Khi model không dùng được (fallback)

Nguồn: `App/Services/OpportunityRankerService.cs:21-35`.

| Tình huống | `mlProb` trả về |
|---|---|
| `Enabled = false` | `input.PredictedHitPercent` (không kẹp 5-95, không làm tròn thêm) |
| `Enabled = true`, model chưa học xong, `FallbackToLegacyHit = true` (mặc định) | `input.PredictedHitPercent` |
| Model học rồi nhưng vector lệch kích thước (`NaN`) | `PredictedHitPercent` nếu `FallbackToLegacyHit`, ngược lại 50 |
| Model dùng được | `clamp(round(p × 100, 1), 5, 95)` |

`PredictedHitPercent` là điểm "xác suất chạm" tính từ bộ chấm điểm mua (`BuyDecisionEngine`), ngoài phạm vi file này. Nhật ký chạy: "Bộ xếp hạng cơ hội ML đang hoạt động" hoặc "OpportunityRanker fallback" (`Runner:230-233`).

Mặc định `FallbackToLegacyHit = true` (`App/Options/OpportunityRankerOptions.cs:16`).

---

## 5. Hygiene, giới hạn ngành, số lượng tối đa

### 5.1. Hygiene (`Runner:538-566`)

Thứ tự kiểm tra, mã nào đầu tiên khớp thì dừng:

| Thứ tự | Điều kiện | Kết quả | Dòng |
|---|---|---|---|
| 1 | `tradeState.State == Avoid` | LOẠI (kể cả RS Leader) | 548-552 |
| 2 | `decision.IsRsLeader` | GIỮ, miễn mọi chặn theo pha | 556-557 |
| 3 | Là setup breakout VÀ không qua cổng pha | LOẠI (lý do "regime") | 559-563 |
| 4 | Còn lại | GIỮ | 565 |

Cổng pha (`PassesRegimeBreakoutGate`, `Runner:578-593`):

| Pha thị trường | Breakout được giữ khi |
|---|---|
| `Favorable` | luôn giữ |
| `Neutral` | chỉ khi `tradeState == Actionable` |
| `Unfavorable` | chỉ khi `tradeState == Actionable` |

Setup được coi là breakout nếu (`Runner:568-576`): `Entry.Type == Breakout`, hoặc `SetupDna` bắt đầu bằng "Breakout", hoặc `SetupDna` chứa "Phá vỡ".

Đối chiếu yêu cầu kiểm lại: "Avoid bị loại; breakout ngoài pha Favorable phải Actionable, trừ RS Leader" là ĐÚNG với code. Chỉ lưu ý: mã không phải breakout (ví dụ Shakeout) không bị cổng pha chặn, kể cả khi thị trường Unfavorable.

Không có chặn riêng cho `AwaitingTrigger`: "Gate 10 ExcludeAwaitingTriggerFromTop" đã bỏ (`Runner:532`). Key `ExcludeAwaitingTriggerFromTop` vẫn còn trong config nhưng không còn tác dụng.

### 5.2. Giới hạn mã mỗi ngành (`Runner:478-497`)

| Điểm | Giá trị |
|---|---|
| `MaxPerSector` | 2 (repo `appsettings.json:130`; mặc định class `MarketJobsOptions.cs:123`) |
| Cách làm | Duyệt theo thứ tự đã xếp, mỗi ngành giữ tối đa 2 mã đầu tiên |
| Ngành rỗng | Gom vào "Khác" (nhưng mã thiếu ngành đã bị loại từ cổng trước, `Runner:123-127`) |
| `MaxPerSector <= 0` | Không giới hạn |
| Ngoại lệ | Không có. Comment tại `Runner:215`: RS Leader cũng không được miễn |

### 5.3. Số mã tối đa (`Runner:227-228`)

`MaxResults = 5` trong `appsettings.json:127`. Nếu `<= 0` thì không cắt. `Rank` ghi vào DB là số thứ tự sau cùng, từ 1 (`Runner:236-241`).

---

## 6. Huấn luyện model

### 6.1. Mẫu học lấy từ đâu

| Điểm | Giá trị | Nguồn |
|---|---|---|
| Bảng | `SetupTracks`, lọc `OutcomeMeasured` và `SourceType == Opportunity`, `EntryDate >= fromDate` | `Infra/Persistence/Repositories/EfPerformanceRepositories.cs:223-233` |
| Ai vào bảng | Chỉ các mã đã lọt Top (tối đa 5 mã/ngày), ghi khi chạy đầy đủ | `Runner:297-303`, `EfPerformanceRepositories.cs:155-195` |
| Bỏ dòng không có `OpportunityRank` hoặc rank ≤ 0 | Có | `App/Services/OpportunityRankingDatasetService.cs:40-41` |
| Cửa sổ | 180 phiên mặc định, kẹp 30-365 | `OpportunityRankerOptions.cs:18`, `DatasetService:24` |
| Đặc trưng thật tại ngày vào lệnh | RS5d, VolumeRatio, ATR14%, DistMa20 được tính lại từ lịch sử giá tại `EntryDate` | `DatasetService:135-190` |
| Công thức tính lại | RS5d = (Close − Close 5 phiên trước)/Close 5 phiên trước × 100; VolumeRatio = Volume / TB 20 phiên trước, trần 5; ATR14 và SMA20 dùng `IndicatorMath` | `DatasetService:158-187` |
| Thiếu dữ liệu giá | Trả (RS5d=0, VolumeRatio=1, ATR=0, Dist=0) | `DatasetService:139-141, 153-154` |

### 6.2. Nhãn y

Nguồn: `DatasetService:192-207`; ngưỡng ở `appsettings.json:287` và `OpportunityRankerOptions.cs:13`.

| Trường hợp | Nhãn y = 1 khi |
|---|---|
| Đã đo MFE/MAE (swing) | `MFE >= SuccessThresholdPercent` VÀ `MAE > MaxAdverseExcursionPercent`, tức MFE ≥ 1% và MAE > −3% |
| Chưa đo MFE/MAE | `OutcomeBucket == "Good"` (đo ở T+2.5) |

Giá trị trong `appsettings.json`: `SuccessThresholdPercent = 1`, `MaxAdverseExcursionPercent = -3`, `ForwardSessions = 2`, `MinSessionsBeforeMeasure = 3` (`appsettings.json:283-292, 320`). Cách tính MFE/MAE và `OutcomeBucket` nằm ở bộ đo hiệu quả, chưa đọc trong phiên này: chưa kiểm được.

### 6.3. Thuật toán

| Tham số | Giá trị | Nguồn |
|---|---|---|
| Phương pháp | Gradient descent toàn bộ mẫu, khởi đầu mọi trọng số = 0 | `LogisticRegressionTrainer.cs:37-57` |
| Số vòng (`epochs`) | 800 (mặc định hàm và `TrainingEpochs`) | `LogisticRegressionTrainer.cs:14`, `OpportunityRankerOptions.cs:20` |
| Tốc độ học | 0,08 | `LogisticRegressionTrainer.cs:15` |
| L2 | 0,01 (chỉ áp lên trọng số, không áp lên intercept) | `LogisticRegressionTrainer.cs:16, 56` |
| Mẫu tối thiểu | 30, dưới đó trả model chưa học | `LogisticRegressionTrainer.cs:18, 22-23` |
| Cân bằng lớp | Mẫu dương nhân `negativeCount / positiveCount`, trần 5 | `LogisticRegressionTrainer.cs:28-32, 47` |
| Chia gradient | Cho `positiveCount × posWeight + negativeCount` | `LogisticRegressionTrainer.cs:53` |
| Điểm đánh giá | AUC (Mann-Whitney), thang 0-100 | `LogisticRegressionTrainer.cs:84-117` |

Tách holdout (`OpportunityRankerTrainingService.cs:76-88`):

| Điểm | Giá trị |
|---|---|
| Cách tách | Theo thời gian: sắp theo `EntryDate`, rồi `Symbol`; holdout = `max(1, n / 5)` mẫu MỚI nhất |
| Không tách được | Nếu số mẫu học `< 24` thì học toàn bộ, đo AUC trên chính mẫu học |
| Điểm lưu vào model | AUC holdout (nếu có) hoặc AUC mẫu học |

### 6.4. Điều kiện thay model

Hai đường vào (`OpportunityRankerTrainingService.cs:21-41, 43-163`):

| Đường | Cách gọi | Mẫu tối thiểu | Có luôn thay không |
|---|---|---|---|
| Thủ công | `POST /api/v1/ml/train/t25-ranking?days=` (cần header `X-Sync-Key`) hoặc `scripts/train-opportunity-ranker.ps1` | 30 | Có (`promoteAlways = true`) |
| Tự động | `TryAutoRetrainAsync`, chỉ khi `AutoRetrainEnabled = true` | `MinSamplesForRetrain` = 30 | Không, phải qua điều kiện dưới |

Nguồn đường thủ công: `Api/Controllers/MlController.cs:47-61`.

Điều kiện chung (cả hai đường), `OpportunityRankerTrainingService.cs:49-74`:

| Điều kiện | Giá trị |
|---|---|
| Số mẫu đã đo | `>= minSamples` |
| Số nhãn dương | `>= MinPositiveLabelsForRetrain` = 1 |

Điều kiện thay model khi tự động (`OpportunityRankerTrainingService.cs:132-134`): thay nếu `promoteAlways`, hoặc chưa có model học xong, hoặc (`accuracy >= MinAccuracyToPromote` (55) VÀ `accuracy >= accuracy của model đang chạy`). Cả hai vế phải đạt. Không đạt thì lưu thành bản phụ mà không bật (`SaveVersionOnlyAsync`, dòng 136-148).

Lưu file (`Infra/Ml/OpportunityRankerInfrastructure.cs`):

| Điểm | Giá trị |
|---|---|
| File đang chạy | `Data/opportunity-ranker-model.json` (tương đối thư mục gốc ứng dụng) |
| Bản lưu | `Data/opportunity-ranker-models/opportunity-ranker-<yyyyMMddHHmmss>.json` |
| Giữ lại | 3 bản mới nhất (`KeepModelVersions`) |
| Đổi model | `POST /api/v1/ml/ranker/revert?version=<tên file>` (`MlController.cs:127-158`) |
| Xem model | `GET /api/v1/ml/ranker/status`, `GET /api/v1/ml/ranker/versions` (`MlController.cs:93-125`) |
| Nạp khi khởi động | `OpportunityRankerBootstrap` (`OpportunityRankerInfrastructure.cs:198-220`) |
| Định dạng JSON | camelCase, thụt dòng |

### 6.5. Lịch chạy huấn luyện: code hiện tại KHÔNG có

| Sự thật | Nguồn |
|---|---|
| Hiện không có chỗ nào trong code gọi `TryAutoRetrainAsync` hay `RunWeeklyAsync` ngoài khai báo và test | tìm toàn `backend/` bằng grep |
| Trước đây có `WeeklyOpportunityReviewJob`: thứ Sáu 15:30 giờ VN, gọi tuần tự (1) review hiệu quả, (2) `TryAutoRetrainAsync`, (3) `RunWeeklyAsync` (HPO) | `git show 183adb5^:.../Jobs/WeeklyOpportunityReviewJob.cs`; cron `WeeklyReviewDay/Hour/Minute` = Friday/15/30 |
| Job này bị xoá ở commit `183adb5` (29/09/2026) | `git show 183adb5` |
| Hai khoá `WeeklyReviewHour/Minute/Day` vẫn còn trong `appsettings.json:289-291` nhưng không còn job nào dùng | `appsettings.json:283-292` |
| Model 25/09 08:30 UTC = thứ Sáu 15:30 VN khớp giờ job tuần | file model |

Kết luận (suy luận, nối từ ba dữ kiện trên): model 25/09/2026 là lần tự học cuối do job tuần chạy; sau 29/09 không còn lịch học nào trong code, nên model "bị đóng băng" cho tới khi gọi tay `POST /ml/train/t25-ranking`. Chưa kiểm được trên server có cron ngoài hay không.

`AutoRetrainEnabled = true` trong `appsettings.json:324` (mặc định class là false, `OpportunityRankerOptions.cs:23`). Spec win-rate cũ ghi "vẫn false" (ngày 10/08); repo hiện đã bật.

---

## 7. HPO (tối ưu tham số hằng tuần)

| Điểm | Giá trị | Nguồn |
|---|---|---|
| Cách chạy | `HyperparameterTuningRunner` mở tiến trình Python chạy `scripts/tune-optuna.py` | `Infra/MarketData/HyperparameterTuningRunner.cs:60-77` |
| Tham số truyền | `--trials 50 --days 60 --folds 3 --timeout 300 --output <file>` | `HyperparameterTuningRunner.cs:60-62`, `appsettings.json:261-271` |
| Hạn tiến trình | 45 phút (tối thiểu 5), quá thì kill | `HyperparameterTuningOptions.cs:26`, `HyperparameterTuningRunner.cs:80-103` |
| Tham số duy nhất được tìm | `max_results`, số nguyên từ 5 đến 15 | `scripts/tune-optuna.py:150` |
| Thuật toán | Optuna, `create_study(direction="maximize")` (mặc định TPE) | `tune-optuna.py:193` |
| Cách chấm mỗi lần thử | Backtest walk-forward 3 fold, mỗi fold `days/3` ngày (tối thiểu 10), lấy trung bình điểm | `tune-optuna.py:81-115, 130-131` |
| Gọi backtest | `POST /api/v1/ml/tune/evaluate` (header `X-Sync-Key`) | `MlController.cs:79-91` |
| Điểm fitness | `100 × winRate + 500 × avgReturn − 300 × maxDrawdown`, trừ `10` điểm cho mỗi lệnh thiếu dưới 15 lệnh; không có lệnh thì −1000 | `App/Services/TuneEvaluateService.cs:52-60`, hệ số `appsettings.json:330-339` |
| Chế độ backtest | `Strict`, giữ lệnh 3 phiên, không relaxed fallback | `TuneEvaluateService.cs:23-31`, `appsettings.json:331-339` |
| Kết quả | Chỉ GỬI tin hệ thống (Telegram/app) đề xuất `MaxResults`; KHÔNG tự áp dụng | `HyperparameterTuningRunner.cs:137-162`, `tune-optuna.py` (`autoApply: False`) |

Lưu ý: HPO KHÔNG tìm trọng số model, KHÔNG tìm `MaxPerSector`, bonus ngành hay ngưỡng hygiene. Nó chỉ thử `MaxResults` 5-15. Backtest mà HPO gọi (`backtest.RunSmartMoneyAsync`, `TuneEvaluateService.cs:23`) là đường riêng; không kiểm được trong phiên này nó có dùng đúng ranker và hygiene như `DailyAnalysisRunner` hay không: chưa kiểm được.

Trong repo: `HyperparameterTuning.Enabled = true` (`appsettings.json:262`) nhưng không còn job gọi (xem 6.5).

---

## 8. Làm mới Top trong phiên và thời gian lưu

### 8.1. Lịch chạy (giờ VN)

Nguồn: `Infra/Scheduling/QuartzSchedulingExtensions.cs:123-160`, `Infra/Scheduling/Jobs/DailyAnalysisJob.cs`, `appsettings.json:112-145`.

| Lần chạy | Giờ | Làm gì | Ghi `SetupTracks`? |
|---|---|---|---|
| Sau đóng cửa (`close-trigger`) | `DailySession` 15:00 + `DelayAfterSessionMinutes` 5 = 15:05, T2-T6 | Chạy Job 2 rồi `RunAsync` đầy đủ | Có |
| Hết phiên sáng (`morning-trigger`) | 11:30 (`MorningRunEnabled = true`), T2-T6 | Như trên | Có |
| Làm mới trong phiên (`intraday-trigger`) | Mỗi 15 phút, chỉ trong 09:00-11:30 và 13:00-14:45 | `RunAsync(runPostProcessing: false, includeStructureAndTracking: false)`, không chạy Job 2 | Không |

Cron intraday: `0 0/15 9-11,13-14 ? * MON-FRI`; job tự lọc theo khung giờ cấu hình (`DailyAnalysisJob.cs` hàm `IsInIntradayWindow`). `IntradayRefreshIntervalMinutes` kẹp 5-60. Bỏ qua ngày không phải ngày giao dịch. Job không chạy song song (`[DisallowConcurrentExecution]`).

### 8.2. Khác gì bản cuối ngày

| Điểm | Bản đầy đủ | Bản làm mới trong phiên |
|---|---|---|
| Cổng, ML, bonus, hygiene, sector cap, MaxResults | Giống nhau | Giống nhau (cùng hàm `RunAsync`, không có nhánh riêng cho phần chọn Top) |
| Ghi `DailyOpportunity` cho ngày | Có, xoá cả ngày rồi ghi lại (`Runner:283`, `EfDailyOpportunityRepository.cs:10-55`) | Có, cũng xoá cả ngày rồi ghi lại |
| Ghi `SetupTracks` (dữ liệu để học) | Có | Không (`includeStructureAndTracking = false`, `Runner:297, 327-328`) |
| Ghi Early Recovery Radar, `analysisRuns` | Có | Có (không phụ thuộc cờ) |
| Dữ liệu giá | Nến ngày đã chốt sau Job 2 | Lấy từ `stocks.GetAllAsync` tại lúc chạy: nến hôm nay có thể là nến dở dang do bộ đồng bộ giá cập nhật; chưa kiểm được cơ chế cập nhật nến trong phiên |
| Cờ `runPostProcessing` | Mặc định true | false, nhưng trong `RunAsync` KHÔNG có chỗ nào đọc cờ này (đã tìm trong file) |

Ngày giao dịch gắn cho Top (`forTradingDate`) = `GetPostSessionAnalysisDate()`: trước 15:00 là hôm nay, từ 15:00 là phiên giao dịch kế tiếp (`Runner:53`, `Application/Common/TradingCalendar.cs:60-74`). Vì vậy bản làm mới trong phiên ghi đè Top của chính hôm nay; bản chạy 15:05 ghi Top cho phiên sau.

### 8.3. `GeneratedAt`

| Điểm | Giá trị | Nguồn |
|---|---|---|
| Giá trị | `DateTime.UtcNow` lấy một lần ở đầu `RunAsync`, dùng chung cho mọi dòng Top | `Runner:54, 252` |
| Múi giờ | UTC. Muốn hiển thị giờ VN phải cộng 7 giờ khi đọc | `Runner:54` |
| Lưu DB | `DailyOpportunityEntity.GeneratedAt` | `EfDailyOpportunityRepository.cs:34` |

Cột `PredictedHitPercent` trong `DailyOpportunity` và `SetupTracks` được ghi bằng `item.MlProb` (xem bẫy số 2 bên dưới): `Runner:254, 271`.

---

## 9. Bẫy khi dựng lại

| # | Bẫy | Chi tiết và nguồn |
|---|---|---|
| 1 | Model chỉ học 45 mẫu, "100%" là AUC trên khoảng 11 mẫu holdout | Xem 3.4. Không dùng con số này để tin model |
| 2 | Vòng lặp tự nhiễm: `predicted_hit_norm` khi học là chính `mlProb` đã lưu | Top lưu `MlProb` vào cột `PredictedHitPercent` (`Runner:254`) và vào `OpportunityTrackSeed` (`Runner:271`). Lần học sau dùng cột này làm đặc trưng #1 (`DatasetService:44-48`, `OpportunityRankFeatures.FromTrack`). Còn lúc xếp hạng, đặc trưng #1 lại là `decision.PredictedHitPercent` thô từ bộ chấm điểm. Hai nguồn khác nhau: lệch phân phối giữa lúc học và lúc dùng |
| 3 | Bonus ngành gần như vô hiệu | Cộng `bonus/100` vào số phần trăm (5-95) nên chỉ +0,10 / +0,05 (phần 4). Dựng lại phải quyết: cố ý như vậy, hay chia cho thang 0-1 |
| 4 | Mẫu học chỉ gồm mã đã lọt Top | `SetupTracks` chỉ ghi Top ≤ 5 mã/ngày. Model không thấy mã bị loại nên thiên lệch chọn mẫu |
| 5 | Lệch đặc trưng `market_favorable` giữa học và dùng | Lúc xếp hạng: `context.MarketPhase == Favorable` HOẶC DNA ghi "thuận" (`OpportunityRankFeatures.cs:27-28`). Lúc học: `BuildInput` không truyền `MarketPhase` (`TrainingService:194-203`), chỉ dùng DNA |
| 6 | Hai đặc trưng trọng số 0, hai đặc trưng trùng nhau | `dna_shakeout`, `market_favorable` = 0; `is_actionable` = `dna_breakout` (phần 3.2). Mô hình thật sự chỉ dựa trên khoảng 7 chiều |
| 7 | `sector_wave_inv` có trọng số âm | Sóng ngành mạnh làm giảm xác suất. Có thể là nhiễu từ 45 mẫu |
| 8 | Fallback dùng thang khác | Khi không có model, `mlProb = PredictedHitPercent` chưa kẹp 5-95 (`OpportunityRankerService.cs:26-27`), bonus ngành vẫn cộng `/100` |
| 9 | Chưa có lịch học | Job tuần đã xoá 29/09/2026 (phần 6.5). Model đứng yên từ 25/09 |
| 10 | Sector cap đứng SAU hygiene và TRƯỚC MaxResults | Đổi thứ tự sẽ đổi kết quả. RS Leader không được miễn sector cap (`Runner:215`) |
| 11 | RS Leader miễn cổng pha nhưng KHÔNG miễn Avoid | `Runner:547-557` |
| 12 | Ghi đè Top cả ngày | `ReplaceForDateAsync` xoá hết dòng cũ của ngày. Bản làm mới trong phiên có thể đổi cả danh sách Top |
| 13 | `GeneratedAt` là UTC | Dễ nhầm khi so với giờ phiên |
| 14 | Config trong repo có thể khác server | Từ ghi nhớ của owner: `appsettings.Production.json` trên server khác bản repo. Giá trị `MaxResults`, `AutoRetrainEnabled`, `MinAccuracyToPromote` ở đây là của repo, chưa đối chiếu server |
| 15 | HPO chỉ đề xuất `MaxResults` | Và chỉ gửi tin, không tự áp dụng (phần 7) |
| 16 | Tên "accuracy" gây hiểu lầm | Các log/DTO ghi "accuracy %" nhưng giá trị là AUC (`TrainingService:107-110, 197`) |

---

## 10. Chỗ docs cũ lệch code

| Chỗ | Docs ghi | Code thật |
|---|---|---|
| `docs/features/win-rate-overhaul/spec.md:6` | "accuracy 82.5% in-sample, AutoRetrainEnabled vẫn false" (ngày 10/08) | Điểm lưu giờ là AUC (có holdout) và `AutoRetrainEnabled = true` trong repo (`appsettings.json:324`) |
| `win-rate-overhaul/spec.md:126` | Auto-retrain chạy trong `WeeklyOpportunityReviewJob` | Job đó đã xoá ở `183adb5` |
| `win-rate-overhaul/spec.md:130` | Đo accuracy in-sample, cần thêm holdout | Đã có holdout 20% theo thời gian (`TrainingService:76-88`) |
| `CLAUDE.md` (dòng "ML rank + bonus ngành") | Gợi ý bonus ngành ảnh hưởng thứ hạng | Thực tế bonus chỉ cộng 0,10 hoặc 0,05 điểm phần trăm (phần 4) |
| `docs/features/win-rate-overhaul/spec.md` (phần HM1) | `ExcludeAwaitingTriggerFromTop` loại AwaitingTrigger | Cổng đã xoá; key còn trong config nhưng vô tác dụng (`Runner:532`) |
| `OpportunityListRecommendation.cs` | Lớp "khuyến nghị hiển thị cho list Cơ hội" | Đánh dấu `[Obsolete]`, không có trong luồng Top 5 mới; thay bằng `TradeStateResolver` (`App/Services/OpportunityListRecommendation.cs:3-6`) |

## 11. Điểm chưa kiểm được

| Điểm | Lý do |
|---|---|
| Cron/tác vụ ngoài trên server gọi `/ml/train/t25-ranking` | Không SSH, không có trong repo |
| Số mẫu thật 56, holdout 11 | Suy luận từ công thức, chưa đối chiếu DB |
| Cách đo MFE/MAE và `OutcomeBucket` | Chưa đọc bộ đo hiệu quả |
| Nến trong phiên khi làm mới 15 phút | Chưa đọc bộ đồng bộ giá |
| Backtest HPO có dùng đúng ranker/hygiene của `DailyAnalysisRunner` không | Chưa đọc `BacktestService` |
| `PredictedHitPercent` thô (đặc trưng #1) tính thế nào | Nằm trong `BuyDecisionEngine`, ngoài phạm vi |
| Giá trị `appsettings.Production.json` trên server | Không đọc server |
