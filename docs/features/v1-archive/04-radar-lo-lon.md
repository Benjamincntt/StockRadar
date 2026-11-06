# V1 lưu trữ 04 — Radar trong phiên, theo dõi lô lớn / dòng tiền, cảnh báo Darvas

Phạm vi: các phần V1 chạy trong phiên ngoài Top: radar "Tín hiệu mới nhất" (`IntradayScanner*`, `RadarService`, `SessionRadarHits`), radar sớm hồi phục (`EarlyRecoveryRadar`), theo dõi lô lớn / dòng tiền (`OpportunityIntradayMonitor*`, `TradeEvent*`, `SessionFlow*`, `OrderBook*`) và cảnh báo phá hộp Darvas (`DarvasBreakoutAlertPublisher`).
Đọc ngày 05/11/2026 từ code tại commit da75683 (commit ngày 03/11/2026). Số cấu hình lấy từ `backend/StockRadar.Api/appsettings.json` trong repo; production có thể khác (xem memory "server-config-diverged-from-repo") — chưa kiểm được.
Đường dẫn bên dưới tính từ `backend/` nếu không ghi khác.

## 1. Bảng tổng quan

| Thành phần | Chạy lúc nào | Đọc gì | Ghi / đẩy ra |
|---|---|---|---|
| Radar "Tín hiệu mới nhất" (`IntradayScannerJob`) | Mỗi 120 giây, chỉ trong phiên | KBS bảng giá cho mọi mã active + lịch sử nến trong DB | Bảng `SessionRadarHits`; SignalR `RadarUpdated` + `QuotesUpdated` |
| Radar sớm hồi phục (`EarlyRecoveryRadar`) | Cùng lúc `DailyAnalysisRunner` chạy (không có job riêng) | Lịch sử nến toàn universe | Bảng `EarlyRecoveryRadar`; endpoint `GET /api/v1/early-recovery` |
| Theo dõi lô lớn / dòng tiền (`OpportunityIntradayMonitorJob`) | Mỗi 60 giây, chỉ trong phiên | KBS bảng giá cho mọi mã active | Bộ nhớ RAM (sự kiện, dòng tiền); SignalR `TradeEventCreated`; ghi giá vào `Stocks` |
| Cảnh báo Darvas | Sau mỗi lần Job 2 (`DailySessionSyncRunner`) | Lịch sử nến toàn bộ mã trong DB | Bảng `Alerts`; SignalR `AlertCreated` |

Không thành phần nào ở trên gửi Telegram hay push FCM (các file trong phạm vi không dùng `IGuiThongBao`).

## 2. Lịch chạy

| Job | Cách đặt lịch | Nguồn |
|---|---|---|
| `intraday-scanner` | Quartz lặp mỗi `max(30, IntervalSeconds)` = 120 giây, bắt đầu sau 5 giây; `[DisallowConcurrentExecution]` | `Infrastructure/Scheduling/QuartzSchedulingExtensions.cs:181-197`, `IntradayScannerOptions.cs:10` |
| `opportunity-monitor` | Quartz lặp mỗi `max(30, IntervalSeconds)` = 60 giây, bắt đầu sau 8 giây | `QuartzSchedulingExtensions.cs:199-216`, `appsettings.json:233-235` |
| Cổng giờ chung của cả hai | Bỏ qua nếu `VietnamMarketCalendar.IsMarketOpen()` sai: ngày giao dịch, 09:00-11:30 và 13:00-14:45 giờ VN. Cờ bỏ cổng: `ForceScanOutsideHours` / `ForceRunOutsideHours`, mặc định `false` | `Infrastructure/MarketData/VietnamMarketCalendar.cs:66-76`, `Scheduling/Jobs/IntradayScannerJob.cs:16`, `OpportunityIntradayMonitorJob.cs:17-21` |
| Job 2 (nơi chạy Darvas) | `DailySession.IntervalMinutes = 5` nên chỉ có trigger lặp mỗi 5 phút; cổng `IsMarketOpen()` trong job. Code Quartz không có trigger 15:00 riêng khi `IntervalMinutes > 0` (mô tả `JobCatalog.cs:41` nói "+ 15:00" — lệch) | `QuartzSchedulingExtensions.cs:94-122`, `Scheduling/Jobs/DailySessionSyncJob.cs:21-28`, `appsettings.json:112-119` |
| `DailyAnalysisRunner` (nơi tính early recovery) | Trigger đóng cửa: giờ Job 2 (15:00) + `DelayAfterSessionMinutes` 5 = 15:05; trigger 11:30 (`MorningRun`); trigger làm mới mỗi 15 phút trong cron `0 0/15 9-11,13-14` | `QuartzSchedulingExtensions.cs:124-161`, `appsettings.json:120-145` |
| Chạy tay | `POST /market/jobs/intraday-scan`, `POST /market/jobs/opportunity-monitor`, cần header `X-Sync-Key` (đường dẫn chính xác theo route của `MarketJobsController`) | `Api/Controllers/MarketJobsController.cs:149-178` |

Tiền tố đường dẫn chạy tay chưa kiểm được (chưa đọc `[Route]` của controller).

## 3. Radar "Tín hiệu mới nhất" (`IntradayScannerRunner`)

### 3.1 Điều kiện vào radar

Quét mọi mã active của universe, mỗi lần gọi KBS cho một lô `max(10, BatchSize=40)` mã. Một mã vào radar khi thỏa cả ba:

| Điều kiện | Ngưỡng | Nguồn |
|---|---|---|
| Khối lượng tích lũy phiên | `SessionVolume >= 1_000_000` cổ phiếu (`MinSessionVolume`) | `IntradayScannerRunner.cs:58`, `IntradayScannerOptions.cs:16` |
| Biến động so tham chiếu | `abs(ChangePercent) >= 3` (`MinAbsChangePercent`) | `IntradayScannerRunner.cs:61`, `IntradayScannerOptions.cs:19` |
| Giá tối thiểu | `Close > MinClosePriceVnd / 1000` = 8 (đơn vị nghìn đồng, tức 8.000đ) | `IntradayScannerRunner.cs:66`, `appsettings.json:231` |

Ghi chú: tùy chọn `Exchange = "HOSE"` chỉ dùng làm nhãn khi ghi bảng. Code không lọc theo sàn; mọi mã active được quét (`IntradayScannerRunner.cs:34`, `:25`). Universe production gồm sàn nào chưa kiểm được (`appsettings.json` cho Job 1 `Groups: HOSE, HNX, UPCOM`).

### 3.2 Số kèm theo mỗi mã

| Trường | Cách tính | Nguồn |
|---|---|---|
| `VolumeRatio` | Khối lượng nến cuối trong DB chia trung bình 20 nến cuối (gồm cả nến cuối), làm tròn 2 số lẻ; không có dữ liệu thì 1 | `Domain/Services/SignalAnalyzer.cs:65-72`, `IndicatorMath.cs:96-102` |
| `RelativeStrength` | `%` đổi giá 5 phiên (lịch sử đã điều chỉnh quyền) trừ `IndexChange5d` của VNINDEX | `SignalAnalyzer.cs:74-78`, `IntradayScannerRunner.cs:76-78` |
| `Signals` | `DetectSignals(stock, index.ChangePercent)` với bộ lọc nền mặc định (không truyền `PriceRunupFilter`); xem 3.3 | `IntradayScannerRunner.cs:80-84`, `SignalAnalyzer.cs:504-523` |

Nến cuối trong DB là nến hôm nay (một phần phiên) vì `EfMarketDataWriter.UpsertQuotesAsync` gộp nến ngày vào `Stocks.HistoryJson` mỗi 60 giây (`Infrastructure/Persistence/Repositories/EfMarketDataWriter.cs:35-54`). Nên `VolumeRatio` trong phiên bị thấp hơn thực tế đầu phiên — đây là hành vi của code, không phải số đã kiểm với production.

### 3.3 Tám loại tín hiệu (`SignalType`) trong `DetectSignals`

| Tín hiệu | Điều kiện | Nguồn |
|---|---|---|
| `Breakout` | Giá đóng > đỉnh cao nhất 20 nến trước; KL > 2 lần trung bình 20 nến trước; % đổi 1 phiên > 3 | `SignalAnalyzer.cs:309-322` |
| `DarvasBreakout` | `AnalyzeFlatBox(...).IsBreakoutConfirmed` (xem mục 7) | `SignalAnalyzer.cs:304-307` |
| `VolumeSpike` | `VolumeRatio >= 2` | `SignalAnalyzer.cs:343` |
| `Accumulation` | 20 nến: biên độ (cao-thấp)/thấp < 15%, không phải breakout, KL nửa sau nhỏ hơn nửa đầu | `SignalAnalyzer.cs:324-341` |
| `Shakeout` | `IsShakeoutFromBase` (rũ qua đáy nền rồi hồi phục) — chi tiết ở `docs/domain/base-price-flatbox.md` | `SignalAnalyzer.cs:380-` |
| `BullishDivergence` | Phân kỳ tăng RSI(14): hằng số `DivergenceLookback=40`, `DivergenceMaxRsiAtLow=45`, `DivergenceMinRsiGain=3` | `SignalAnalyzer.cs:404-413` |
| `Distribution` | 5 nến: KL nến cuối > KL nến đầu, giá 5 phiên đi ngang (< 2%), có ít nhất 2 nến bóng trên dài hơn thân | `SignalAnalyzer.cs:346-363` |
| `RelativeStrength` | `%` 5 phiên trừ `index.ChangePercent` (của 1 phiên, không phải 5 phiên) > 3 | `SignalAnalyzer.cs:519-520` |

Bẫy nhỏ: `RelativeStrength` bên trong `DetectSignals` trừ chỉ số 1 phiên, còn trường `RelativeStrength` của hit trừ chỉ số 5 phiên (xem `IntradayScannerRunner.cs:75-82`).

### 3.4 Ghi bảng và đẩy ra

| Bước | Chi tiết | Nguồn |
|---|---|---|
| Ghi DB | Xóa hết dòng của (`SessionDate`, `Exchange`) rồi thêm lại toàn bộ hit của lần quét — không giữ lịch sử, mỗi 120 giây ghi đè | `Infrastructure/Persistence/Repositories/EfSessionRadarRepository.cs:13-48` |
| Bảng | `SessionRadarHits`, khóa (`SessionDate`, `Exchange`, `Symbol`); cột `Name`, `Sector`, `Price`, `ChangePercent`, `SessionVolume`, `VolumeRatio`, `RelativeStrength`, `SignalsJson`, `ScannedAt` | `Infrastructure/Persistence/ApplicationDbContext.cs:234-247`, `EfSessionRadarRepository.cs:30-44` |
| Cache giá + SignalR | Chỉ giá của các mã hit: `quoteCache.SetQuotes`, `PublishQuotesAsync` (`QuotesUpdated`) | `IntradayScannerRunner.cs:118-122` |
| SignalR radar | `RadarUpdated` gửi cho tất cả client, nội dung là toàn bộ hit đã sắp theo `abs(ChangePercent)` giảm dần rồi `SessionVolume` giảm dần | `IntradayScannerRunner.cs:106-126`, `Api/Hubs/MarketHub.cs:9` |

### 3.5 Đường đọc (API)

| Endpoint | Hành vi | Nguồn |
|---|---|---|
| `GET /api/v1/radar-items/live` | Đọc `SessionRadarHits` của hôm nay, lọc lại theo query (`MinSessionVolume` mặc định 1.000.000, `MinAbsChangePercent` mặc định 3, `Direction` All/Up/Down; Up là `ChangePercent >= min`, Down là `<= -min`) | `Api/Controllers/RadarItemsController.cs:14-19`, `Application/DTOs/RadarLiveDtos.cs:34-39`, `EfSessionRadarRepository.cs:50-80` |
| `GET /api/v1/radar-items` | Khác hẳn "live": không đọc bảng radar. `RadarService.GetRadarAsync` chấm `SmartMoneyEvaluationService` cho toàn bộ mã trong `IStockRepository`, kèm `DetectSignals`, lọc cờ (Breakout/Accumulation/RelativeStrength/VolumeSpike/Shakeout/Distribution), sắp theo điểm, phân trang. Là đường V1 chấm điểm — chỉ web gọi (`frontend/src/lib/api.ts:203`) | `Application/Services/RadarService.cs:15-45`, `RadarItemsController.cs:21-26` |

Client dùng: mobile gọi `/radar-items/live` (`mobile/lib/core/api/api_client.dart:195`), web gọi cả hai (`frontend/src/lib/api.ts:203,214`), thẻ web ở `frontend/src/components/radar/SessionRadarList.tsx`.

## 4. Radar sớm hồi phục (`EarlyRecoveryRadar`)

Không phải job riêng. Nó là bước phụ trong `DailyAnalysisRunner` (V1 chấm điểm Top), nên gỡ Top là mất nó theo. Cột `Reason` nói thẳng ý nghĩa: "mã đã qua cổng xu hướng nhẹ nhưng chưa đủ sức mạnh tương đối để mua, theo dõi ngầm".

### 4.1 Điều kiện (hàm `BuildEarlyRecoveryRadar`, `Infrastructure/MarketData/DailyAnalysisRunner.cs:340-398`)

Mã vào radar khi thỏa tất cả:

| # | Điều kiện | Giá trị | Nguồn |
|---|---|---|---|
| 1 | Không nằm trong Top của phiên | `topSymbols` | `DailyAnalysisRunner.cs:355-356` |
| 2 | Đủ lịch sử | `History.Count >= MinHistoryDays` = 250 | `Application/Options/SmartMoneyOptions.cs:10`, `DailyAnalysisRunner.cs:358-360` |
| 3 | Xu hướng "Loose MA" | Giá đóng > MA20 và MA20 hôm nay >= MA20 cách 3 phiên (thiếu dữ liệu để so thì coi như đạt); cần ít nhất 20 nến | `SignalAnalyzer.cs:80-113` (hàm `PassesLooseMaStack`), `Ma20SlopeNonNegative` `:128-` |
| 4 | Không đủ sức mạnh để mua | Loại mã nếu `RsPercentile >= 80` **và** `Rs5 > 0`. Còn lại (percentile < 80 hoặc `Rs5 <= 0`) thì vào | `DailyAnalysisRunner.cs:371-373`, `SmartMoneyOptions.cs:29`, `appsettings.json:188` |

Cách tính `Rs5` và `RsPercentile`:

| Số | Cách tính | Nguồn |
|---|---|---|
| `Rs5` | `%` đổi giá 5 phiên (đã điều chỉnh quyền) trừ `IndexChangePercent5d` | `DailyAnalysisRunner.cs:370`, `SignalAnalyzer.cs:74-78` |
| `RsPercentile` | Xếp hạng `Rs5` trong các mã có ít nhất `max(250, 6)` nến: thứ hạng `i / (n-1) * 100`, làm tròn 2 số lẻ (mã thấp nhất 0, cao nhất 100; chỉ 1 mã thì 100) | `Domain/Services/RsPercentileCalculator.cs:16-45` |

Cột kèm theo: `Price` = `Stock.LatestPrice`, `ChangePercent` = % 1 phiên, `VolumeRatio`, `MarketPhase` = nhãn pha thị trường lúc chạy, `Reason` (chuỗi tiếng Việt nêu lý do thấp RS, `:374-379`). Sắp xếp: `RsPercentile` giảm dần, `Rs5` giảm dần, `Symbol`.

### 4.2 Ghi và đọc

| Việc | Chi tiết | Nguồn |
|---|---|---|
| Ghi | `ReplaceForDateAsync(forTradingDate, ...)`: xóa hết dòng của ngày đó rồi thêm lại. Chạy ở mọi lần phân tích, kể cả lần "light" làm mới giữa phiên (lệnh nằm ngoài khối `if (includeStructureAndTracking)`) | `DailyAnalysisRunner.cs:288-297`, `EfEarlyRecoveryRadarRepository.cs:9-41` |
| Bảng | `EarlyRecoveryRadar`, khóa (`ForTradingDate`, `Symbol`), chỉ mục theo `ForTradingDate` | `ApplicationDbContext.cs:218-231` |
| Đọc | `GET /api/v1/early-recovery` (phân trang). Lấy theo `TradingCalendar.GetActiveOpportunityDate()`; nếu chưa có thì lùi về ngày gần nhất có dữ liệu và ghi `StatusMessage` | `Api/Controllers/EarlyRecoveryController.cs:14-19`, `Application/Services/MarketService.cs:306-367` |
| Client | Không thấy mobile (`mobile/lib`) hay web (`frontend/src`) gọi endpoint này (grep `early-recovery` / `EarlyRecovery` trống) | — |

## 5. Theo dõi lô lớn / dòng tiền (`OpportunityIntradayMonitorRunner`)

Mỗi 60 giây: lấy bảng giá KBS cho mọi mã active, so với ảnh chụp lần quét trước của chính mã đó, và suy ra "lô lớn" từ chênh lệch.

### 5.1 Nguồn dữ liệu

`KbsPriceBoardClient.FetchAsync` gọi `POST https://kbbuddywts.kbsec.com.vn/iis-server/investment/stock/iss` với `{code: "A,B,C"}` và header `x-lang: vi` (`Infrastructure/MarketData/KbsPriceBoardClient.cs:9-24`). Mỗi dòng trả về được đổi thành `KbsBoardRow` (`:57-117`, kiểu ở `:131-154`):

| Trường trong code | Khóa KBS đọc | Ghi chú |
|---|---|---|
| `Close` | `CP` (nếu 0 thì `RE`) | Giá `>= 1000` được chia 1000 (đơn vị nghìn đồng) |
| `SessionVolume` | `TT` | Khối lượng tích lũy cả phiên |
| `ChangePercent` | `CHP` | |
| `BidPrice1..3` / `AskPrice1..3` | `B1..B3` / `S1..S3` | |
| `BidVolume1..3` / `AskVolume1..3` | `V1..V3` / `U1..U3` | Ba mức giá mua / bán tốt nhất |
| `ForeignBuyVolume` / `ForeignSellVolume` | `FB` / `FS` | Tích lũy phiên theo cách code dùng (lấy hiệu hai lần quét) |
| `ProprietaryVolume` | `CV` | Tên cột KBS cho trường này chưa kiểm được; code đặt tên là "tự doanh" |
| `PutThroughVolume` / `PutThroughValue` | `PTQ` / `PTV` | Được đọc nhưng không dùng trong phát hiện lô lớn |

Ý nghĩa các khóa KBS là suy từ cách code đặt tên, chưa có tài liệu KBS để đối chiếu.

### 5.2 Định nghĩa "lô lớn"

Một "lô" ở đây là **tổng khối lượng khớp tăng thêm giữa hai lần quét liên tiếp (khoảng 60 giây)**, không phải một lệnh khớp riêng lẻ. Mã không có ảnh chụp trước (lần quét đầu sau khi chạy API) thì bỏ qua (`TradeEventDetector.cs:14-15`).

| Đại lượng | Công thức | Nguồn |
|---|---|---|
| `deltaVol` | `SessionVolume` hiện tại trừ lần trước; `<= 0` thì bỏ | `TradeEventDetector.cs:17-19` |
| `valueVnd` | `Close * 1000 * deltaVol` (VND) | `TradeEventDetector.cs:21` |
| Lô lớn tức thì (`isBlock`) | `deltaVol >= 25_000` **và** `valueVnd >= 500_000_000` | `TradeEventDetector.cs:23`, `Application/Options/PipelineOptions.cs:12,15` |
| Lô nhỏ chờ gộp (`isMicro`) | `deltaVol >= 8_000` **và** `valueVnd >= 150_000_000` | `TradeEventDetector.cs:22`, `PipelineOptions.cs:18,20` |
| Khác | Không đạt cả hai thì không có sự kiện | `TradeEventDetector.cs:24-25` |

Cấu hình trong `appsettings.json:233-246` trùng giá trị mặc định trên.

### 5.3 Phân mua chủ động / bán chủ động — code KHÔNG làm

Code không phân loại bên chủ động (không có dữ liệu từng lệnh khớp). Hướng được suy hoàn toàn từ giá giữa hai lần quét:

| Thành phần | Cách tính | Nguồn |
|---|---|---|
| Biên độ `spreadPct` | `abs(Close - previous.Close) / previous.Close * 100`, làm tròn 3 số lẻ | `TradeEventDetector.cs:27-30` |
| Nhãn | `spreadPct < 0.25` là `GomIm`; `spreadPct >= 0.4` thì giá tăng là `DayGia`, giảm là `Xa` (bằng không là `TrungTinh`, nhưng không thể xảy ra vì biên độ đã `>= 0.4`); còn lại `TrungTinh` | `TradeEventDetector.cs:50-66`, `PipelineOptions.cs:26,29`, `Application/DTOs/TradeEventLabels.cs:6-9` |
| Ý nghĩa nhãn (theo tên và `OpportunityMonitorOptions`) | `GomIm` khối lượng lớn mà giá gần như không đổi; `DayGia` giá tăng mạnh; `Xa` giá giảm mạnh | Suy từ tên, không có tài liệu nghiệp vụ trong code |
| Khối ngoại ròng của lần quét | `(FB hiện - FB trước) - (FS hiện - FS trước)` | `OrderBookMetrics.cs:16-26` |
| Tự doanh của lần quét | `CV hiện - CV trước` | `OrderBookMetrics.cs:28-36` |
| Chênh lệch sổ lệnh | Tổng khối lượng 3 mức mua trừ tổng khối lượng 3 mức bán | `OrderBookMetrics.cs:7-14` |

Lớp `TradePrintDto` có trường `Side` nhưng đã đánh dấu `[Obsolete]` và không được dùng (`Application/DTOs/MarketDtos.cs:448-454`).

### 5.4 Cửa sổ gộp (`TradeEventAggregator`)

| Quy tắc | Chi tiết | Nguồn |
|---|---|---|
| Lô lớn tức thì | Phát ngay một sự kiện (`IsAggregated = false`) và **bỏ** bucket gộp đang chờ của mã đó | `TradeEventAggregator.cs:24-29` |
| Lô nhỏ | Cho vào bucket của mã. Sau mỗi lần thêm, nếu tổng `Volume >= 25_000` và tổng `ValueVnd >= 500_000_000` thì phát sự kiện gộp (`IsAggregated = true`) và xóa bucket | `TradeEventAggregator.cs:31-43`, `:81-114` |
| Hết hạn bucket | Bucket sống `max(60, AggregateWindowSeconds=180)` giây tính từ lúc mở; hết hạn thì kiểm ngưỡng tổng một lần nữa, đạt thì phát, không đạt thì bỏ | `TradeEventAggregator.cs:61-79`, `appsettings.json:240` |
| Thời điểm kiểm hết hạn | Mỗi lần có `Add` và cuối mỗi vòng quét (`FlushExpired`); sự kiện hết hạn cần có `SessionFlow` của mã, không có thì bỏ qua | `TradeEventAggregator.cs:22,49-59`, `OpportunityIntradayMonitorRunner.cs:90-100` |
| Nhãn của sự kiện gộp | Tính lại bằng biên độ giữa giá cuối và giá đầu của chuỗi quét trong bucket (`last.Price - first.Price`), cùng ngưỡng 0.25 / 0.4 | `TradeEventAggregator.cs:95-101` |
| Nội dung sự kiện gộp | `Price` là giá lần quét cuối; `BookImbalance` lấy của lần quét cuối; `ForeignNetDelta` và `PropDelta` là tổng các lần quét | `TradeEventAggregator.cs:103-113` |

### 5.5 Dòng tiền trong phiên (`SessionFlowTracker`)

Giữ trong RAM theo mã, cập nhật mỗi lần quét có ảnh chụp trước (`OpportunityIntradayMonitorRunner.cs:59-81`):

| Trường | Cách tính | Nguồn |
|---|---|---|
| `SessionForeignNet` / `SessionPropNet` | Cộng dồn `ForeignNetDelta` / `PropDelta` của từng lần quét | `SessionFlowTracker.cs:45-46` |
| `LastBookImbalance` | Giá trị lần quét mới nhất | `SessionFlowTracker.cs:47` |
| `SessionPressure` | `clamp(foreignNet / 50_000, -40, 40) + clamp(propNet / 80_000, -25, 25) + clamp(bookImbalance / 100_000, -35, 35)`, làm tròn 1 số lẻ (tối đa 100) | `SessionFlowTracker.cs:92-98` |
| `ForeignNetSinceAfternoon` | Chụp `SessionForeignNet` đúng một lần/mã/phiên ở lần cập nhật đầu tiên sau 13:00; giá trị = hiện tại trừ lần chụp. Trước 13:00 là `null` | `SessionFlowTracker.cs:38-43,80-90` |
| Đặt lại | So ngày với `VietnamMarketCalendar.TodayVietnam()`, khác thì xóa toàn bộ trạng thái | `SessionFlowTracker.cs:23-27` |

`SessionFlowDto` (API) không có trường `ForeignNetSinceAfternoon` — chỉ có trong snapshot nội bộ (`SessionFlowQueryService.cs:31-38`). Bảng xếp hạng (`GetLeaders`): các mã có `SessionForeignNet >= 0`, sắp giảm dần, lấy `take`; biến `min` trong `SessionFlowQueryService.cs:20` khai báo nhưng không dùng.

### 5.6 Lưu và đẩy ra

| Việc | Chi tiết | Nguồn |
|---|---|---|
| Lưu sự kiện | RAM, `TradeEventStore`, tối đa 500 sự kiện (thêm vào đầu danh sách, bỏ cuối). Không có bảng DB. Mất khi API khởi động lại | `Infrastructure/MarketData/TradeEventStore.cs:10-22` |
| Đọc sự kiện | `GetRecent(limit, label)`: lọc theo nhãn hoặc `ForeignStrong` (`SessionForeignNet >= 500_000`, `ForeignStrongSessionNet`), sắp theo `ValueVnd` giảm dần rồi `At` giảm dần | `TradeEventStore.cs:24-49`, `PipelineOptions.cs:32` |
| SignalR | `TradeEventCreated` gửi cho tất cả client (`hub.Clients.All`) mỗi sự kiện | `Api/Realtime/SignalRMarketRealtimePublisher.cs:38-39`, `Api/Hubs/MarketHub.cs:11` |
| Giá | Cuối vòng: `quoteCache.SetQuotes` + `PublishQuotesAsync` cho mọi mã đã quét | `OpportunityIntradayMonitorRunner.cs:102-106` |
| Ghi giá vào DB | Mỗi lô 40 mã: `sync.ApplyAsync(MarketSyncRequest(null, quotes))` — ghi `Open/High/Low/Close/Volume/ChangePercent` vào nến hôm nay trong `Stocks` (không có sector). `MarketSyncService.ApplyAsync` còn tự `PublishQuotesAsync` nên giá bị đẩy hai lần mỗi vòng | `OpportunityIntradayMonitorRunner.cs:84-87`, `Application/Services/MarketSyncService.cs:26-39` |
| Trạng thái quét | `IntradayMonitorStatusTracker` (RAM): lần quét cuối, số mã, số sự kiện, lý do bỏ qua. `IsStale` khi đang mở phiên và chưa quét lại sau `interval * 2.5` giây | `IntradayMonitorStatusTracker.cs`, `IntradayMonitorStatusQueryService.cs:13-36` |

### 5.7 Endpoint

| Endpoint | Nguồn |
|---|---|
| `GET /api/v1/market/intraday-monitor` — trạng thái quét | `Api/Controllers/MarketController.cs:31-35` |
| `GET /api/v1/market/trades?limit&label` — `limit` kẹp 1-200 | `MarketController.cs:37-43` |
| `GET /api/v1/market/flow/{symbol}` — 404 nếu chưa có | `MarketController.cs:43-50` |
| `GET /api/v1/market/flow/leaders?limit` — `limit` kẹp 1-50 | `MarketController.cs:52-55` |

Client: mobile có hàm gọi `/market/intraday-monitor`, `/market/trades` (`mobile/lib/core/api/api_client.dart:206,212`) và nghe `TradeEventCreated` (`mobile/lib/core/services/market_hub_service.dart:66`); web có `useLiveTrades.ts` và `api.ts:139,143`. Spec 016 ghi "không UI nào hiện"; có màn nào đang hiển thị hay không chưa kiểm được.

## 6. Việc ghi giá của monitor và `KbsMarketSyncJob`

Spec 016 Q2 yêu cầu xác nhận `KbsMarketSyncJob` làm đủ việc ghi giá trước khi gỡ monitor. Đối chiếu code:

| Mặt | `OpportunityIntradayMonitorRunner` | `KbsMarketSyncRunner` (job `kbs-market-sync`) |
|---|---|---|
| Tập mã | `GetActiveSymbolsAsync` (mã active) | `GetAllAsync` (mọi mã trong DB, bỏ VNINDEX) |
| Trường ghi | `Open, High, Low, Close, SessionVolume, ChangePercent`, sector `null` | Cùng các trường, có `Sector` của mã |
| Chỉ số VNINDEX | Không | Có (`KbsIndexClient.FetchVnIndexAsync`) |
| Chu kỳ | 60 giây | `MarketData.SyncIntervalSeconds` = 60 |
| Cổng giờ | `IsMarketOpen()` | `IsMarketOpen()` trừ khi `ForceSyncOutsideHours` (mặc định `true` trong `AppOptions.cs:38`, `appsettings.json` không ghi đè) |
| Nguồn | `OpportunityIntradayMonitorRunner.cs:84-87` | `Infrastructure/MarketData/KbsMarketSyncRunner.cs:16-58`, `Scheduling/Jobs/KbsMarketSyncJob.cs:16-19` |

Theo code, `KbsMarketSyncRunner` ghi là tập cha của việc ghi giá của monitor. Chưa kiểm được: dữ liệu thực trên production và cấu hình `appsettings.Production.json` trên server.

## 7. Cảnh báo phá hộp Darvas (`DarvasBreakoutAlertPublisher`)

### 7.1 Chạy lúc nào

Gọi cuối `DailySessionSyncRunner.RunAsync` sau khi đã ghi giá và chỉ số, bọc `try/catch` (lỗi thì ghi log rồi bỏ qua) (`Infrastructure/MarketData/DailySessionSyncRunner.cs:106-121`). Chạy trên toàn bộ mã `stocks.GetAllAsync` (không chỉ mã active). Job 2 chạy mỗi 5 phút trong phiên (mục 2).

### 7.2 Điều kiện phát cảnh báo (`DarvasBreakoutAlertPublisher.cs:34-86`)

| # | Điều kiện | Giá trị | Nguồn |
|---|---|---|---|
| 1 | Lịch sử đã điều chỉnh quyền đủ dài | `History.Count >= ConsolidationMinSessions + 1` = 11 | `DarvasBreakoutAlertPublisher.cs:36-39`, `appsettings.json:204` |
| 2 | Hôm nay có tín hiệu `DarvasBreakout` | `DetectSignals(..., filter)` chứa `DarvasBreakout` | `:41-43` |
| 3 | Phiên trước chưa có tín hiệu | Cùng phép kiểm trên lịch sử đã bỏ nến cuối; nếu đã có thì bỏ | `:45-51` |
| 4 | Phá vỡ hợp lệ | `EvaluateDarvasBreakout(...).IsValidBreakout` | `:53-55` |

Kết quả phá vỡ (`DarvasBreakoutAnalyzer`, `Domain/Services/DarvasBreakoutAnalyzer.cs`):

| Phần | Quy tắc | Ngưỡng appsettings | Nguồn |
|---|---|---|---|
| Tìm hộp | Thử độ dài hộp từ 45 phiên xuống 10 phiên (`MaxBaseWindowSessions` 45, `ConsolidationMinSessions` 10), hộp kết thúc ngay trước phiên phá vỡ; lấy hộp đầu tiên hợp lệ | `appsettings.json:204-206` | `DarvasBreakoutAnalyzer.cs:21-60,150-182` |
| Hộp hợp lệ (nhánh phá vỡ) | Chiều cao giá đóng cửa của hộp <= 10% (`BreakoutMaxBoxHeightPercent`); bóng nến không vượt giá đóng cao/thấp nhất quá 3% (`ShadowTolerancePercent`); chạm đỉnh >= 2 lần và chạm đáy >= 2 lần, vùng chạm 1,5% (`TouchThresholdPercent`); không đòi KL cạn dần (`requireVolumeDryUp: false`); còn các kiểm tra khác ở nửa sau hàm | `appsettings.json:214-226` | `Domain/Services/BaseQualityEvaluator.cs:192-` (đọc tới dòng 249; phần sau chưa đọc), `DarvasBreakoutAnalyzer.cs:167-174` |
| Phá vỡ | Giá đóng > giá đóng cao nhất của hộp; tăng so phiên trước >= 2,5% (`BreakoutMinPriceGainPercent`); KL >= 1,5 lần KL trung bình hộp (`BreakoutMinVolumeMultiplier`); bóng trên / biên độ nến <= 0,25 (`BreakoutMaxUpperShadowRatio`) | `appsettings.json:222-224` | `DarvasBreakoutAnalyzer.cs:184-221` |
| Cắt lỗ gợi ý | Đáy giá đóng cửa của hộp (`BoxMinClose`) | — | `DarvasBreakoutAnalyzer.cs:96,143` |

Phần "hộp hợp lệ" còn nhiều chi tiết (chia 3 đoạn, pivot 3 phiên cuối, biên độ 3 phiên cuối `MaxLast3AvgRangePercent` 3,5); đầy đủ ở `docs/domain/base-price-flatbox.md` — file này không chép lại.

### 7.3 Nội dung và đẩy ra

| Việc | Chi tiết | Nguồn |
|---|---|---|
| Tạo `Alert` | `SignalType.DarvasBreakout`, `AlertCategory.Buy`, `SourceTag = BasePriceLabels.Base`; tiêu đề `"{mã} — {nhãn sự kiện}"`; nội dung nêu giá phá đỉnh nền, % tăng, KL gấp mấy lần trung bình nền, kỳ nền, cắt lỗ gợi ý | `DarvasBreakoutAlertPublisher.cs:19,57-78` |
| Ghi DB | `IAlertRepository.AddAsync` → bảng `Alerts` (khóa Guid, mỗi cảnh báo một dòng, không có ràng buộc duy nhất theo mã/ngày) | `Infrastructure/Persistence/Repositories/EfRepositories.cs:158-162`, `ApplicationDbContext.cs:91-99` |
| SignalR | `PublishAlertAsync` → sự kiện `AlertCreated` | `Api/Realtime/SignalRMarketRealtimePublisher.cs:35-36`, `MarketHub.cs:10` |
| Telegram / FCM | Không có | — |

## 8. V3 có dùng chung không

Kết quả `grep` các tên `TradeEvent`, `SessionFlow`, `OrderBook`, `OrderFlowSnapshotTracker`, `IntradayMonitorStatus`, `SessionRadar`, `IntradayScanner`, `EarlyRecovery`, `DarvasBreakout`, `RadarLive`, `IRadarService`, `OpportunityMonitor` trên `backend/**/*.cs` cho thấy các file `V3*` không tham chiếu thành phần nào trong số này. Spec 016 chỉ gỡ phần V1 nên các thứ sau phải **giữ**:

| Thành phần dùng chung | Ai dùng | Nguồn |
|---|---|---|
| `KbsPriceBoardClient` | V3 (`V3Engine`), `KbsMarketSyncRunner`, Job 2, và hai runner bị gỡ | `Infrastructure/MarketData/V3Engine.cs:26` |
| `IMarketSyncService.ApplyAsync`, `IQuoteTickCache`, `IMarketRealtimePublisher.PublishQuotesAsync` (`QuotesUpdated`) | `KbsMarketSyncJob`, Job 2 | `Application/Services/MarketSyncService.cs:12-41` |
| `VietnamMarketCalendar.IsMarketOpen/IsTradingDay`, `IJobStockRepository.GetActiveSymbolsAsync` | Nhiều job | — |

Khi gỡ chỉ bỏ ba phương thức `PublishRadarAsync`, `PublishTradeEventAsync`, `PublishAlertAsync` khỏi `IMarketRealtimePublisher` (`Application/Abstractions/IMarketRealtimePublisher.cs:11-15`), cùng `NullMarketRealtimePublisher` và `SignalRMarketRealtimePublisher`.

`SignalAnalyzer` (dùng trong radar, early recovery, Darvas) còn được gọi bởi đường khác: `MarketService.GetSignalsAsync` (`Application/Services/MarketService.cs:602-624`, endpoint `GET /api/v1/signals`) và `StockService` (xuất hiện trong kết quả grep). Gỡ nó phụ thuộc quyết định Q3 trong spec 016, không phải riêng file này.

## 9. Bẫy khi dựng lại

| # | Bẫy | Nguồn |
|---|---|---|
| 1 | "Lô lớn" là tổng khối lượng tăng trong ~60 giây, không phải một lệnh. Dựng lại bằng chu kỳ khác sẽ đổi ý nghĩa ngưỡng 25.000 cổ / 500 triệu đồng. | `TradeEventDetector.cs:17-23` |
| 2 | Không có dữ liệu mua/bán chủ động; nhãn chỉ dựa biên độ giá hai lần quét. Muốn phân bên chủ động thật cần nguồn khớp lệnh từng dòng, chưa có trong code. | `TradeEventDetector.cs:27-32` |
| 3 | Ảnh chụp trước (`OrderFlowSnapshotTracker`) không bao giờ bị xóa theo ngày (`Reset()` không có nơi gọi) trong khi `SessionFlowTracker` thì xóa theo ngày. Nếu API chạy qua đêm, lần quét đầu ngày so với ảnh chụp hôm qua: `FB`, `FS`, `CV`, `TT` là số tích lũy phiên nên hiệu có thể âm lớn hoặc dương lớn, và nhánh `else if (previous is not null)` vẫn cộng vào dòng tiền. Suy từ đọc code; chưa kiểm trên production. | `OrderFlowSnapshotTracker.cs:24-28`, `OpportunityIntradayMonitorRunner.cs:53-81`, `SessionFlowTracker.cs:23-27` |
| 4 | Toàn bộ sự kiện và dòng tiền nằm trong RAM (`TradeEventStore`, `SessionFlowTracker`, snapshot); khởi động lại là mất. `TradeEventStore` cũng không dọn theo ngày, chỉ bị đẩy ra khi quá 500. | `TradeEventStore.cs:10-22` |
| 5 | `Exchange = "HOSE"` của radar chỉ là nhãn, không lọc sàn. | `IntradayScannerRunner.cs:25,34` |
| 6 | Radar và monitor mỗi cái gọi KBS cho toàn bộ mã active (lô 40 mã) ở chu kỳ 120s và 60s; thêm `KbsMarketSyncJob` 60s và Job 2 5 phút. Dựng lại nên dùng chung một lần gọi bảng giá. | `QuartzSchedulingExtensions.cs:163-216` |
| 7 | Monitor ghi giá vào DB và tự đẩy `QuotesUpdated` trùng với `KbsMarketSyncJob` (mục 6). | `OpportunityIntradayMonitorRunner.cs:84-87,102-106` |
| 8 | Radar dùng nến cuối trong DB (nến hôm nay chưa đủ phiên) làm `VolumeRatio`, nên giá trị trong phiên thấp hơn cuối phiên. `DetectSignals` trong radar còn dùng `stock.History` thô (không điều chỉnh quyền), trong khi RS dùng lịch sử đã điều chỉnh. | `IntradayScannerRunner.cs:71-82`, `SignalAnalyzer.cs:25-32,74-78` |
| 9 | `RelativeStrength` trong `DetectSignals` trừ chỉ số 1 phiên, còn trường `RelativeStrength` của hit trừ chỉ số 5 phiên. | `SignalAnalyzer.cs:519`, `IntradayScannerRunner.cs:75-82` |
| 10 | `GET /radar-items` (không `live`) không đọc bảng radar mà chấm lại điểm toàn universe mỗi lần gọi — nặng và phụ thuộc V1 SmartMoney. | `RadarService.cs:15-45` |
| 11 | Early recovery sống trong `DailyAnalysisRunner`; nếu dựng lại độc lập phải tự tính `RsPercentile` (cần cả rổ >= 250 nến) và nhãn pha thị trường (V1 `MarketPhaseClassifier`, spec 016 Q6 gỡ). Trên production nhãn pha không bao giờ ra Favorable (PH-31, spec 016). | `DailyAnalysisRunner.cs:340-398`, `specs/016-remove-v1-top/spec.md` (Q6) |
| 12 | Cảnh báo Darvas không có bước chống lặp theo ngày. Điều kiện "phiên trước chưa có tín hiệu" chỉ bỏ nến cuối; nếu Job 2 chạy mỗi 5 phút khi nến hôm nay đang cập nhật, cùng một mã có thể bị cảnh báo nhiều lần trong ngày. Suy từ code; số cảnh báo trùng thực tế trên production chưa kiểm được. | `DarvasBreakoutAlertPublisher.cs:45-51`, `EfRepositories.cs:158-162` |
| 13 | Mô tả `JobCatalog` cho `opportunity-monitor` ("Theo dõi Top intraday...") lỗi thời so với code (không còn dùng Top). | `Application/Jobs/JobCatalog.cs:35-37` |
| 14 | Ý nghĩa các khóa KBS (`CV`, `FB`, `FS`, `V1..U3`) chỉ có trong tên biến của code. Trước khi dựng lại cần đối chiếu lại với phản hồi thật của KBS. | `KbsPriceBoardClient.cs:57-117` |

## 10. Danh sách cấu hình cần ghi nhớ

Giá trị tại commit da75683. Mục không có trong `appsettings.json` lấy từ giá trị mặc định trong class options.

| Cấu hình | Giá trị | Nguồn |
|---|---|---|
| `IntradayScanner.Enabled` / `IntervalSeconds` / `MinClosePriceVnd` | `true` / 120 / 8000 | `appsettings.json:228-232` |
| `IntradayScanner.MinSessionVolume` / `MinAbsChangePercent` / `BatchSize` / `Exchange` | 1.000.000 / 3 / 40 / "HOSE" (mặc định code) | `IntradayScannerOptions.cs:13-21` |
| `OpportunityMonitor` | Toàn bộ trong `appsettings.json:233-246`: `IntervalSeconds` 60, `MinTradeVolume` 25000, `MinTradeValueVnd` 500000000, `MinMicroVolume` 8000, `MinMicroValueVnd` 150000000, `AggregateWindowSeconds` 180, `VsaSpreadTightPercent` 0,25, `VsaSpreadWidePercent` 0,4, `ForeignStrongSessionNet` 500000, `BatchSize` 40 | `appsettings.json:233-246` |
| `MarketData` (`KbsMarketSyncJob`) | `AutoSyncEnabled` true, `SyncIntervalSeconds` 60 | `appsettings.json:86-90` |
| `MarketJobs.DailySession` | `IntervalMinutes` 5, `Hour` 15, `Minute` 0, `BatchSize` 40 | `appsettings.json:112-119` |
| `SmartMoney` (cho early recovery) | `MinHistoryDays` 250, `MinRsPercentileForUnfavorable` 80, `MaStack.MinSessionsForMa50` 50, `MinSessionsForFullStack` 200 | `appsettings.json:163-188` |
| `PriceRunupFilter` (cho Darvas) | `ConsolidationMinSessions` 10, `MaxBaseWindowSessions` 45, khối `Darvas` 9 / 3 / 0,8 / 1,5 / 2 / 2 / 3,5 / 2,5 / 1,5 / 0,25 / 10 | `appsettings.json:203-227` |

## 11. Điểm chưa kiểm được

| Điểm | Lý do |
|---|---|
| Cấu hình và dữ liệu production | Chỉ đọc repo; server có `appsettings.Production.json` riêng |
| Có màn mobile / web nào đang hiển thị sự kiện lô lớn, dòng tiền | Chỉ thấy hàm gọi API và hub; chưa đọc các màn hình |
| Tiền tố route `/market/jobs/...` của `MarketJobsController` | Chưa đọc thuộc tính `[Route]` ở đầu controller |
| Ý nghĩa các khóa KBS (`CV`, `FB`, `FS`, `TT`, `V1..U3`) và việc `TT` có gồm khớp thỏa thuận | Không có tài liệu KBS trong repo |
| Bẫy 3 (snapshot qua ngày) và bẫy 12 (Darvas trùng) có xảy ra thật không | Chỉ suy từ code, chưa đo trên log / DB production |
| Nửa sau hàm `BaseQualityEvaluator.PassesDarvasBox` (từ dòng 250) | Chưa đọc hết; chi tiết ở `docs/domain/base-price-flatbox.md` |
| `DailyAnalysisJob` tự lọc giờ thế nào cho các trigger làm mới | Chưa đọc file job |
