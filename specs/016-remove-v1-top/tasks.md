# Tasks: Gỡ V1 (spec 016)

**Input**: [spec.md](./spec.md), [plan.md](./plan.md) — owner duyệt plan 06/11/2026 ("Duyệt, lập tasks").

`[P]` = làm song song được (khác file, không phụ thuộc nhau). Mỗi đợt kết thúc bằng một điểm dừng: AI báo kết quả, owner ship / cài app / kiểm, rồi mới sang đợt sau. Chỉ commit khi owner nói "commit đi".

## Đợt 0 — Chốt lại trước khi xoá (không đổi hành vi)

- [x] T001 (commit `40b3335`, `d6ebbff`, `cfa3960` — 06/11/2026) Owner duyệt bản nháp commit cho phần đang nằm trên disk: harness `backend/StockRadar.Tests/Research/V1TopReplayTests.cs`, `docs/features/v1-archive/`, sổ nghiên cứu (PH-26…PH-32, Q7, scripts), spec 015/016/017. Harness **phải** vào git trước đợt 3 để sau này chạy lại được ở commit cũ (D0).

## Đợt 1 — D1: Job 2 sau đóng cửa tách khỏi V1

- [x] T002 Tách hàm quyết định chạy/bỏ của `DailySessionSyncJob` (`backend/StockRadar.Infrastructure/Scheduling/Jobs/DailySessionSyncJob.cs`) thành hàm thuần nhận (ngày giao dịch?, đang trong giờ?, cờ `sauDongCua`, `ForceRunOutsideHours`, `IntervalMinutes`).
- [x] T003 Thêm cờ `sauDongCua` trong `JobDataMap`: có cờ thì bỏ qua kiểm giờ khớp lệnh, vẫn kiểm ngày giao dịch. Một dòng `// DO-NOT-CHANGE:` tại chỗ bỏ qua giờ (thiếu lần chạy này thì `HistoryJson` không có nến ATC, nhịp V3 16:00 tính trên giá 14:45).
- [x] T004 `QuartzSchedulingExtensions.ConfigureDailySessionJob`: khi `IntervalMinutes > 0`, thêm trigger cron `daily-session-sync-close` lúc `DailySession.Hour:Minute + 5` (15:05) thứ 2–6, mang cờ `sauDongCua`.
- [x] T005 [P] Test `backend/StockRadar.Tests/` cho hàm ở T002: có cờ chạy ngoài giờ; không cờ bỏ qua ngoài giờ; ngày nghỉ bỏ qua dù có cờ; `ForceRunOutsideHours` giữ nghĩa cũ.
- [x] T006 `dotnet build` + `dotnet test`; chạy API local, xem log Quartz liệt kê trigger mới. Báo số test.
- [ ] T007 **Điểm dừng**: owner ship. AI đọc (chỉ đọc) log production 2 phiên liên tiếp: trigger 15:05 chạy; `HistoryJson` của 5 mã có nến phiên đó khớp giá đóng cửa ATC trên bảng giá; nhịp V3 16:00 log bình thường.

## Đợt 2 — Client không còn gọi API V1, server chỉ thêm

### Server (chỉ thêm, không bỏ trường nào)

- [x] T008 D9: `ViTheV3Dto` thêm `GiaChotLoi`, `GiuToiDa`, `SoPhienConLai` (cuối record, nullable). Tính bằng hàm của `V3LuatBanThucTe` (tách hàm tính ngưỡng dùng chung nếu cần, không chép công thức); `V3HieuQuaService.ToDto` truyền vào.
- [x] T009 [P] Test D9: lệnh thường có / không có `NguongChamMa`, hoảng loạn sâu, N3 (`GiaChotLoi` null); `SoPhienConLai` không âm; `GiaChotLoi` khớp ngưỡng `V3LuatBanThucTe.Xet` cùng bộ số.
- [x] T010 D5: `WatchlistItemDto` thêm trạng thái V3 rút gọn (`DangGiu` / `ChoMua` / `CoTinHieuHomNay` / null), **giữ** `Score`. `WatchlistService.EnrichItemsAsync` đọc `ViTheV3` + `TinHieuV3` theo danh sách mã, một truy vấn mỗi bảng (không N+1).
- [x] T011 [P] Test D5: mã đang giữ, chờ mua, có tín hiệu bị cắt danh mục, không có gì.

### Mobile (`mobile/lib/`)

- [x] T012 `home_screen.dart`: bỏ khối Top, nút chạy phân tích, thẻ "Tín hiệu mới nhất" (`_radarTile`, `_SessionRadarStatusLine`, `_signalLabelVi`); tách `Future.wait` thành các lời gọi độc lập (một nguồn lỗi không làm hỏng cả Home); sparkline chỉ cho mã đang giữ.
- [x] T013 Home: danh sách vị thế V3 đang giữ (D9) — gọi `v3/hieu-qua/vi-the-dang-giu`, lọc `DangGiu`; mã, kịch bản, giá mua, giá hiện tại (quote hub), % lãi/lỗ so giá mua, giá chốt lời (N3: "Bán ATO theo lịch"), số phiên còn lại; bấm mở chi tiết mã; rỗng → "Chưa giữ mã nào". Đăng ký quote cho các mã này.
- [x] T014 `vnindex_market_card.dart`: bỏ nhãn pha và đỉnh gần nhất (parse vẫn chịu được khi server còn trả).
- [x] T015 `stock_detail_screen.dart`: bỏ khối tỉ lệ KL / RS / flatBox (`_FlatBoxCard`), dòng "vùng tích lũy" ở tiêu đề biểu đồ; giữ biểu đồ (`history`), dòng "mã ngoài universe", phần V3, sự kiện quyền.
- [x] T016 `watchlist_screen.dart`: thay `ScorePill(item.score)` bằng huy hiệu V3 từ trường mới (T010), dùng lại widget huy hiệu của chi tiết mã; trường thiếu → không hiện gì.
- [x] T017 Dọn: `market_hub_service.dart` bỏ listener `TradeEventCreated` + `recentTrades`; xoá `trade_state_badge.dart`, `core/labels/trade_state_labels.dart`, widget mồ côi (`smart_money_backtest_card.dart`, `price_levels_card.dart`, `stock_detail_widgets.dart`, `core/labels/job_state_labels.dart`, `base_price_labels.dart` nếu hết nơi dùng); lớp và hàm V1 trong `models.dart` / `api_client.dart` theo Phụ lục C spec. Giữ `score_pill.dart`, `getSparklines`.
- [x] T018 Tăng phiên bản app; `flutter analyze` sạch. Grep `mobile/lib` không còn `opportunities`, `radar-items`, `market/trades`, `intraday-monitor`, `backtest/smartmoney`, `criteria/summary`, `performance/`.

### Web (`frontend/src/`)

- [x] T019 [P] `pages/HomePage.tsx`: bỏ "Cơ hội tốt nhất", nút chạy phân tích, `SessionRadarList`; thêm danh sách đang giữ như T013.
- [x] T020 [P] `pages/StockDetailPage.tsx`: bỏ khối KL / RS / flatBox và vùng tích luỹ trên biểu đồ (`lib/chartAccumulation.ts`, `components/chart/AccumulationLegend.tsx`, prop `accumulationPeriods` và đường hộp của `PriceVolumeChart`).
- [x] T021 [P] `pages/WatchlistPage.tsx`: huy hiệu V3 thay `ScorePill score`; giữ component `ScorePill` (`LiveChangePill` dùng).
- [x] T022 Dọn web theo Phụ lục C: `TradeStateBadge.tsx`, `lib/tradeState.ts`, `SessionRadarList.tsx`, `SmartMoneyBacktestPanel.tsx`, `TradePrintList.tsx`, `IntradayMonitorStatusLine.tsx`, `hooks/useLiveTrades.ts`, `hooks/useIntradayMonitorStatus.ts`, `lib/tradeLabels.ts`, `HeatmapGrid.tsx`, `Charts.tsx` (kiểm từng file hết nơi import trước khi xoá); type/hàm V1 trong `types/index.ts`, `lib/api.ts`. `npm run build` qua.

### Kiểm đợt 2

- [x] T023 `dotnet build` + `dotnet test`, `flutter analyze`, `npm run build`. Chạy API + web local: Home, chi tiết mã, Watchlist không lỗi, network không còn request API V1 (skill `browser-qa-execution`).
- [x] T024 (owner xác nhận 06/11/2026: server + web đã ship, app 3.0.3 đã cài, không còn máy dùng app cũ) **Điểm dừng**: owner ship server + web, build và cài app mobile lên **mọi máy** đang dùng. Chỉ sang đợt 3 khi owner xác nhận.

## Đợt 3 — Gỡ backend V1 (D2, D3, D8, D7 phần repo)

Mỗi nhóm build + chạy test V3 (`StockRadar.Tests/V3`, `KichBan`, `NhatKy`, `DieuChinhGia`) trước khi sang nhóm sau.

- [ ] T025 Ghi mã commit ngay trước đợt này vào `docs/features/v1-archive/README.md` ("commit cuối còn code V1").
- [ ] T026 Chuyển test `StockRadar.Tests/DieuChinhGia/*` sang test thẳng `BoDieuChinhGiaTheoQuyen` (bỏ phụ thuộc `SignalAnalyzer`).
- [ ] T027 Nhóm 1 — Job/hosted service: `DailyAnalysisJob`, `IntradayScannerJob`, `OpportunityIntradayMonitorJob`, cấu hình Quartz và id trong `QuartzJobIds`/`JobCatalog` (cả `weekly-opportunity-review`), `SmartMoneyContextWarmer`, `OpportunityRankerBootstrap`.
- [ ] T028 Nhóm 2 — Runner/Infrastructure theo plan D2 mục 2; trong `DailySessionSyncRunner` gỡ khối Darvas, giữ append nến / VNINDEX / rescreen.
- [ ] T029 Nhóm 3 — API theo plan D2 mục 3 (controller, action trong `MarketJobsController`/`MarketController`, `GET sectors`, SignalR `RadarUpdated`/`TradeEventCreated`/`AlertCreated`).
- [ ] T030 Nhóm 4 — Application theo plan D2 mục 4 (cả phần V1 của `MarketService`, pha trong VNINDEX chart).
- [ ] T031 D3 — `StockService.GetDetailAsync` + `StockDetailDto`: bỏ trường V1, giữ `history`, `isUniverseActive`/`universeStatusReason`/`universeUpdatedAt`; `GET market/vnindex/chart` bỏ pha và đỉnh. Làm theo skill `api-contract-review`.
- [ ] T032 D5 — xoá `Score` khỏi `WatchlistItemDto`; `WatchlistService` bỏ `dailyOpportunities`, `smartMoneyEval`, `buyDecision`.
- [ ] T033 Nhóm 5 — Domain theo plan D2 mục 5. Giữ: `BoDieuChinhGiaTheoQuyen`, nguồn sự kiện quyền, `INguonLichChotQuyen`/`FireAntLichChotQuyenClient`/`ThongTinChotQuyen`/`FireAntOptions` (đăng ký DI giữ cho spec 015), `StockUniverseFilter`, `IndicatorMath`, `TradingSessionMath`, `RealizedPnlMath`, `OutcomeBucketNames`, `ExchangePriceBand`.
- [ ] T034 Nhóm 6 — Persistence: repository, entity, cấu hình trong `ApplicationDbContext` theo plan D2 mục 6 (bảng chưa xoá, chỉ bỏ ánh xạ — migration ở đợt 4).
- [ ] T035 Xoá test V1 và harness `V1TopReplayTests.cs` cùng code (đã commit ở T001).
- [ ] T036 Cấu hình repo: xoá mục Phụ lục B khỏi `appsettings.json`, `appsettings.Development.json`, `appsettings.Production.json.example`. Soạn đoạn hướng dẫn sửa `appsettings.Production.json` **trên server** cho owner.
- [ ] T037 Kiểm: `dotnet build` + `dotnet test` (báo số test trước/sau, số giảm = test V1 đã gỡ); grep backend không còn tên ở Phụ lục A–C (trừ migration lịch sử); chạy nhịp V3 thủ công trên dev với cùng dữ liệu trước/sau → tín hiệu, vị thế giống hệt.
- [ ] T038 **Điểm dừng**: owner ship + sửa cấu hình server. AI đọc log 2 phiên: không còn log job V1; V3 08:30 / mỗi phút / 14:25 / 16:00 bình thường; `QuotesUpdated` vẫn đến app ~60 giây/lần; giá danh sách đang giữ nhảy.

## Đợt 4 — Dữ liệu (D6)

- [ ] T039 Script `specs/016-remove-v1-top/xuat-bang-v1.sh`: chạy trên server, xuất từng bảng Phụ lục A (cả `SessionRadarHits`) ra CSV nén trong `/tmp`, in `COUNT(*)`. Không in mật khẩu.
- [ ] T040 Kiểm thư mục migration đang dùng (`Infrastructure/Migrations/` hay `Infrastructure/Persistence/Migrations/`); sinh migration `RemoveV1Tables`, **đọc lại toàn bộ body**, chỉ giữ `DropTable` các bảng Phụ lục A; `Down` tạo lại bảng rỗng. Apply trên DB dev.
- [ ] T041 **Điểm dừng**: owner chạy script xuất, tải file về, xoá file trên server, rồi ship migration. AI kiểm (chỉ đọc) bảng đã mất, `COUNT(*)` khớp file xuất, bảng V3 và bảng dùng chung còn nguyên.

## Đợt 5 — Tài liệu, script (D7, FR-011)

- [ ] T042 [P] `docs/domain/buy-decision.md`, `ma-stack-and-market-phase.md`, `base-price-flatbox.md` thành tombstone trỏ `docs/features/v1-archive/`; `pipeline-jobs.md` viết lại (Job 1, Job 2 + trigger 15:05, KBS sync, V3).
- [ ] T043 [P] `CLAUDE.md` (Pipeline, bảng Luật sản phẩm, entry thường dùng), `docs/architecture.md`, `docs/entity_model.md`, use case UC-003/005/006/008, `.claude/skills/*` nhắc file V1.
- [ ] T044 Xoá script V1 trong `scripts/` (xác nhận từng file không dùng cho V3 / ship).
- [ ] T045 Grep toàn repo (backend, mobile, frontend, docs sống) không còn tham chiếu V1 ngoài tombstone, `v1-archive` và migration lịch sử (SC-002).

## Phụ thuộc

- T001 trước T025/T035.
- Đợt 1 (T002–T007) xong và kiểm 2 phiên trước đợt 3.
- Đợt 2: T008 → T013, T019; T010 → T016, T021. Client chịu được khi server chưa có trường mới.
- Đợt 3 chỉ khi T024 xác nhận mọi máy đã cài app mới. T026 trước T033 (xoá `SignalAnalyzer`).
- Đợt 4 sau T038. Đợt 5 sau đợt 4 (hoặc song song phần docs không phụ thuộc bảng).
