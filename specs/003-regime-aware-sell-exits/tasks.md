---

description: "Task list for feature implementation"
---

# Tasks: Điểm bán 1/2 theo bối cảnh giá (nền trên vs vượt đỉnh)

**Input**: Design documents from `/specs/003-regime-aware-sell-exits/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/internal-contracts.md)

**Tests**: Có. `quickstart.md` liệt kê ma trận kịch bản bắt buộc và luật thoát lệnh trực tiếp quyết định tiền của người dùng, nên test đi kèm từng story.

**Organization**: Task nhóm theo user story để mỗi story triển khai và kiểm chứng độc lập.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Chạy song song được (khác file, không phụ thuộc task chưa xong)
- **[Story]**: Story tương ứng (US1, US2, US3)
- Mỗi task ghi rõ đường dẫn file

## Path Conventions

Monorepo StockRadar, tính năng chỉ chạm backend: `backend/StockRadar.{Domain,Application,Infrastructure,Api,Tests}/`. Không chạm `mobile/` và `frontend/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Đưa toàn bộ tham số mới vào cấu hình trước khi có code đọc chúng

- [x] T001 Thêm tham số mới vào `backend/StockRadar.Application/Options/MasterAlertOptions.cs`: `SellPoint1DropFromAnchorPercent=4`, `SellPoint2DropFromAnchorPercent=6`, `AnchorLookbackSessions=20`, `OverheadBoxMinSessions=20`, `OverheadBoxMaxHeightPercent=15`, `OverheadBaseMaxAgeSessions=250`, `OverheadBaseBufferPercent=0.5`, `SellConfirmationTicks=2`
- [x] T002 Đảo chiều `MarketPhaseMultipliers` trong `backend/StockRadar.Application/Options/MasterAlertOptions.cs` thành `Favorable=1.25`, `Neutral=1.0`, `Unfavorable=0.75`
- [x] T003 Đồng bộ toàn bộ khoá của T001 và T002 vào section `MasterAlerts` trong `backend/StockRadar.Api/appsettings.json`

**Checkpoint**: Cấu hình sẵn sàng, chưa có hành vi nào đổi

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Lưu trữ trạng thái chế độ, nguồn dữ liệu lịch sử, nguồn pha, và bộ đếm xác nhận — mọi story đều cần

**⚠️ CRITICAL**: Không story nào bắt đầu được trước khi phase này xong

- [x] T004 Thêm 5 thuộc tính `ExitRegime`, `OverheadBaseLow`, `OverheadBaseHigh`, `EntryBarLow`, `AnchorWindowStart` vào `MasterAlertPositionEntity` trong `backend/StockRadar.Infrastructure/Persistence/Entities/PerformanceEntities.cs` theo bảng ở `data-model.md` mục 1
- [x] T005 Cấu hình kiểu và precision cho 5 cột mới trong `backend/StockRadar.Infrastructure/Persistence/ApplicationDbContext.cs`, giữ đúng khuôn `decimal(18,2)` và `nvarchar` như các cột hiện có của bảng
- [x] T006 Tạo migration `AddSellRegimeColumns` trong `backend/StockRadar.Infrastructure/Migrations/` (chỉ `AddColumn`, không backfill) và cập nhật `backend/StockRadar.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs`
- [x] T007 Thêm 5 thuộc tính tương ứng vào cuối `MasterAlertPositionRecord` trong `backend/StockRadar.Application/Abstractions/IPerformanceServices.cs`, có giá trị mặc định để không lan diff sang nơi khởi tạo cũ
- [x] T008 Mở rộng `IMasterAlertPositionRepository` trong `backend/StockRadar.Application/Abstractions/IPerformanceServices.cs`: `UpsertOnBuyAsync` nhận thêm chế độ, hai cạnh nền và `EntryBarLow`; thêm phương thức cập nhật chế độ cho vị thế đã tồn tại
- [x] T009 Hiện thực các thay đổi của T008 trong `backend/StockRadar.Infrastructure/Persistence/Repositories/EfPerformanceRepositories.cs`, giữ nguyên ngữ nghĩa nâng vị thế từ Mua 1 lên Mua 2
- [x] T010 Tạo `backend/StockRadar.Infrastructure/Notifications/VipPositionHistoryCache.cs` theo đúng khuôn `VipPullbackMaCache`: prefetch OHLCV cho các mã có vị thế mở, cache theo phiên, trả `Unavailable` khi thiếu lịch sử
- [x] T011 Thêm hàm dựng mốc tham chiếu trong `backend/StockRadar.Infrastructure/Notifications/VipPositionHistoryCache.cs`: `max(High)` từ `max(AnchorWindowStart, hôm nay − AnchorLookbackSessions)` tới hiện tại, có gộp `row.High` đang chạy
- [x] T012 Cung cấp pha phiên hiện tại: thêm accessor trên `backend/StockRadar.Infrastructure/Notifications/TopOpportunityVipAlertPublisher.cs` lấy từ `topMap` đã nạp, và đổi `backend/StockRadar.Infrastructure/MarketData/OpportunityIntradayMonitorRunner.cs` dòng gọi `ProcessPositionAsync` từ `pos.MarketPhaseAtEntry ?? "Neutral"` sang pha phiên hiện tại với fallback `MarketPhaseAtEntry` rồi `Neutral`
- [x] T013 Thêm bộ đếm xác nhận tín hiệu bán (`SellConfirmationTicks`) theo cặp mã + loại tín hiệu trong `backend/StockRadar.Infrastructure/Notifications/MasterAlertSessionTracker.cs`, reset khi giá quay lại phía an toàn của ngưỡng

**Checkpoint**: Trạng thái, dữ liệu và pha đã sẵn — story bắt đầu được

---

## Phase 3: User Story 1 - Bảo vệ theo mốc đỉnh khi mua vượt đỉnh (Priority: P1) 🎯 MVP

**Goal**: Mọi vị thế đều có cắt lỗ thật, đo theo mốc tham chiếu 20 phiên không lùi xa hơn ngày mua, ngưỡng 4%/6% nhân hệ số pha, cộng lệnh bán hết tức thì khi phủ nhận cây vượt đỉnh.

**Independent Test**: Mở vị thế mô phỏng trên mã vừa phá đỉnh, hạ giá dần và kiểm tra mốc phát `BanNua` rồi `BanHet` đúng theo pha; lặp với kịch bản thủng `EntryBarLow`.

### Tests for User Story 1

> Viết trước, đảm bảo FAIL trước khi hiện thực

- [x] T014 [P] [US1] Test ngưỡng theo pha trong `backend/StockRadar.Tests/SellExit/BlueSkyThresholdTests.cs`: Neutral mốc 100 giá 96 ⇒ `BanNua`; đã bán nửa giá 94 ⇒ `BanHet`; Unfavorable giá 97 ⇒ `BanNua`; Favorable giá 96 ⇒ `null`
- [x] T015 [P] [US1] Test cắt lỗ và phủ nhận trong `backend/StockRadar.Tests/SellExit/BlueSkyStopTests.cs`: vị thế chưa từng lãi vẫn bắn khi rơi qua ngưỡng; giá thủng `EntryBarLow` ⇒ `BanHet` dù chưa đủ 6%
- [x] T016 [P] [US1] Test cửa sổ mốc tham chiếu trong `backend/StockRadar.Tests/SellExit/AnchorWindowTests.cs`: mua 8.5 trong khi đỉnh 20 phiên trước ngày mua là 12, giá vẫn 8.5 ⇒ `null`; mốc bằng High phiên mua ở ngày đầu; mốc trượt theo cửa sổ khi vị thế giữ quá 20 phiên
- [x] T017 [P] [US1] Test chặn theo cửa sổ bán trong `backend/StockRadar.Tests/SellExit/SellWindowTests.cs`: chưa đủ số phiên tối thiểu mà thủng ngưỡng ⇒ `CanhBaoRuiRoT0`, không bao giờ ra kind bán

### Implementation for User Story 1

- [x] T018 [US1] Đổi chữ ký `EvaluatePositionSignal` trong `backend/StockRadar.Infrastructure/Notifications/TopOpportunityVipAlertEvaluator.cs` để nhận mốc tham chiếu dựng sẵn và pha hiện tại, theo hợp đồng C-1 trong `contracts/internal-contracts.md`
- [x] T019 [US1] Viết lại nhánh chế độ Vượt đỉnh trong `backend/StockRadar.Infrastructure/Notifications/TopOpportunityVipAlertEvaluator.cs`: mẫu số là mốc tham chiếu, ngưỡng `SellPoint1/2DropFromAnchorPercent` nhân hệ số pha, bỏ hẳn gate `TrailingStopMinPeak`, giữ nguyên nhánh phân phối và guard đã bán nửa
- [x] T020 [US1] Thêm luật bán hết tức thì khi giá thủng `EntryBarLow` trong cùng hàm ở `backend/StockRadar.Infrastructure/Notifications/TopOpportunityVipAlertEvaluator.cs`, ưu tiên trước mọi ngưỡng phần trăm
- [x] T021 [US1] Ghi `EntryBarLow` và `AnchorWindowStart` khi mở vị thế trong `backend/StockRadar.Infrastructure/Notifications/TopOpportunityVipAlertPublisher.cs`, dùng `row.Low` của phiên mở và giữ nguyên mốc gốc khi nâng từ Mua 1 lên Mua 2
- [x] T022 [US1] Nối mốc tham chiếu và pha hiện tại vào `ProcessPositionAsync` trong `backend/StockRadar.Infrastructure/Notifications/TopOpportunityVipAlertPublisher.cs`, và mặc định chế độ `BlueSky` cho vị thế chưa phân loại
- [x] T023 [US1] Cập nhật nội dung tin chế độ Vượt đỉnh trong `backend/StockRadar.Infrastructure/Notifications/VipTelegramMessageFormatter.cs` để nêu mốc tham chiếu, % đã giảm so với mốc, ngưỡng áp dụng và pha, theo bảng C-3
- [x] T024 [US1] Cập nhật `BuildPositionSignalReasoning` trong `backend/StockRadar.Infrastructure/Notifications/TopOpportunityVipAlertPublisher.cs` cho khớp ngữ nghĩa mới, thay câu "Mất x% lãi từ peak" bằng mức giảm so với mốc tham chiếu

**Checkpoint**: US1 chạy độc lập — mọi vị thế đã có cắt lỗ thật, lỗ hổng nặng nhất được bịt

---

## Phase 4: User Story 2 - Chốt lãi tại cạnh dưới nền trên (Priority: P2)

**Goal**: Vị thế mua dưới một nền tích lũy dài được gắn mục tiêu chốt lãi bằng cạnh dưới nền, bán 1 nửa tại đó, phần còn lại bán hết khi bị đẩy ngược hoặc chuyển sang chế độ Vượt đỉnh khi vượt nền.

**Independent Test**: Nạp chuỗi OHLCV có nền 20+ phiên rồi gãy, mở vị thế mô phỏng dưới nền, cho giá hồi chạm cạnh dưới và kiểm tra `BanNua` cùng mục tiêu hiển thị.

### Tests for User Story 2

- [x] T025 [P] [US2] Test dò nền trên trong `backend/StockRadar.Tests/SellExit/OverheadBoxTests.cs`: chuỗi có nền 20+ phiên khoảng 10–12 rồi gãy ⇒ trả hộp với cạnh dưới đúng bằng giá đóng cửa thấp nhất của nền; nền ngắn hơn 20 phiên ⇒ không nhận; nền cũ hơn `OverheadBaseMaxAgeSessions` ⇒ không nhận; nhiều nền phía trên ⇒ chọn nền gần giá nhất
- [x] T026 [P] [US2] Test hồi quy bộ dò hộp trong `backend/StockRadar.Tests/SellExit/DarvasRegressionTests.cs`: `Analyze` và `Evaluate` cho kết quả y hệt trước và sau thay đổi trên cùng dữ liệu, theo bất biến C-2
- [x] T027 [P] [US2] Test luật thoát chế độ Có nền trên trong `backend/StockRadar.Tests/SellExit/UnderBaseExitTests.cs`: chạm vùng dưới cạnh dưới nền ⇒ `BanNua`; đã bán nửa và đóng cửa lại dưới cạnh dưới ⇒ `BanHet`; vượt cạnh trên kèm vol ⇒ `null` và chế độ chuyển sang `BlueSky`

### Implementation for User Story 2

- [x] T028 [US2] Thêm entry point liệt kê hộp phía trên một mức giá vào `backend/StockRadar.Domain/Services/DarvasBreakoutAnalyzer.cs`, dùng lại `BaseQualityEvaluator.PassesDarvasBox` và phần đo biên hộp sẵn có, không nhân bản logic và không đổi hành vi `Analyze` / `Evaluate`
- [x] T029 [US2] Bổ sung lọc theo mức giá, độ dài tối thiểu và tuổi tối đa vào entry point ở T028 trong `backend/StockRadar.Domain/Services/DarvasBreakoutAnalyzer.cs`, chọn hộp có cạnh dưới nhỏ nhất trong số hộp hợp lệ
- [x] T030 [US2] Thêm khả năng dò nền trên vào `backend/StockRadar.Infrastructure/Notifications/VipPositionHistoryCache.cs`, dựng `DarvasBoxSettings` riêng cho vùng cản từ `OverheadBoxMaxHeightPercent` mà không đụng bộ tham số breakout
- [x] T031 [US2] Phân loại chế độ khi mở vị thế trong `backend/StockRadar.Infrastructure/Notifications/TopOpportunityVipAlertPublisher.cs`: có nền hợp lệ phía trên giá vào ⇒ `UnderBase` kèm hai cạnh nền, ngược lại ⇒ `BlueSky`
- [x] T032 [US2] Phân loại lười cho vị thế cũ có `ExitRegime` rỗng trong `backend/StockRadar.Infrastructure/Notifications/TopOpportunityVipAlertPublisher.cs`, dùng `EntryPrice` và `EntryDate` đã lưu, ghi kết quả một lần
- [x] T033 [US2] Hiện thực nhánh chế độ Có nền trên trong `backend/StockRadar.Infrastructure/Notifications/TopOpportunityVipAlertEvaluator.cs`: chạm cạnh dưới trừ đệm `OverheadBaseBufferPercent` (nhân nghịch hệ số pha) ⇒ `BanNua`; đóng cửa lại dưới cạnh dưới sau khi đã chạm, hoặc thủng đáy nhịp hồi ⇒ `BanHet`
- [x] T034 [US2] Hiện thực chuyển chế độ `UnderBase → BlueSky` khi giá vượt cạnh trên nền kèm xác nhận thanh khoản trong `backend/StockRadar.Infrastructure/Notifications/TopOpportunityVipAlertPublisher.cs`: xoá hai cạnh nền, đặt `AnchorWindowStart` về phiên vượt nền, cấm chiều ngược lại
- [x] T035 [US2] Thêm định dạng tin chế độ Có nền trên vào `backend/StockRadar.Infrastructure/Notifications/VipTelegramMessageFormatter.cs`: nêu mục tiêu là cạnh dưới nền, khoảng nền và giá hiện tại

**Checkpoint**: US1 và US2 chạy độc lập; vị thế mua hồi đã có mục tiêu chốt lãi tại cản

---

## Phase 5: User Story 3 - Cảnh báo trong cửa sổ khoá T+ và ghi nhận đối chứng (Priority: P3)

**Goal**: Trước khi được phép bán, người dùng vẫn biết đã chạm mốc nào; mọi cảnh báo bán đều lưu đủ bối cảnh để đo hiệu quả.

**Independent Test**: Cho vị thế chạm ngưỡng ở phiên chưa đủ điều kiện bán, kiểm tra tin nêu đúng mốc và không dùng chữ "Bán"; kiểm tra bản ghi lưu đủ chế độ, mốc, ngưỡng, pha.

### Tests for User Story 3

- [x] T036 [P] [US3] Test nội dung cảnh báo trước cửa sổ bán trong `backend/StockRadar.Tests/SellExit/PreSellWindowMessageTests.cs`: tin nêu mốc đã chạm, có câu chưa bán được, và dòng tiêu đề không chứa chữ "Bán"

### Implementation for User Story 3

- [x] T037 [US3] Mở rộng `FormatRiskWarning` trong `backend/StockRadar.Infrastructure/Notifications/VipTelegramMessageFormatter.cs` để nêu mốc đã chạm (mục tiêu chốt lãi hoặc mốc tham chiếu) thay vì chỉ mức sụt từ đỉnh
- [x] T038 [US3] Ghi bối cảnh cảnh báo bán (chế độ, mốc tham chiếu, ngưỡng, pha) vào cột JSON bối cảnh của `VipAlertFires` trong `backend/StockRadar.Infrastructure/Persistence/Entities/PerformanceEntities.cs` và `backend/StockRadar.Infrastructure/Persistence/Repositories/EfPerformanceRepositories.cs`, kèm migration cột mới nếu cần
- [x] T039 [US3] Thêm log phân loại chế độ tại lần chạm đầu tiên của mỗi vị thế trong `backend/StockRadar.Infrastructure/Notifications/TopOpportunityVipAlertPublisher.cs`, đủ để đối chiếu trong `backend/logs/api-dev.log`

**Checkpoint**: Cả ba story hoàn chỉnh và đo được

---

## Phase 6: Polish & Cross-Cutting Concerns

- [x] T040 Cập nhật mẫu gửi thử trong `backend/StockRadar.Infrastructure/Notifications/TopOpportunityVipAlertPublisher.cs` (`SendSampleAlertsAsync`) để phát mẫu bán 1 nửa cho **cả hai** chế độ
- [x] T041 Xoá `TrailingStopMinPeak`, `BaseTrailingStopPercent1`, `BaseTrailingStopPercent2` khỏi `backend/StockRadar.Application/Options/MasterAlertOptions.cs` và `backend/StockRadar.Api/appsettings.json` sau khi xác nhận không còn nơi đọc
- [x] T042 [P] Cập nhật luật bán trong `docs/domain/buy-decision.md` — bắt buộc cùng change set theo nguyên tắc IV của hiến pháp
- [x] T043 [P] Cập nhật bảng tham số `MasterAlerts` trong `docs/architecture.md`, gồm bộ hệ số pha đã đảo chiều
- [ ] T044 Chạy đối chứng backtest 12 tháng luật cũ với luật mới, so ba chỉ số SC-002, SC-003, SC-006 trong `spec.md`, và dò lại hệ số Unfavorable trong vùng 0.75–0.85 — **deferred** (chạy trước khi bật production; không chặn ship code)
- [x] T045 Chạy `dotnet test backend/StockRadar.Tests/StockRadar.Tests.csproj` toàn bộ để chắc không vỡ luồng hiện tại
- [x] T046 Restart API bằng `backend/restart-api.ps1` rồi chạy các bước kiểm chứng trong `quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: không phụ thuộc, bắt đầu ngay
- **Foundational (Phase 2)**: cần Phase 1 xong — CHẶN mọi user story
- **US1 (Phase 3)**: cần Phase 2 xong
- **US2 (Phase 4)**: cần Phase 2 xong; chia sẻ file evaluator và publisher với US1 nên thực tế nên làm sau US1
- **US3 (Phase 5)**: cần Phase 2 xong; nội dung tin phụ thuộc US1/US2 đã định hình mốc
- **Polish (Phase 6)**: cần các story mong muốn đã xong

### User Story Dependencies

- **US1 (P1)**: độc lập hoàn toàn sau Foundational. Là MVP.
- **US2 (P2)**: độc lập về mặt kiểm thử, nhưng T033 và T019 sửa cùng file `TopOpportunityVipAlertEvaluator.cs` → không làm song song hai người.
- **US3 (P3)**: độc lập về mặt kiểm thử; T037 sửa cùng file formatter với T023 và T035.

### Within Each User Story

- Test viết trước và phải FAIL trước khi hiện thực
- Persistence trước service, service trước nội dung tin
- Hoàn tất story trước khi sang story ưu tiên thấp hơn

### Parallel Opportunities

- Phase 1: T001 và T002 cùng file nên tuần tự; T003 sau cả hai
- Phase 2: T004→T005→T006 tuần tự (cùng mạch schema); T007→T008→T009 tuần tự; T010→T011 tuần tự; T012 và T013 chạy song song được với hai mạch trên
- Phase 3: T014–T017 song song hoàn toàn (bốn file test khác nhau)
- Phase 4: T025–T027 song song hoàn toàn
- Giữa các story: không nên song song vì dùng chung evaluator, publisher và formatter

---

## Parallel Example: User Story 1

```bash
# Bốn file test độc lập, chạy cùng lúc:
Task: "Test ngưỡng theo pha trong backend/StockRadar.Tests/SellExit/BlueSkyThresholdTests.cs"
Task: "Test cắt lỗ và phủ nhận trong backend/StockRadar.Tests/SellExit/BlueSkyStopTests.cs"
Task: "Test cửa sổ mốc tham chiếu trong backend/StockRadar.Tests/SellExit/AnchorWindowTests.cs"
Task: "Test chặn theo cửa sổ bán trong backend/StockRadar.Tests/SellExit/SellWindowTests.cs"
```

---

## Implementation Strategy

### MVP First (chỉ US1)

1. Phase 1: Setup
2. Phase 2: Foundational — bắt buộc, chặn mọi thứ
3. Phase 3: US1
4. **DỪNG và KIỂM CHỨNG**: chạy ma trận test US1 trong `quickstart.md`, theo dõi một phiên thật
5. Tới đây lỗ hổng "không có cắt lỗ" đã được bịt — có thể dừng và quan sát trước khi đi tiếp

### Incremental Delivery

1. Setup + Foundational → nền tảng sẵn
2. US1 → kiểm chứng độc lập → chạy thật (MVP)
3. US2 → kiểm chứng độc lập → chạy thật
4. US3 → kiểm chứng độc lập → chạy thật
5. Polish: dọn tham số cũ, cập nhật docs, đối chứng backtest

### Lưu ý riêng của feature này

- T044 (đối chứng backtest) nên chạy **trước** khi bật luật mới trên production, dù nó nằm ở phase cuối; kết quả có thể buộc chỉnh lại hệ số Unfavorable ở T002.
- T041 chỉ làm sau khi cả ba story xong, tránh xoá tham số mà nhánh chưa viết còn cần.

---

## Notes

- `[P]` = khác file, không phụ thuộc
- Luật thoát lệnh nằm trong hàm thuần `EvaluatePositionSignal` — ưu tiên phủ test ở tầng này thay vì test tích hợp nặng
- Commit theo từng task hoặc nhóm task hợp lý
- Không refactor kèm: giữ nguyên nhánh phân phối, cooldown và toàn bộ luồng mua
