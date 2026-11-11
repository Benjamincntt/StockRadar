# StockRadar — Báo cáo phân tích kiến trúc

- Ngày: 2026-11-09
- Phạm vi: chỉ phân tích, không sửa code. Nguồn: code trên disk (`backend/`, `frontend/`, `mobile/`), `docs/architecture.md`, `docs/domain/pipeline-jobs.md`.
- Cách ghi mức tin cậy: **[Đoạn code]** = đọc trực tiếp trong code. **[Suy luận]** = suy ra từ cấu trúc code, chưa đo bằng profiler/log production.

---

## Tóm tắt điều hành

StockRadar là monolith modular một tiến trình: .NET 10 API chứa luôn Quartz scheduler, toàn bộ job pipeline, SignalR hub và outbound KBS/Telegram/FCM. Điểm mạnh là ranh giới Domain/Application/Infrastructure/Api rõ, luật nghiệp vụ tập trung ở Domain thuần và có test domain khá dày. Rủi ro lớn nhất không nằm ở thuật toán chấm điểm mà ở lớp dữ liệu và vận hành async: lịch sử OHLCV lưu dạng blob JSON trong `Stocks.HistoryJson`, mọi job đều đọc–giải mã–ghi lại toàn bộ blob, nhiều poller 60 giây cùng ghi vào cùng bảng, và không có cơ chế chống chạy trùng, giao dịch nguyên tử hay outbox cho thông báo.

Ba việc nên làm trước mọi tối ưu khác: (1) đo và khóa chống trùng cho pipeline Job 2 → Analysis, (2) tách bảng nến ngày khỏi `HistoryJson`, (3) làm lỗi KBS thành lỗi nhìn thấy được thay vì danh sách rỗng.

---

## 1. Các module chính

### 1.1 Bản đồ tổng thể

| Lớp | Vị trí | Trách nhiệm | Ghi chú |
|---|---|---|---|
| Domain | `backend/StockRadar.Domain/` (82 file .cs) | Entity/record thuần, engine chấm điểm, luật V1 + V3 | Không phụ thuộc EF/HTTP. File lớn nhất: `BaseQualityEvaluator.cs` 993 dòng, `BuyDecisionEngine.cs` 706 dòng, `SignalAnalyzer.cs` 539 dòng |
| Application | `backend/StockRadar.Application/` | DTO, Options, interface (`Abstractions/`, ~35 file), service điều phối mỏng | `MarketService.cs` 625 dòng, `NhatKyService.cs` 608 dòng là hai service lớn nhất |
| Infrastructure | `backend/StockRadar.Infrastructure/` | EF Core + SQL Server, Quartz, client KBS, runner job, Telegram/FCM, ML file store | Nơi chứa toàn bộ I/O và lịch chạy |
| Api | `backend/StockRadar.Api/` (17 controller) | REST `/api/v1`, JWT, SignalR `/hubs/market`, Swagger | `Program.cs` lắp ráp toàn bộ |
| Tests | `backend/StockRadar.Tests/` (~70 file) | Test Domain/V3/Nhật ký/FCM | Hầu như không có test tích hợp pipeline Job 1 → Job 2 |
| Web | `frontend/src/` (React 19 + Vite) | 6 page: Home, Watchlist, StockDetail, HieuQua, RightsEvents, Login | Realtime qua `@microsoft/signalr`, fallback poll |
| Mobile | `mobile/lib/` (Flutter) | 9 screen, bottom nav 3 tab | Dùng chung REST + SignalR + FCM push |

### 1.2 Backend — phân rã theo chức năng

| Module | Entry tiêu biểu | Đầu vào → đầu ra |
|---|---|---|
| Thu thập giá KBS | `KbsPriceBoardClient`, `KbsHistoryClient`, `KbsIndexClient`, `KbsGroupListingClient`, `KbsStockListingClient`, `KbsSectorLookupClient` | HTTP tới `kbbuddywts.kbsec.com.vn` → board/history/index |
| Job 1 — Backfill | `HistoryBackfillRunner` + `HistoryBackfillJob` | Listing KBS → lọc universe → `Stocks` |
| Job 2 — Sync phiên | `DailySessionSyncRunner` + `DailySessionSyncJob` | Board KBS (mã active) → nến ngày T trong `HistoryJson` + alert Darvas |
| Phân tích V1 | `DailyAnalysisRunner` + `DailyAnalysisJob` | `Stocks` + VNINDEX → `DailyOpportunities` (Top 5), `EarlyRecoveryRadar`, `SetupTracks`, `DailyAnalysisRuns` |
| Realtime trong phiên | `KbsMarketSyncRunner` (60s), `IntradayScannerRunner` (120s), `OpportunityIntradayMonitorRunner` (60s) | Board KBS → `QuoteTickCache`, SignalR, `SessionRadarHits`, `TradeEvent` (in-memory) |
| Bộ kịch bản V3 | `V3Engine` (686 dòng, máy dùng chung) + 4 runner `V3TruocPhienRunner` 08:30, `V3TrongPhienRunner` mỗi phút, `V3AtcRunner` 14:25, `V3SauPhienRunner` 16:00 | `ChiSoNgayV3`/`TinHieuV3`/`ViTheV3` + Telegram/FCM |
| Engine V1 | `BuyDecisionEngine`, `SignalAnalyzer`, `SmartMoneyOpportunitySelector`, `DarvasBreakoutAnalyzer`, `MarketPhaseClassifier`, `SectorWaveRegimeEngine` | `Stock.History` → Buy Score 0–100, Entry, TradeState, 4 cổng Top |
| ML / đo hiệu quả | `OpportunityRankerService`, `OpportunityRankingDatasetService`, `HyperparameterTuningRunner` (gọi `scripts/tune-optuna.py`) | `SetupTracks` → model JSON trên đĩa → xếp hạng Top; HPO chỉ gợi ý qua Telegram, không tự áp |
| Thông báo | `TelegramNotifier`, `KenhDayTinFcm`/`GuiThongBao`, `DarvasBreakoutAlertPublisher` | Sự kiện job → Telegram/FCM/SignalR |
| Persistence | `ApplicationDbContext` (~35 DbSet), `Ef*Repository`, `CachedStockRepository`/`CachedMarketIndexProvider` | SQL Server `StockRadarDb` |
| Lịch | `QuartzSchedulingExtensions` + `Scheduling/Jobs/*` | Quartz in-memory store, thread pool tối đa 6, múi giờ VN |

### 1.3 Luồng request của user (đọc)

`Web/Mobile → REST (JWT) → Application Service → Repository (EF, có cache MemoryCache cho stock list/index) → SQL Server`. Giá live đi đường riêng: job 60s ghi `QuoteTickCache` (singleton `ConcurrentDictionary`) rồi `SignalRMarketRealtimePublisher` đẩy theo group `symbol:{SYMBOL}`; mất kết nối thì client poll `GET /market/quotes`.

---

## 2. Luồng dữ liệu Job 1 → Job 2

### 2.1 Job 1 — `HistoryBackfillRunner.RunInternalAsync` [Đoạn code]

```text
Trigger: POST /market/jobs/history[/night] (header X-Sync-Key hoặc user đã đăng nhập)
         | Quartz startup (RunOnStartup, +8s) | Quartz CN 02:00 chế độ night
         v
ResolveSymbolsAsync: Groups=[HOSE,HNX,UPCOM] qua KbsGroupListingClient
                     (hoặc AllListed qua KbsStockListingClient)
  -> listings.GetListingsAsync (sàn, hạn chế GD) + sectors.GetSymbolSectorsAsync
  -> VÒNG LẶP TUẦN TỰ từng mã, delay 300ms (nhanh) / 1500ms (đêm):
       FetchBarsAsync(screening: end-450 ngày .. end)        [KBS history API]
       StockUniverseFilter.Screen: giá đóng > 8.000đ, loại IPO < 365 ngày,
                                   loại mã hạn chế giao dịch
       |-- rớt  -> writer.MarkUniverseInactiveAsync (IsActive=false, 1 SaveChanges/mã)
       |-- đậu  -> FetchBarsAsync(full: 2000-01-01 .. T-1)  [KBS history API lần 2]
                   writer.UpsertUniverseStockAsync -> Stocks (HistoryJson = toàn bộ nến)
  -> DeactivateStaleUniverseAsync: mã active không còn trong danh sách -> IsActive=false
  -> UniverseRescreenRunner.RunAsync: chấm lại toàn bộ theo History đã lưu
```

Tính chất: chống chạy trùng bằng `[DisallowConcurrentExecution]` trên `HistoryBackfillJob` và cờ `HistoryBackfillState.IsRunning` ở tầng API (chỉ cho đường manual). Idempotent theo nghĩa chạy lại ghi đè cùng dữ liệu (merge theo ngày, lấy bản ghi cuối). Không có transaction bao toàn job: mỗi mã một `SaveChangesAsync` riêng trong `EfMarketDataWriter`, hủy giữa chừng để lại universe nửa vời nhưng chạy lại được.

### 2.2 Cầu nối Job 1 → Job 2

Không có message/handoff rõ ràng. Cầu nối duy nhất là trạng thái DB: cột `Stocks.IsActive` (+ `TradingRestricted`) do Job 1/rescreen ghi, Job 2 đọc bằng `GetActiveSymbolsAsync`. Hệ quả: Job 2 không biết Job 1 đã xong hay chưa, chỉ thấy universe tại thời điểm đọc. Đây là lý do tồn tại Job 1 tuần (CN 02:00): mã bị rescreen loại oan không tự quay lại được vì Job 2 mặc định chỉ append cho mã active; Job 2 có cờ `WarmInactiveUniverse` để append cả mã inactive nhằm phá vòng chết này [Đoạn code, `DailySessionSyncRunner`].

### 2.3 Job 2 — `DailySessionSyncRunner.RunAsync` [Đoạn code]

```text
Trigger: Quartz mỗi IntervalMinutes=5 trong phiên + cron 15:00 + lần sau đóng cửa +5'
         (DailySessionSyncJob tự lọc ngày giao dịch/giờ bằng DailySessionSyncDecision)
         | POST /market/jobs/session | DailyAnalysisJob gọi đảm bảo trước khi phân tích
         v
GetActiveSymbolsAsync (loại VNINDEX) [+ mã inactive nếu WarmInactiveUniverse]
  -> batch 40 mã: KbsPriceBoardClient.FetchAsync (POST iss, code=SYM1,SYM2,...)
  -> MarketSyncService.ApplyAsync -> EfMarketDataWriter.UpsertQuotesAsync:
       từng quote: SELECT Stock -> DeserializeHistory(HistoryJson)
                   -> MergeBars (từ điển theo ngày, nến ngày T ghi đè)
                   -> SerializeHistory -> (cuối batch) 1 SaveChanges
       -> QuoteTickCache.SetQuotes + SignalR PublishQuotesAsync
  -> KbsIndexClient.FetchVnIndexAsync -> UpsertIndexAsync (cùng cơ chế blob cho VNINDEX)
  -> DarvasBreakoutAlertPublisher.PublishAsync(GetAllAsync(), market)   [try/catch, lỗi chỉ log]
  -> UniverseRescreenRunner.RunAsync                                    [try/catch, lỗi chỉ log]
```

### 2.4 Sau Job 2 — Phân tích (thường nối tiếp ngay)

`DailyAnalysisJob` (11:30 và ~15:05) luôn gọi `session.RunAsync` trước rồi `analysis.RunAsync`, nên đường Quartz không có khoảng trống dữ liệu. Ngược lại trigger manual `POST /jobs/analysis` chạy phân tích độc lập, có thể chấm trên nến cũ nếu Job 2 chưa chạy — API không kiểm tra độ tươi của phiên [Đoạn code, `MarketJobsController`].

Trong `DailyAnalysisRunner.RunAsync`: nạp toàn bộ `GetAllAsync` (giải mã mọi `HistoryJson`) → `BuildContext` (pha thị trường, sóng ngành, RS percentile) → vòng lặp từng mã: cổng chia chác FireAnt đầu tiên (fail-open) → `BuyDecisionEngine.Evaluate` → các gate volume/GTGD/lệch giá → `SmartMoneyOpportunitySelector` → ML rank + bonus ngành → hygiene → sector cap 2/ngành → Top 5 → `ReplaceForDateAsync` (xóa + chèn lại theo `ForTradingDate`, idempotent) → Early Recovery + `SetupTracks` + `DailyAnalysisRuns.GateStatsJson`.

### 2.5 Đánh giá theo lăng kính async (idempotency / retry / ordering / failure)

| Khía cạnh | Hiện trạng | Đánh giá |
|---|---|---|
| Idempotency Job 2 | Merge theo ngày trong blob; chạy lại cùng phiên ghi đè nến T bằng giá mới nhất | Đạt cho nến. Chưa kiểm chứng được alert Darvas có chống trùng theo phiên trong `DarvasBreakoutAlertPublisher` hay không — cần đọc thêm trước khi kết luận |
| Idempotency phân tích | `ReplaceForDateAsync` xóa–chèn theo ngày; `SetupTracks` có unique index `(Symbol, SourceType, EntryDate)` | Đạt |
| Retry KBS | Không retry, không circuit breaker. Mọi client KBS `catch (Exception) -> return []` | **Không đạt**: lỗi mạng KBS nhìn giống "không có dữ liệu", job báo thành công với 0 mã |
| Ordering / overlap | `[DisallowConcurrentExecution]` chống trùng trong cùng một Quartz job | Một phần: đường manual qua controller gọi thẳng service, không qua Quartz lock — manual và scheduled có thể chạy đồng thời |
| Partial failure | Job 2 đếm `stocksUpdated` theo batch và log; batch lỗi KBS trả rỗng thì `continue` lặng | Chưa đạt: kết quả cuối không phân biệt "xong đủ" với "xong một phần" ở `JobRunStatus` |
| Transaction | Mỗi batch một `SaveChanges`; Darvas + rescreen là bước phụ `try/catch` sau khi nến đã lưu | Chấp nhận được, nhưng alert có thể phát cho nến của batch đã lưu trong khi batch sau thất bại — không có outbox |
| Observability | `JobRunStatuses` 1 dòng/job (trạng thái + thời lượng + summary), log theo batch | Chưa có cảnh báo khi trượt cửa sổ lịch, chưa có correlation id xuyên Job 2 → Analysis → Alert |

---

## 3. Các điểm nghẽn hiệu năng

> Tất cả mục này là [Suy luận] từ cấu trúc code. Chưa có số đo profiler/production trong báo cáo này.

### P0 — Nghiêm trọng

**N1. Blob `HistoryJson` là nút thắt trung tâm.** `Stocks.HistoryJson` chứa toàn bộ nến từ 2000 (hàng nghìn nến/mã, `nvarchar(max)`). Mọi lần ghi một nến mới đều phải: SELECT cả blob → `JsonSerializer.Deserialize` toàn bộ → merge → serialize lại toàn bộ → UPDATE cả blob (`EfMarketDataWriter.UpsertQuotesAsync`, vòng `foreach` từng quote, [Đoạn code]). Chi phí mỗi quote tỉ lệ với độ dài lịch sử, và Job 2/poller 60s lặp việc này cho hàng trăm mã mỗi phút. Đây là nghẽn số một cả về CPU (JSON), I/O (ghi blob lớn) và khóa hàng (row lock giữ lâu trên `Stocks`).

**N2. N+1 trong đường ghi.** `UpsertQuotesAsync` chạy một `FirstOrDefaultAsync` riêng cho từng quote trong batch thay vì nạp một lần theo danh sách symbol. Batch 40 → 40 query + 40 lần giải mã blob trước một `SaveChanges` duy nhất.

**N3. Nhiều poller cùng ghi một chỗ.** Trong phiên có 4 vòng độc lập cùng gọi KBS board và cùng ghi `Stocks`: `KbsMarketSyncRunner` 60s (toàn bộ universe trong **một** POST), `IntradayScannerRunner` 120s, `OpportunityIntradayMonitorRunner` 60s (batch 40, ghi qua `MarketSyncService` từng batch), `DailySessionSyncRunner` 5 phút (batch 40). Hệ quả: ghi trùng lặp cùng một nến ngày T nhiều lần mỗi phút, `CacheInvalidation.InvalidateQuotes` hủy token cache sau mỗi batch khiến `CachedStockRepository` phải nạp lại toàn bộ stock list đã giải mã JSON cho request kế tiếp.

**N4. Job 1 tuần tự hoàn toàn.** Vòng lặp `for` từng mã: 2 lần gọi KBS + `Task.Delay` 300/1500ms, không song song. Với ~1.700 mã niêm yết, chế độ đêm tốn hàng giờ và mọi `UpsertUniverseStockAsync`/`MarkUniverseInactiveAsync` là một `SaveChanges` riêng. Trong thời gian đó universe ở trạng thái nửa cũ nửa mới.

**N5. Phân tích nạp và tính lại toàn bộ từ đầu.** `DailyAnalysisRunner` gọi `GetAllAsync` (giải mã mọi blob vào RAM) rồi với từng mã chạy `BuyDecisionEngine.Evaluate`, bên trong lại tính MA/RSI/ATR/Darvas/flatBox bằng cách quét lịch sử nhiều lần (`IndicatorMath`, `SignalAnalyzer`). Không có tính tăng dần (incremental indicator). Thêm `BuildEarlyRecoveryRadar` duyệt lại toàn bộ universe lần hai, và `AdvanceSectorWaveRegimesAsync` await DB tuần tự theo từng ngành.

### P1 — Đáng kể

**N6. SignalR gửi thừa payload.** `SignalRMarketRealtimePublisher.PublishQuotesAsync` gửi **toàn bộ** danh sách quotes tới hợp nhất các group symbol; client trong một group vẫn nhận quotes của mọi mã trong batch. `PublishIndexAsync`/`PublishRadarAsync`/`PublishTradeEventAsync` dùng `Clients.All`. Khi số kết nối tăng, băng thông tăng theo (số client × kích thước batch).

**N7. Quartz thread pool 6 dùng chung cho mọi job.** `UseDefaultThreadPool(tp => tp.MaxConcurrency = 6)` trong khi có ~11 job (7 V1 + 4 V3), trong đó V3 trong phiên chạy mỗi phút và phân tích có thể chạy lâu. Một lần phân tích dài có thể làm trễ nhịp V3 kế tiếp; Quartz in-memory không có ưu tiên.

**N8. Cache giữ bản đã giải mã nhưng bị vô hiệu liên tục.** `CachedStockRepository` cache `List<Stock>` kèm toàn bộ `History` đã parse (TTL 300s) — tiết kiệm parse khi yên tĩnh, nhưng trong phiên cứ mỗi batch ghi là `InvalidateStocks` hủy token, đúng lúc tải cao nhất thì cache vô dụng nhất.

**N9. V3 nhịp 16:00 nạp toàn universe.** `V3Engine.LayCacMaAsync` → `GetAllForUniverseScreeningAsync` giải mã mọi blob (kể cả inactive) để tính chỉ số + UNI. Đã được owner thiết kế là nhịp đầy đủ duy nhất trong ngày nên chấp nhận được, nhưng nó kế thừa chi phí của N1.

### P2 — Cục bộ

**N10.** `QuoteTickCache` dùng `ConcurrentDictionary` không giới hạn theo symbol — an toàn vì số mã hữu hạn, nhưng `GetQuotes()` sắp xếp lại toàn bộ mỗi lần gọi.

**N11.** `KbsMarketSyncRunner` nạp `GetAllAsync` (đã giải mã mọi lịch sử) chỉ để lấy `Sector` cho từng quote — thông tin có sẵn qua `GetSummariesBySymbolsAsync` dạng thuần cột.

**N12.** Ba nơi định nghĩa lịch/giờ giao dịch riêng (`VietnamMarketCalendar`, `TradingCalendar`, `TradingSessionMath`) — không phải nghẽn hiệu năng nhưng mọi phép "hôm nay có phải ngày giao dịch" đi qua các hàm này trong vòng lặp merge nến (`TradingSessionMath.IsTradingDay` gọi cho từng nến trong `MergeBars`).

---

## 4. Các vấn đề kiến trúc có nguy cơ gây lỗi

### Mức cao

**R1. Lỗi KBS bị nuốt thành "thành công rỗng".** Cả `KbsPriceBoardClient.FetchAsync` và `KbsHistoryClient.FetchRangeAsync` đều `catch (Exception) -> return []` kèm log Warning. Job 2 gặp `quotes.Count == 0` thì `continue`; Job 1 gặp 0 nến thì đánh dấu mã inactive ("Không có dữ liệu screening"). Tức là một đợt KBS sập ngắn có thể (a) khiến Job 1 loại hàng loạt mã khỏi universe, (b) khiến Job 2 báo xong mà không có nến mới, phân tích sau đó chấm trên dữ liệu cũ mà không có tín hiệu phân biệt. Đây là mẫu "failed dependency trông như kết quả hợp lệ" nguy hiểm nhất trong hệ thống.

**R2. Đua ghi (lost update) trên `HistoryJson`.** Chu trình đọc–sửa–ghi blob không có concurrency token/rowversion. Poller 60s, Job 2 và trigger manual dùng các `DbContext` scoped khác nhau; hai vòng ghi chồng nhau cho cùng mã có thể ghi đè nến của nhau. `[DisallowConcurrentExecution]` chỉ chặn trong cùng một Quartz job, không chặn manual-vs-scheduled và không chặn các job khác nhau cùng ghi `Stocks`.

**R3. Scheduler in-memory, trạng thái in-memory.** Quartz `UseInMemoryStore`: restart API giữa ngày là mất lịch sử trigger/misfire; `HistoryBackfillState`, `QuoteTickCache`, `TradeEventStore`, `SessionFlowTracker` đều là singleton trong tiến trình. Hệ thống chỉ chạy đúng với đúng một instance API — không scale-out được, deploy/restart trong phiên làm mất ngữ cảnh intraday mà không có cảnh báo.

**R4. Bí mật và tài khoản mặc định trong repo.** `appsettings.json` chứa `Jwt:Secret = "dev-secret-change-in-production-min-32-chars!!"` và `MarketData:SyncApiKey = "dev-sync-key-change-me"`; `DatabaseInitializer.InitializeAsync` tự tạo admin với mật khẩu cố định `123456aH` mỗi lần khởi động. Nếu production quên override bằng biến môi trường, toàn bộ job trigger và JWT đều lộ. Cần kiểm tra cấu hình production thực tế — báo cáo này không kết luận production đang dùng giá trị nào.

**R5. Mọi user đã đăng nhập đều kích được job nặng.** `MarketJobsController.IsAuthorized` cho phép `User.Identity.IsAuthenticated == true` **hoặc** đúng `X-Sync-Key`. Bất kỳ tài khoản thường nào cũng POST được `/jobs/history` (Job 1 chạy hàng giờ) và `/jobs/daily`. Kết hợp R2, đây là đường gây đua ghi và quá tải KBS dễ nhất.

### Mức trung bình

**R6. Không có ranh giới transaction cho chuỗi ghi → báo.** Job 2 lưu nến (đã commit theo batch) rồi mới quét Darvas; V3 lưu vị thế rồi mới xếp hàng Telegram/FCM (`DatTinV3` → `GuiTatCaAsync` cuối nhịp). Lỗi giữa chừng: nến đã lưu nhưng alert thất bại chỉ nằm trong log; ngược lại không có outbox để gửi lại. Chấp nhận được với mức độ hiện tại nhưng phải ghi nhận là mất tin lặng.

**R7. Hai hệ logic song song V1/V3 dùng chung một bảng `Stocks`.** V3 tự duy trì `ChiSoNgayV3.DanhSachUni` làm tập mã riêng (~200 mã) trong khi Job 2/rescreen duy trì `IsActive`. Hai định nghĩa "universe" khác nhau cùng đọc một lịch sử; đổi luật lọc ở một bên dễ làm bên kia tính chỉ số trên tập mã không như kỳ vọng. Đã có quy ước tải dữ liệu V3 trong `docs/domain/pipeline-jobs.md` giảm rủi ro này, nhưng coupling vẫn nằm ở dữ liệu dùng chung.

**R8. Logic ngày giao dịch nhân ba.** `VietnamMarketCalendar` (Infrastructure), `TradingCalendar` (Application), `TradingSessionMath` (Domain) cùng trả lời câu hỏi lịch. Lệch nhau một chỗ (ví dụ ngày nghỉ lễ) sẽ cho triệu chứng khó truy: Job 2 bỏ qua nhưng Analysis vẫn chạy, hoặc merge nến loại ngày mà nơi khác coi là phiên.

**R9. File lớn, phụ thuộc chéo cao.** `DailyAnalysisRunner` 674 dòng với 18 dependency constructor; `V3Engine` 686 dòng; `BuyDecisionEngine` 706 dòng; `BaseQualityEvaluator` 993 dòng. Thay một cổng Top phải chạm nhiều nơi; test hiện có bao phủ tốt Domain nhưng runner điều phối gần như không có test tích hợp, nên lỗi wiring (thiếu bước, sai thứ tự) chỉ lộ khi chạy thật.

**R10. Tính điều chỉnh giá theo quyền làm lại mỗi lần chấm.** Lịch sử lưu giá thô; mỗi `Evaluate` gọi `LayLichSuChamDiem` để điều chỉnh theo `su-kien-quyen.json` rồi tính chỉ báo trên bản đã chỉnh. Đúng nghiệp vụ, nhưng (a) tốn CPU lặp lại, (b) nếu file sự kiện sai/thiếu, mọi điểm số lệch đồng loạt mà không có kiểm tra chéo — seed sự kiện đang quản lý thủ công trong file JSON đi kèm bản build.

**R11. Migration lệch trạng thái.** Tài liệu ghi migration V3 `20261026130015_AddV3Tables` chưa apply production trong khi dev tự apply lúc khởi động (`DatabaseInitializer.MigrateAsync` chạy mọi lần start); bảng V2 cũ `KetQuaKichBan` còn tồn tại chờ duyệt xóa; có hai thư mục migration (`Infrastructure/Migrations` và `Infrastructure/Persistence/Migrations`). Rủi ro: môi trường dev "chạy được" che giấu việc production thiếu bảng cho tới lần deploy V3.

### Mức thấp / vệ sinh

**R12.** Config chết còn trong `appsettings.json` (`DailyAnalysis.MinScore`, `FallbackMinScore`, `ExcludeAwaitingTriggerFromTop`...) — tài liệu đã ghi các key này không còn tác động Top, nhưng người vận hành vẫn có thể sửa chúng và tưởng có hiệu lực.

**R13.** `DailyAnalysisRunner.FetchIntradayAsync`/`FetchBarsSafeAsync` và `_intradayGate` không còn caller (gate phân kỳ đã bỏ) — code chết giữ semaphore và đường gọi KBS dễ gây nhầm khi sửa sau.

**R14.** CORS cho phép credentials với hàm kiểm origin tự viết (localhost chỉ ở Development/đúng port, domain production liệt kê cứng). Đúng hiện tại, nhưng thêm môi trường mới (staging) rất dễ nới tay quá rộng.

---

## 5. Kế hoạch cải tiến theo ưu tiên

Nguyên tắc: mỗi giai đoạn độc lập, triển khai được và kiểm chứng được trước khi sang giai đoạn sau. Đổi cổng/điểm/pipeline trọng yếu phải qua Spec Kit theo constitution dự án — kế hoạch này chỉ sắp thứ tự, không thay spec.

### Giai đoạn 0 — Đo và bịt lỗ hổng gây lỗi lặng (1–2 tuần, không đổi nghiệp vụ)

| # | Việc | Vì sao | Kiểm chứng |
|---|---|---|---|
| 0.1 | KBS client phân biệt "lỗi" với "rỗng": trả kết quả có trạng thái lỗi hoặc ném exception có kiểm soát; Job 2/Job 1 ghi `JobRunStatus = failed/partial` khi KBS lỗi, và **không** đánh dấu inactive khi lỗi là lỗi transport | Bịt R1 — hết cảnh mất universe/thiếu nến lặng | Test fake KBS 500/timeout: job báo failed, `IsActive` không đổi |
| 0.2 | Thêm kiểm tra độ tươi trước phân tích: `DailyOpportunities` chỉ ghi khi nến phiên T đã có cho ≥ ngưỡng % mã active; manual `/jobs/analysis` trả 409 khi dữ liệu cũ | Bịt khoảng trống [mục 2.4](#24-sau-job-2--phân-tích-thường-nối-tiếp-ngay) | Chạy analysis khi chưa Job 2 → bị chặn kèm lý do |
| 0.3 | Khóa chạy trùng dùng chung cho cả Quartz và manual (một lock theo job-id, ví dụ `SemaphoreSlim` tĩnh hoặc cờ trong `JobRunStatuses`), trả 409 khi đang chạy | Bịt nửa R2 do người vận hành gây ra | POST manual trong lúc scheduled đang chạy → 409 |
| 0.4 | Bỏ đường `IsAuthenticated` khỏi trigger job: chỉ `X-Sync-Key` (và sau này role admin); kiểm tra production đã override `Jwt:Secret`/`SyncApiKey`; đổi mật khẩu admin seed thành bắt buộc qua biến môi trường | Bịt R4, R5 | User thường POST job → 401; khởi động thiếu secret ở Production → fail fast |
| 0.5 | Thêm log có cấu trúc cho mỗi lần chạy job: job-id, trigger (schedule/manual), số mã vào/ra, số lỗi theo mã, thời lượng; cảnh báo khi một cửa sổ lịch bị trượt quá ngưỡng | Nền quan sát cho mọi bước sau (mục 2.5) | Xem được một bản ghi đầy đủ cho Job 2 trong log |

### Giai đoạn 1 — Giải nghẽn dữ liệu (3–6 tuần, cần Spec Kit vì chạm pipeline)

| # | Việc | Vì sao | Kiểm chứng |
|---|---|---|---|
| 1.1 | Tách bảng nến ngày `StockDailyBars (Symbol, Date, OHLCV)` có khóa chính `(Symbol, Date)`; `HistoryJson` chỉ còn là cache đọc hoặc bỏ dần. Ghi nến ngày T = một `INSERT ... ON CONFLICT`-tương đương phía EF (upsert theo khóa), không đọc blob | Giải N1, N2, R2 ở gốc: ghi theo khóa là idempotent tự nhiên và không còn lost update | Benchmark Job 2 trước/sau trên DB bản sao; chạy trùng 2 vòng Job 2 cho kết quả giống hệt |
| 1.2 | Đọc phân tích chuyển sang truy vấn nến theo khoảng ngày cần thiết (ví dụ 250 phiên gần nhất cho chỉ báo) thay vì toàn bộ từ 2000 | Giảm N5: RAM và thời gian giải mã giảm theo cửa sổ tính | So sánh điểm Top trước/sau trên cùng một ngày dữ liệu — phải khớp |
| 1.3 | Hợp nhất các poller ghi: chỉ một vòng 60s ghi nến/quotes; scanner và monitor đọc từ `QuoteTickCache`/snapshot của vòng đó thay vì tự gọi KBS + tự ghi DB | Giải N3, giảm tải KBS và khóa hàng | Đếm số UPDATE lên `Stocks`/bảng nến mỗi phút trước/sau |
| 1.4 | Job 1 song song có giới hạn (ví dụ 4–8 luồng) + ghi theo lô | Giải N4; giữ delay tổng để không bị KBS chặn | Thời gian Job 1 đêm giảm; tỉ lệ lỗi KBS không tăng |
| 1.5 | SignalR gửi theo từng symbol group đúng payload của group đó; cân nhắc nén batch | Giải N6 | Đo payload/phiên kết nối trước/sau |

### Giai đoạn 2 — Cứng cáp vận hành async (sau Giai đoạn 1)

| # | Việc | Vì sao |
|---|---|---|
| 2.1 | Outbox cho alert (Darvas, V3 Telegram/FCM): ghi sự kiện cùng transaction với dữ liệu, một vòng gửi riêng có retry + đánh dấu đã gửi | Giải R6; gửi lại được khi Telegram/FCM lỗi |
| 2.2 | Quartz JDBC store (persist lịch) hoặc ít nhất ghi nhận misfire + job "bù" khi khởi động nếu trượt cửa sổ 15:00/16:00 | Giảm R3 mà chưa cần đổi kiến trúc |
| 2.3 | Hợp nhất lịch giao dịch về một nguồn trong Domain, hai nơi còn lại bọc lại gọi chung | Giải R8 |
| 2.4 | Test tích hợp pipeline: KBS fake → Job 1 → Job 2 → Analysis trên DB test, khẳng định Top và nến; test chạy trùng Job 2 | Lấp khoảng trống test ở R9 trước khi refactor lớn hơn |
| 2.5 | Xóa code chết (`FetchIntradayAsync`), dọn config chết, tách `DailyAnalysisRunner`/`V3Engine` theo bước (context → chấm → hạng → lưu) | Giảm R9, R12, R13 |

### Giai đoạn 3 — Chỉ khi có nhu cầu thật

| # | Việc | Điều kiện |
|---|---|---|
| 3.1 | Tách scheduler/worker khỏi tiến trình API (Quartz vẫn được, chạy host riêng) | Khi deploy/restart API trong phiên bắt đầu gây mất nhịp, hoặc cần scale API đọc |
| 3.2 | Hàng đợi/outbox thành broker thật | Chỉ khi 2.1 cho thấy lượng alert và yêu cầu đảm bảo gửi vượt khả năng vòng gửi DB — theo skill integration-event-design, đây là thay đổi kiến trúc cần spec riêng |
| 3.3 | Tính chỉ báo tăng dần (lưu MA/RSI theo ngày) | Khi cửa sổ đọc ở 1.2 vẫn chưa đủ nhanh cho universe lớn hơn |

### Thứ tự nếu chỉ làm được ba việc

1. **0.1** — lỗi KBS không còn giả dạng thành công.
2. **1.1** — bỏ blob `HistoryJson` khỏi đường ghi.
3. **1.3** — một vòng ghi duy nhất trong phiên.

---

## Phụ lục — Bằng chứng đã đọc

| Khẳng định | Nguồn |
|---|---|
| Job 1 tuần tự + delay + 2 lần fetch/mã | `HistoryBackfillRunner.cs` vòng `for`, `FetchBarsAsync`, `Task.Delay(delay)` |
| Job 2 batch 40, Darvas + rescreen trong try/catch | `DailySessionSyncRunner.cs`, `appsettings.json` `DailySession:BatchSize=40` |
| Ghi blob đọc–sửa–ghi từng quote | `EfMarketDataWriter.UpsertQuotesAsync` |
| `GetAllAsync` lọc active + giải mã JSON từng mã | `EfRepositories.cs` (`EfStockRepository`), `EntityMapper.ToDomain` |
| Quartz in-memory, pool 6, các trigger | `QuartzSchedulingExtensions.cs` |
| KBS nuốt lỗi trả rỗng | `KbsPriceBoardClient.FetchAsync`, `KbsHistoryClient.FetchRangeAsync` |
| Trigger job cho user đã đăng nhập | `MarketJobsController.IsAuthorized` |
| Admin seed mật khẩu cố định; secret dev trong appsettings | `DatabaseInitializer.cs`, `StockRadar.Api/appsettings.json` |
| Lịch V1 + V3 và quy ước tải dữ liệu V3 | `docs/domain/pipeline-jobs.md`, đối chiếu `V3SauPhienRunner.cs`, `V3Engine.cs` |

Chưa kiểm chứng trong lượt này: hành vi chống trùng alert của `DarvasBreakoutAlertPublisher`, cấu hình production thực tế (biến môi trường trên server), số đo hiệu năng thật, và phía client (web/mobile) ngoài mức cấu trúc thư mục.
