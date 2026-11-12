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
- [x] T007 **Điểm dừng**: owner ship. AI đọc (chỉ đọc) log production 2 phiên liên tiếp: trigger 15:05 chạy; `HistoryJson` của 5 mã có nến phiên đó khớp giá đóng cửa ATC trên bảng giá; nhịp V3 16:00 log bình thường.

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

- [x] T025 Ghi mã commit ngay trước đợt này vào `docs/features/v1-archive/README.md` ("commit cuối còn code V1").
- [x] T026 Chuyển test `StockRadar.Tests/DieuChinhGia/*` sang test thẳng `BoDieuChinhGiaTheoQuyen` (bỏ phụ thuộc `SignalAnalyzer`).
- [x] T027 Nhóm 1 — Job/hosted service: `DailyAnalysisJob`, `IntradayScannerJob`, `OpportunityIntradayMonitorJob`, cấu hình Quartz và id trong `QuartzJobIds`/`JobCatalog` (cả `weekly-opportunity-review`), `SmartMoneyContextWarmer`, `OpportunityRankerBootstrap`.
- [x] T028 Nhóm 2 — Runner/Infrastructure theo plan D2 mục 2; trong `DailySessionSyncRunner` gỡ khối Darvas, giữ append nến / VNINDEX / rescreen.
- [x] T029 Nhóm 3 — API theo plan D2 mục 3 (controller, action trong `MarketJobsController`/`MarketController`, `GET sectors`, SignalR `RadarUpdated`/`TradeEventCreated`/`AlertCreated`).
- [x] T030 Nhóm 4 — Application theo plan D2 mục 4 (cả phần V1 của `MarketService`, pha trong VNINDEX chart).
- [x] T031 D3 — `StockService.GetDetailAsync` + `StockDetailDto`: bỏ trường V1, giữ `history`, `isUniverseActive`/`universeStatusReason`/`universeUpdatedAt`; `GET market/vnindex/chart` bỏ pha và đỉnh. Làm theo skill `api-contract-review`.
- [x] T032 D5 — xoá `Score` khỏi `WatchlistItemDto`; `WatchlistService` bỏ `dailyOpportunities`, `smartMoneyEval`, `buyDecision`.
- [x] T033 Nhóm 5 — Domain theo plan D2 mục 5. Giữ: `BoDieuChinhGiaTheoQuyen`, nguồn sự kiện quyền, `INguonLichChotQuyen`/`FireAntLichChotQuyenClient`/`ThongTinChotQuyen`/`FireAntOptions` (đăng ký DI giữ cho spec 015), `StockUniverseFilter`, `IndicatorMath`, `TradingSessionMath`, `RealizedPnlMath`, `OutcomeBucketNames`, `ExchangePriceBand`.
- [x] T034 Nhóm 6 — Persistence: repository, entity, cấu hình trong `ApplicationDbContext` theo plan D2 mục 6 (bảng chưa xoá, chỉ bỏ ánh xạ — migration ở đợt 4).
- [x] T035 Xoá test V1 và harness `V1TopReplayTests.cs` cùng code (đã commit ở T001).
- [x] T036 Cấu hình repo: xoá mục Phụ lục B khỏi `appsettings.json`, `appsettings.Development.json`, `appsettings.Production.json.example`. Soạn đoạn hướng dẫn sửa `appsettings.Production.json` **trên server** cho owner.
- [x] T037 Kiểm: `dotnet build` + `dotnet test` (báo số test trước/sau, số giảm = test V1 đã gỡ); grep backend không còn tên ở Phụ lục A–C (trừ migration lịch sử); chạy nhịp V3 thủ công trên dev với cùng dữ liệu trước/sau → tín hiệu, vị thế giống hệt.
- [ ] T038 **Điểm dừng**: owner ship + sửa cấu hình server. AI đọc log 2 phiên: không còn log job V1; V3 08:30 / mỗi phút / 14:25 / 16:00 bình thường; `QuotesUpdated` vẫn đến app ~60 giây/lần; giá danh sách đang giữ nhảy.
- [x] T046 (10/11/2026) Bỏ bước Phân tích SmartMoney khỏi `scripts/ship-all.ps1`, xoá `scripts/run-pipeline-jobs.sh` và hai cờ `-SkipJobs`/`-LocalOnly` (owner chọn làm sớm 10/11/2026: endpoint analysis gỡ ở đợt 3 sẽ làm ship báo lỗi).

## Đợt 4 — Dữ liệu (D6)

- [x] T039 (12/11/2026) Script `specs/016-remove-v1-top/xuat-bang-v1.sh`: chạy trên server, xuất từng bảng Phụ lục A (cả `SessionRadarHits`) ra file `.dat` (trường 0x1F, dòng 0x1E, UTF-8), nén `/tmp/v1-export-<ngày>.tar.gz`, in `COUNT(*)` + `sha256` + tổng KHỚP/LỆCH. Không in mật khẩu. Chưa chạy trên server (T041).
- [x] T039b (12/11/2026) Hướng dẫn owner: `specs/016-remove-v1-top/huong-dan-xuat-va-xoa-bang-v1.md` (PowerShell 5.1, kèm 4 câu SQL kiểm sau ship).
- [x] T040 (12/11/2026) Thư mục migration đang dùng: `Infrastructure/Migrations/`. Đã sinh `20261112064612_RemoveV1Tables`: `Up` chỉ có 26 `DropTable` (`PositionSellLegs` trước `MasterAlertPositions`), `Down` tạo lại 26 bảng rỗng + khoá ngoại, snapshot còn 13 bảng. Apply/rollback/apply lại trên DB dev: 26 → 0 → 26 → 0; test ảnh chụp model bật lại, 537 pass.
- [x] T041 (12/11/2026) AI chạy script xuất trên production theo lệnh owner ("làm hết đi"): **26/26 KHỚP**, file `/tmp/v1-export-20261112.tar.gz` (5.5 MB), sha256 `6c99e039a1103318e62bb61a1f600d4089b91e4087e8bed4e1939a18b5ce5330`; bản trên máy `%USERPROFILE%\Downloads\v1-export-20261112.tar.gz` trùng sha256, đủ 26 `.dat` + 26 `.columns.txt` + `counts.txt` + `so-sanh.txt`; đã xoá file trên server (`DA-XOA-HET`). Ship commit `0872f10` (kèm `604ab08`). Kiểm sau ship: 0 bảng V1, đủ 13 bảng giữ lại, `KetQuaKichBan` còn, `__EFMigrationsHistory` có `RemoveV1Tables`; `Stocks` 1135, `TinHieuV3` 8, `ViTheV3` 4, `ChiSoNgayV3` 961; service `active`, 0 `fail:`/`crit:`, không còn trigger `daily-session-sync-interval`, ngoài khung giờ không có dòng `KBS sync:`.

## Đợt 5 — Tài liệu, script (D7, FR-011)

- [x] T042 (12/11/2026) Tombstone `buy-decision.md`, `ma-stack-and-market-phase.md`, `base-price-flatbox.md` (banner + trỏ `docs/features/v1-archive/`); thân giữ làm lịch sử thiết kế. `buy-decision.md` ghi rõ phần còn sống — `IndicatorMath` (`StockUniverseFilter`) và `Domain/MasterAlerts/OutcomeBucketNames` (`NhatKyService`, `RealizedPnlMath`) — không thuộc luật chấm điểm V1. `pipeline-jobs.md` viết lại theo `QuartzSchedulingExtensions.cs`: Job 1, Job 2 15:05/15:15/15:30, KBS sync trong khung bảng giá, 4 nhịp V3 (08:30 · mỗi phút 9–14h · 14:25 · 16:00).
- [x] T043 (12/11/2026) `CLAUDE.md` (Pipeline, bảng Luật sản phẩm, entry), `docs/architecture.md` + `docs/entity_model.md` viết lại (13 bảng / 13 entity theo snapshot), `docs/README.md`, `docs/ai-context.md`, 5 stub SUPERSEDED → "ĐÃ GỠ", UC-002/003/005/006/008, `use_cases.puml`, 6 skill `.claude/skills/*`, banner ảnh chụp cho `architecture-review-2026-11-09.md`.
- [x] T044 (12/11/2026) Owner duyệt: `git rm` 17 file — 14 file V1 (`_test-base-price.ps1`, `_test-darvas-breakout.ps1`, `check-prod-pipeline.ps1`, `compare-backtest-max-results.ps1`, `criteria-backfill-only.sh`, `monitor-ranker-weekly.ps1`, `run-opportunity-monitor.ps1`, `test-vip-telegram.sh`, `train-opportunity-ranker.ps1`, `purge-setup-tracks-before-date.ps1`, `purge-setup-tracks-before-date.sh`, `tune-optuna.py`, `tune-optuna-requirements.txt`, `setup-tune-venv.sh`) + 3 rác tạm (`_tmp_hieuqua_dev_rows.ps1`, `_tmp_tn_out.txt`, `_tmp_tn_symbols.txt`). Comment `ship-all.ps1` đổi thành một dòng ghi lịch Job 2 15:05/15:15/15:30, không đổi logic. Owner duyệt xoá thêm `run-daily-jobs.ps1` (nửa gọi `/market/jobs/analysis` đã gỡ). **Giữ, chưa sửa:** `ua-structural-graph.mjs` (tour mục 2 lọc `DailyAnalysis|Criterion`). Grep `scripts/`, `.github/`, `deploy.sh`: 0 tham chiếu 17 file đã xoá.
- [x] T045 (12/11/2026) Grep Phụ lục A–C trên `backend/`, `mobile/lib/`, `frontend/src/`, `docs/`, `scripts/`, `.claude/skills/`, `CLAUDE.md`: code chỉ còn `Domain/MasterAlerts` (Phụ lục D giữ) + route `/radar` redirect ở web; docs sống chỉ còn câu "đã gỡ"; script V1: 17 file đã xoá ở T044. Sửa 3 skill còn trỏ script vừa xoá: `release-deploy-gate`, `sql-data-review`, `sr-build-run-test` → dùng `GET /api/v1/market/jobs` và script còn sống.

## Phụ thuộc

- T001 trước T025/T035.
- Đợt 1 (T002–T007) xong và kiểm 2 phiên trước đợt 3.
- Đợt 2: T008 → T013, T019; T010 → T016, T021. Client chịu được khi server chưa có trường mới.
- Đợt 3 chỉ khi T024 xác nhận mọi máy đã cài app mới. T026 trước T033 (xoá `SignalAnalyzer`).
- Đợt 4 sau T038. Đợt 5 sau đợt 4 (hoặc song song phần docs không phụ thuộc bảng).
