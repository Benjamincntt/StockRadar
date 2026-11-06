# V1 — Chuỗi cổng lọc (từ universe tới danh sách ứng viên)

Phạm vi: các cổng V1 áp lên từng mã, theo đúng thứ tự chạy, cho tới khi có danh sách ứng viên đưa vào xếp hạng ML. Xếp hạng ML, bonus ngành, sector cap, Top N nằm ở file khác trong thư mục này.

Đọc ngày 05/11/2026 từ code tại commit da75683 (commit ngày 03/11/2026). Giá trị cấu hình là của `backend/StockRadar.Api/appsettings.json` trong repo tại commit này (AI đã đọc file trên server khi làm PH-31 ngày 05/11/2026: 4 mục `MarketJobs`, `SmartMoney`, `PriceRunupFilter`, `OpportunityRanker` giống repo; các mục khác chưa so). Số dòng là dòng tại commit da75683.

Viết tắt đường dẫn: `Runner` = `backend/StockRadar.Infrastructure/MarketData/DailyAnalysisRunner.cs`; `Engine` = `backend/StockRadar.Domain/Services/BuyDecisionEngine.cs`.

## 1. Universe: ai được vào

Universe là cột `IsActive` trong bảng `Stocks`. Daily analysis chỉ đọc mã `IsActive = true` và `TradingRestricted = false` (`backend/StockRadar.Infrastructure/Persistence/Repositories/EfRepositories.cs:11-18`, qua `IJobStockRepository.GetAllAsync`, Runner:57).

| Việc | Luật | Nguồn |
|---|---|---|
| Danh sách mã gốc | Union các nhóm `MarketJobs:History:Groups` = HOSE, HNX, UPCOM (`Universe = "Groups"`). Chế độ khác: `AllListed`. | `HistoryBackfillRunner.cs:217-233`, appsettings |
| Hạn chế giao dịch | Mã `TradingRestricted` bị loại ("Hạn chế giao dịch…") | `StockUniverseFilter.cs:26-27` |
| Không có nến | Loại ("Không có dữ liệu giá") | `StockUniverseFilter.cs:29-30` |
| Mã mới niêm yết | Loại nếu nến đầu tiên `>= hôm nay − 365 ngày`. Cấu hình `ExcludeIpoWithinDays = 365`. Nến đầu lấy từ mẫu `ScreeningLookbackDays = 450` ngày. | `StockUniverseFilter.cs:32-36`, appsettings |
| Giá | Loại nếu giá đóng cửa mới nhất `<= MinClosePriceVnd / 1000`. `MinClosePriceVnd = 8000`. Nghĩa là phải **giá > 8.000đ** (lớn hơn hẳn, bằng 8.000đ vẫn bị loại). Giá lưu theo nghìn đồng nên chia 1000. | `StockUniverseFilter.cs:90-96` |
| Thanh khoản | KHÔNG còn sàn thanh khoản ở universe. `VolumeLookbackSessions = 20` chỉ dùng để tính TB KL lưu lại. | `StockUniverseFilter.cs:98-103` |

Hai nơi gọi bộ lọc universe:
- Job 1 backfill gọi `Screen` (có kiểm IPO + giá), mã rớt bị `MarkUniverseInactiveAsync` (`HistoryBackfillRunner.cs:104-117`).
- Rescreen gọi `ScreenQuality` (chỉ giá, không kiểm IPO) để tắt/bật lại mã. Mã đang inactive chỉ được bật lại nếu lịch sử không bị "đóng băng": nến cuối không quá 15 ngày lịch và không có khoảng trống quá 15 ngày trong 20 phiên cuối (`UniverseRescreenRunner.cs:36-60`, `StockUniverseFilter.cs:60-84`).
- Job 1 chạy lại hàng tuần: `WeeklyRefreshEnabled = true`, Chủ nhật 02:00.

Có cấu hình `IntradayScanner:MinClosePriceVnd = 8000` trong appsettings. Đó là máy quét khác (không thuộc chuỗi cổng này).

## 2. Chuỗi cổng theo thứ tự chạy

Trước vòng lặp, `RunAsync` dựng sẵn một "bối cảnh thị trường" (`Runner:68`, `SmartMoneyOpportunitySelector.BuildContext` tại `SmartMoneyOpportunitySelector.cs:120-145`): pha VNINDEX, ảnh chụp sóng ngành, RS percentile 5 phiên của cả universe, danh sách chốt quyền (mục 3). Phần này không loại mã nào.

Vòng lặp chạy từng mã ở `Runner:111-178`. Mã bị loại thì cộng 1 vào `gateStats[nhãn]` và `continue`. `gateStats` được lưu vào bảng chạy phân tích qua `GateStatsJsonMapper` (`Runner:305-311`). Các cổng 9-10 không đi qua `gateStats` (xem ghi chú ở từng cổng).

Số trong ngoặc `[...]` là ngưỡng/hằng số; chuỗi trong dấu nháy kép là chuỗi đúng như code.

1. **Sắp chốt quyền** (cổng đầu tiên, loại ngay)
   - Điều kiện: `context.CoChiaQuyenSapDen(symbol)` trả về có sự kiện. Chi tiết cửa sổ ở mục 3.
   - Nhãn `gateStats`: `"sap-chot-quyen"`. Log debug: `Bỏ {Symbol} — chốt quyền {ThongTin}` với `ThongTin.Nhan()`.
   - Nguồn ngưỡng: `FireAnt:Enabled = true`, `FireAnt:LookaheadDays = 7`.
   - Dòng: `Runner:113-120`; map nạp ở `Runner:101-104` và `Runner:630-647`; kiểm trong context `SmartMoneyOpportunitySelector.cs:67-98`.

2. **Thiếu ngành**
   - Điều kiện: `string.IsNullOrWhiteSpace(stock.Sector)`.
   - Nhãn: `"thieu-nganh"`.
   - Dòng: `Runner:122-127`.

3. **Không có khối lượng**
   - Điều kiện: không có nến nào, hoặc `latestBar.Volume <= 0` (nến cuối của `stock.History` thô, chưa điều chỉnh quyền).
   - Nhãn: `"khong-khoi-luong"`.
   - Dòng: `Runner:128-133`.

4. **Chấm điểm một lần** (chưa loại, nhưng kéo theo cổng 8)
   - `buyDecision.Evaluate(stock, context)` tại `Runner:137`. Hàm này tính lại các tín hiệu trên chuỗi giá đã điều chỉnh quyền (`LayLichSuChamDiem`, `Engine:68`), dựng điểm, điểm vào (`BuildEntry`), rồi gọi `ResolveTopGateFailure` (`Engine:145`). Kết quả `GateFailure` quyết định cổng 8.

5. **Tỉ lệ khối lượng thấp**
   - Điều kiện: `decision.VolumeRatio < cfg.MinVolumeRatioForTop`. `VolumeRatio` = `Round(KL nến cuối / TB KL 20 phiên, 2)`; nếu TB <= 0 thì trả `1` (`SignalAnalyzer.cs:65-72`, `Lookback = 20` ở dòng 9). Chuỗi dùng là chuỗi đã điều chỉnh quyền.
   - Ngưỡng: `MarketJobs:DailyAnalysis:MinVolumeRatioForTop = 0.3` (cùng giá trị mặc định trong code).
   - Nhãn: `"volume-ratio-thap"`.
   - Dòng: `Runner:139-144`.

6. **Giá trị giao dịch thấp**
   - Điều kiện: `IndicatorMath.AverageTurnoverValue(stock.History, 20) < MinGiaTriGiaoDichTB`. Công thức: trung bình 20 nến cuối của `Close × 1000 × Volume` (VND; `Close` lưu nghìn đồng). Dùng `stock.History` thô.
   - Ngưỡng: `MarketJobs:DailyAnalysis:MinGiaTriGiaoDichTB = 10000000000` (10 tỷ VND).
   - Nhãn: `"gia-tri-gd-thap"`.
   - Dòng: `Runner:146-153`; công thức `backend/StockRadar.Domain/Services/IndicatorMath.cs:121-131`.

7. **Dữ liệu lệch giá** (phòng chia tách/phát hành chưa điều chỉnh)
   - Điều kiện: nếu `Entry.BaseLow > 0` và `Entry.BaseHigh > 0` thì loại khi `latestBar.Close < BaseLow × 0.5` hoặc `latestBar.Close > BaseHigh × 2.0`. Nếu mã không có nền giá hợp lệ (BaseLow/High = 0) thì cổng này bỏ qua.
   - Ngưỡng: hằng số `0.5` và `2.0` ghi cứng trong code.
   - Nhãn: `"du-lieu-lech-gia"`.
   - Dòng: `Runner:155-166`.
   - Lưu ý: `latestBar.Close` là giá thô, còn `BaseLow/BaseHigh` tính trên chuỗi đã điều chỉnh. Xem mục 6.

8. **Cổng "không qua Top"** (kết quả của `ResolveTopGateFailure`)
   - Điều kiện: `smartMoney.PassesFilter(eval)` trả `false`. `PassesFilter` chỉ là `eval.Passes` (`SmartMoneyOpportunitySelector.cs:189-190`), và `eval.Passes = decision.PassesTopFilter = (GateFailure is null)` (`SmartMoneyOpportunitySelector.cs:167-171`, `Engine:158`).
   - Nhãn `gateStats`: `GateFailureClassifier.Classify(eval.Reasons.FirstOrDefault())` (`Runner:171`). Chỉ 3 nhãn ổn định, cộng `"Khác"` (`GateFailureClassifier.cs:11-35`). Chi tiết 3 cổng con ở mục 2.1.
   - Nếu lý do chứa chữ "FOMO" thì cộng thêm bộ đếm `runupExcluded` (`Runner:172-173`), chỉ để ghi log.
   - Dòng: `Runner:168-175`.

9. **Vào danh sách ứng viên**
   - Mã qua hết thì `candidates.Add` (`Runner:177`). Cổng điểm tối thiểu `MinScore` đã bỏ ("ĐÃ BỎ Gate 9", `Runner:176`). `MarketJobs:DailyAnalysis:MinScore = 55` còn trong appsettings nhưng Daily analysis không đọc; chỉ `ShadowAnalysisService.cs:55` và `SmartMoneyBacktestRunner.cs:44` dùng.

10. **Top hygiene** (chạy sau khi đã xếp hạng ML, nhưng là cổng loại thêm trước sector cap)
    - Gọi ở `Runner:214` → `ApplyTopHygiene` (`Runner:510-536`) → `PassesTopHygiene` (`Runner:538-566`). Cổng này KHÔNG ghi vào `gateStats`; chỉ có log `Top hygiene (...)` khi loại > 0 (`Runner:217-225`).
    - 10a. Loại mã trạng thái `Avoid` (`Runner:548-552`, lý do nội bộ `"avoid"`). Với ứng viên đã qua cổng 8, trạng thái được tính bằng `TradeStateResolver.Resolve(entry, GateFailure, BuyScore, onList = true)` (`Runner:184-188`; `TradeStateResolver.cs:16-52`). Vì `GateFailure` đã null nên chỉ còn các đường dẫn tới `Avoid`:
      - `entry.Status` là `Late` hoặc `Invalid` (`TradeStateResolver.cs:20-21`).
      - Trạng thái còn lại không phải `Ready`/`Watch` (`TradeStateResolver.cs:48`, lý do "Không đạt tiêu chí tối thiểu").
      - `Invalid` xảy ra khi: `history.Count < MinHistoryDays` (= 250; `Engine:366-373`) hoặc không có nền giá hợp lệ `!flatBox.HasValidBox` (`Engine:381-387`). `Late` xảy ra khi đã chạy xa nền: `gainFromBase > PriceRunupFilter:MaxGainFromBasePercent` (= 10%; `Engine:426-436`).
      - Hệ quả: dù các cổng "thiếu lịch sử" và "chưa có nền" đã bỏ khỏi `ResolveTopGateFailure`, cổng 10a vẫn loại mã không đủ 250 nến, không có nền giá, hoặc đã vượt 10% so đỉnh nền.
    - 10b. Mã `IsRsLeader` được miễn mọi chặn còn lại (`Runner:554-557`). Định nghĩa: `(hasBreakoutEntry || hasFlatBoxBreakout) && rsPercentile >= RsLeaderMinRsPercentile && rs5 >= 0` (`Engine:110-112`). `RsLeaderMinRsPercentile = 85`.
    - 10c. Cổng pha cho mã breakout không phải leader (`Runner:559-563`, `Runner:578-593`; lý do nội bộ `"regime"`). `IsBreakoutSetup` = `Entry.Type == Breakout` hoặc `SetupDna` bắt đầu bằng "Breakout" hoặc chứa "Phá vỡ" (`Runner:568-576`). Rồi: pha `Favorable` → cho qua; pha `Neutral` hoặc `Unfavorable` → chỉ giữ nếu `TradeState == Actionable`. Mã không phải breakout thì không bị cổng này.
    - Cờ `ExcludeAwaitingTriggerFromTop = true` còn trong cấu hình nhưng code không đọc (`Runner:532`, "ĐÃ BỎ Gate 10").

Sau cổng 10 là `ApplySectorCap` (`MaxPerSector = 2`, `Runner:216`) rồi `Take(MaxResults)` (`MaxResults = 5`, `Runner:227-228`). Hai bước này thuộc phần xếp hạng/Top, ghi ở đây chỉ để biết vị trí.

### 2.1. Ba cổng con trong `ResolveTopGateFailure` (cổng 8)

`ResolveTopGateFailure` ở `Engine:543-590`. Kiểm theo thứ tự, trả lý do đầu tiên gặp. Bốn cổng cũ đã bỏ: thiếu lịch sử, thanh khoản thấp, chưa phá nền, chưa có phân kỳ dương (`Engine:554-557`).

| # | Điều kiện loại | Lý do trả về (chuỗi đúng như code) | Nhãn đếm (`GateFailureClassifier`) | Ngưỡng và nguồn | Dòng |
|---|---|---|---|---|---|
| a | Sắp chốt quyền (cổng này lặp lại cổng 1; trong luồng Daily analysis mã đã bị loại ở cổng 1 nên không tới đây, chỉ có tác dụng cho nơi khác gọi `Evaluate`) | `ThongTinChotQuyen.Nhan()` | `"Khác"` (không có nhánh riêng trong classifier) | như cổng 1 | `Engine:564-565` |
| b | FOMO: `gainFromLow5 = (latestClose − minLow5) / minLow5 × 100 > MaxGainFromLow5SessionsPercent`, với `minLow5` = `Low` thấp nhất trong 5 nến cuối của chuỗi đã điều chỉnh; bỏ qua nếu `minLow5 <= 0` | `$"FOMO +{gainFromLow5:0.#}% so đáy 5 phiên"` | `"FOMO vượt ngưỡng so đáy 5 phiên"` | `SmartMoney:MaxGainFromLow5SessionsPercent = 7` | `Engine:567-577` |
| c | Thị trường khó: pha `Unfavorable` và không phải RS leader và (`rsPercentile < MinRsPercentileForUnfavorable` hoặc `rs5 <= 0`) | `"Thị trường khó — chỉ mua mã dẫn dắt (RS top + khỏe hơn VNINDEX)"` | `"Thị trường khó — chỉ mua mã dẫn dắt"` | `SmartMoney:MinRsPercentileForUnfavorable = 80` | `Engine:579-583` |
| d | Sóng ngành: không phải RS leader và ngành không có sóng phiên này (`!sectorWave.HasWave`) và ngành không đang trong chu kỳ sóng kế thừa (`!IsSectorRegimeActive`) và `rs5 < 0` | `"Ngành chưa có sóng + RS âm"` | `"Ngành chưa có sóng + RS âm"` | `rs5 < 0` ghi cứng; ngưỡng sóng ngành ở mục 2.2 | `Engine:585-587` |

Cách tính các đại lượng dùng ở cổng 8:
- `rs5` = `Round(%thay đổi 5 phiên của mã − IndexChange5d của VNINDEX, 2)` (`SignalAnalyzer.cs:74-78`).
- `rsPercentile` = thứ hạng `rs5` trong các mã có ít nhất `max(MinHistoryDays, 6)` nến: `Round(vị trí / (n−1) × 100, 2)` (`RsPercentileCalculator.cs:16-56`). `MinHistoryDays = 250`.
- Pha thị trường: xem 2.3.

### 2.2. Sóng ngành (đầu vào của cổng 8d)

`BuildSectorSnapshots` (`SmartMoneyOpportunitySelector.cs:210-268`) và `ClassifyWave` (`SmartMoneyOpportunitySelector.cs:274-297`). Giá trị `SmartMoney:SectorWave` trong appsettings:

| Khoá | Giá trị | Ý nghĩa |
|---|---|---|
| `MinStocksPerSector` | 3 | Ngành có ít hơn 3 mã (đủ 250 nến, không phải "Khác"/"Other"/"N/A") thì không được chấm sóng |
| `MinAdvancerRatio` | 0.6 | Tỉ lệ mã tăng giá phiên |
| `MinMedianChangePercent` | 1.5 | Trung vị %thay đổi phiên |
| `NearCeilingChangePercent` | 4 | Mã tăng ≥ 4% coi là "gần trần" |
| `MinNearCeilingRatio` | 0.25 | Tỉ lệ mã gần trần |
| `MinVolumeRatio` | 1.3 | Tổng KL phiên / tổng TB KL 20 phiên của ngành |
| `MinSectorRs5d` | 0 | RS 5 phiên trung bình của ngành, phải `> 0` |

Luật: không đủ độ rộng (`advancerRatio >= 0.6`) → `None`. Đủ độ rộng và đủ cả ba điều kiện (lực: `median >= 1.5` hoặc `nearCeilingRatio >= 0.25`; tiền: `volumeRatio >= 1.3`; RS: `sectorRs5d > 0`) → `Strong`. Đủ độ rộng và đạt ít nhất một → `Emerging`. `HasWave` được dùng ở cổng 8d; chưa kiểm được định nghĩa chính xác của `HasWave` (Strong và/hoặc Emerging) vì nằm ở `SectorSnapshot`, ngoài các file được giao.

Sóng ngành kế thừa nhiều phiên (`IsSectorRegimeActive`): `AdvanceSectorWaveRegimesAsync` (`Runner:600-624`), ngưỡng gãy sóng: `FailureMaxVolumeRatio = 0.5`, `FailureConsecutiveSessions = 3`, `MaxActiveSessions = 20`. Logic `Advance` chưa đọc (nằm ở `sectorWaveRegimeEngine`).

### 2.3. Pha thị trường (đầu vào của cổng 8c và 10c)

`MarketPhaseClassifier.Classify(index.Bars, settings.PhaseThresholds)` (`SmartMoneyOpportunitySelector.cs:129`, `backend/StockRadar.Domain/Services/MarketPhaseClassifier.cs`):
- Ít hơn 20 nến VNINDEX → `Unfavorable` (dòng 18).
- Đóng cửa không trên MA20 → `Unfavorable` (dòng 37).
- Trên MA20 và `slopeOk && hasFtd && hasHl` → `Favorable` (dòng 49).
- Còn lại → `Neutral`.
- `hasFtd` (ngày bứt phá xác nhận): trong các nhịp hồi ngày thứ 4 đến 7 (`FtdMinRallyDay = 4`, `FtdMaxRallyDay = 7`), có nến tăng `>= FtdMinGainPercent = 1.2%` so nến trước, KL **lớn hơn** KL nến trước (dòng 105) và lớn hơn TB 20 phiên trước đó (dòng 109).
- `hasHl` (đáy sau cao hơn đáy trước): đáy pivot bán kính 2, nhìn lùi 60 nến (`HigherLowPivotRadius = 2`, `HigherLowLookbackSessions = 60`).
- `slopeOk`: MA20 hiện tại `>=` MA20 cách 3 phiên (`Ma20SlopeLookbackSessions = 3`).
- Ý nghĩa thực tế: xem mục 6, bẫy 2.

## 3. Cổng sắp chốt quyền

Một dòng chuyển giao: cổng này sẽ chuyển sang V3, xem `specs/015-v3-rights-adjusted-prices/spec.md` User Story 6.

| Hạng mục | Luật | Dòng / nguồn |
|---|---|---|
| Bật/tắt | `FireAnt:Enabled = true`. Tắt hoặc `LookaheadDays <= 0` → bản đồ rỗng, cổng mở hoàn toàn, không gọi FireAnt | `Runner:632-634`, `FireAntLichChotQuyenClient.cs:39-41` |
| Nguồn | FireAnt `GET {ApiBaseUrl}/events/search?symbol=&orderBy=1&type=0&startDate=…&endDate=…&offset=0&limit=1000`. `ApiBaseUrl = https://restv2.fireant.vn` (mặc định trong `FireAntOptions`, appsettings chỉ đặt `Enabled` và `LookaheadDays`) | `FireAntLichChotQuyenClient.cs:64-66`, `FireAntOptions.cs` |
| Ngày "hôm nay" | Ngày hiện tại theo múi giờ "SE Asia Standard Time" | `Runner:638-639` |
| Số ngày nhìn tới | `LookaheadDays = 7` ngày lịch, tối đa 120 (`Math.Min(soNgay, 120)`). `den = hôm nay + 7` | `FireAntOptions.cs:18`, `FireAntLichChotQuyenClient.cs:43,57` |
| Số ngày nhìn lùi | Yêu cầu FireAnt từ `hôm nay − 45` ngày tới `den`. Mục đích: bắt sự kiện đã chốt nhưng chưa thực hiện | `FireAntLichChotQuyenClient.cs:58-63` |
| Loại sự kiện | Chỉ `type` 1 (tiền mặt), 2 (cổ phiếu), 3 (phát hành cho cổ đông hiện hữu, tức quyền mua). Loại khác bỏ qua | `FireAntLichChotQuyenClient.cs:31-32,90-91` |
| Ngày dùng làm mốc | Trường `recordDate` (ngày chốt danh sách). Thiếu `recordDate` → bỏ sự kiện. Trong `ThongTinChotQuyen` nó được đặt tên `ExDate`, và comment ghi "ngày không hưởng quyền"; nhưng giá trị gán vào là `recordDate`, không phải ngày GDKHQ riêng của nguồn | `FireAntLichChotQuyenClient.cs:92-93,107`, `ThongTinChotQuyen.cs:9-14` |
| Điều kiện giữ sự kiện | `recordDate <= hôm nay + 7` VÀ `(executionDate ?? recordDate) >= hôm nay`. Sự kiện nào đã chốt lẫn thực hiện xong (hạn `< hôm nay`) thì bỏ | `FireAntLichChotQuyenClient.cs:94-101` |
| Cửa sổ chặn một mã | Từ khi `recordDate` rơi vào trong 7 ngày lịch tới (tức từ 7 ngày lịch trước `recordDate`) cho đến hết `executionDate` (nếu có, tính cả ngày đó) hoặc hết `recordDate` (nếu nguồn không có `executionDate`). Nếu mã có nhiều sự kiện, giữ sự kiện có hạn chặn xa nhất; hai sự kiện cùng hạn thì gộp mô tả bằng " · " | `FireAntLichChotQuyenClient.cs:108-129` |
| Cờ `DaChot` | `recordDate < hôm nay` (đã chốt, đang chờ thực hiện). Chỉ ảnh hưởng nhãn | `FireAntLichChotQuyenClient.cs:107` |
| Kiểm theo mã | `CoChiaQuyenSapDen(symbol)` = tra khoá `symbol.Trim().ToUpperInvariant()` trong bản đồ. Có mặt trong bản đồ là bị chặn (tham số `soNgay = 0`, không lọc thêm) | `SmartMoneyOpportunitySelector.cs:67-98` |
| Nhãn hiển thị | `"Chờ chốt quyền dd/MM"` hoặc `"Đã chốt quyền dd/MM"`, thêm `", chờ thực hiện dd/MM"` nếu `executionDate > recordDate`, thêm `" — {mô tả}"` nếu có. Mô tả lấy từ `title` của FireAnt, cắt còn 77 ký tự + "…" nếu dài hơn 80 | `ThongTinChotQuyen.cs:19-31` |
| Fail-open khi lỗi | Có. Mọi lỗi (không lấy được token, HTTP không thành công, JSON hỏng, ngoại lệ bất kỳ trừ `OperationCanceledException`) → trả bản đồ rỗng → cổng mở. Log cảnh báo | `FireAntLichChotQuyenClient.cs:51-56,73-77,137-141`, `Runner:642-646` |
| Lỗi có được cache không | Không. Chỉ kết quả thành công mới `cache.Set`; lần chạy sau (kể cả refresh 15 phút) thử gọi lại | `FireAntLichChotQuyenClient.cs:133` |
| Cache kết quả | `IMemoryCache`, khoá `fireant:exdates:{yyyyMMdd}:{ngay}`, 6 giờ | `FireAntLichChotQuyenClient.cs:44-46,133` |
| Token | Nếu `FireAnt:AccessToken` có giá trị thì dùng. Không thì cào `SiteUrl` (`https://fireant.vn`), tìm file `/_next/static/chunks/pages/_app-….js`, rút `ANONYMOUS_ACCESS_TOKEN` bằng regex, cache 12 giờ (`fireant:guest-token`). Cào hỏng → fail-open | `FireAntLichChotQuyenClient.cs:144-179` |
| Không đụng universe | Chỉ bỏ qua trong lần chạy đó; qua ngày thực hiện, mã vào lại | `Runner:113-114` |

Lưu ý: giá trị appsettings `FireAnt` chỉ có `Enabled = true` và `LookaheadDays = 7`. `FireAnt:AccessToken` không có trong repo (đã chú thích là secret). Việc server có đặt token cứng hay không chưa kiểm được.

## 4. Cổng chạy trong phiên (intraday refresh) khác gì cuối ngày

Cùng một hàm `DailyAnalysisRunner.RunAsync`, cùng toàn bộ cổng ở mục 2. Khác nhau chỉ ở lịch chạy và hai tham số:

| | Cuối ngày / hết phiên sáng | Refresh trong phiên |
|---|---|---|
| Trigger Quartz | `…-close-trigger`: 15:00 + `DelayAfterSessionMinutes` (5) = 15:05, thứ 2–6. `…-morning-trigger` 11:30 vì `MorningRunEnabled = true` | `…-intraday-trigger`: cron `0 0/{interval} 9-11,13-14 ? * MON-FRI`, `interval = Clamp(IntradayRefreshIntervalMinutes, 5, 60)` = 15 |
| Trước khi phân tích | Gọi `session.RunAsync` (Job 2) để đảm bảo nến đã append | Không gọi Job 2 |
| Khung giờ cho phép | — | 09:00–11:30 và 13:00–14:45 (đã gồm hai đầu); ngoài khung thì bỏ qua |
| Tham số | `runPostProcessing = true`, `includeStructureAndTracking = true` | `runPostProcessing = false`, `includeStructureAndTracking = false` |
| Hệ quả | Ghi `SetupTracks` (`Runner:297-303`) | Chỉ bỏ bước ghi `SetupTracks`. Tham số `runPostProcessing` không được dùng ở đâu trong thân hàm (chỉ có trong chữ ký, `Runner:47`) |

Dòng: `backend/StockRadar.Infrastructure/Scheduling/QuartzSchedulingExtensions.cs:124-163`, `backend/StockRadar.Infrastructure/Scheduling/Jobs/DailyAnalysisJob.cs:21-58, 61-82`.

Cổng lọc giống hệt nhau. Khác biệt thực tế nằm ở dữ liệu: refresh đọc `Stocks` trong DB tại lúc đó, nên nến cuối có thể là nến phiên chưa chốt (`DailySession:IntervalMinutes = 5` trong appsettings, tức Job 2 lặp 5 phút trong giờ giao dịch theo `QuartzSchedulingExtensions.cs:103-105`). Các cổng 3, 5, 6, 7 và cổng FOMO (8b) khi đó chạy trên nến dở dang. Chưa kiểm được cách Job 2 ghi nến dở dang (nằm ở `DailySessionSyncRunner`, ngoài phạm vi). Chưa kiểm được cổng 5 (`volume-ratio-thap`) hoạt động ra sao khi KL phiên còn nhỏ giữa giờ.

Cả hai loại chạy đều thay toàn bộ danh sách ngày đó: `opportunities.ReplaceForDateAsync` (`Runner:283`).

Ngày gán cho danh sách: `TradingCalendar.GetPostSessionAnalysisDate()` = nếu trước 15:00 giờ VN thì hôm nay, từ 15:00 trở đi thì ngày giao dịch kế tiếp (`backend/StockRadar.Application/Common/TradingCalendar.cs:60-73`). Ngày giao dịch chỉ loại thứ 7 và Chủ nhật, KHÔNG có lịch nghỉ lễ (`TradingCalendar.cs:43-44`).

## 5. Nhánh "tắt" qua cấu hình

| Khoá | Giá trị trong appsettings.json | Tác dụng nếu đổi | Có tác dụng trong chuỗi cổng V1 không |
|---|---|---|---|
| `FireAnt:Enabled` | `true` | `false` → cổng chốt quyền mở hoàn toàn | Có |
| `FireAnt:LookaheadDays` | `7` | `<= 0` → cổng mở hoàn toàn | Có |
| `MarketJobs:DailyAnalysis:Enabled` | `true` | `false` → không đăng ký job phân tích | Có (toàn bộ) |
| `MarketJobs:DailyAnalysis:IntradayRefreshEnabled` | `true` | `false` → không có trigger refresh trong phiên | Có |
| `MarketJobs:DailyAnalysis:MorningRunEnabled` | `true` (mặc định code `false`) | Có thêm lần chạy đầy đủ lúc 11:30 | Có |
| `SmartMoney:MaStack:Enabled` | `true` | `false` → `MaStackStrictness.Off` | Chỉ ảnh hưởng dòng checklist "Xếp lớp MA" và cờ `HasMaStack` (`Engine:596-597`), không phải cổng |
| `SmartMoney:RequireBaseBreakout` | `false` (mặc định code `true`) | Không có tác dụng: cờ được đưa vào `SmartMoneySettings` nhưng `BuyDecisionEngine` không đọc | Không |
| `MarketJobs:DailyAnalysis:ExcludeAwaitingTriggerFromTop` | `true` | Không có tác dụng: comment `Runner:532` ghi cổng này đã bỏ | Không |
| `MarketJobs:DailyAnalysis:MinScore` | `55` (mặc định code `60`) | Không có tác dụng trong `RunAsync` | Không (chỉ shadow + backtest) |
| `MarketJobs:DailyAnalysis:FallbackMinScore` / `FallbackMaxResults` | `45` / `5` | Chỉ cho backtest | Không |

Không có công tắc riêng cho các cổng 2, 3, 5, 6, 7 ngoài hằng số/giá trị ngưỡng ở mục 2. `MaxResults = 0` thì bỏ giới hạn Top (`Runner:227`); `MaxPerSector <= 0` thì bỏ sector cap (`Runner:483`).

### Giá trị cấu hình V1 khác mặc định trong code (dễ nhầm khi dựng lại)

Giá trị thực tế dùng là cột appsettings.json.

| Khoá | Mặc định trong class | appsettings.json |
|---|---|---|
| `MarketJobs:DailyAnalysis:MinScore` | 60 | 55 (không dùng) |
| `MarketJobs:DailyAnalysis:MaxResults` | 30 | 5 |
| `MarketJobs:DailyAnalysis:DelayAfterSessionMinutes` | 2 | 5 |
| `MarketJobs:DailyAnalysis:MorningRunEnabled` | false | true |
| `MarketJobs:DailyAnalysis:FallbackMaxResults` | 15 | 5 (không dùng) |
| `MarketJobs:DailySession:IntervalMinutes` | 0 | 5 |
| `SmartMoney:MinSessionVolume` | 800000 | 300000 |
| `SmartMoney:MinSessionChangePercent` | 3 | 2 |
| `SmartMoney:RequireBaseBreakout` | true | false (không dùng) |

Các khoá còn lại trong `MarketJobs:DailyAnalysis` (`MinVolumeRatioForTop = 0.3`, `MinGiaTriGiaoDichTB = 10000000000`, `MaxPerSector = 2`, `IntradayRefreshIntervalMinutes = 15`, khung giờ 9:00–11:30 / 13:00–14:45), `SmartMoney` (`MinHistoryDays = 250`, `BreakoutMinVolumeRatio = 1.5`, `MaxGainFromLow5SessionsPercent = 7`, `MinRsPercentileForUnfavorable = 80`, `RsLeaderMinRsPercentile = 85`, `MaStack`, `MarketPhase`, `SectorWave`), `PriceRunupFilter` (`MaxGainFromBasePercent = 10`, Darvas, v.v.) đều bằng mặc định trong code.

Ghi chú về các mục config được giao: appsettings không có mục `Pipeline`. File `PipelineOptions.cs` thực chất chứa `OpportunityMonitorOptions` (mục `OpportunityMonitor`, máy theo dõi lệnh khối realtime), không thuộc chuỗi cổng lọc này.

## 6. Bẫy khi dựng lại

1. **`GeneratedAt` lưu UTC.** `generatedAt = DateTime.UtcNow` (`Runner:54`), ghi thẳng vào `DailyOpportunityRecord` (`Runner:252`) và `analysisRuns.UpsertAsync` (`Runner:305-311`). Hiển thị phải đổi sang giờ VN; nếu không sẽ lệch 7 tiếng.

2. **Pha thị trường không bao giờ ra `Favorable` với dữ liệu hiện có.** Nguồn của pha là `index.Bars`, tức cột `HistoryJson` của bảng `MarketIndices` (`EntityMapper.cs:72-84`). Trong code hiện tại chỉ có MỘT nơi ghi chuỗi này: `EfMarketDataWriter.UpsertIndexAsync` (`EfMarketDataWriter.cs:253-290`), tạo mỗi ngày một nến `new OhlcvBar(sessionDate, Price, Price, Price, Price, 0)` — tức `Open = High = Low = Close` và `Volume = 0` (dòng 270). Ngày bứt phá xác nhận cần KL nến `>` KL nến trước (`MarketPhaseClassifier.cs:105`) và `>` TB 20 phiên (dòng 109); với `Volume = 0` cả hai đều sai → `hasFtd` luôn `false` → pha chỉ có thể là `Unfavorable` (dưới MA20) hoặc `Neutral` (trên MA20), không bao giờ `Favorable`. Hệ quả dây chuyền: cổng 8c ("Thị trường khó") áp mỗi khi dưới MA20; cổng 10c coi `Neutral` như `Unfavorable` (mã breakout phải `Actionable`); điểm `market` của Buy Score không bao giờ lên mức Favorable (5 điểm). Ngày bắt đầu có dữ liệu VNINDEX "từ 25/06/2026" là thông tin do owner nêu, chưa kiểm được trong code (không thấy mốc ngày nào trong code đọc). Ngoài ra `Low`/`High` giả (bằng `Close`) làm `HasHigherLow` tính trên đáy = giá đóng cửa. Khi dựng lại, cần nguồn OHLCV thật cho VNINDEX.

3. **Dưới 20 nến VNINDEX → `Unfavorable`** (`MarketPhaseClassifier.cs:18`). Nếu bảng chỉ có ít ngày thì pha luôn xấu.

4. **Giá thô và giá đã điều chỉnh trộn lẫn trong cổng 7.** `Runner:160` dùng `latestBar.Close` từ `stock.History` (thô), còn `Entry.BaseLow/BaseHigh` do `Engine` tính trên `LayLichSuChamDiem(stock)` (đã điều chỉnh theo quyền, `Engine:68`). Cổng 3 và 6 cũng dùng `stock.History` thô trong khi cổng 5 dùng `decision.VolumeRatio` từ chuỗi đã điều chỉnh. Khi dựng lại cần chọn một loại giá cho cả chuỗi.

5. **Cổng 8 không phải mọi cổng "thật" nằm trong `ResolveTopGateFailure`.** Mã không có nền giá, dưới 250 nến, hoặc chạy xa quá 10% so đỉnh nền bị loại ở cổng 10a (Top hygiene) chứ không ở cổng 8, và không có dòng nào trong `gateStats`. Nếu chỉ nhìn `gateStats` sẽ tưởng cổng lọc lỏng hơn thực tế.

6. **Thứ tự chạy ảnh hưởng chi phí, không ảnh hưởng kết quả.** `Evaluate` (nặng) chạy trước cổng 5, 6, 7 (rẻ). Khi dựng lại muốn nhanh hơn có thể đưa cổng 5, 6 lên trước, nhưng cổng 5 cần `decision.VolumeRatio` (chuỗi đã điều chỉnh) nên sẽ phải tự tính lại.

7. **Nhãn đếm cổng chỉ nhìn lý do đầu tiên.** `Classify(eval.Reasons.FirstOrDefault())` (`Runner:171`) và `GateFailureClassifier` chỉ nhận 3 tiền tố: "FOMO", "Thị trường khó", "Ngành chưa có sóng"; mọi lý do khác thành `"Khác"`. Lý do chốt quyền (cổng 8a) sẽ thành `"Khác"` nếu có đường nào đó đi qua (hiện không).

8. **Cờ `runupExcluded` chỉ đếm chữ "FOMO".** `eval.Reasons.Any(r => r.Contains("FOMO"))` (`Runner:172-173`); bản đếm này chỉ ra log, không phải số cổng.

9. **Mốc `ExDate` thật ra là `recordDate`.** Đặt tên `ExDate`, ghi chú "ngày không hưởng quyền" nhưng giá trị là ngày chốt danh sách (`FireAntLichChotQuyenClient.cs:93,107`). Spec 015 US6 ghi `recordDate` = GDKHQ "theo khảo sát R1"; chưa kiểm được khảo sát đó, chỉ dẫn lại.

10. **Cổng chốt quyền dùng ngày hôm nay, không phải ngày phiên sẽ giao dịch.** `homNay` lấy theo giờ VN lúc chạy (`Runner:639`), trong khi danh sách ghi cho `forTradingDate` (sau 15:00 là ngày giao dịch kế tiếp). Lần chạy 15:05 xét cửa sổ chặn theo hôm nay, không phải ngày mai. Cuối tuần/ngày lễ cũng vậy (không có lịch nghỉ lễ).

11. **Khoá cache FireAnt theo ngày + số ngày.** `fireant:exdates:{yyyyMMdd}:{ngay}` giữ 6 giờ: bản đồ lấy sáng nay vẫn được dùng cho cả các lần refresh 15 phút và lần chạy 15:05, nên sự kiện mới công bố giữa ngày có thể trễ tới 6 giờ. Token 12 giờ.

12. **`Pipeline` không phải mục cấu hình.** Xem cuối mục 5. Dùng nhầm sẽ đọc ra `null`.

13. **`SmartMoney` trong appsettings khác mặc định** (`MinSessionVolume`, `MinSessionChangePercent`). Hai khoá này ảnh hưởng kiểu điểm vào (`meetsSessionBar`), điểm số và trạng thái `Ready`/`Watch`, từ đó ảnh hưởng cổng 10a/10c. Dựng lại bằng giá trị mặc định trong class sẽ ra danh sách khác.

## 7. Chưa kiểm được

- Định nghĩa chính xác `SectorSnapshot.HasWave` và `SectorWaveRegimeEngine.Advance` (ngoài file được giao).
- Cách `DailySessionSyncRunner` (Job 2) ghi nến dở dang giữa phiên, nên chưa biết các cổng volume/giá hoạt động ra sao khi refresh intraday.
- `FireAnt:AccessToken` có được đặt trên server không (appsettings repo không có; PH-31 chỉ so 4 mục cấu hình V1, không so mục `FireAnt`).
- Ngày dữ liệu VNINDEX bắt đầu (25/06/2026) và việc `recordDate` của FireAnt là GDKHQ: đều là thông tin từ owner/spec, không kiểm được qua code.
- Hành vi thật của FireAnt `events/search` (định dạng trả về, `limit=1000` có đủ cho cửa sổ [−45, +7] ngày không): chưa gọi thử; không kiểm được mã nào bị cắt nếu vượt 1000 sự kiện.
- Các hàm tín hiệu `DetectSignals`, `AnalyzeFlatBox`, `IsSetupZone` (quyết định `HasValidBox`, `IsBreakoutConfirmed`, `GainFromBoxTopPercent`) chưa đọc; cổng 10a phụ thuộc chúng. Chi tiết nền giá ghi ở `docs/domain/base-price-flatbox.md`, chưa đối chiếu lại với code.
