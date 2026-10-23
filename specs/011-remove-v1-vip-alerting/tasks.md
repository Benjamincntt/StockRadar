# Tasks: Gỡ phần bắn tin mua/bán + theo dõi vị thế V1 (VIP)

**Input**: `specs/011-remove-v1-vip-alerting/plan.md`, `spec.md` (Phụ lục A = danh bạ file:line)
**Tests**: BẮT BUỘC (yêu cầu: `dotnet test` xanh + 3 file NhatKy sửa + flutter analyze + frontend build).
**Waves**: **ĐỢT A** = Phase 1–5 (gỡ code, không xoá bảng). **ĐỢT B** = Phase 6 (migration drop bảng — CHỈ viết, không áp, ship sau khi xuất production).

## Prereq — baseline
- [ ] T000 Ghi mốc kiểm chứng trước khi gỡ: `dotnet test backend/StockRadar.Tests` = **418 pass**; `dotnet build backend/StockRadar.slnx` 0 lỗi. Chụp số để đối chiếu sau.

---

## Phase 1 — Setup / mở khoá (bẫy thứ tự, nền cho mọi pha sau)

- [ ] T001 Tách `RealizedPnlOptions` (kèm `FeeProfile` mapping nếu dính) khỏi `StockRadar.Application/Options/MasterAlertOptions.cs:232` sang file mới `StockRadar.Application/Options/RealizedPnlOptions.cs`; giữ nguyên `SectionName="RealizedPnl"`. **Không đổi hành vi.** (bẫy #1)
- [ ] T002 [P] Di chuyển DI registration `RealizedPnlOptions`/`OpportunityPerformanceOptions`/`MasterAlertOptions` cho khớp file mới trong `StockRadar.Application/DependencyInjection.cs:24,26`; xác nhận `NhatKyService.cs:19,24` vẫn resolve `RealizedPnlOptions`.
- [ ] T003 [P] Liệt kê chính xác các hằng số `MasterAlertKinds` mà 010 còn dùng (`EfNhatKyTinHieuRepository.cs:83-84` `SellPoint1Half`/`SellAll`); thêm comment nhãn `DO-NOT-CHANGE` ≤2 dòng nếu phải giữ lại hằng số leg cho 010. (bẫy #3)
- [ ] T004 [P] Xác nhận `RealizedPnlMath.cs`, `Domain/MasterAlerts/OutcomeBucketNames.cs` **không** bị xoá (010 + `NhatKyThongKeTests` dùng); ghi chú vào plan nếu cần. (bẫy #2)
- [ ] T005 `dotnet build backend/StockRadar.slnx` = 0 lỗi sau khi tách options.

## Phase 2 — Foundational: runner intraday + job (giữ realtime)

- [ ] T010 Sửa `StockRadar.Infrastructure/MarketData/OpportunityIntradayMonitorRunner.cs`: **xoá** field `TopOpportunityVipAlertPublisher vipAlerts` (`:23`) + mọi lời gọi `vipAlerts.*` (`:32-41,96-121,149-157`); **GIỮ** KBS fetch/`tradeDetector`/`tradeAggregator`/`sessionFlow`/`publisher.PublishTradeEventAsync`/`PublishQuotesAsync`/`quoteCache`/`sync.ApplyAsync`/`monitorStatus` (`:60-94,124-146,159-163`). (bẫy #4)
- [ ] T011 [P] Xoá `IOptions<OpportunityMonitorOptions>` không còn dùng? — **KIỂM**: `cfg.BatchSize` vẫn dùng (`:51`) → GIỮ `OpportunityMonitorOptions`. Không đổi job cadence.
- [ ] T012 [P] Đổi nhãn job `opportunity-monitor` cho hết "VIP": `Scheduling/QuartzJobIds.cs:9` giữ id (an toàn DB/trigger); cập nhật text hiển thị "Monitor cơ hội (VIP)" → "Quét lô lớn / dòng tiền trong phiên" tại nguồn sinh `JobCatalog`/`jobs.json` description (tìm `JobCatalog`/`OpportunityMonitor` display name). **Không đổi `jobId`** (dùng cho endpoint trigger).
- [ ] T013 Gỡ registration publisher + caches + judge khỏi `StockRadar.Infrastructure/DependencyInjection.cs:130-144` (MasterAlertSessionTracker, Vip*Cache, VipLlmContextBuilder, HttpClient "VipLlmJudge", IVipLlmJudge, TopOpportunityVipAlertPublisher, IVipTelegramAlertTestService). **GIỮ** `TelegramNotifier`(`:143` V2 dùng), `OrderFlowSnapshotTracker`, runner(`:144-145`), `IntradayMonitorStatus*`.
- [ ] T014 `dotnet build backend/StockRadar.slnx` = 0 lỗi sau khi runner không còn tham chiếu publisher (publisher class vẫn tồn tại tới Phase 4).

## Phase 3 — US1: bảo vệ đường Top/Home + realtime (không đổi hành vi)

- [ ] T020 [US1] Grep-xác minh `DailyAnalysisRunner`/`SmartMoneyOpportunitySelector`/`OpportunitiesController`/`OpportunityRankerService`/`DarvasBreakoutAnalyzer` **0** tham chiếu lớp VIP đã/ sẻ gỡ. Ghi kết quả (rỗng = đạt).
- [ ] T021 [US1] Xoá feed alert VIP-only: `StockRadar.Api/Controllers/AlertsController.cs` (file) + `StockRadar.Application/Services/AlertService.cs` (file) + abstraction `IAlertService`/`AlertDto`/`AlertQuery` nếu không còn consumer (grep). Gỡ DI tương ứng. **Lý do**: `AlertService.cs:16-19` AllowedSources `["Trong phiên","Master"]`, chỉ publisher ghi "Master" → bản chất VIP-only.
- [ ] T022 [US1] [P] Kiểm SignalR hub `Api/Hubs/MarketHub.cs`: gỡ event `AlertCreated` (`:10`) nếu không còn publisher nào bắn (`PublishAlertAsync`); **GIỮ** `QuotesUpdated`/`IndexUpdated`/`RadarUpdated`/`TradeEventCreated`. Cập nhật `Api/Realtime/SignalRMarketRealtimePublisher.cs`.
- [ ] T023 [US1] `dotnet build backend/StockRadar.slnx` = 0 lỗi; chạy `dotnet test` — không đổi số pass so với nền (trừ các test VIP sẽ gỡ ở Phase 5).

## Phase 4 — US2: gỡ backend bắn tin / vị thế / ML / AI judge

- [ ] T030 [US2] Xoá Notification VIP: `Infrastructure/Notifications/` — `TopOpportunityVipAlertPublisher.cs`, `TopOpportunityVipAlertEvaluator.cs`, `VipTelegramMessageFormatter.cs`, `AnthropicVipLlmJudge.cs`, `VipLlmContextBuilder.cs`, `VipLlmJudgeParsing.cs`, `VipPullbackMaCache.cs`, `VipPositionHistoryCache.cs`, `VipVnIndexPeakCache.cs`, `MasterAlertSessionTracker.cs`. **GIỮ** `TelegramNotifier.cs`, `V2TelegramFormatter.cs`, `OrderFlowSnapshotTracker.cs`, các file V2/Telegram khác.
- [ ] T031 [US2] [P] Xoá VIP LLM abstractions/options: `Application/Abstractions/IVipLlmJudge.cs`, `Application/Options/VipLlmJudgeOptions.cs`; gỡ `DependencyInjection.cs:33` + section `VipLlmJudge` appsettings (`appsettings.json:267-289`, `appsettings.Production.json:59`, `.example`).
- [ ] T032 [US2] [P] Xoá VipIntraday ML: `Application/Services/VipIntradayRankerService.cs`/`VipIntradayCalibrationService.cs`/`VipIntradayThresholdService.cs`/`VipIntradayTrainingService.cs`, `Application/Abstractions/IVipIntradayServices.cs`/`IVipIntradayCalibrationStore.cs`, `Infrastructure/Ml/VipIntradayBootstrap.cs`/`VipIntradayInfrastructure.cs`/`FileVipIntradayCalibrationStore.cs`, `Domain/Services/OpportunityRanking/VipIntradayFeatures.cs`; gỡ DI `DependencyInjection.cs:74-80,250-252`.
- [ ] T033 [US2] [P] Xoá RealizedPnl-cho-VIP orphan: `Application/Services/RealizedPnlService.cs`, `RealizedPnlBackfillService.cs`, `VipAlertAccuracyQueryService.cs` + abstractions (`IVipAlertAccuracyQueryService.cs`, phần `IRealizedPnlBackfillService` trong `IPerformanceServices.cs:483`, DTO `RealizedPnlBackfillResultDto`); gỡ DI `:73,86,88`. **GIỮ `Domain/Services/RealizedPnlMath.cs`** (010 dùng). Kiểm `IOpportunityNorthStarQueryService`: nếu không consumer → gỡ (`:72`); nếu mập mờ → để nguyên + ghi chú CHƯA RÕ.
- [ ] T034 [US2] [P] Gỡ VIP read/write repo trong `Infrastructure/Persistence/Repositories/EfPerformanceRepositories.cs` (MasterAlertPositions/PositionSellLegs writes+reads, `GetAlertHistoryAsync`, `GetPositionsSinceAsync`) + `EfVipAlertFireRepository.cs` + `IMasterAlertPositionRepository`/`IVipAlertFireRepository` abstraction; gỡ DI `:188-191`. **KHÔNG đụng** các phần SetupTracks/DailyOpportunity dùng chung trong cùng file.
- [ ] T035 [US2] Xoá `Application/Options/MasterAlertOptions.cs` (còn lại sau khi tách RealizedPnlOptions ở T001) + section `MasterAlerts` appsettings (`:290-335`, `Production:2-33`) + `OpportunityMonitorOptions` **chỉ nếu** chắc chắn không còn dùng (runner vẫn dùng → GIỮ). Gỡ `DependencyInjection.cs:24`.
- [ ] T036 [US2] Gỡ cờ VIP trong `TelegramNotifyOptions` (`VipAlertsEnabled`) + appsettings `TelegramNotify:261-266`; **GIỮ** BotToken/ChatId (V2 vẫn bắn Telegram).
- [ ] T037 [US2] Gỡ endpoint VIP trong controller: `MarketJobsController.cs:195-204` (`telegram/vip-test`, bỏ dep `IVipTelegramAlertTestService:20`), `MlController.cs:166-220` (`train/vip-intraday`,`vip-intraday/status`,`recalibrate`, bỏ deps `:18-21,26`). **GIỮ** `MarketJobsController` `opportunity-monitor`(`:165`) + các endpoint Ml dùng chung.
- [ ] T038 [US2] `dotnet build backend/StockRadar.slnx` = 0 lỗi (bắt leftover DI/using).

## Phase 5 — US3: picker nhật ký (010) + web + mobile

- [ ] T040 [US3] Backend picker: xoá `GET tin-hieu/vip` (`NhatKyController.cs:115-124`); trong `NhatKyService.cs` rút `NguonHopLe` còn `["V2"]`(`:22`), xoá `LayTinHieuVipAsync`(`:299-312`), nhánh VIP `LayNhomGiaAsync:423-429`, `LaySymbolTinHieuAsync` VIP branch, `GanKetQuaTinHieuAsync` VIP(`:603-609,614-615`), alias `V1→VIP:335`. **GIỮ** xử lý an toàn cho `NguonTinHieu='VIP'` đã lưu (không crash thống kê `:189-210`).
- [ ] T041 [US3] Repository 010: trong `EfNhatKyTinHieuRepository.cs` + `INhatKyTinHieuRepository.cs` gỡ `TinHieuVipRow`/`KetQuaVipRow`/`LayTinHieuVipAsync`/`LayMotTinHieuVipAsync`/`LayKetQuaVipAsync` (`:43-105`, `:17-33,46-54`). Xoá tham chiếu `MasterAlertKinds` leg (thay literal nội bộ nếu 010 còn cần).
- [ ] T042 [US3] Mobile picker: `mobile/lib/screens/nhat_ky_tin_hieu_picker.dart` bỏ tab "VIP (V1)"(`:88`) + nhánh `_nguon => ...'vip'`(`:31,39`); `nhat_ky_lenh_screen.dart`(`:93,184`) bỏ chữ VIP; `api_client.dart`(`:616-639`) mất giá trị `'vip'`; `models.dart` `TinHieuNhatKy`(`:2119,2134,2146`) tỉa comment/nhánh VIP.
- [ ] T043 [US3] Mobile nhãn "VIP (đã ngừng)": ở cho nơi render `lenh.nguonTinHieu=='VIP'` (`nhat_ky_screen.dart:224` / detail) hiển thị chuỗi "VIP (đã ngừng)"; không đổi giá đã chụp.
- [ ] T044 [US3] [P] Web xoá mã chết VIP: `frontend/src/hooks/useLiveAlerts.ts`, `useViewedAlerts.ts`, `components/alerts/RealtimeOrderRow.tsx`, `RealtimeOrderList.tsx`, `lib/alertViewSlate`→`lib/alertViewState.ts`; tỉa `lib/api.ts:290-306` (`getAlerts`), `types/index.ts:13,15,384-397` (`Alert`/`AlertFeedScope`), `types/index.ts` `IntradayMonitorStatus.lastAlertsSent`(`:56`) nếu feed đã bỏ. **GIỮ** `LiveMarketContext`/`marketHub`/`useLiveTrades`/`TradePrintList`/`HomePage`/`HieuQuaPage`.
- [ ] T045 [US3] [P] Mobile xoá mã chết VIP: `api_client.dart` `/alerts`(`:183-203`) + `/performance/*`(`:445-515`); `models.dart` `RealizedTrade`/`RealizedPnlSummary`/`RealizedTradesResponse`/`OpportunityPerformanceSummary`/`AlertHistory*`/`AlertItem`(`:347-792,1088-1125`). **CHƯA RÕ** `smart_money_backtest_card.dart`/`formatAlertTime`: để nguyên (không phải VIP) + ghi chú.
- [ ] T046 [US3] `flutter analyze` = 0 issue; `npm run build` (frontend) = sạch.

## Phase 6 — Test & xanh hoá (điều kiện hoàn thành ĐỢT A)

- [ ] T050 [P] Xoá test VIP dedicated: `Tests/VipAlerts/BullTrapBuyPathTests.cs`; `Tests/SellExit/SellExitFixtures.cs`·`BlueSkyStopTests.cs`·`BlueSkyThresholdTests.cs`·`SellWindowTests.cs`·`SellHalfSizeTests.cs`·`AnchorWindowTests.cs`·`PreSellWindowMessageTests.cs`; `Tests/RealizedPnl/RealizedPnlServiceTests.cs`·`RealizedAggregateTests.cs`. **TRƯỚC** khi xoá `SellExitFixtures.cs`: chuyển helper `BuildBoxThenBreak`(`:94`) ra nơi `DarvasRegressionTests.cs:11` còn dùng.
- [ ] T051 [P] `DarvasRegressionTests.cs` + `RealizedPnlMathTests.cs`: **GIỮ** (test code dùng chung V2/010); cân nhắc di chuyển ra thư mục hợp tên (không bắt buộc, tránh refactor lạc loài).
- [ ] T052 [P] Sửa `VipAlerts/VnIndexPriorPeakAnalyzerTests.cs`: nếu giữ `VnIndexPriorPeakAnalyzer` (dùng cho `MarketService`), đổi ca sang `IsBullTrapEnvironment`/`IsNearPriorPeak`, bỏ ca alias `ShouldBlockBuy`; xoá file nếu cả analyzer bị gỡ (KIỂM consumer trước).
- [ ] T053 [P] Sửa 3 file NhatKy: `NhatKyTinHieuTests.cs` (xoá `SeedVip`` :75-93` + ca `PickerVip/GanLenhVip*` `:264-299`), `NhatKyN8GiaChupTests.cs` (xoá `SeedVip``:72-107` + 2 ca `N8_Vip_*` `:323-360` + comment `:21`), `NhatKyThongKeTests.cs` (xoá `SeedVip``:75-91` + nửa VIP `:268-285`). Giữ nguyên các ca V2.
- [ ] T054 `dotnet test backend/StockRadar.Tests` = **tất cả pass** (0 fail); ghi số pass sau đợt A (so nền 418).
- [ ] T055 `dotnet build backend/StockRadar.slnx` = 0 lỗi (toàn solution, gồm Api).

## Phase 7 — ĐỢT B (CHỈ viết sẵn, KHÔNG áp; ship sau khi xuất production)

- [ ] T060 Viết hướng dẫn xuất 3 bảng production trong `specs/011-remove-v1-vip-alerting/export-guide.md`: dùng `scripts/export-db.ps1` **hoặc** `sqlcmd`/`bcp` xuất `MasterAlertPositions`/`PositionSellLegs`/`VipAlertFires` ra file (kèm lệnh cụ thể, đọc-only, không đổi schema). Chủ sản phẩm tự chạy trên server.
- [ ] T061 [P] Viết migration drop 3 bảng (`dotnet ef migrations add DropVipAlertingTables`) sau khi (đợt B) gỡ entity `MasterAlertPositionEntity`/`PositionSellLegEntity`/`VipAlertFireEntity` (`PerformanceEntities.cs:41,100,120`) + DbSet/config (`ApplicationDbContext.cs:29-31,362,387,404`) + `Down()` tái tạo. **Đọc body** (`Up`/`Down`) — repo hay sinh sai.
- [ ] T062 [P] Test migration trên **DEV** (KHÔNG 103.226.248.6): `database update` → rollback 1 bậc → `update` lại; xác nhận sạch. Ghi log áp/rollback vào `export-guide.md`.
- [ ] T063 [P] Dọn leftover: xoá `Domain/MasterAlerts/MasterAlertKinds.cs`/`RealizedStatusNames.cs` nếu 010/V2 không còn dùng (kiểm lại lần cuối); gỡ `RealizedPnlOptions`/`OutcomeBucketNames` **CHỈ KHI** chắc 010 không dùng (mặc định GIỮ).

## Phase 8 — Polish & xác minh cuối

- [ ] T070 [P] Smoke DEV (restart-api): `GET /api/v1/opportunities` (Top còn), `GET /api/v1/nhat-ky/tin-hieu/v2?size=5` (picker V2 còn), `GET /api/v1/nhat-ky/vi-the`+`/thong-ke` (lệnh VIP cũ không lỗi), `GET /api/v1/market/intraday-monitor` (còn scan); `GET /api/v1/nhat-ky/tin-hieu/vip` → 404/405 (đã gỡ).
- [ ] T071 [P] Đọc `backend/logs/api-dev.log`: không còn job/tin VIP khởi động; `opportunity-monitor` vẫn chạy trade scan; không exception DI thiếu.
- [ ] T072 [P] Cập nhật `CLAUDE.md` (mục Pipeline V1/V2) + `docs/domain/pipeline-jobs.md`: mô tả `opportunity-monitor` = quét lô lớn/dòng tiền (hết VIP), bỏ mention "monitor VIP". 
- [ ] T073 Chạy `/better-harness`? — không. Chỉ chắc chắn `dotnet test` xanh + `git status` review diff trước khi báo cáo (KHÔNG commit).

## Dependencies

- T000 → T001 → {T002..T005} → T010..T014 (Phase 2 phải xong trước Phase 4 vì runner bỏ dependency publisher).
- Phase 3 (T020-023) nên xong trước/song song Phase 4 để cô lập lỗi feed/SignalR.
- Phase 4 (T030-038) và Phase 5 (T040-046) độc lập tương đối nhưng **cùng cần** Phase 2 options tách (T001).
- Phase 6 (T050-055) phụ thuộc **tất cả** backend Phase 2–5 (test chỉ xanh khi code đã gỡ xong).
- Phase 7 (ĐỢT B) bị **chặn** bởi xác nhận xuất production của chủ sản phẩm (T060 trước T061-063).
- Phase 8 smoke (T070-071) sau toàn bộ Đợt A (T001–T055).

## Parallelization

- **[P]** trong Phase 2: T011/T012/T013 song song (file khác nhau) sau T010.
- **[P]** Phase 4: T030–T037 chủ yếu file rời rạc → nhiều luồng song song; T038 build gom cuối.
- **[P]** Phase 5: backend (T040-041) ∥ web (T044) ∥ mobile (T042-043,045) — khác tree.
- **[P]** Phase 6: T050–T053 song song (file test rời rạc) **trừ** ràng buộc chuyển helper T050 trước khi xoá fixtures.

## MVP Scope (ĐỢT A tối thiểu có giá trị)

Phase 1→2 (tách options + runner strip) đủ để hệ **không còn bắn tin VIP** mà **vẫn giữ realtime + Home**, `dotnet test` xanh. Phase 4→6 dọn phần còn lại. Picker V2 + nhãn "VIP (đã ngừng)" (Phase 5) để 010 không vỡ UX. ĐỢT B (Phase 7) tách riêng, chặn bởi bước xuất production.

## Format Validation

- Mọi task: checkbox + T### + ([P] khi song song) + ([US#] cho Phase 3/4/5) + đường file cụ thể. ✅
- Không shim/"deprecated" enum tồn tại (chỉ `DO-NOT-CHANGE` comment cho leg-label 010 nếu bắt buộc). ✅
