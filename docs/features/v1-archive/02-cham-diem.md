# V1 — Chấm điểm và phân loại

Phạm vi: cách V1 (Top 5 trang Home) chấm Buy Score, ước lượng PredictedHitPercent, gán trạng thái giao dịch, xác định pha thị trường, sóng ngành, RS và nền giá Darvas. Không gồm chuỗi cổng lọc và xếp hạng ML (có file riêng).

Đọc ngày 05/11/2026 từ code tại commit da75683 (commit ngày 03/11/2026).

Quy ước: đường dẫn viết tắt, gốc `backend/StockRadar.Domain/` (Domain), `backend/StockRadar.Application/` (App), `backend/StockRadar.Infrastructure/` (Infra). Giá OHLCV lưu theo nghìn đồng (25 = 25.000đ). Cấu hình lấy từ `backend/StockRadar.Api/appsettings.json` tại commit này; AI đã đọc file trên server khi làm PH-31 ngày 05/11/2026: 4 mục `MarketJobs`, `SmartMoney`, `PriceRunupFilter`, `OpportunityRanker` giống repo; các mục khác chưa so.

Thuật ngữ dùng trong file: "phiên kích hoạt" = phiên cuối đạt ngưỡng tăng giá và khối lượng tối thiểu (mục 1.3). "Hộp" = vùng đi ngang Darvas (mục 6). RS = chênh lệch % tăng giá 5 phiên của mã so với VNINDEX.

---

## 0. Luồng gọi một mã

`BuyDecisionEngine.Evaluate` (Domain/Services/BuyDecisionEngine.cs:60-201) chạy theo thứ tự:

| Bước | Việc | Dòng |
|---|---|---|
| 1 | Lấy dãy giá đã điều chỉnh theo quyền `LayLichSuChamDiem` | 68-69 |
| 2 | Dò tín hiệu `DetectSignals`, tính `volRatio`, `rs5` | 71-73 |
| 3 | Lấy sóng ngành của mã, phân loại pha cổ phiếu (`ClassifyStock`), tìm hộp (`AnalyzeFlatBox`) | 74-78 |
| 4 | Dựng các cờ: `hasBreakoutEntry`, `hasShakeoutEntry`, `hasDivergenceEntry`, `hasMaStack`, `hasFlatBoxBreakout`, `hasFlatBoxSetup`, `isRsLeader` | 79-112 |
| 5 | Chấm điểm `BuildScore` | 116-129 |
| 6 | Dựng điểm vào `BuildEntry` | 131-143 |
| 7 | Cổng Top `ResolveTopGateFailure` (nội dung cổng: file khác) | 145-156 |
| 8 | Nhãn khuyến nghị, PredictedHit, độ tin cậy điểm vào, trạng thái giao dịch | 158-173 |

Điểm cần biết:
- Toàn bộ chỉ số (RS, FOMO, hộp, phần trăm phiên) tính trên dãy giá đã điều chỉnh quyền, không phải giá thô (SignalAnalyzer.cs:25-26, `LayLichSuChamDiem`).
- Hai tham số `intraday15m`, `intraday1h` có trong chữ ký `Evaluate` nhưng thân hàm không dùng (BuyDecisionEngine.cs:62-64).
- `TrendSetupEvaluator`, `EntryPointEvaluator`, `PlaybookClassifier` được đăng ký DI (App/DependencyInjection.cs:45-58) nhưng không có nơi nào khác gọi (grep ngoài Domain/Services và Tests). `PlaybookClassifier` chỉ đọc các cờ `HasFlatBoxBreakout`, `HasBreakoutEntry`, `HasFlatBoxSetup`, `HasMaStack` (PlaybookClassifier.cs:27-39). Cờ không tham gia điểm.

---

## 1. Buy Score

### 1.1 Tám thành phần

Nguồn: BuyDecisionEngine.cs:203-330 (hàm `BuildScore`), trọng số gốc ở AdaptiveScoringProfile.cs:21-32.

| Id | Nhãn | Điều kiện đạt điểm thô | Tối đa | Dòng |
|---|---|---|---|---|
| `market` | Thị trường | Favorable 5, Neutral 2, còn lại 0 | 5 | 236-252 |
| `sector` | Sóng ngành | Strong 18, Emerging 10, None 0 | 18 | 254-261 |
| `rs` | Sức mạnh tương đối | `rs5` ≥ 3 → 20; `rs5` ≥ 0 → 12; âm → 0 | 20 | 263-269 |
| `base` | Nền giá | `hasFlatBoxBreakout` 18; `hasFlatBoxSetup` 12; còn lại 0 | 18 | 271-279 |
| `breakout` | Nổ hướng lên + khối lượng | `hasBreakoutEntry` → 22 | 22 | 281-292 |
| `shakeout` | Shakeout / Phân kỳ | `hasShakeoutEntry` hoặc `hasDivergenceEntry` → 10 | 10 | 294-308 |
| `volume` | Khối lượng đột biến | có tín hiệu `VolumeSpike` (volRatio ≥ 2) → 8 | 8 | 310-316 |
| `wyckoff` | Pha tăng giá | pha cổ phiếu = `Markup` → 5 | 5 | 318-324 |

Tổng tối đa gốc = 5 + 18 + 20 + 18 + 22 + 10 + 8 + 5 = 106.

Định nghĩa các cờ:

| Cờ | Điều kiện | Dòng |
|---|---|---|
| `hasBreakoutEntry` | (có tín hiệu `Breakout` hoặc `DarvasBreakout`) và đạt phiên kích hoạt và (có `DarvasBreakout` hoặc `volRatio` ≥ `BreakoutMinVolumeRatio`) | 81-85 |
| `hasShakeoutEntry` | hộp hợp lệ và `IsShakeoutFromBase` và đạt phiên kích hoạt | 86-88 |
| `hasDivergenceEntry` | có tín hiệu `BullishDivergence` và đạt phiên kích hoạt | 89-90 |
| `hasFlatBoxBreakout` | `flatBox.IsBreakoutConfirmed` và `GainFromBoxTopPercent` ≤ `MaxGainFromBasePercent` (10) | 100-101 |
| `hasFlatBoxSetup` | `IsSetupZone` (mục 6.4) | 102-106 |
| `hasMaStack` | `HasBullishMaStack` theo độ chặt theo pha (mục 1.6) | 91-96 |

### 1.2 Cách cộng và chuẩn hoá

1. Mỗi thành phần: `điểm = round(điểm thô / tối đa gốc × tối đa hiệu lực)` (hàm cục bộ `Add`, dòng 223-234). Tối đa hiệu lực lấy từ `AdaptiveScoringProfile` (mục 2.4). Hồ sơ mặc định: tối đa hiệu lực = tối đa gốc, nên điểm = điểm thô.
2. Cộng tất cả thành `score` thô.
3. Chuẩn hoá: `BuyScore = clamp(round(score thô / tổng tối đa hiệu lực × 100), 0, 100)` (dòng 329, 332-340). Mặc định mẫu số là 106.
4. Ví dụ với hồ sơ mặc định: điểm thô 85 → 80 (85/106 = 80,2); điểm thô 84 → 79. Nghĩa là muốn `BuyScore` ≥ 80 cần điểm thô ≥ 85.
5. Không có thành phần "MA stack" trong điểm. Mã `MaStack` có ánh xạ sang id `trend` ở AdaptiveScoringProfile.cs:310 nhưng `BuildScore` không tạo thành phần này (chú thích ở BuyDecisionEngine.cs:326-328).
6. Điểm của từng dòng trong `Breakdown` là điểm thô đã nhân hiệu lực, chưa chuẩn hoá. Tổng các dòng không bằng `BuyScore` hiển thị (trừ khi mẫu số = 100).
7. Thành phần `shakeout` cộng thêm độc lập với `breakout` (cả hai `Add` đều chạy). Chú thích trong code nói "chọn 1" ở dòng 294 chỉ đúng giữa shakeout và phân kỳ, không đúng giữa breakout và shakeout.
8. Điểm mốc (hồ sơ mặc định): tối đa khi không có breakout lẫn hộp = 5+18+20+10+8+5 = 66 thô → 62. Một mã điển hình đạt breakout trong hộp: market 2 + sector 10 + rs 12 + base 18 + breakout 22 + wyckoff 5 = 69 thô → 65.

### 1.3 Phiên kích hoạt (`MeetsSessionEntryBar`)

SignalAnalyzer.cs:496-502: phiên cuối tăng giá **lớn hơn hẳn** `MinSessionChangePercent` (so phiên trước, dãy đã điều chỉnh) và `Volume` ≥ `MinSessionVolume`.

Giá trị repo (appsettings.json mục `SmartMoney`): `MinSessionChangePercent` = 2, `MinSessionVolume` = 300.000 cổ phiếu, `BreakoutMinVolumeRatio` = 1,5. Giá trị mặc định trong lớp `SmartMoneyOptions` khác: 3 và 800.000 (App/Options/SmartMoneyOptions.cs:13-18). Giá trị chạy thật là của appsettings.

### 1.4 Tín hiệu dùng cho điểm

| Tín hiệu | Công thức | Dòng (SignalAnalyzer.cs) |
|---|---|---|
| `volRatio` | KL phiên cuối / KL trung bình 20 phiên gần nhất (có tính phiên cuối). Nếu trung bình ≤ 0 thì trả 1 | 65-72; IndicatorMath.cs:96-102 |
| `rs5` | % giá 5 phiên (dãy điều chỉnh) − `IndexChange5d` của VNINDEX, làm tròn 2 số | 74-78 |
| `Breakout` (Donchian 20) | Close > đỉnh High cao nhất của 20 phiên liền trước, và KL > 2 × KL trung bình 20 phiên liền trước (không tính phiên cuối), và tăng 1 phiên > 3% | 309-322 |
| `DarvasBreakout` | = `flatBox.IsBreakoutConfirmed` (mục 6) | 304-307, 514 |
| `VolumeSpike` | `volRatio` ≥ 2 | 343-344 |
| `Shakeout` | `IsShakeoutFromBase`: trong 8 phiên trước phiên cuối có phiên Low < đáy hộp và KL phiên đó < 1,2 × KL trung bình; phiên cuối Close > đáy hộp | 380-400 |
| `BullishDivergence` | phân kỳ dương RSI(14), xem 1.5 | 413-469 |
| `Distribution` | trong 5 phiên: KL phiên cuối > KL phiên đầu, giá 5 phiên đi ngang (|%| < 2), và ≥ 2 phiên có râu trên dài hơn thân | 346-363 |
| `Accumulation` | 20 phiên biên độ < 15%, không phải breakout, KL nửa sau < nửa đầu | 324-341 |
| `RelativeStrength` | RS 5 phiên > 3 (so với `ChangePercent` 1 phiên của index) | 520-521 |

Chú ý chỗ đặc biệt ở dòng 520: tín hiệu `RelativeStrength` trừ `indexChangePercent` là biến động 1 phiên (`context.Index.ChangePercent`, BuyDecisionEngine.cs:71), còn `rs5` dùng `IndexChange5d`. Tín hiệu này không vào điểm.

### 1.5 Phân kỳ dương RSI

SignalAnalyzer.cs:404-469. Hằng số: RSI 14 phiên, cửa sổ 40 phiên, pivot đáy bán kính 2, hai đáy cách ≥ 5 phiên, đáy sau trong 6 phiên gần nhất, đáy sau ≤ đáy trước × 1,01, RSI tại đáy trước ≤ 45, RSI đáy sau − RSI đáy trước ≥ 3. Xác nhận: phiên cuối là nến tăng (Close > Open), Close > Close của đáy sau và > Low đáy sau. RSI dùng trung bình đơn giản, không làm mượt (IndicatorMath.cs:168-183).

### 1.6 MA stack (chỉ cho checklist và cờ, không vào điểm, không vào cổng)

`ResolveMaStackStrictness` (BuyDecisionEngine.cs:592-614): nếu `MaStack.Enabled` = false → Off (luôn đạt). Còn lại theo pha: Favorable → Full, Neutral hoặc khác → Medium, Unfavorable → Loose (appsettings: FavorableMode "Full", NeutralMode "Medium", UnfavorableMode "Loose").

`HasBullishMaStack` (SignalAnalyzer.cs:80-123):

| Độ chặt | Điều kiện |
|---|---|
| Off | luôn đạt |
| (mọi mức) | dưới 20 phiên → không đạt |
| Loose | Close > MA20 và MA20 không dốc xuống so với 3 phiên trước (thiếu dữ liệu → coi là đạt) |
| Medium | dưới 50 phiên: MA20 ≥ Close × 0,97; từ 50 phiên: MA20 > MA50 |
| Full | dưới 50 phiên → dùng Loose; dưới 200 phiên → dùng Medium; từ 200 phiên: MA20 > MA50 > MA100 > MA200 |

`MinSessionsForMa50` = 50, `MinSessionsForFullStack` = 200.

### 1.7 Pha cổ phiếu (`ClassifyStock`)

BuyDecisionEngine.cs:639-659, theo thứ tự: có `Distribution` → Distribution; có `Shakeout` → Accumulation; (`Breakout` và `volRatio` ≥ `BreakoutMinVolumeRatio`) hoặc `DarvasBreakout` → Markup; tăng 20 phiên < −10% → Markdown; còn lại Unknown. Chỉ `Markup` cho điểm (5).

### 1.8 Nhãn khuyến nghị `BuyRecommendation`

`ResolveRecommendation` (BuyDecisionEngine.cs:616-637). Ba giá trị: `Avoid`, `Watch`, `StrongBuy` (Domain/Enums, `BuyRecommendation`).

| Thứ tự xét | Điều kiện | Nhãn |
|---|---|---|
| 1 | điểm vào trạng thái `Late` | Avoid |
| 2 | có lý do rớt cổng Top | Avoid |
| 3 | `BuyScore` ≥ 80 (`StrongBuyMinScore`) và điểm vào `Ready` | StrongBuy |
| 4 | mọi trường hợp còn lại | Watch |

Các hằng `WatchMinScore` = 70 và `WatchMinScoreWithSetup` = 60 (dòng 54-55) tồn tại nhưng không đổi kết quả: nhánh dòng 630-634 và dòng 636 đều trả `Watch`. Nhãn này chỉ dựng ở engine; API hiện suy nhãn từ trạng thái giao dịch bằng `TradeStateLabels.ToLegacyRecommendation` (App/Mapping/DtoMapper.cs:175, App/Services/MarketService.cs:435). Ánh xạ đó: `Actionable` và `BuyScore` ≥ 80 → StrongBuy; `Actionable` còn lại → Watch; `AwaitingTrigger` hoặc `Watchlist` → Watch; `Avoid` → Avoid (TradeStateLabels.cs:16-23).

Hai bộ nhãn có thể khác nhau: điểm vào `Invalid` (không có hộp) mà không rớt cổng → engine ra `Watch`, nhưng trạng thái giao dịch là `Avoid` (mục 3).

### 1.9 Điểm vào (`BuildEntry`) và mức giá

Trạng thái điểm vào `EntryPointStatus` = Ready / Watch / Late / Invalid. Thứ tự xét (BuyDecisionEngine.cs:342-501):

| Thứ tự | Điều kiện | Trạng thái |
|---|---|---|
| 1 | số phiên lịch sử < `MinHistoryDays` (250) | Invalid, "Chưa đủ dữ liệu" (366-373) |
| 2 | không có hộp hợp lệ | Invalid, "Không có nền giá" (381-387) |
| 3 | có hộp nhưng chưa `IsBreakoutConfirmed` | Watch, "Chờ phá vỡ nền giá" (404-422) |
| 4 | đã phá nền nhưng tăng so đỉnh hộp > `MaxGainFromBasePercent` (10%) | Late (426-436) |
| 5 | có kiểu điểm vào, và `rsOk` (rs5 ≥ 0 hoặc có `hasBreakoutEntry`), và thanh khoản (KL phiên cuối > KL TB 20 phiên) | xem dưới |
| 6 | còn lại | Watch, "Chờ kích hoạt" (495-500) |

Ở bước 5: `RiskReward` < 1,5 (`MinRiskRewardForReady`, dòng 58) → Watch, ghi "R:R x < 1,5 — chưa đáng vào"; ngược lại → Ready (471-492).

Quan trọng: Shakeout và Divergence cũng chỉ thành Ready khi hộp đã `IsBreakoutConfirmed`, vì bước 3 trả Watch trước khi xét kiểu điểm vào. Cờ Donchian `Breakout` mà hộp chưa xác nhận cũng chỉ ra Watch.

Mức giá (`EntryLevels`, dòng 661-694):

| Mức | Công thức |
|---|---|
| range | `max(đỉnh hộp − đáy hộp, đáy hộp × 0,02)` |
| trigger | `đỉnh hộp × 1,01` |
| entry | kiểu Breakout/Shakeout/Divergence → giá hiện tại; còn lại → `min(giá hiện tại, trigger)` |
| stop | `đáy hộp × 0,98`; nếu entry > đỉnh hộp thì `max(đáy hộp × 0,98, đỉnh hộp × 0,97)`; nếu stop ≥ entry thì `min(đáy hộp × 0,98, entry × 0,95)` |
| target | `đỉnh hộp + 2 × range`; nếu ≤ entry thì `entry + range` |
| R:R | `(target − entry) / (entry − stop)`, 2 số lẻ; 0 nếu mẫu số hoặc tử số ≤ 0 |

Độ tin cậy điểm vào `Confidence` (hiển thị): tỉ lệ mục checklist đạt (dòng 519-526), sau đó `RefineEntryConfidence` (528-541): Ready → `(checklist% + PredictedHit) / 2`; Watch → `checklist% × 0,55 + PredictedHit × 0,45`; khác → `checklist%`. Không ảnh hưởng điểm.

Khi rớt cổng Top, điểm vào `Ready` bị hạ thành `Watch` (`AlignEntryWithTopGate`, dòng 503-517). `ActionScore` = `BuyScore` nếu qua cổng, ngược lại 0 (dòng 172).

---

## 2. PredictedHitPercent

### 2.1 "Hit" nghĩa là gì

Một setup được tính "Good" (trúng) khi lãi tính từ giá vào đến giá T+2,5 (sau 2 phiên rưỡi) ≥ `SuccessThresholdPercent` = 1% (App/Options/OpportunityPerformanceOptions.cs:16-17; appsettings.json mục `OpportunityPerformance`: `ForwardSessions` 2, `MinSessionsBeforeMeasure` 3, `SuccessThresholdPercent` 1, `FlatMinPercent` 0). Dưới 0% là "Failed", giữa là "Flat". Hàm phân loại: App/Services/ShadowAnalysisService.cs:144-152 và Infra/MarketData/SmartMoneyBacktestRunner.cs:302.

Hiệu chỉnh dùng cột `OutcomeBucket == "Good"` của bảng setup đã đo (App/Services/HitCalibrationService.cs:15-20). Chưa kiểm được: nơi ghi cột `OutcomeBucket` cho setup thật trong code hiện tại (hàm `UpdateOutcomeBucketAsync` ở Infra/Persistence/Repositories/EfPerformanceRepositories.cs:67 không có nơi gọi trong grep).

### 2.2 Công thức

HitProbabilityPredictor.cs:14-91:

1. Lấy các thành phần có điểm > 0. Không có thành phần nào → trả 50%, 0 mẫu (dòng 22-26).
2. Với mỗi thành phần: trọng `w = điểm / tối đa`; độ tin cậy `rel = ReliabilityPercent` của hồ sơ thích nghi (nếu ≤ 0 thì 50). `baseProb = Σ(w × rel) / Σw` (dòng 28-43).
3. Nhân các hệ số rồi chặn trong [8, 92]:

| Hệ số | Giá trị | Dòng |
|---|---|---|
| theo pha thị trường | Favorable 1,05; Neutral 1,0; còn lại 0,9 | 44-49 |
| theo kiểu điểm vào | Breakout → `clamp(0,85 + rel_breakout/500, 0,85, 1,15)`; Shakeout hoặc Divergence → cùng công thức với `rel_shakeout`; khác → 0,98 | 51-56, 93-98 |
| theo BuyScore | ≥ 80 → 1,06; ≥ 70 → 1,03; còn lại 1 | 58-63 |
| theo sóng ngành | Strong 1,04; Emerging 1,01; None 0,97 | 65-70 |

4. Làm tròn 1 số lẻ, rồi áp hồ sơ hiệu chỉnh `Calibration.Apply` (mục 2.3) (dòng 72-74).
5. `SampleCount` = số mẫu nhỏ nhất trong các thành phần đang có điểm (dòng 30, 39-41, 89).
6. `TopExplainLines` = 3 thành phần có `w × rel` lớn nhất, dạng "Nhãn: chi tiết" (dòng 76-87).
7. `SetupDna` = "{Breakout|Shakeout|Divergence|Chờ kích hoạt} · {TT thuận|Nỗ lực hồi phục|TT bất lợi} · {Sóng ngành mạnh|Chớm sóng ngành|Ngành chưa có sóng}" (dòng 100-126).

Với hồ sơ mặc định (mọi `rel` = 50, không hiệu chỉnh): kết quả = `50 × hệ số pha × hệ số kiểu vào × hệ số điểm × hệ số ngành`, trong đó hệ số kiểu vào = 0,95 (Breakout/Shakeout/Divergence) hoặc 0,98 (khác). Cao nhất ≈ 55,0 (Favorable, Breakout, điểm ≥ 80, sóng mạnh: 50 × 1,05 × 0,95 × 1,06 × 1,04). Thấp nhất ≈ 41,5 (50 × 0,9 × 0,95 × 1 × 0,97). Nếu không thành phần nào có điểm thì đúng 50.

### 2.3 Hồ sơ hiệu chỉnh mặc định và cách dựng

`HitCalibrationProfile.Default` = không nhóm, hệ số chung 1,0, 0 mẫu (Domain/ValueObjects/HitCalibrationProfile.cs:16). `IsCalibrated` khi tổng mẫu ≥ 10 (dòng 34-35, `MinSamplesForGlobal`). Chưa hiệu chỉnh → `Apply` trả nguyên giá trị.

`Apply` (dòng 39-52): tìm nhóm chứa giá trị dự đoán; nhóm có ≥ 5 mẫu (`MinSamplesPerBucket`) thì dùng hệ số nhóm, không thì hệ số chung; kết quả `clamp(round(giá trị × hệ số, 1), 5, 92)`. Nếu không nhóm nào chứa giá trị thì lấy nhóm đầu tiên của từ điển (dòng 55-64, không phải nhóm gần nhất).

`HitCalibrationBuilder.Build` (Domain/Services/HitCalibrationBuilder.cs:15-49): 4 nhóm theo giá trị dự đoán: 50-59 (giữa 55), 60-69 (65), 70-79 (75), 80-92 (85) (dòng 7-12). Tỉ lệ trúng thực = số "Good" / số mẫu trong nhóm. Hệ số = `clamp(tỉ lệ thực / giữa nhóm, 0,7, 1,35)`, 3 số lẻ (dòng 51-57). Hệ số chung = tỉ lệ thực toàn bộ / trung bình dự đoán, cùng chặn. Nhóm dưới 50 không có.

Nạp: `HitCalibrationProfileFactory.LoadAsync` đọc từ kho (App/Services/HitCalibrationProfileFactory.cs:5-9). Dựng lại: `HitCalibrationService.RebuildAsync` (App/Services/HitCalibrationService.cs:12-26). Chưa kiểm được: có job nào gọi `RebuildAsync` không. Grep ngoài Tests chỉ thấy khai báo DI (App/DependencyInjection.cs:54), không thấy nơi gọi. Cũng không có nơi gọi `UpsertWeightsAsync` (ghi trọng số tiêu chí). Nghĩa là trong code hiện tại, hai bảng hồ sơ này không có thành phần ghi; nội dung bảng trên production chưa kiểm.

### 2.4 Hồ sơ thích nghi mặc định (`AdaptiveScoringProfile`)

- Mặc định: mọi thành phần `WeightMultiplier` 1, `ReliabilityPercent` 50, `SampleCount` 0, hoạt động (AdaptiveScoringProfile.cs:360-364). Thành phần không có trong từ điển cũng trả trạng thái mặc định (dòng 320-323).
- Tối đa hiệu lực (dòng 12-15): hoạt động → `max(1, round(tối đa gốc × clamp(WeightMultiplier, 0,25, 2,5)))`; không hoạt động → `max(0, round(tối đa gốc × 0,25))`.
- Nạp từ bảng trọng số tiêu chí: `FromWeightDetails` (dòng 60 trở xuống): `WeightMultiplier` = trọng số nếu hoạt động, ngược lại 0,25; `ReliabilityPercent` = `Reliability7d` nếu > 0, nếu không thì `Accuracy7d`, nếu không thì 50. Bảng trống → `Default` (App/Services/AdaptiveScoringProfileFactory.cs:8-14).
- Công thức trọng số động từ độ tin cậy 7 ngày: `0,5 + reliability/100 × 1,5`, dưới 30 mẫu thì 1, không hoạt động 0,25 (Domain/Services/CriterionReviewHelper.cs:60-66). Nhưng như nói ở 2.3, chưa thấy nơi nào ghi bảng này.
- `Math.Round` mặc định của C# làm tròn về số chẵn (ví dụ 4,5 → 4). Ảnh hưởng tối đa hiệu lực khi hồ sơ không mặc định.

---

## 3. Trạng thái giao dịch (`TradeStateResolver`)

Domain/Services/TradeStateResolver.cs:14-64. Bốn trạng thái (Domain/Enums/StockTradeState.cs): `Avoid` ("Tránh"), `Watchlist` ("Theo dõi"), `AwaitingTrigger` ("Chờ kích hoạt"), `Actionable` ("Vào ngay") (nhãn ở TradeStateLabels.cs:7-14).

| Thứ tự | Điều kiện | Trạng thái | Lý do hiển thị |
|---|---|---|---|
| 1 | điểm vào `Late` hoặc `Invalid` | Avoid | headline của điểm vào |
| 2 | có lỗi cổng và là "cổng nặng" | Avoid | nội dung cổng |
| 3 | có lỗi cổng, không nặng | AwaitingTrigger | nội dung cổng |
| 4 | điểm vào `Ready` | Actionable | "Mua mạnh — đạt chuẩn SmartMoney" nếu `BuyScore` ≥ 80, nếu không "Đạt chuẩn SmartMoney" |
| 5 | điểm vào `Watch`, đang trong danh sách (`OnOpportunityList`) | Watchlist | headline; nếu headline chứa "phá vỡ" hoặc "Phá vỡ nền giá" thì "Chưa phá vỡ nền / Chờ phiên kích hoạt" |
| 6 | điểm vào `Watch`, không trong danh sách | AwaitingTrigger | headline |
| 7 | còn lại | Avoid | "Không đạt tiêu chí tối thiểu" |

"Cổng nặng" (dòng 59-63): nội dung cổng chứa "lịch sử", "Thanh khoản", "phân phối" hoặc "FOMO" (không phân biệt hoa thường). Trong chuỗi cổng hiện tại chỉ FOMO còn có thể xuất hiện ("FOMO +x% so đáy 5 phiên"). Cổng chia chác ("Chờ chốt quyền…"), cổng thị trường khó và cổng ngành chưa có sóng đều thuộc nhóm không nặng → AwaitingTrigger. Hệ quả: một mã chia chác có điểm vào `Ready` bị `AlignEntryWithTopGate` hạ thành Watch trước khi vào resolver, nên ra AwaitingTrigger.

Hai nơi gọi:
- Trong `Evaluate`: không truyền `listContext` → `OnOpportunityList` = false (BuyDecisionEngine.cs:173) → Watch ra `AwaitingTrigger`.
- Trong runner Top, mỗi ứng viên được tính lại với `OnOpportunityList` = true (Infra/MarketData/DailyAnalysisRunner.cs:184-188) → Watch ra `Watchlist`. Đây là trạng thái được lưu cùng Top.

Hệ quả cần nhớ: điểm vào `Invalid` do không có hộp luôn ra `Avoid`. Hygiene ở runner loại `Avoid` trước mọi miễn trừ, kể cả RS Leader (DailyAnalysisRunner.cs:548-552). Vì vậy mã không có hộp không bao giờ vào Top, dù khoá `RequireBaseBreakout` đang để false (khoá này không còn tác dụng, chỉ có trong options).

---

## 4. Pha thị trường (`MarketPhaseClassifier`)

Domain/Services/MarketPhaseClassifier.cs:12-186. Ba pha trong `MarketWyckoffPhase`: `Favorable`, `Neutral`, `Unfavorable` (có thêm `Unknown` nhưng `Classify` không trả). Nhãn hiển thị: Favorable "TT thuận", Neutral "Nỗ lực hồi phục", Unfavorable "Điều chỉnh" (App/Common/MarketPhaseDisplay.cs:8-14); DNA ghi "TT bất lợi" cho Unfavorable (HitProbabilityPredictor.cs:112-117).

### 4.1 Điều kiện

| Pha | Điều kiện (xét theo thứ tự) |
|---|---|
| Unfavorable | dưới 20 phiên dữ liệu (dòng 18-28); hoặc Close phiên cuối ≤ MA20 (dòng 37-47) |
| Favorable | Close > MA20 **và** MA20 không dốc xuống **và** có Follow-Through Day **và** có đáy cao dần (dòng 49-59) |
| Neutral | Close > MA20 nhưng thiếu một trong ba điều kiện kia (dòng 61-68) |

Thông số (Domain/ValueObjects/MarketPhaseThresholds.cs:1-14; appsettings mục `SmartMoney:MarketPhase` giống mặc định): `FtdMinGainPercent` 1,2; `FtdMinRallyDay` 4; `FtdMaxRallyDay` 7; `Ma20SlopeLookbackSessions` 3; `HigherLowLookbackSessions` 60; `HigherLowPivotRadius` 2; `RallyLookbackSessions` 20.

- MA20: trung bình Close 20 phiên gần nhất (IndicatorMath.cs:45-56).
- Dốc MA20: `MA20 hiện tại ≥ MA20 của 3 phiên trước`; dưới 23 phiên dữ liệu thì coi là đạt (dòng 155-164).
- Follow-Through Day, `FindFollowThroughDay` (dòng 71-117): quét ngày thứ nhất của nỗ lực hồi phục từ `Count − max(20×3, 60)` = 60 phiên gần nhất đến trước `Count − 4`. Ngày thứ nhất = phiên tăng (Close > Close hôm trước) **sau một phiên giảm** (Close hôm trước < Close hôm kia; nếu bằng nhau thì không tính). Với mỗi ngày thứ nhất, xét các phiên ngày thứ 4 đến thứ 7 của nỗ lực; một phiên là FTD khi cùng lúc: tăng ≥ 1,2% so phiên trước; KL > KL phiên trước; KL > KL trung bình 20 phiên liền trước. Có một phiên đạt là đủ. Không kiểm tra điều kiện "không thủng đáy ngày thứ nhất" của FTD kiểu O'Neil.
- Đáy cao dần, `HasHigherLow` (dòng 119-153): trong 60 phiên gần nhất, tìm các đáy pivot theo `Low` (thấp nhất trong bán kính 2 phiên mỗi bên; bằng nhau vẫn tính pivot). Đạt khi tồn tại **một cặp đáy liên tiếp** mà đáy sau cao hơn đáy trước (không nhất thiết là cặp mới nhất). Dữ liệu ít hơn `2×2+2` phiên trong cửa sổ → không đạt.

### 4.2 Dữ liệu VNINDEX đọc từ đâu

- Pha dựng ở `SmartMoneyOpportunitySelector.BuildContext` (Domain/Services/SmartMoneyOpportunitySelector.cs:128-130): `MarketPhaseClassifier.Classify(index.Bars, settings.PhaseThresholds)`.
- `index.Bars` = `MarketIndex.History` (Domain/Entities/MarketEntities.cs:29-41), nạp từ cột `HistoryJson` của một dòng trong bảng `MarketIndices` (Infra/Persistence/Mapping/EntityMapper.cs:72-84; Infra/MarketData/MarketDataServices.cs:46-53, `DatabaseMarketIndexProvider.GetCurrentAsync` rồi `EfMarketIndexRepository.GetAsync`). `DailyAnalysisRunner` lấy index ở dòng 55 và dựng ngữ cảnh ở dòng 68.
- `IndexChange5d` = `% (Close cuối so Close 5 phiên trước)` từ chính `HistoryJson`; nếu dưới 6 phiên hoặc mẫu số ≤ 0 thì dùng `ChangePercent` 1 phiên; và nếu giá trị 5 phiên bằng đúng 0 thì thuộc tính `IndexChange5d` cũng rơi về `ChangePercent` (EntityMapper.cs:97-108; MarketEntities.cs:38).

### 4.3 Vì sao trên production pha không bao giờ Favorable (đã đối chiếu code)

Dữ liệu ghi vào `HistoryJson` của VNINDEX chỉ đi qua `EfMarketDataWriter.UpsertIndexAsync` (Infra/Persistence/Repositories/EfMarketDataWriter.cs:253-299). Mỗi lần gọi, nó tạo một nến duy nhất `new OhlcvBar(ngày, price, price, price, price, 0)` (dòng 270): Open = High = Low = Close = giá hiện tại, **Volume = 0**. DTO đầu vào chỉ có `Symbol`, `Price`, `ChangePercent` (App/DTOs/MarketSyncDtos.cs). Trong ngày, nến hôm nay bị ghi đè theo ngày (hàm `MergeBars`, dòng 301-310).

Hệ quả theo từng cột:
- `Volume` = 0: trong `FindFollowThroughDay` điều kiện `bar.Volume <= prev.Volume` (0 ≤ 0) luôn đúng, nên `continue` (dòng 105-106). Không bao giờ có FTD, `HasFollowThroughDay` luôn false, nên không bao giờ qua nhánh Favorable (dòng 49). Đây là nguyên nhân chính. Cũng vì KL trung bình 20 phiên = 0 nên điều kiện ở dòng 108-110 cũng chặn.
- `Low` = Close: `HasHigherLow` chạy được nhưng thực chất tìm pivot trên giá đóng cửa; không chặn Favorable.
- `High` = Close: không ảnh hưởng `MarketPhaseClassifier` (chỉ dùng Close, Low, Volume), nhưng `VnIndexPriorPeakAnalyzer` dùng High để tìm đỉnh swing (VnIndexPriorPeakAnalyzer.cs:39, 126-137) nên đỉnh kháng cự cũng chỉ là đỉnh Close.
- Lịch sử chỉ từ 25/06/2026 (đo bởi owner): chưa đủ để xét đỉnh cũ xa (`FindActiveResistance` mặc định nhìn lại 750 phiên, dòng 20).

Kết luận: pha tối đa là Neutral (khi Close > MA20), còn lại Unfavorable. Tác động xuống dưới: thành phần `market` chỉ được 2 hoặc 0 điểm (không bao giờ 5); hệ số pha 1,0 hoặc 0,9 (không bao giờ 1,05); MA stack dùng Medium hoặc Loose (không bao giờ Full). Chưa kiểm được: nơi gọi `UpsertIndexAsync` hằng ngày (đường nhận được là `MarketSyncService`, App/Services/MarketSyncService.cs:19, và lần đầu từ KBS ở MarketDataServices.cs:20-24).

### 4.4 Đỉnh kháng cự VNINDEX (phụ, phục vụ cổng bull-trap ở Home)

`VnIndexPriorPeakAnalyzer` (Domain/Services/VnIndexPriorPeakAnalyzer.cs): tìm swing high trong `lookback` 750 phiên, bán kính pivot 2, phải còn nằm trên giá hiện tại và "nhô" (prominence, đo bằng đáy thấp nhất sau đỉnh) ≥ 3%; chọn đỉnh thấp nhất trên giá hiện tại (dòng 16-54). "Gần đỉnh" = cách đỉnh ≤ 1,5% (dòng 56-66). Môi trường bull-trap = cổng bật và gần đỉnh và pha khác Favorable (dòng 73-90). Chỉ `MarketService` gọi: thẻ VNINDEX (MarketService.cs:60-75) và dòng trạng thái cổng bull-trap (dòng 502-521). Tham số lấy từ `MasterAlertOptions` (`BullTrapPeakLookbackSessions`, `BullTrapPivotRadius`, `BullTrapMinProminencePercent`, `BullTrapNearPeakBandPercent`), không phải mặc định của hàm; giá trị các khoá này chưa kiểm. Không vào Buy Score. Thẻ VNINDEX khi nến cuối Volume = 0 thì lấy khối lượng từ tổng KL thị trường (MarketService.cs:56-58).

---

## 5. Sóng ngành, regime nhiều phiên, RS percentile, RS Leader

### 5.1 Ảnh chụp ngành trong phiên (`BuildSectorSnapshots`)

Domain/Services/SmartMoneyOpportunitySelector.cs:206-268, phân loại ở dòng 270-297.

1. Chỉ lấy mã có ngành khác rỗng, khác "Khác"/"Other"/"N/A" (dòng 195-204) và có ≥ `MinHistoryDays` (250) phiên.
2. Gom theo ngành; ngành có dưới `MinStocksPerSector` (3) mã bị bỏ, không có ảnh chụp → `SectorWaveFor` trả `Unknown` (không sóng) (dòng 51-56).
3. Với mỗi ngành, lấy % thay đổi 1 phiên của từng mã, giữ giá trị trong khoảng (−95, 500) (loại dữ liệu hỏng):
   - độ rộng `advancerRatio` = số mã tăng (>0) / số mã hợp lệ;
   - trung vị % phiên;
   - tỉ lệ gần trần = số mã tăng ≥ `NearCeilingChangePercent` (4%) / số mã hợp lệ;
   - `volumeRatio` = tổng KL phiên cuối của ngành / tổng KL trung bình 20 phiên của các mã trong ngành;
   - `avgRs5d` = trung bình cộng RS 5 phiên của mọi mã trong ngành (không lọc theo khoảng hợp lệ).
4. Phân loại:

| Trạng thái | Điều kiện | Điểm Buy Score |
|---|---|---|
| None | độ rộng < `MinAdvancerRatio` (0,6), hoặc đủ độ rộng nhưng không đạt điều kiện nào trong ba điều kiện dưới | 0 |
| Emerging | đủ độ rộng và đạt ít nhất 1 trong 3: lực, tiền vào, RS ngành | 10 |
| Strong | đủ độ rộng và đạt cả 3 | 18 |

Ba điều kiện: lực = trung vị ≥ `MinMedianChangePercent` (1,5) **hoặc** tỉ lệ gần trần ≥ `MinNearCeilingRatio` (0,25); tiền vào = `volumeRatio` ≥ `MinVolumeRatio` (1,3); RS ngành = `avgRs5d` > `MinSectorRs5d` (0, lớn hơn hẳn). Giá trị trong appsettings.json mục `SmartMoney:SectorWave` trùng mặc định.

`SectorSnapshot.WaveScore` (0-100, chỉ để xếp danh sách ngành trên UI, không vào Buy Score): 40 × độ rộng + 25 × clamp(trung vị/3) + 20 × clamp(2 × tỉ lệ gần trần) + 15 × clamp((volumeRatio − 1)/0,8) (Domain/ValueObjects/AnalysisResults.cs:186-199).

### 5.2 Regime nhiều phiên (`SectorWaveRegimeEngine`)

Domain/Services/SectorWaveRegimeEngine.cs:25-80; trạng thái `SectorWaveRegimeState` ở AnalysisResults.cs:134-141. Mỗi phiên, mỗi ngành có ảnh chụp:

| Tình huống | Kết quả |
|---|---|
| ảnh chụp hôm nay có sóng (Strong hoặc Emerging) | Active, `SessionsSinceActivation` = 1, đếm cạn tiền = 0 (dòng 34-42) |
| chưa có trạng thái trước, hoặc trước đó không Active | không Active (dòng 44-52) |
| đang Active, hôm nay không sóng | tăng số phiên; nếu `volumeRatio` < `FailureMaxVolumeRatio` (0,5) thì tăng đếm "cạn tiền" liên tiếp, ngược lại reset 0 (dòng 55-57) |
| đếm cạn tiền ≥ `FailureConsecutiveSessions` (3), hoặc số phiên ≥ `MaxActiveSessions` (20) | hết Active, ghi `FailedOn` (dòng 59-70) |
| còn lại | vẫn Active (dòng 72-79) |

Chỗ dùng: runner tính và lưu mỗi phiên rồi nhét tập ngành đang Active vào `context.ActiveSectorRegimes` (DailyAnalysisRunner.cs:92-94, hàm `AdvanceSectorWaveRegimesAsync` dòng 600-620; ngành đã tính cho cùng ngày thì đọc lại, ngành không có ảnh chụp hôm nay thì không advance). Regime **chỉ** được dùng ở cổng "Ngành chưa có sóng + RS âm" (BuyDecisionEngine.cs:586), không vào điểm: thành phần `sector` luôn theo sóng đúng phiên hiện tại. Có dịch vụ dựng lại lịch sử regime: Infra/MarketData/SectorWaveRegimeBackfillService.cs.

### 5.3 RS percentile (`RsPercentileCalculator`)

Domain/Services/RsPercentileCalculator.cs:16-45; gọi với `days` = 5 (SmartMoneyOpportunitySelector.cs:148-157).

1. Rổ = mã có số phiên ≥ `max(MinHistoryDays, days + 1)` = 250. Không còn lọc thanh khoản ở đây.
2. RS mỗi mã = `GetRelativeStrength(mã, IndexChange5d, 5)`; sắp tăng dần.
3. Percentile = `chỉ số thứ tự / (n − 1) × 100`, 2 số lẻ. Một mã duy nhất → 100. Mã bằng nhau về RS vẫn nhận percentile khác nhau (theo thứ tự sắp).
4. Mã ngoài rổ (dưới 250 phiên) không có khoá; engine đọc bằng `GetValueOrDefault(symbol, 0)` nên coi là 0 (BuyDecisionEngine.cs:97).

### 5.4 RS Leader

BuyDecisionEngine.cs:108-112:

`isRsLeader = (hasBreakoutEntry hoặc hasFlatBoxBreakout) và rsPercentile ≥ RsLeaderMinRsPercentile (85) và rs5 ≥ 0`.

Tác dụng (phần thân cổng nằm ở file khác, ghi lại để khỏi đọc lại): miễn cổng "Thị trường khó" và cổng "Ngành chưa có sóng + RS âm" (BuyDecisionEngine.cs:580-587), và miễn chặn breakout theo pha trong hygiene (DailyAnalysisRunner.cs:556). Không miễn cổng chia chác, không miễn trạng thái `Avoid`. Chặn "Thị trường khó" dùng `MinRsPercentileForUnfavorable` = 80.

---

## 6. Nền giá, hộp phẳng Darvas

Có hai đường "nền" trong code. V1 chỉ dùng đường 1 (hộp Darvas, kiểu `FlatBoxProfile`). Đường 2 (`BaseQualityEvaluator` chấm chất lượng nền kiểu VCP) V1 không dùng để chấm điểm (mục 6.5).

### 6.1 Thông số

`PriceRunupFilter` trong appsettings.json (kiểu `BasePriceFilterSettings`, Domain/ValueObjects/AnalysisResults.cs:57-68) và `DarvasBoxSettings` (Domain/ValueObjects/DarvasBoxSettings.cs:1-18; App/Options/DarvasBoxOptions.cs:5-15). Giá trị repo trùng mặc định:

| Khoá | Giá trị | Nghĩa |
|---|---|---|
| `ConsolidationMinSessions` | 10 | số phiên tối thiểu của hộp |
| `MaxBaseWindowSessions` | 45 | số phiên tối đa của hộp |
| `MaxGainFromBasePercent` | 10 | ngưỡng chống FOMO so đỉnh hộp |
| `Darvas.MaxBoxHeightPercent` | 9 | (không dùng khi dò hộp V1, xem 6.2) |
| `Darvas.BreakoutMaxBoxHeightPercent` | 10 | độ cao tối đa của hộp khi dò |
| `Darvas.ShadowTolerancePercent` | 3 | râu nến được vượt biên Close tối đa |
| `Darvas.TouchThresholdPercent` | 1,5 | khoảng cách tính là "chạm" biên |
| `Darvas.MinTopTouches`, `MinBottomTouches` | 2, 2 | số lần chạm biên trên, biên dưới |
| `Darvas.MaxLast3AvgRangePercent` | 3,5 | biên độ trung bình 3 phiên cuối hộp |
| `Darvas.VolDryUpRatio` | 0,8 | KL cạn (không dùng khi dò hộp V1) |
| `Darvas.BreakoutMinPriceGainPercent` | 2,5 | phiên phá nền tăng tối thiểu |
| `Darvas.BreakoutMinVolumeMultiplier` | 1,5 | KL phiên phá / KL TB hộp |
| `Darvas.BreakoutMaxUpperShadowRatio` | 0,25 | râu trên / biên độ phiên tối đa |

Các khoá khác trong `PriceRunupFilter` (`MaxScanSessions` 90, `MinBaseQualityScore` 50, `StrongBaseQualityScore` 80, `IdealBaseMinSessions` 15, `IdealBaseMaxSessions` 40, `MinPriorImpulsePercent` 15, `PriorImpulseLookbackSessions` 30) chỉ dành cho đường 2.

### 6.2 Cách tìm hộp (`DarvasBreakoutAnalyzer.Analyze`)

Domain/Services/DarvasBreakoutAnalyzer.cs:9-99, 150-182. Gọi với `minSessions` = 10, `maxSessions` = 45. Cần ≥ 12 phiên dữ liệu.

1. Duyệt `boxEnd` (phiên cuối của hộp) từ `Count − 2` (hộp luôn loại phiên cuối) lùi về `minSessions − 1`.
2. Với mỗi `boxEnd`, thử độ dài từ 45 giảm dần xuống 10; lấy **độ dài dài nhất** qua được `PassesDarvasBox` (dòng 162-181, với `requireVolumeDryUp: false`, `maxBoxHeightPercent` = 10).
3. Gặp `boxEnd` đầu tiên có hộp thì xét phá vỡ; xong dừng, không thử `boxEnd` cũ hơn (dòng 95).
4. Hộp ghi: `BoxHigh` = Close cao nhất, `BoxLow` = Close thấp nhất của hộp (không phải High/Low), `GainFromBoxTopPercent` = `(Close cuối − BoxHigh)/BoxHigh × 100` (dòng 34-49).

`PassesDarvasBox` (Domain/Services/BaseQualityEvaluator.cs:192-278) các điều kiện, tất cả phải đạt:

| # | Điều kiện | Dòng |
|---|---|---|
| 1 | ≥ 10 phiên | 200-202 |
| 2 | `(maxClose − minClose)/minClose` ≤ 10% (qua `BreakoutMaxBoxHeightPercent`) | 215-218 |
| 3 | mọi High ≤ `maxClose × 1,03` và mọi Low ≥ `minClose × 0,97` | 220-224 |
| 4 | số phiên có Close trong `minClose × 1,5%` của `maxClose` ≥ 2, và của `minClose` ≥ 2 | 226-238 |
| 5 | (bỏ qua ở V1) KL phần ba cuối ≤ 0,8 × phần ba đầu, và < MA20 KL | 240-257 |
| 6 | biên độ `(High−Low)/Low` trung bình 3 phiên cuối ≤ 3,5% | 259-275 |

### 6.3 Điều kiện phá vỡ (`PassesBreakoutGates`)

DarvasBreakoutAnalyzer.cs:184-222. Với **mọi** phiên `bi` từ `boxEnd + 1` đến phiên cuối, phiên đạt đủ cả bốn thì hộp là `IsBreakoutConfirmed`:

1. Close > `BoxHigh`.
2. Tăng so phiên trước ≥ 2,5% (`priceGain`).
3. KL phiên / KL trung bình của hộp ≥ 1,5 (KL TB hộp > 0).
4. Râu trên `(High − Close)/(High − Low)` ≤ 0,25 (nếu biên độ > 0).

Quan trọng: nến phá vỡ không nhất thiết là phiên cuối. Phá vỡ xảy ra vài phiên trước vẫn cho `IsBreakoutConfirmed` = true; chống FOMO nằm ở `GainFromBoxTopPercent` ≤ 10% và cổng FOMO 5 phiên (7%). Trả về `PriceGainPercent`, `VolumeMultiplier` của phiên phá đầu tiên đạt.

Hộp chưa phá: `HasValidBox` = true, `IsBreakoutConfirmed` = false (dòng 81-95).

Nhãn sự kiện (`BasePriceLabels.ResolveEventLabel`, Domain/Services/BasePriceLabels.cs:15-28): không hộp → "Nền giá"; Close cuối < đáy hộp → "Gãy nền"; phá vỡ và `PriceGainPercent` ≥ 3 → "Nổ hướng lên"; phá vỡ → "Phá vỡ nền giá"; còn lại "Nền giá".

### 6.4 Vùng chuẩn bị (`IsSetupZone`)

DarvasBreakoutAnalyzer.cs:105-128: hộp hợp lệ, chưa phá, Close ≥ đáy hộp, `GainFromBoxTopPercent` ≤ `MaxGainFromBasePercent`, và Close cách đáy hộp hoặc cách đỉnh hộp ≤ `TouchThresholdPercent × 2` = 3%. Cho thành phần `base` 12 điểm. Lưu ý: dù có điểm này, entry của mã chưa phá nền vẫn là Watch (1.9).

Mã "shakeout" cần hộp hợp lệ (cờ `hasShakeoutEntry` đòi `flatBox.HasValidBox`), còn "phân kỳ" không cần hộp để có cờ.

### 6.5 Chấm chất lượng nền `BaseQualityEvaluator` (đường 2, không dùng chấm Buy Score)

V1 chỉ gọi `PassesDarvasBox` (tĩnh) từ bộ dò hộp. Phần còn lại (`FindBestBase`, `FindTopBases`, `ScoreWindow`) chỉ nằm sau `SignalAnalyzer.AnalyzeBasePrice*`; grep trong App, Infra, Api không thấy nơi gọi. Ghi lại để dựng lại nếu cần:

- Tổng điểm = 0,15 × PriorTrend + 0,20 × AtrContraction + 0,20 × Compression + 0,20 × VolumeDry + 0,15 × ContractionPattern + 0,05 × Distribution + 0,05 × Duration (Domain/ValueObjects/BaseQualityComponents.cs:13-20). Ngưỡng giữ cửa sổ: `MinBaseQualityScore` 50; "mạnh" 80.
- Qua cổng pipeline (BaseQualityEvaluator.cs:142-167): có xu hướng tăng trước nền (tăng ≥ 15% trong 30 phiên trước, EMA20 dốc lên, nửa sau trước nền cao hơn nửa đầu ≥ 3%, neo ≥ 0,97 × EMA50) → tối đa 2 phiên giảm mạnh ≥ 5% với KL > 1,5 × TB trong 15 phiên cuối → hình thái một trong VCP, Darvas, Spring → biên độ cả nền ≤ 18% và lệch đầu-cuối ≥ −4% (Spring: ≥ −8%).
- Thang điểm từng thành phần: PriorTrend theo mức tăng trước nền (≥ 40% → 100; ≥ 30 → 90; ≥ 25 → 80; ≥ 20 → 70; ≥ 15 → 60; còn lại 30), ATR co (tỉ lệ ATR quý cuối/đầu: < 0,5 → 100; < 0,65 → 90; < 0,75 → 80; < 0,85 → 65; còn lại 40; +10 nếu giảm liên tục ≥ 3 lần), Compression (tỉ lệ dao động cuối/đầu: < 0,35 → 100; < 0,5 → 90; < 0,6 → 80; < 0,75 → 60; còn lại 35; +15 nếu giảm đều), VolumeDry (KL MA5/MA20: < 0,55 → 100; < 0,7 → 85; < 0,85 → 70; còn lại 45; +10 nếu nửa sau < 0,75 nửa đầu), Distribution (0 phiên xấu 100; 1 phiên 50; ≥ 2 phiên 0), Duration (15-40 phiên → 100; dưới 15 → 55 + 5 × (phiên − 10); trên 40 → max(50, 90 − (phiên − 40))). Số dòng: BaseQualityEvaluator.cs:583-753.

---

## 7. Bẫy khi dựng lại

| # | Bẫy | Chỗ |
|---|---|---|
| 1 | VNINDEX phải có nến thật (O, H, L, C, Volume). Dữ liệu hiện chỉ là O=H=L=C, Volume=0, từ 25/06/2026, nên không có FTD, không bao giờ Favorable (mục 4.3). Dựng lại phải đổi nguồn ghi `HistoryJson` của VNINDEX | EfMarketDataWriter.cs:270 |
| 2 | Hồ sơ thích nghi và hiệu chỉnh P(hit) không có nơi ghi trong code hiện tại; chạy sẽ ra hồ sơ mặc định (mọi độ tin cậy 50; PredictedHit nằm khoảng 41-55 nếu chưa hiệu chỉnh). Đừng giả định mô hình tự học | mục 2.3 |
| 3 | Cấu hình chạy thật khác mặc định trong lớp options: `MinSessionVolume` 300.000 (lớp: 800.000), `MinSessionChangePercent` 2 (lớp: 3), `RequireBaseBreakout` false (lớp: true, và khoá này không còn tác dụng). Chép giá trị từ appsettings | SmartMoneyOptions.cs:13-26 |
| 4 | `Breakdown` cho ra điểm thô theo thành phần, `BuyScore` đã chuẩn hoá ÷ tổng tối đa hiệu lực; hai số không cùng thang | BuyDecisionEngine.cs:223-234, 329-340 |
| 5 | `Math.Round` kiểu C# làm tròn về chẵn (4,5 → 4). Chép logic sang ngôn ngữ khác thì kiểm lại chỗ này khi hồ sơ không mặc định | AdaptiveScoringProfile.cs:12-15 |
| 6 | Shakeout và phân kỳ chỉ ra Ready khi hộp đã phá vỡ xác nhận; hộp chưa phá luôn Watch dù có kiểu điểm vào | BuyDecisionEngine.cs:404-422 |
| 7 | Nến phá vỡ Darvas có thể cách vài phiên (mọi phiên sau hộp đều được xét), nên `hasBreakoutEntry` có thể true khi phiên cuối chỉ cần thoả phiên kích hoạt | DarvasBreakoutAnalyzer.cs:54-79 |
| 8 | Hai kiểu trung bình KL khác nhau: `volRatio` và kiểm thanh khoản dùng 20 phiên **có phiên cuối**; Donchian `Breakout` dùng 20 phiên **trước** phiên cuối; hộp dùng KL trung bình bên trong hộp | SignalAnalyzer.cs:65-72, 309-322; Darvas 45 |
| 9 | Mọi chỉ số phải tính trên dãy giá đã điều chỉnh quyền; giá thô sẽ hiểu nhầm gap chia cổ tức là rớt giá. Giá OHLCV lưu theo nghìn đồng | SignalAnalyzer.cs:25-26 |
| 10 | RS 5 phiên phải trừ `IndexChange5d`, không phải `ChangePercent` 1 phiên (tín hiệu `RelativeStrength` ở dòng 520 trừ 1 phiên, khác `rs5`) | SignalAnalyzer.cs:74-78, 520 |
| 11 | `IndexChange5d` rơi về `ChangePercent` 1 phiên khi dưới 6 phiên dữ liệu hoặc khi 5 phiên đúng bằng 0 | EntityMapper.cs:97-108; MarketEntities.cs:38 |
| 12 | Mã dưới 250 phiên: không có RS percentile (đọc ra 0), không vào snapshot ngành, entry `Invalid` | RsPercentileCalculator.cs:23; BuyDecisionEngine.cs:97, 366 |
| 13 | Không có hộp → entry `Invalid` → trạng thái `Avoid` → hygiene loại, kể cả RS Leader. Muốn bán lại mã không hộp phải đổi chỗ này, `RequireBaseBreakout` không còn công dụng | TradeStateResolver.cs:22-23; DailyAnalysisRunner.cs:548-552 |
| 14 | Trạng thái `Watchlist` vs `AwaitingTrigger` chỉ khác nhau do cờ `OnOpportunityList`: `Evaluate` truyền false, runner tính lại với true | BuyDecisionEngine.cs:173; DailyAnalysisRunner.cs:184-188 |
| 15 | `BuyRecommendation` của engine chỉ có hai nhánh hữu ích (Avoid, StrongBuy); ngưỡng 70 và 60 là mã chết. API suy nhãn từ trạng thái giao dịch, có thể khác nhãn engine | BuyDecisionEngine.cs:616-637; DtoMapper.cs:175 |
| 16 | Chú thích dòng 326-328 trong `BuildScore` ("MA stack chỉ còn là gate cứng") đã cũ: `ResolveTopGateFailure` không kiểm MA; MA chỉ còn ở checklist và cờ | BuyDecisionEngine.cs:326-328, 543-590 |
| 17 | `FindFollowThroughDay` thiếu luật "không thủng đáy ngày thứ nhất"; `HasHigherLow` chỉ cần một cặp đáy liên tiếp tăng trong 60 phiên. Pha dễ vào Favorable hơn định nghĩa kinh điển nếu có dữ liệu thật | MarketPhaseClassifier.cs:71-153 |
| 18 | Regime sóng ngành cần lưu trạng thái giữa các phiên (bảng riêng) và phiên đầu tiên chưa có trạng thái trước. Dựng lại phải chạy backfill để có lịch sử | SectorWaveRegimeEngine.cs:44-52; SectorWaveRegimeBackfillService.cs |
| 19 | `HitCalibrationProfile.ResolveBucket` rơi về nhóm đầu tiên của từ điển khi giá trị không nằm trong nhóm nào (không phải nhóm gần nhất); nhóm dưới 50 không tồn tại | HitCalibrationProfile.cs:55-64 |
| 20 | Giá trị `MaxGainInBasePercent` (5) chỉ dùng ở `HasValidBaseSetup`, không nằm trong chuỗi chấm điểm V1 | SignalAnalyzer.cs:139-152 |

---

## 8. Chỗ tài liệu cũ lệch code

| Tài liệu | Nói | Code thực tế |
|---|---|---|
| `docs/domain/ma-stack-and-market-phase.md` (đoạn "Cập nhật 2026-09") | `ResolveTopGateFailure` chỉ còn 3 cổng | 4 cổng: chia chác, FOMO, thị trường khó, ngành chưa có sóng (BuyDecisionEngine.cs:564-587). `buy-decision.md` ghi đúng 4 |
| `docs/domain/buy-decision.md` (mục "Công thức chỉ số") | rổ RS percentile lọc cả thanh khoản | không còn lọc thanh khoản, chỉ lọc số phiên (RsPercentileCalculator.cs:23-27, chú thích dòng 6-12) |
| `docs/domain/buy-decision.md` (mục "Chỉ báo kỹ thuật") | màn hình có "9 tiêu chí SmartMoney" | `BuildScore` chỉ có 8 thành phần (không có `trend`) |
| `docs/domain/base-price-flatbox.md` | "Setup zone: Top có thể chấp nhận re-test cạnh hộp" | Cổng Top không còn xét; `IsSetupZone` chỉ cho 12 điểm thành phần `base`; entry mã chưa phá nền vẫn là Watch (BuyDecisionEngine.cs:404-422) |
| `docs/domain/base-price-flatbox.md` | "Pass bất kỳ phiên sau hộp → IsBreakoutConfirmed" | Đúng, nhưng cần nhớ hệ quả: nến phá có thể cách vài phiên (mục 6.3) |
| `docs/features/sector-wave-entry-patterns/spec.md` §3 | các kiểu điểm vào "thay thế nhau, đạt 1 trong 3 là đủ" | Trong `BuildEntry`, Shakeout/Divergence chỉ ra Ready khi hộp đã phá xác nhận (mục 1.9). Về điểm, breakout và shakeout cộng dồn |
| `docs/domain/buy-decision.md` (mục "VIP") | `VnIndexPriorPeakAnalyzer` còn phục vụ gate bull-trap trên Home Top | Code: `MarketService` gọi cho thẻ VNINDEX và dòng trạng thái bull-trap (tham số từ `MasterAlertOptions`); không dính chuỗi cổng trong `BuyDecisionEngine` |
| `docs/domain/ma-stack-and-market-phase.md` | Favorable = đủ MA20 + slope + FTD + HL, trình bày như hoạt động | Đúng về luật, nhưng trên production dữ liệu VNINDEX không có KL nên Favorable không thể xảy ra (mục 4.3). Docs không nói chuyện này |
| `docs/domain/buy-decision.md` | `MinScore` còn dùng cho backtest + shadow | Không kiểm lại trong file này (ngoài phạm vi) |

---

## 9. Điểm chưa kiểm được

- Nội dung thật của bảng trọng số tiêu chí và bảng hiệu chỉnh P(hit) trên production (không có nơi ghi trong code; có thể còn dữ liệu cũ).
- Nơi ghi cột `OutcomeBucket` cho setup thật (hàm cập nhật không có nơi gọi) và nơi gọi `HitCalibrationService.RebuildAsync`.
- Ai gọi `EfMarketDataWriter.UpsertIndexAsync` hằng ngày trên production (chỉ thấy `MarketSyncService` và nhánh lần đầu từ KBS).
- Việc cấu hình trên server giống repo: chỉ 4 mục đã so khi làm PH-31; mục khác chưa so.
- Test đơn vị liên quan (SectorWave, RsiDivergence) chưa chạy; file này đọc code, không chạy build hay test.
