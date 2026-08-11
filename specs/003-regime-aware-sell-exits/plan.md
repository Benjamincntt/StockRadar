# Implementation Plan: Điểm bán 1/2 theo bối cảnh giá (nền trên vs vượt đỉnh)

**Branch**: `003-regime-aware-sell-exits` | **Date**: 2026-08-10 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-regime-aware-sell-exits/spec.md`

## Summary

Thay mô hình thoát lệnh đơn nhất (trailing từ peak kể từ entry, khoá sau khi lãi 3%, mẫu số là giá vốn) bằng mô hình **hai chế độ chốt tại thời điểm mở vị thế**:

- **Có nền trên** — còn hộp tích lũy phía trên giá vào: bán 1 nửa tại cạnh dưới nền, phần còn lại bán hết khi bị đẩy ngược hoặc chuyển sang chế độ Vượt đỉnh khi giá vượt nền.
- **Vượt đỉnh** — mặc định khi không có nền trên: bán 1 nửa khi giá thấp hơn mốc tham chiếu 4%, bán hết khi thấp hơn 6%, nhân hệ số pha; bán hết ngay khi thủng đáy cây nến vượt đỉnh.

Mốc tham chiếu có **một** định nghĩa dùng chung: giá cao nhất trong phiên của 20 phiên gần nhất, không lùi xa hơn ngày mua. Hệ số pha đảo chiều so với hiện tại (Favorable 1.25 / Neutral 1.0 / Unfavorable 0.75) và lấy theo **pha phiên hiện tại**, không phải pha lúc mua.

Cách tiếp cận kỹ thuật: giữ nguyên kiến trúc hiện có (monitor 60s → `TopOpportunityVipAlertPublisher.ProcessPositionAsync` → `TopOpportunityVipAlertEvaluator.EvaluatePositionSignal` thuần hàm), mở rộng ba bề mặt — bộ dò hộp trong Domain, bản ghi vị thế trong Infrastructure, và bảng tham số trong Application.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0` trên cả 5 project backend)

**Primary Dependencies**: Entity Framework Core (SQL Server), `Microsoft.Extensions.Options`, `TelegramNotifier` nội bộ, `KbsPriceBoardClient` cho giá trong phiên

**Storage**: SQL Server — bảng `MasterAlertPositions` (mở rộng cột), `VipAlertFires` (log đối chứng)

**Testing**: xUnit 2.9.3 trong `backend/StockRadar.Tests`; trọng tâm là unit test cho hàm thuần `EvaluatePositionSignal` và bộ dò hộp phía trên

**Target Platform**: API .NET chạy nền trên Windows Server, dev local `:5280`

**Project Type**: Web service (monorepo backend); tính năng này không chạm `mobile/` hay `frontend/`

**Performance Goals**: Đánh giá vị thế nằm trong vòng quét ~60s cho toàn bộ universe; chi phí thêm cho mỗi vị thế phải là O(1) nhờ prefetch một lần mỗi phiên, không truy vấn lịch sử trong vòng lặp

**Constraints**: Không đổi Buy Score, cổng Top, luật vào lệnh, hệ chấm điểm sóng hồi; giữ ràng buộc chỉ bán từ phiên thứ ba; tham số dò hộp dùng cho breakout phải giữ nguyên giá trị hiện hành

**Scale/Scope**: Vài chục vị thế mở đồng thời, universe vài trăm mã, lịch sử ~250 phiên mỗi mã

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*
*Source: `.specify/memory/constitution.md` v1.0.1*

- [x] **I. Code as truth**: Plan trích đúng file entry đã đọc — `TopOpportunityVipAlertEvaluator.cs`, `TopOpportunityVipAlertPublisher.cs`, `OpportunityIntradayMonitorRunner.cs`, `DarvasBreakoutAnalyzer.cs`, `BaseQualityEvaluator.cs`, `MasterAlertOptions.cs`, `PerformanceEntities.cs`. Một khẳng định sớm trong quá trình phân tích ("pha truyền vào là pha live") đã được sửa lại theo code: runner đang truyền `pos.MarketPhaseAtEntry`.
- [x] **II. Spec-first**: `spec.md` đã hoàn tất, ba điểm mơ hồ đã chốt trực tiếp với người dùng, checklist `requirements.md` đạt toàn bộ trước khi lập plan.
- [x] **III. Minimal surface**: Không refactor luồng mua, không đổi tên kiểu hiện có, không đụng nhánh phân phối và cooldown. Kind cảnh báo giữ nguyên `BanNua` / `BanHet` / `CanhBaoRuiRoT0`.
- [x] **IV. Domain gates**: Đây là thay đổi hợp đồng cảnh báo bán → cùng change set phải cập nhật `docs/domain/buy-decision.md` và `docs/architecture.md` (bảng tham số `MarketPhaseMultipliers`). Không đụng `MarketWyckoffPhase` vs `MarketRegime`.
- [x] **V. Simplicity**: Không thêm dependency. Bộ dò hộp phía trên **dùng lại** `BaseQualityEvaluator.PassesDarvasBox` qua một entry point mới trên `DarvasBreakoutAnalyzer`, không dựng engine song song. Cache lịch sử theo mẫu `VipPullbackMaCache` đã có.
- [x] **Stack**: Luật thoát nằm ở Domain/Notifications evaluator (hàm thuần), điều phối ở Infrastructure runner, tham số ở Application Options, không đẩy luật xuống tầng chỉ-UI. Sau khi xong chạy `backend/restart-api.ps1`.

## Project Structure

### Documentation (this feature)

```text
specs/003-regime-aware-sell-exits/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── internal-contracts.md
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit-tasks — chưa tạo)
```

### Source Code (repository root)

```text
backend/
├── StockRadar.Application/
│   ├── Options/MasterAlertOptions.cs          # Tham số ngưỡng + hệ số pha đảo chiều
│   └── Abstractions/IPerformanceServices.cs   # MasterAlertPositionRecord + repo contract
├── StockRadar.Domain/
│   ├── Services/DarvasBreakoutAnalyzer.cs     # Entry point liệt kê hộp phía trên một mức giá
│   └── ValueObjects/DarvasBoxSettings.cs      # Tham số dò vùng cản (tách khỏi breakout)
├── StockRadar.Infrastructure/
│   ├── Notifications/TopOpportunityVipAlertEvaluator.cs   # EvaluatePositionSignal — luật hai chế độ
│   ├── Notifications/TopOpportunityVipAlertPublisher.cs   # Phân loại lúc mở vị thế, nội dung tin
│   ├── Notifications/VipTelegramMessageFormatter.cs       # Nêu chế độ + mốc + ngưỡng
│   ├── Notifications/VipOverheadBaseCache.cs              # MỚI — prefetch nền trên theo phiên
│   ├── MarketData/OpportunityIntradayMonitorRunner.cs     # Truyền pha phiên hiện tại
│   ├── Persistence/Entities/PerformanceEntities.cs        # Cột mới trên MasterAlertPositionEntity
│   ├── Persistence/Repositories/EfPerformanceRepositories.cs
│   └── Migrations/                                        # Migration cột mới + model snapshot
├── StockRadar.Api/appsettings.json                        # Bộ tham số mới
└── StockRadar.Tests/                                      # Unit test luật thoát + dò nền

docs/
├── domain/buy-decision.md      # Cập nhật luật bán (bắt buộc cùng change set)
└── architecture.md             # Bảng tham số MasterAlerts
```

**Structure Decision**: Chỉ chạm backend. Domain nhận thêm khả năng liệt kê hộp; Infrastructure giữ toàn bộ điều phối và trạng thái vị thế; Application chỉ mở rộng Options và record. `mobile/` và `frontend/` không đổi vì tính năng chỉ phát qua Telegram và không thêm route API.

### Re-check sau Phase 1

Thiết kế không phát sinh vi phạm mới. Ba điểm được xác nhận lại sau khi dựng `data-model.md` và `contracts/`:

- **III. Minimal surface**: `MasterAlertPositionRecord` là `record` positional nên thêm trường sẽ chạm mọi nơi khởi tạo; đặt trường mới ở cuối kèm giá trị mặc định để diff không lan sang luồng mua.
- **IV. Domain gates**: `contracts/internal-contracts.md` ràng buộc `Analyze` và `Evaluate` của bộ dò hộp phải cho kết quả y hệt trước/sau — cổng Top và flatBox không đổi hành vi.
- **V. Simplicity**: Không thêm dependency; hai bổ sung duy nhất là một entry point trên engine sẵn có và một cache sao khuôn `VipPullbackMaCache`.

## Complexity Tracking

> Không có vi phạm Constitution Check cần biện minh.

Hai điểm đáng ghi chú nhưng không phải vi phạm:

| Quyết định | Vì sao chấp nhận |
|-----------|------------------|
| Thêm cache mới `VipOverheadBaseCache` | Sao chép đúng khuôn `VipPullbackMaCache` đã tồn tại cho cùng mục đích (prefetch lịch sử một lần mỗi phiên), không phải abstraction mới; gộp vào cache MA sẽ trộn hai concern khác vòng đời |
| Thêm 5 cột vào `MasterAlertPositions` | Trạng thái chế độ phải bền qua restart API và nhiều instance, giống lý do bảng này ra đời; giữ trong bộ nhớ sẽ tái lập đúng lỗi mà guard SQL hiện tại đang chống |
