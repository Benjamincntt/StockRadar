---
description: "Task list for feature 005 — điều chỉnh giá theo sự kiện quyền"
---

# Tasks: Điều chỉnh giá theo sự kiện quyền

**Input**: Design documents from `specs/005-ohlcv-corporate-adjust/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/dieu-chinh-gia.md, quickstart.md

**Tests**: Có — spec Independent Test + FR-008/FR-009 + quickstart. Viết test trước, fail, rồi implement.

**Organization**: Setup → Foundational (chặn mọi story) → US1 → US2 → US3 → Polish.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Chạy song song được (file khác, không phụ thuộc task chưa xong)
- **[Story]**: [US1] / [US2] / [US3] — chỉ phase user story
- Mỗi task có đường dẫn file cụ thể

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Seed + cấu hình đường dẫn; chưa có logic điều chỉnh

- [x] T001 Tạo seed v1 SSI 17/08/2026 (`tienMat` 1.0, `heSoPhaLoang` 1.2) trong `backend/StockRadar.Api/Data/su-kien-quyen.json` đúng schema `specs/005-ohlcv-corporate-adjust/contracts/dieu-chinh-gia.md`
- [x] T002 `backend/StockRadar.Api/StockRadar.Api.csproj`: Content `Data/su-kien-quyen.json` CopyToOutputDirectory PreserveNewest
- [x] T003 [P] Tạo `backend/StockRadar.Application/Options/SuKienQuyenOptions.cs` (SectionName + FilePath, không secret)
- [x] T004 [P] Thêm section đường dẫn file trong `backend/StockRadar.Api/appsettings.json` (và Development nếu project đang override)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Thực thể, công thức, nguồn file, `LayLichSuChamDiem` — bắt buộc xong trước mọi user story

**⚠️ CRITICAL**: Không bắt đầu US1–US3 trước khi phase này xong

- [x] T005 [P] Tạo record `SuKienQuyen` (`ma`, `ngayKhongHuongQuyen`, `tienMat`, `heSoPhaLoang`) trong `backend/StockRadar.Domain/ValueObjects/SuKienQuyen.cs`
- [x] T006 [P] Tạo `INguonSuKienQuyen` trong `backend/StockRadar.Domain/Services/INguonSuKienQuyen.cs` (tra theo mã; kèm `NguonRong` cho test)
- [x] T007 Implement `TinhGiaThamChieu` / `TinhHeSoNgayQuyen` / `TaoDayGiaDieuChinh` trong `backend/StockRadar.Domain/Services/BoDieuChinhGiaTheoQuyen.cs` (hệ số từ Close **thô** phiên trước quyền; Volume không đổi; nến cuối không nhân)
- [x] T008 Implement đọc JSON + cache + từ chối dòng thiếu `ma`/`ngayKhongHuongQuyen` hoặc `heSoPhaLoang` ≤ 0 (log, không crash) trong `backend/StockRadar.Infrastructure/SuKienQuyen/FileNguonSuKienQuyen.cs`
- [x] T009 Đăng ký `INguonSuKienQuyen` → `FileNguonSuKienQuyen` và `BoDieuChinhGiaTheoQuyen` trong `backend/StockRadar.Infrastructure/DependencyInjection.cs`; `Configure<SuKienQuyenOptions>` trong `backend/StockRadar.Application/DependencyInjection.cs`
- [x] T010 Thêm `LayLichSuChamDiem(Stock)` vào `backend/StockRadar.Domain/Services/IAnalysisServices.cs` và `backend/StockRadar.Domain/Services/SignalAnalyzer.cs` (giữ ctor không tham số = `NguonRong` để test hiện tại `new SignalAnalyzer()` không vỡ)

**Checkpoint**: Domain công thức + nguồn file + DI sẵn; chưa đổi % sóng ngành / Evaluate

---

## Phase 3: User Story 1 - % và RS không bị rớt giả ngày quyền (Priority: P1) 🎯 MVP

**Goal**: `GetChangePercent(Stock)` / RS 5 phiên / % 1 phiên sóng ngành dùng dãy điều chỉnh. SSI 17/08 không còn ≈ −19% / RS ≈ −17.6 chỉ vì gap quyền.

**Independent Test**: `dotnet test --filter FullyQualifiedName~DieuChinhGia` — SSI 14/08 Close 24.5 → 17/08 ≈ 19.8 cho lợi suất ≈ +1% ±1 điểm; không seed thì trùng thô.

### Tests for User Story 1

> Viết trước, để fail, rồi mới sửa engine

- [x] T011 [US1] Viết test fail SSI: `giaThamChieu` ≈ 19.58; `GetChangePercent(stock, 1)` ≈ +1 không ≈ −19; không sự kiện ⇒ trùng thô — `backend/StockRadar.Tests/DieuChinhGia/DieuChinhGiaTheoQuyenTests.cs`
- [x] T012 [P] [US1] Viết test fail RS 5 phiên SSI không còn ≈ −17.6 chỉ vì 17/08 — `backend/StockRadar.Tests/DieuChinhGia/RsSsiSauQuyenTests.cs`

### Implementation for User Story 1

- [x] T013 [US1] `backend/StockRadar.Domain/Services/SignalAnalyzer.cs`: `GetChangePercent(Stock)` / `GetRelativeStrength(Stock)` dùng `LayLichSuChamDiem`; overload `IReadOnlyList` giữ thô
- [x] T014 [US1] `backend/StockRadar.Domain/Services/SmartMoneyOpportunitySelector.cs`: đổi `GetChangePercent(s.History, 1)` thành `GetChangePercent(s, 1)` trong `BuildSectorSnapshots`
- [x] T015 [US1] Chạy pass T011–T012; không đổi ngưỡng 4 trục sóng trong `backend/StockRadar.Domain/Services/SmartMoneyOpportunitySelector.cs`

**Checkpoint**: US1 testable độc lập. Sóng ngành hết fail RS **chỉ vì** gap SSI. FOMO/hộp Evaluate vẫn có thể thô cho đến US2.

---

## Phase 4: User Story 2 - Mọi cổng % giá cùng dãy điều chỉnh (Priority: P1)

**Goal**: Buy Score / FOMO / hộp / MA trong `Evaluate`, flatBox detail, Darvas alert dùng `LayLichSuChamDiem`. Chart/last/Job 2 vẫn thô.

**Independent Test**: Nến cuối OHLC = thô; Volume không đổi; % so đỉnh hộp không lấy Close thô hai phía GDKHQ; `StockService` last = giá sàn.

### Tests for User Story 2

- [x] T016 [US2] Viết test fail: nến cuối không đổi; Volume không đổi; FOMO/biên hộp không dùng hai Close thô qua quyền — `backend/StockRadar.Tests/DieuChinhGia/DayGiaChamDiemTests.cs`
- [x] T017 [P] [US2] Viết test fixture SC-005 (sự kiện thứ hai, gap thô ≠ lợi suất điều chỉnh) — `backend/StockRadar.Tests/DieuChinhGia/SuKienThuHaiTests.cs`

### Implementation for User Story 2

- [x] T018 [US2] `backend/StockRadar.Domain/Services/BuyDecisionEngine.cs`: đầu `Evaluate` gán `stock = stock with { History = signals.LayLichSuChamDiem(stock) }` rồi mới `DetectSignals` / `AnalyzeFlatBox` / MA
- [x] T019 [P] [US2] `backend/StockRadar.Application/Services/StockService.cs`: `AnalyzeFlatBox` / `CalculatePriceLevels` trên `LayLichSuChamDiem`; chart/`IChartBarProvider` giữ `match.History` thô
- [x] T020 [P] [US2] `backend/StockRadar.Infrastructure/Notifications/DarvasBreakoutAlertPublisher.cs`: `AnalyzeFlatBox` trên dãy chấm điểm
- [x] T021 [US2] Chạy pass T016–T017; grep `EntityMapper` không nhân hệ số; Job 2 không đụng `backend/StockRadar.Infrastructure/Persistence/Mapping/EntityMapper.cs`

**Checkpoint**: US1+US2. Giá last/chart thô; engine %/hộp cùng thang.

---

## Phase 5: User Story 3 - Sự kiện quyền ghi nhận và kiểm chứng được (Priority: P2)

**Goal**: Tra được SSI 17/08 trong seed; dòng thiếu trường / sai đơn vị không áp im lặng.

**Independent Test**: Tra seed SSI đủ `tienMat` 1.0 + `heSoPhaLoang` 1.2 ngày 17/08/2026; nạp dòng hỏng → dãy thô + log.

### Tests for User Story 3

- [x] T022 [US3] Viết test fail: thiếu ngày; `heSoPhaLoang` ≤ 0; `tienMat` = 1000 không cho `(24.5 − 1000)` — `backend/StockRadar.Tests/DieuChinhGia/NguonSuKienQuyenTests.cs`

### Implementation for User Story 3

- [x] T023 [US3] Hoàn thiện từ chối + log trong `backend/StockRadar.Infrastructure/SuKienQuyen/FileNguonSuKienQuyen.cs` và `backend/StockRadar.Domain/Services/BoDieuChinhGiaTheoQuyen.cs` (thiếu `giaTruocQuyen` > 0 → bỏ sự kiện)
- [x] T024 [US3] File hỏng/thiếu → nguồn rỗng, không crash job; log tại `backend/StockRadar.Infrastructure/SuKienQuyen/FileNguonSuKienQuyen.cs`
- [x] T025 [US3] Chạy pass T022; xác nhận seed SSI trong `backend/StockRadar.Api/Data/su-kien-quyen.json`

**Checkpoint**: P2 ops: sửa JSON → ship/restart, không sửa engine

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Living docs từ “chưa land” → hành vi đã land; quickstart; restart API

- [x] T026 [P] Cập nhật `docs/domain/buy-decision.md` (G-BD-6 + RS/sóng ngành: % dùng dãy chấm điểm; last vẫn thô)
- [x] T027 [P] Cập nhật `docs/domain/pipeline-jobs.md` (G-PL-3: Job 2 thô; điều chỉnh lúc tính %)
- [x] T028 [P] Cập nhật `docs/domain/base-price-flatbox.md` (G-FB-3 FOMO trên dãy chấm điểm)
- [x] T029 [P] Cập nhật `docs/features/sector-wave-entry-patterns/spec.md` (RS đầu vào đã điều chỉnh; ngưỡng 4 trục không đổi)
- [x] T030 [P] Cập nhật `docs/README.md` (bỏ/sửa mục “Đang Spec Kit chưa land” cho 005)
- [x] T031 [P] Cập nhật `CLAUDE.md` và `.continue/rules/stockradar.md` (điều chỉnh quyền đã land, runtime không còn Close thô cho %)
- [x] T032 Chạy `specs/005-ohlcv-corporate-adjust/quickstart.md`: `dotnet test backend/StockRadar.Tests/StockRadar.Tests.csproj --filter FullyQualifiedName~DieuChinhGia` và hồi quy `FullyQualifiedName~SectorWave`
- [x] T033 Restart API bằng `backend/restart-api.ps1` (không ship production)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Bắt đầu ngay
- **Foundational (Phase 2)**: Sau Setup — **chặn** US1–US3
- **US1 (Phase 3)**: Sau Phase 2 — MVP
- **US2 (Phase 4)**: Sau Phase 2; nên sau US1 vì dùng cùng `LayLichSuChamDiem` (T018 đụng `BuyDecisionEngine`, T013 đụng `SignalAnalyzer` — tuần tự)
- **US3 (Phase 5)**: Sau Phase 2; T023 đụng `FileNguonSuKienQuyen.cs` như T008 — làm giàu validation, không song song T008
- **Polish**: Sau US1–US3 muốn land

### User Story Dependencies

- **US1 (P1)**: Không phụ thuộc US2/US3. T013 rồi T014 (cùng ý % nhưng hai file — T014 [không P với T013] vì T013 phải có trước để `GetChangePercent(s)` đúng)
- **US2 (P1)**: Cần `LayLichSuChamDiem` (T010) + tốt nhất T013. T018 / T019 / T020 file khác — T019 và T020 [P] với nhau sau T018
- **US3 (P2)**: Cần T008; test T022 song song được với US2 nếu T008 đã đủ khung

### Parallel Opportunities

- T003, T004 song song sau khi biết section name
- T005, T006 song song
- T011, T012 song song (hai file test)
- T016, T017 song song
- T019, T020 song song sau T018
- T026–T031 song song (docs khác file; T031 hai file — làm một task)

---

## Parallel Example: User Story 1

```text
Task: "Viết test fail SSI trong backend/StockRadar.Tests/DieuChinhGia/DieuChinhGiaTheoQuyenTests.cs"
Task: "Viết test fail RS SSI trong backend/StockRadar.Tests/DieuChinhGia/RsSsiSauQuyenTests.cs"
```

Sau đó tuần tự: `SignalAnalyzer.cs` → `SmartMoneyOpportunitySelector.cs` → chạy test.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 Setup
2. Phase 2 Foundational
3. Phase 3 US1
4. **STOP**: `dotnet test --filter FullyQualifiedName~DieuChinhGia` — SSI %/RS sạch
5. Demo: sóng ngành không fail RS chỉ vì gap SSI (không cam kết nhãn Sóng mạnh)

### Incremental Delivery

1. Setup + Foundational
2. US1 → MVP %/RS
3. US2 → FOMO/hộp/Evaluate/detail/alert
4. US3 → seed cứng + từ chối dòng hỏng
5. Polish docs + restart API

### Parallel Team Strategy

Một người: US1 rồi US2 rồi US3 (tránh conflict `SignalAnalyzer.cs` / `FileNguonSuKienQuyen.cs`). Hai người: A làm T011–T015, B làm T016–T017 (test US2) rồi merge T018.

---

## Notes

- Identifier mới tiếng Việt không dấu; giữ `GetChangePercent`, `OhlcvBar`, DTO API
- Không crawler, không migration EF, không đụng `mobile/` `frontend/`
- `tienMat` = 1.0 cho 1.000đ — cấm 1000
- Không cam kết phiên 21/08 thành Sóng mạnh
- Backend xong → `restart-api.ps1`; user tự `ship-all.ps1` nếu muốn production
