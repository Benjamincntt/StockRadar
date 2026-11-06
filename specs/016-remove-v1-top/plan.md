# Implementation Plan: Gỡ V1 (spec 016)

**Spec**: [spec.md](./spec.md) · **Created**: 2026-11-05 · **Status**: Draft, cập nhật 06/11/2026 theo bổ sung của owner (thêm danh sách vị thế V3 lên Home, gỡ hẳn radar, bỏ vùng tích luỹ); owner duyệt 06/11/2026 → [tasks.md](./tasks.md)

## Kiểm lại trước khi lập plan (đọc code 05/11/2026)

| Điểm spec còn ngờ | Kết quả kiểm | Hệ quả |
|---|---|---|
| Nến ATC chỉ được ghi trong `DailyAnalysisJob` | Đúng. `DailySessionSyncJob` bỏ qua khi `IntervalMinutes > 0` và ngoài giờ khớp lệnh (`DailySessionSyncJob.cs`). `DailyAnalysisJob` lần 15:05 gọi `session.RunAsync()` rồi mới phân tích. | D1: thêm trigger Job 2 sau đóng cửa **trước** khi gỡ job V1. |
| Bộ theo dõi lô lớn ghi giá vào DB (`OpportunityIntradayMonitorRunner.cs:87`) | `KbsMarketSyncRunner` lấy cùng tập mã (`GetAllAsync` = active, không hạn chế; monitor dùng `GetActiveSymbolsAsync`, cùng điều kiện), cùng nguồn KBS, mỗi 60 giây, có cả VNINDEX. | Gỡ monitor không mất giá trong phiên. |
| `MasterAlertPositions`, `PositionSellLegs` còn nơi dùng? | Chỉ `EfPerformanceRepositories.GetAlertHistoryAsync`/`GetAlertHistoryTracksAsync`/realized aggregate; **không** service hay controller nào gọi. | Xoá cùng `SetupTracks` (Phụ lục A spec). |
| `Domain/MasterAlerts` mà nhật ký dùng | `NhatKyService` chỉ dùng `OutcomeBucketNames` và `RealizedPnlMath` (+ `RealizedPnlOptions`). `MasterAlertKinds`, `RealizedStatusNames` chỉ V1 dùng. | Giữ `OutcomeBucketNames`, `RealizedPnlMath`; xoá hai file kia. |
| Huy hiệu V3 cho Watchlist | Hiện chỉ có `GET stocks/{symbol}/v3` (từng mã). | D5: thêm trạng thái V3 vào `WatchlistItemDto`, đọc một lượt theo danh sách mã. |
| Gỡ hẳn radar có ảnh hưởng V3? (rà 06/11) | Không file V3 nào dùng `SignalAnalyzer`, `SessionRadarHits`, `IntradayScannerRunner`, `RadarService`, `RadarUpdated`. Tác dụng phụ duy nhất của radar: `quoteCache.SetQuotes` + `PublishQuotesAsync` (`IntradayScannerRunner.cs:118-122`); `MarketSyncService.ApplyAsync` (job `kbs-market-sync`, 60 giây, toàn bộ mã) làm y việc này. V3 trong phiên đọc `KbsPriceBoardClient`, không qua bộ nhớ đệm. | D8: gỡ hẳn radar. |
| Nguồn danh sách đang giữ cho Home (rà 06/11) | Đã có `GET v3/hieu-qua/vi-the-dang-giu` (chờ mua + đang giữ), chưa client nào gọi. `ViTheV3Dto` có `SoPhienDaGi`, `LaHoangLoanSau`, chưa có `NguongChamMa` (entity có, `V3Entities.cs:97`) và giữ tối đa. V3 **không có luật cắt lỗ** (`V3LuatBanThucTe.cs`). | D9: thêm giá chốt lời và số phiên còn lại vào DTO; chưa có cột cắt lỗ. |
| Màn bị ảnh hưởng (rà 06/11) | Mobile: Home, chi tiết mã, Watchlist, thẻ VNINDEX. Web: Home, chi tiết mã (cả vùng tích luỹ trên biểu đồ), Watchlist. Không ảnh hưởng: Hiệu quả, Nhật ký, Sự kiện quyền, Login, điều hướng, `GET /market`. App mobile cũ: Home dùng một `Future.wait` nên 404 ở `opportunities` làm hỏng cả Home. | D4 cập nhật; đợt 3 chỉ chạy khi mọi máy đã cài app mới. |

## Thiết kế

### D1 — Job 2 sau đóng cửa tách khỏi V1

- `QuartzSchedulingExtensions.ConfigureDailySessionJob`: khi `IntervalMinutes > 0`, thêm trigger cron thứ hai `daily-session-sync-close` lúc `DailySession.Hour:Minute + 5` (15:05) ngày thường, gắn `JobDataMap["sauDongCua"] = true`.
- `DailySessionSyncJob.Execute`: nếu trigger mang cờ `sauDongCua` thì bỏ qua kiểm `IsMarketOpen()` (vẫn kiểm ngày giao dịch). Cờ cấu hình `ForceRunOutsideHours` giữ nguyên ý nghĩa.
- Cần một dòng `// DO-NOT-CHANGE:` tại chỗ bỏ qua giờ: thiếu lần chạy này thì `HistoryJson` không có nến ATC và nhịp V3 16:00 tính trên giá 14:45.
- Lần chạy 11:30 của `DailyAnalysisJob` không cần thay: trigger 5 phút của Job 2 đã phủ tới 11:30.
- Test: job có cờ chạy ngoài giờ; job không cờ vẫn bỏ qua ngoài giờ (tách hàm quyết định ra để test được, không cần Quartz thật).

### D2 — Gỡ backend V1

Theo nhóm, mỗi nhóm build được trước khi sang nhóm sau:

1. **Job và hosted service**: `DailyAnalysisJob`, `IntradayScannerJob`, `OpportunityIntradayMonitorJob` + cấu hình Quartz tương ứng và id trong `QuartzJobIds`/`JobCatalog` (kể cả mục chết `weekly-opportunity-review`); `SmartMoneyContextWarmer`, `OpportunityRankerBootstrap`.
2. **Runner/Infrastructure**: `DailyAnalysisRunner`, `SmartMoneyBacktestRunner`, `HyperparameterTuningRunner`, `SectorWaveRegimeBackfillService`, `IntradayScannerRunner`, stack monitor (`OpportunityIntradayMonitorRunner`, `TradeEvent*`, `SessionFlow*`, `OrderBookMetrics`, `IntradayMonitorStatus*`, `OrderFlowSnapshotTracker`), `DarvasBreakoutAlertPublisher` (gỡ khối gọi trong `DailySessionSyncRunner` dòng ~104–120, giữ phần append nến/VNINDEX/rescreen), `Ml/OpportunityRankerInfrastructure`.
3. **API**: `OpportunitiesController`, `EarlyRecoveryController`, `SignalsController`, `BacktestController`, `MlController`, `RadarItemsController`; trong `MarketJobsController` bỏ `analysis`, `daily`, `sector-wave-backfill`, `intraday-scan`, `opportunity-monitor`; trong `MarketController` bỏ `intraday-monitor`, `trades`, `flow/*`; `GET sectors` (SmartMoney sector wave). SignalR: bỏ `RadarUpdated`, `TradeEventCreated`, `AlertCreated` khỏi publisher, interface và hằng số trong `MarketHub`; giữ `QuotesUpdated`, `IndexUpdated`.
4. **Application**: các service V1 (EngineTrust, EntryTiming, HitCalibration*, AdaptiveScoringProfileFactory, ListRecommendation, NorthStar, OpportunityRanker*, RankingDataset, RadarService (+ `IIntradayScannerService`, `ISessionRadarRepository`, `IntradayScannerOptions`), SetupTrackBackfill, ShadowAnalysis, SmartMoneyEvaluation, TuneEvaluate); phần V1 của `MarketService` (`GetOpportunitiesAsync`, `RunOpportunityAnalysisAsync`, `GetSignalsAsync`, `GetSectorsAsync`, pha trong VNINDEX chart); abstractions, DTO, mapper, options đi kèm.
5. **Domain**: `BuyDecisionEngine`, `SmartMoneyOpportunitySelector`, `SmartMoneyCriterionScorer`, `TrendSetupEvaluator`, `PlaybookClassifier`, `Criterion*`, `DarvasBreakoutAnalyzer`, `HitProbability*`/`HitCalibration*`, `OpportunityRanking/*`, `EntryPoint*`, `TradeState*`, `GateFailureClassifier`, `EntryTimingAnalyzer`, `RsPercentileCalculator`, `SectorWaveRegimeEngine`, `MarketPhaseClassifier`, `VnIndexPriorPeakAnalyzer`, `SignalAnalyzer`, `BaseQualityEvaluator`, value objects đi kèm, `MasterAlertKinds`, `RealizedStatusNames`.
   - **Giữ**: `BoDieuChinhGiaTheoQuyen` và nguồn sự kiện quyền (spec 015), `StockUniverseFilter`, `IndicatorMath`, `TradingSessionMath`, `RealizedPnlMath`, `OutcomeBucketNames`, `ExchangePriceBand`.
   - `INguonLichChotQuyen`/`FireAntLichChotQuyenClient`: gỡ nơi gọi của V1 (cổng sắp chốt quyền trong `DailyAnalysisRunner`, `SmartMoneyMarketContext`), giữ client và đăng ký DI cho spec 015.
6. **Persistence**: repository V1 (`EfCriterionScoringRepository`, phần V1 của `EfPerformanceRepositories`, `EfDailyOpportunityRepository`, `EfSessionRadarRepository`, `EfHitCalibrationRepository`, `EfEarlyRecoveryRadarRepository`, `EfDailyAnalysisRunRepository`, `EfSectorWaveRegimeRepository`, `EfAlertRepository`, `EfTradeJournalRepository`), entity và cấu hình trong `ApplicationDbContext`.

### D3 — Chi tiết mã và VNINDEX (Q3, Q6)

- `StockService.GetDetailAsync`: bỏ `BuyDecisionEngine`, SmartMoney context, đọc `DailyOpportunities`; `StockDetailDto` bỏ các trường V1 (Buy Score/quyết định/entry/criteria, `volumeRatio`, `relativeStrength`, `flatBox`). Giữ thông tin mã, giá, `history` (biểu đồ ngày cả mobile lẫn web dùng), `isUniverseActive`/`universeStatusReason`/`universeUpdatedAt` (dòng "mã ngoài universe" trên mobile), sự kiện quyền. Phần V3 vẫn qua `GET stocks/{s}/v3`.
- `GET market/vnindex/chart`: bỏ trường pha và đỉnh trước; giữ chuỗi giá.
- Đây là đổi hợp đồng API → làm theo skill `api-contract-review`; app cũ nhận DTO thiếu trường phải không vỡ (mobile/web parse null-an-toàn — kiểm khi làm D4).

### D4 — Client (Q2–Q4, Q6)

- **Mobile**: `home_screen.dart` bỏ khối Top; Home còn ô tìm mã, `VnIndexMarketCard` không nhãn pha, danh sách đang giữ (D9). Bỏ thẻ "Tín hiệu mới nhất" (`_radarTile`, `_SessionRadarStatusLine`, `_signalLabelVi`, model `RadarLive*`, `getRadarLive`). Tách `Future.wait` để một nguồn lỗi không làm hỏng cả Home. Sparkline chỉ lấy cho mã đang giữ. Xoá listener `TradeEventCreated`/`recentTrades` trong `market_hub_service.dart`; tiêu đề biểu đồ chi tiết mã bỏ nhánh "vùng tích lũy"; xoá `trade_state_badge.dart`, `trade_state_labels.dart`, lớp V1 trong `models.dart`, hàm V1 trong `api_client.dart`, widget mồ côi, route chết; `stock_detail_screen.dart` bỏ khối tỉ lệ khối lượng/RS/flatBox; `watchlist_screen.dart` thay `ScorePill(item.score)` bằng huy hiệu V3 (dùng lại widget huy hiệu của chi tiết mã); listener `TradeEventCreated`. Tăng phiên bản app.
- **Web**: `HomePage.tsx` bỏ "Cơ hội tốt nhất" và nút chạy phân tích; thêm danh sách đang giữ (D9); xoá `SessionRadarList.tsx` và type/hàm radar. Xoá `TradeStateBadge.tsx`, `tradeState.ts`, type/hàm API V1, file mồ côi (Phụ lục C spec). `StockDetailPage.tsx`: bỏ khối KL/RS/flatBox **và** vùng tích luỹ trên biểu đồ (`lib/chartAccumulation.ts`, `AccumulationLegend.tsx`, prop `accumulationPeriods` và đường hộp của `PriceVolumeChart`). `WatchlistPage.tsx` như mobile; giữ component `ScorePill` (`LiveChangePill` dùng).

### D5 — Watchlist huy hiệu V3 (Q4)

- `WatchlistItemDto`: bỏ `Score`, thêm trạng thái V3 rút gọn của mã (vd. `DangGiu` / `ChoMua` / `CoTinHieuHomNay` / null).
- `WatchlistService.EnrichItemsAsync`: bỏ `dailyOpportunities`, `smartMoneyEval`, `buyDecision`; đọc `ViTheV3` (ChoMua/DangGiu) và `TinHieuV3` phiên gần nhất theo danh sách mã trong **một** truy vấn mỗi bảng (thêm hàm vào `V3HieuQuaService` hoặc repository V3, không N+1).
- Test: mã có vị thế đang giữ, mã chờ mua, mã có tín hiệu bị cắt danh mục, mã không có gì.

### D8 — Gỡ hẳn radar (FR-007, FR-019)

- Gỡ trong đợt 3 cùng D2 (đã liệt kê ở nhóm 1–4, 6): job, runner, service, controller, repository, options, SignalR `RadarUpdated`, entity `SessionRadarHits` (bảng xoá ở đợt 4).
- Kiểm sau deploy: trong phiên, `GET market/quotes` vẫn trả giá mới, app nhận `QuotesUpdated` mỗi ~60 giây (từ `kbs-market-sync`), giá trên danh sách đang giữ nhảy.

### D9 — Danh sách vị thế V3 đang giữ trên Home (FR-018)

- Server (chỉ thêm, đợt 2): `ViTheV3Dto` thêm
  - `GiaChotLoi`: lệnh thường = max(`NguongChamMa` đã lưu, giá vào × `BanPhanTramGiaVao`); hoảng loạn sâu = giá vào × `HoangLoanSauChotLoi`; N3 = null. Dùng hàm của `V3LuatBanThucTe` (tách hàm tính ngưỡng dùng chung nếu cần), không chép công thức.
  - `GiuToiDa` = `V3LuatBanThucTe.GiuToiDaTheoViThe`, `SoPhienConLai` = max(0, `GiuToiDa` − `SoPhienDaGi`).
- Client: gọi `v3/hieu-qua/vi-the-dang-giu`, lọc `DangGiu`, hiện mã, kịch bản, giá mua, giá hiện tại (quote trực tiếp qua hub), % lãi/lỗ so với giá mua (tính ở client từ quote trực tiếp), giá chốt lời (N3: "Bán ATO theo lịch"), số phiên còn lại; bấm mở chi tiết mã; rỗng thì "Chưa giữ mã nào". Đăng ký quote cho các mã này.
- Chưa có cột cắt lỗ (V3 không có luật cắt lỗ; chờ kết quả nghiên cứu riêng).
- Tiêu đề thẻ "Đang giữ" (mobile và web). Server thêm `TinHieuV3Id`, `KhoiLuong` vào `ViTheV3Dto`. Mobile: icon bút lông mỗi dòng → đăng nhập? → tìm lệnh Bán V3 của vị thế trong Nhật ký (size 200): có thì mở sửa lệnh gần nhất, không thì mở form ghi lệnh Bán điền sẵn (`NhatKyLenhPrefill`). Lưu xong làm mới danh sách và báo mã chỉ rời sau nhịp 16:00 (spec 014). Web không có màn Nhật ký nên không có bút lông.
- Không thêm logic chọn mã. Danh sách hiện số hệ thống cho tới khi đặt `V3:TaiKhoanDanhMuc` trên server (spec 014).
- Test: lệnh thường có / không có ngưỡng, hoảng loạn sâu, N3; `SoPhienConLai` không âm; giá chốt lời khớp `V3LuatBanThucTe.Xet` với cùng bộ số.

### D6 — Dữ liệu (FR-009)

- Script xuất `specs/016-remove-v1-top/xuat-bang-v1.sh`: chạy trên server, `sqlcmd` xuất từng bảng Phụ lục A ra CSV nén trong `/tmp`, in `COUNT(*)`; owner tải về rồi xoá file trên server (theo tiền lệ `dotb-export-and-drop-guide.md` của spec 011).
- Migration `RemoveV1Tables`: chỉ `DropTable` các bảng Phụ lục A (cùng `SessionRadarHits`). Sinh bằng `dotnet ef migrations add` rồi **đọc lại toàn bộ body** trước khi apply (repo này sinh body sai); `Down` tạo lại bảng rỗng là đủ.
- Hai thư mục migration (`Infrastructure/Migrations/`, `Infrastructure/Persistence/Migrations/`): kiểm thư mục nào là snapshot đang dùng trước khi sinh.

### D0 — Tài liệu lưu trữ V1 (FR-017, owner bổ sung 05/11/2026)

- Viết `docs/features/v1-archive/` **khi code V1 còn trên disk**, đọc trực tiếp từ code và cấu hình, không viết theo trí nhớ:
  - `README.md`: V1 là gì, vì sao gỡ (PH-30, PH-31), commit cuối còn code, cách dựng lại.
  - `01-cong-loc.md`: thứ tự cổng, điều kiện, ngưỡng, nhãn.
  - `02-cham-diem.md`: Buy Score, PredictedHit, trạng thái giao dịch, pha thị trường, sóng ngành, RS, flatBox/Darvas.
  - `03-xep-hang-ml.md`: đặc trưng, model 25/09/2026 (trọng số), huấn luyện, cộng điểm ngành, hygiene, giới hạn ngành.
  - `04-radar-lo-lon.md`: ngưỡng radar trong phiên và theo dõi lô lớn.
  - `05-do-luong.md`: harness, cách chạy, kết quả.
- Cổng sắp chốt quyền ghi kèm trỏ sang spec 015 US6.
- Làm trước đợt 3 (xoá code); có thể làm ngay vì chỉ là tài liệu.

### D7 — Cấu hình, tài liệu, script

- Xoá mục cấu hình Phụ lục B khỏi `appsettings.json`, `appsettings.Development.json`, `appsettings.Production.json.example`; soạn đoạn hướng dẫn sửa `appsettings.Production.json` **trên server** (owner tự sửa hoặc duyệt lệnh).
- Docs theo FR-011; `docs/domain/buy-decision.md`, `ma-stack-and-market-phase.md`, `base-price-flatbox.md` thành tombstone trỏ sang `docs/features/v1-archive/` (D0); `pipeline-jobs.md` viết lại chỉ còn Job 1/Job 2 (+ trigger sau đóng cửa)/KBS sync/V3.
- `CLAUDE.md`: đoạn Pipeline, bảng Luật sản phẩm, entry thường dùng; `.claude/skills/*` nhắc file V1.
- Xoá script V1 trong `scripts/` (danh sách ở báo cáo kiểm kê, xác nhận từng file không dùng cho V3/ship).
- Harness `StockRadar.Tests/Research/V1TopReplayTests.cs` và test V1 khác: xoá cùng code; `DieuChinhGia/*` chuyển sang test thẳng `BoDieuChinhGiaTheoQuyen`.

## Thứ tự đợt deploy (Q5)

| Đợt | Nội dung | Ai làm | Điều kiện sang đợt sau |
|---|---|---|---|
| 1 | D1 (trigger Job 2 sau đóng cửa), deploy server | AI code, owner ship | 2 phiên liên tiếp: log `daily-session-sync-close` chạy 15:05; nến ATC có trong `HistoryJson` (so 5 mã với bảng giá); V3 16:00 bình thường |
| 2 | App mobile + web không còn gọi API V1 (D4, D5 phía client, Home có danh sách đang giữ D9, không còn thẻ radar) và server chỉ thêm (trường V3 cho Watchlist D5, giá chốt lời + số phiên còn lại cho `ViTheV3Dto` D9; chưa bỏ `Score`) | AI code, owner build/cài app + ship | **Mọi máy** đang dùng đã cài app mới (app cũ hỏng cả Home khi `opportunities` 404) |
| 2b | Tài liệu lưu trữ V1 (D0), ghi commit cuối còn code V1 | AI | Owner đọc qua tài liệu |
| 3 | Gỡ backend V1 (D2, D3, D8), cấu hình (D7 phần repo), xoá `Score` khỏi Watchlist DTO; sửa `appsettings.Production.json` trên server | AI code, owner ship + sửa server | Build, test sạch; 2 phiên V3 bình thường; không còn log job V1 |
| 4 | Xuất dữ liệu + migration xoá bảng (D6) | AI soạn script/migration, owner chạy xuất + ship | `COUNT(*)` khớp file xuất |
| 5 | Docs, CLAUDE.md, skills, scripts (D7) | AI | — |

Mỗi đợt một commit (hoặc vài commit), chỉ commit khi owner nói "commit đi".

## Kiểm chứng

- Mỗi đợt: `dotnet build`, `dotnet test`, `flutter analyze`, `npm run build` (đợt chạm phần đó).
- Đợt 1: test đơn vị cho quyết định chạy/bỏ của Job 2; trên production đọc log và `HistoryJson` (chỉ đọc).
- Đợt 3: grep tên class/bảng/endpoint/option Phụ lục A–C không còn; so tín hiệu V3 một phiên trước/sau (SC-003, US2 #3) bằng cách chạy nhịp V3 thủ công trên dev với cùng dữ liệu.
- Đợt 4: so `COUNT(*)` với file xuất (SC-005).

## Rủi ro

| Rủi ro | Giảm thiểu |
|---|---|
| Thiếu nến ATC sau khi gỡ job V1 | Đợt 1 tách riêng và chờ 2 phiên kiểm trước khi gỡ |
| Gỡ nhầm thứ V3 dùng | Phụ lục D; sau mỗi nhóm D2 build + chạy test V3 (`StockRadar.Tests/V3`, `KichBan`, `NhatKy`) |
| Spec 015 cần `SignalAnalyzer` phần điều chỉnh quyền | Spec 015 dùng thẳng `BoDieuChinhGiaTheoQuyen`; chuyển test `DieuChinhGia/*` trước khi xoá `SignalAnalyzer` |
| App cũ vỡ khi DTO chi tiết mã mất trường | Đợt 2 đưa app mới trước; kiểm parse null-an-toàn |
| `appsettings.Production.json` trên server còn mục V1 | Không làm hỏng chạy (mục thừa bị bỏ qua) nhưng phải dọn trong đợt 3 |
| Migration sinh sai | Đọc lại body trước apply; chạy thử trên DB dev |

## Ngoài phạm vi

- Thêm khối V3 nào khác lên Home ngoài danh sách đang giữ (D9).
- Luật cắt lỗ cho V3 và cột "giá cắt lỗ": owner yêu cầu backtest các phương án cắt lỗ (kể cả cách mới) — việc nghiên cứu riêng; nếu chốt được luật thì làm spec riêng (đổi luật bán = Spec Kit).
- Sửa dữ liệu VNINDEX O = H = L = C trong `MarketIndices` (không còn ai dùng pha sau khi gỡ).
- Spec 015 (V3 giá điều chỉnh theo quyền) — chạy riêng; chỉ phối hợp ở phần giữ `BoDieuChinhGiaTheoQuyen`.
