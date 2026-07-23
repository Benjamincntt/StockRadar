# Tasks: Xác nhận Uptrend thị trường (ClassifyMarket)

**Input**: Design documents from `/specs/002-confirmed-market-uptrend/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Có — plan yêu cầu xUnit Domain classifier + regression message/MA (`backend/StockRadar.Tests`)

**Organization**: Tasks theo user story để implement/test độc lập

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Song song được (file khác, không phụ thuộc task chưa xong)
- **[Story]**: US1…US5
- Mỗi task có đường dẫn file cụ thể

## Path Conventions

```text
backend/StockRadar.Domain/Services/
backend/StockRadar.Application/Options/
backend/StockRadar.Infrastructure/MarketData/
backend/StockRadar.Tests/
docs/domain/
specs/002-confirmed-market-uptrend/
```

---



## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Chuẩn bị ngưỡng + khung classifier theo plan/research

- [X] T001 Thêm value object ngưỡng `MarketPhaseThresholds` (defaults FTD 1.2%, ngày 4–7, HL lookback 60, pivot 2, slope 3) trong `backend/StockRadar.Domain/` (file mới cạnh settings hiện có, vd. `ValueObjects/` hoặc cùng file settings SmartMoney Domain)
- [X] T002 [P] Mở rộng `SmartMoneyOptions` / `SmartMoneySettings` map optional `MarketPhase` trong `backend/StockRadar.Application/Options/SmartMoneyOptions.cs` + `backend/StockRadar.Api/appsettings.json` (defaults khớp contracts)
- [X] T003 [P] Tạo skeleton `MarketPhaseClassifier` + result VO `MarketPhaseClassification` trong `backend/StockRadar.Domain/Services/MarketPhaseClassifier.cs` (method `Classify` chưa đủ logic — chỉ chữ ký + stub Unfavorable nếu history rỗng)

---



## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Nạp index history vào context Top — chặn mọi story dùng ClassifyMarket mới

**⚠️ CRITICAL**: Không bắt đầu US1–US5 logic Favorable đầy đủ cho đến khi BuildContext nhận được history

- [X] T004 Đổi `SmartMoneyOpportunitySelector.BuildContext` nhận `IReadOnlyList<OhlcvBar> indexHistory` (hoặc tương đương) và gọi `MarketPhaseClassifier` thay `ClassifyMarket(index.Trend)` trong `backend/StockRadar.Domain/Services/SmartMoneyOpportunitySelector.cs`
- [X] T005 Wire load `VNINDEX` `HistoryJson` trong `backend/StockRadar.Infrastructure/MarketData/DailyAnalysisRunner.cs` (pattern giống `MarketBreadthRunner.LoadIndexHistoryAsync`) trước `BuildContext`
- [X] T006 [P] Parity backtest: truyền index history vào classify trong `backend/StockRadar.Infrastructure/MarketData/SmartMoneyBacktestRunner.cs` (không còn Favorable chỉ vì % một phiên tại `BuildIndexAt` cho pha Top)
- [X] T007 Đăng ký DI nếu cần interface classifier trong `backend/StockRadar.Infrastructure/DependencyInjection.cs` (hoặc static thuần — chọn một, ghi chú trong PR/commit message tasks)

**Checkpoint**: Analysis/backtest build được với history; pha có thể vẫn stub — sẵn sàng US

---



## Phase 3: User Story 1 — Không Favorable khi chỉ nảy kỹ thuật (Priority: P1) 🎯 MVP

**Goal**: Close < MA20 hoặc thiếu FTD → không Favorable; không bật MA Full chỉ vì % phiên > 0.5

**Independent Test**: Fixture index 1–3 phiên xanh / dưới MA20 / không FTD → `Unfavorable` hoặc `Neutral`; `ResolveMaStackStrictness` ≠ Full

### Tests for User Story 1

- [X] T008 [P] [US1] Thêm tests Close < MA20 → Unfavorable và nảy ngắn không FTD → không Favorable trong `backend/StockRadar.Tests/MarketPhase/MarketPhaseClassifierTests.cs`



### Implementation for User Story 1

- [X] T009 [US1] Implement nhánh Correction: Close < MA20 → `Unfavorable` trong `backend/StockRadar.Domain/Services/MarketPhaseClassifier.cs`
- [X] T010 [US1] Implement nhánh thiếu FTD / thiếu xác nhận → `Neutral` (Attempted) khi Close ≥ MA20 nhưng chưa đủ Favorable trong cùng file classifier
- [X] T011 [US1] Xóa/ngừng dùng `ChangePercent > 0.5` → Favorable trong đường Top (`SmartMoneyOpportunitySelector` private `ClassifyMarket` cũ) — đảm bảo không còn call path Favorable từ Trend một phiên
- [X] T012 [US1] Xác nhận `BuyDecisionEngine.ResolveMaStackStrictness` với phase Neutral/Unfavorable không trả Full khi config mặc định (`backend/StockRadar.Domain/Services/BuyDecisionEngine.cs` — chỉ đọc/chỉnh nếu lệch)

**Checkpoint**: MVP — false Favorable từ nảy ngắn hết trên classifier + Top context

---



## Phase 4: User Story 2 — Favorable chỉ khi uptrend xác nhận (Priority: P1)

**Goal**: Favorable = Close > MA20 ∧ slope ∧ FTD ∧ Higher Low → MA Full

**Independent Test**: Fixture đủ điều kiện → Favorable + Full; thiếu một điều kiện → không Favorable

### Tests for User Story 2

- [X] T013 [P] [US2] Tests FTD hợp lệ (gain ≥1.2%, vol > prev & > TB20, ngày 4–7) và Higher Low pivot trong `backend/StockRadar.Tests/MarketPhase/MarketPhaseClassifierTests.cs`
- [X] T014 [P] [US2] Test thiếu FTD hoặc thiếu HL hoặc slope xuống → không Favorable dù phiên cuối +2% trong cùng file tests



### Implementation for User Story 2

- [X] T015 [US2] Implement MA20 + slope (lookback 3) trong `backend/StockRadar.Domain/Services/MarketPhaseClassifier.cs` (tái dùng tinh thần `SignalAnalyzer.Ma20SlopeNonNegative`)
- [X] T016 [US2] Implement đánh dấu ngày 1 đợt nỗ lực + phát hiện FTD theo `specs/002-confirmed-market-uptrend/research.md` trong cùng classifier
- [X] T017 [US2] Implement Higher Low (lookback 60, pivot radius 2) trong cùng classifier; Favorable chỉ khi đủ cả bộ FR-003
- [X] T018 [US2] Điền `MarketPhaseClassification` flags (`HasFollowThroughDay`, `HasHigherLow`, …) để log/tests trong Domain VO
- [X] T019 [US2] Log pha + flags khi analysis trong `backend/StockRadar.Infrastructure/MarketData/DailyAnalysisRunner.cs`

**Checkpoint**: Confirmed uptrend → Favorable → Full đúng contracts

---



## Phase 5: User Story 3 — Attempted Rally UX (Priority: P2)

**Goal**: Phase ≠ Favorable + cổng MA → reason “Chờ xác nhận thị trường chung”; Favorable vẫn hiện câu MA

**Independent Test**: Gate MA + Neutral trên list → reason mới; Favorable + MA fail → câu MA cũ

### Tests for User Story 3

- [X] T020 [P] [US3] Unit test rewrite reason MA khi phase ≠ Favorable trong `backend/StockRadar.Tests/MarketPhase/TradeStateOrGateMessageTests.cs` (hoặc mở rộng test BuyDecision/TradeState hiện có)



### Implementation for User Story 3

- [X] T021 [US3] Rewrite `GateFailure` / `TradeStateReason` khi phase ≠ Favorable và message chứa MA stack → `Chờ xác nhận thị trường chung` trong `backend/StockRadar.Domain/Services/BuyDecisionEngine.cs` và/hoặc `TradeStateResolver.cs` (một chỗ duy nhất, tránh double-rewrite)
- [X] T022 [US3] Đổi nhãn DNA Neutral từ “TT trung tính” → “Nỗ lực hồi phục” trong `backend/StockRadar.Domain/Services/HitProbabilityPredictor.cs` (và chỗ map label pha khác nếu Grep ra)
- [X] T023 [P] [US3] Kiểm tra mobile chỉ đọc `tradeStateReason` — không hard-code câu MA; chỉ sửa `mobile/lib/` nếu có copy cứng (Grep)

**Checkpoint**: Home Top Attempted không còn đổ lỗi MA Full giả Favorable

---



## Phase 6: User Story 4 — Correction / rủi ro (Priority: P2)

**Goal**: Close < MA20 → Unfavorable; không Full; fallback thận trọng

**Independent Test**: Fixture dưới MA20 → Unfavorable + Loose; không TT thuận

### Implementation for User Story 4

- [X] T024 [US4] Xác nhận mapping Unfavorable → Loose còn đúng config `MaStack.UnfavorableMode` trong `backend/StockRadar.Api/appsettings.json` + `ResolveMaStackStrictness`
- [X] T025 [US4] Siết hoặc ghi chú fallback khi Unfavorable: rà `RelaxedFallbackEnabled` / `FallbackMaxResults` trong `DailyAnalysisJobOptions` + `DailyAnalysisRunner.CollectRelaxedCandidates` — đảm bảo Correction không mở Top “giả Favorable” (chỉnh config hoặc filter phase nếu plan yêu cầu hạn chế)
- [X] T026 [P] [US4] Test Unfavorable + Loose trong `backend/StockRadar.Tests/MarketPhase/MarketPhaseClassifierTests.cs` (hoặc MA strictness test)

**Checkpoint**: Correction không Favorable+Full

---



## Phase 7: User Story 5 — Tách sóng hồi (Priority: P3)

**Goal**: Không đụng `MarketRegime` / ReversalBounce classifiers

**Independent Test**: Diff không sửa `MarketRegimeClassifier` / breadth regime semantics; Grep xác nhận

### Implementation for User Story 5

- [X] T027 [US5] Grep và xác nhận không đổi `backend/StockRadar.Domain/Services/ReversalBounce/MarketRegimeClassifier.cs` / `MarketBreadthAnalyzer.cs` trong scope feature (chỉ comment tách hệ nếu cần trong classifier tăng trưởng)
- [X] T028 [P] [US5] Thêm ghi chú một dòng trong `backend/StockRadar.Domain/Services/MarketPhaseClassifier.cs`: độc lập với `MarketRegime`
- [X] T029 [US5] Chạy smoke quickstart kịch bản 4 (regime endpoint) sau implement — ghi kết quả vào checklist Notes hoặc comment PR

**Checkpoint**: SC-005 giữ hợp đồng sóng hồi

---



## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Docs living + quickstart + đóng G-MA-1

- [X] T030 Cập nhật as-is + G-MA-1 **resolved** trong `docs/domain/ma-stack-and-market-phase.md`
- [X] T031 [P] Chéo UX Attempted / reason mới trong `docs/domain/buy-decision.md` nếu cần
- [X] T032 [P] Đồng bộ map ngắn `CLAUDE.md` / `.continue/rules/stockradar.md` nếu còn mô tả Favorable = % một phiên
- [X] T033 Chạy checklist `specs/002-confirmed-market-uptrend/quickstart.md` (kịch bản 1–3 tối thiểu) sau code xong
- [X] T034 Restart API bằng `backend/restart-api.ps1` sau thay đổi backend (auto-run rule)
- [X] T035 Đánh dấu SC-001…SC-006 / ghi Notes cuối `specs/002-confirmed-market-uptrend/checklists/requirements.md` hoặc cuối `docs/domain/ma-stack-and-market-phase.md`

---



## Dependencies & Execution Order



### Phase Dependencies

- **Setup (Phase 1)**: Bắt đầu ngay
- **Foundational (Phase 2)**: Sau Setup — **BLOCKS** mọi user story wiring
- **US1 (Phase 3)**: Sau Foundational — MVP
- **US2 (Phase 4)**: Sau US1 nhánh cơ bản (cùng classifier — tuần tự logic Favorable đầy đủ)
- **US3 (Phase 5)**: Sau pha thật (US1/US2) để rewrite reason có nghĩa
- **US4 (Phase 6)**: Phần lớn đã cover bởi US1 Correction; siết fallback sau classifier ổn
- **US5 (Phase 7)**: Song song kiểm chứng / cuối
- **Polish (Phase 8)**: Sau US1–US4 (docs phản ánh as-is mới)



### User Story Dependencies

- **US1**: Nền classifier + bỏ Trend→Favorable
- **US2**: Phụ thuộc US1 structure classifier (cùng file)
- **US3**: Phụ thuộc phase đúng từ US1/US2
- **US4**: Phụ thuộc nhánh Unfavorable US1; config fallback độc lập một phần
- **US5**: Soft — không phụ thuộc code US khác ngoài “không sửa Reversal”



### Parallel Opportunities

- T001 ∥ T002 ∥ T003 (sau khi thống nhất tên type)
- T005 ∥ T006 (sau T004 chữ ký BuildContext)
- T008 tests ∥ bắt đầu T009 nếu TDD fail-first
- T013 ∥ T014
- T022 ∥ T023
- T030 ∥ T031 ∥ T032

---



## Parallel Example: User Story 2

```text
# Song song tests:
Task: "T013 FTD tests …"
Task: "T014 missing-condition tests …"

# Tuần tự implement classifier:
Task: "T015 MA20+slope …"
Task: "T016 FTD …"
Task: "T017 Higher Low …"
```

---



## Implementation Strategy



### MVP First (User Story 1 + foundation)

1. Phase 1–2: thresholds + BuildContext + load HistoryJson
2. Phase 3 US1: không Favorable khi nảy / dưới MA20; MA không Full
3. **STOP** — validate tests T008 + analysis log phase
4. Tiếp US2 Favorable đầy đủ → US3 UX → US4 fallback → docs



### Incremental Delivery

1. Setup + Foundational
2. US1 MVP
3. US2 confirmed Favorable
4. US3 copy UX
5. US4 Correction/fallback
6. US5 + Polish docs/quickstart



### Notes

- Không đổi Buy Score 9 tiêu chí, không đổi ReversalBounce runtime  
- Enum giữ Favorable/Neutral/Unfavorable (map Attempted=Neutral, Correction=Unfavorable)  
- Commit theo checkpoint story khi user yêu cầu commit

