# Implementation Plan: Điều chỉnh giá theo sự kiện quyền

**Branch**: `005-ohlcv-corporate-adjust` | **Date**: 2026-08-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/005-ohlcv-corporate-adjust/spec.md`

## Summary

Mọi phép đo **% giá / RS / FOMO / hộp / cổng phiên** đang lấy Close thô (`SignalAnalyzer.GetChangePercent`) nên gap GDKHQ (SSI 17/08: 24.5→19.8) bị hiểu là dump. v1: đọc **file seed nhập tay**, áp **một công thức** (`tienMat` + `heSoPhaLoang`), tạo dãy OHLC **lùi về thang nến cuối** lúc chấm điểm. Job 1/2 và chart API **giữ nến thô**. Không crawler, không bảng SQL, không đổi ngưỡng sóng ngành.

## Technical Context

**Language/Version**: C# / .NET 10 (backend). Không đổi Dart/TS.

**Primary Dependencies**: BCL `System.Text.Json`. Không thêm NuGet.

**Storage**: File seed versioned trong repo (`Data/su-kien-quyen.json`), **không** bảng EF. OHLCV SQL/JSON history giữ thô.

**Testing**: xUnit `StockRadar.Tests` — neo SSI 17/08 + một sự kiện thứ hai (fixture). `dotnet test` filter theo tên lớp tiếng Việt.

**Target Platform**: API .NET (chấm điểm). Web/mobile không đổi hợp đồng.

**Project Type**: web-service trong monorepo hiện có

**Performance Goals**: Universe hàng trăm mã × ~250 nến × vài sự kiện/mã — tính lại mỗi `Evaluate` / RS chấp nhận được; không cache trừ khi đo được nóng.

**Constraints**: FR-003 giá last = giá sàn (nến cuối hệ số 1). Không ghi đè history. `tienMat` cùng thang Close (1.000đ = 1.0). ESOP ngoài v1. Không B/C.

**Scale/Scope**: Domain helper + nguồn file + vài chỗ gọi `History` thô còn sót (sóng ngành, Stock detail flatBox, Darvas alert). ~6–10 file backend + 1 JSON seed + test.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*
*Source: `.specify/memory/constitution.md` v1.0.1*

- [x] **I. Code as truth**: Entry đã đọc trên disk: `SignalAnalyzer.GetChangePercent`, `BuyDecisionEngine.Evaluate` (`history = stock.History`), `SmartMoneyOpportunitySelector` (`GetChangePercent(s.History, 1)`), `EntityMapper.ToDomain`, `DarvasBreakoutAnalyzer` FOMO, `StockService` `AnalyzeFlatBox(match.History)`
- [x] **II. Spec-first**: `spec.md` đủ; không còn `[NEEDS CLARIFICATION]`
- [x] **III. Minimal surface**: Không rename `GetChangePercent`; không điều chỉnh trong `EntityMapper`/Job 2; chỉ thêm `TaoDayGiaDieuChinh` + nối chỗ chấm điểm
- [x] **IV. Domain gates**: Đầu vào %/RS/FOMO đổi — living docs đã trỏ spec 005 (chưa land). Cùng change set implement: cập nhật `buy-decision.md` / sóng ngành / pipeline / flatBox từ “chưa land” → hành vi mới. Không đổi ngưỡng 4 trục sóng. Wyckoff ≠ Reversal regime
- [x] **V. Simplicity**: Không project/DI framework mới. Một service thuần + một nguồn file
- [x] **Stack**: Luật ở Domain; đọc file ở Infrastructure; Application DI. Restart `backend/restart-api.ps1` sau land. Không đụng `mobile/` `frontend/`

**Post–Phase 1**: Cổng vẫn pass. Hợp đồng nội bộ (seed JSON + `LayLichSuChamDiem`) không lộ route mới.

## Project Structure

### Documentation (this feature)

```text
specs/005-ohlcv-corporate-adjust/
├── spec.md
├── plan.md              # file này
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── dieu-chinh-gia.md
└── tasks.md             # /speckit-tasks — chưa tạo
```

### Source Code (repository root)

```text
backend/
├── StockRadar.Api/Data/su-kien-quyen.json          # seed v1 (SSI 17/08 bắt buộc)
├── StockRadar.Api/appsettings.json                 # đường dẫn file (không secret)
├── StockRadar.Domain/
│   ├── ValueObjects/SuKienQuyen.cs                 # record mới
│   └── Services/BoDieuChinhGiaTheoQuyen.cs         # công thức + TaoDay
│   └── Services/INguonSuKienQuyen.cs
│   └── Services/IAnalysisServices.cs               # + LayLichSuChamDiem
│   └── Services/SignalAnalyzer.cs                  # GetChangePercent(Stock) dùng dãy điều chỉnh
│   └── Services/BuyDecisionEngine.cs               # Evaluate: stock with History = LayLichSuChamDiem
│   └── Services/SmartMoneyOpportunitySelector.cs   # GetChangePercent(s, 1) không còn s.History
├── StockRadar.Infrastructure/
│   └── SuKienQuyen/FileNguonSuKienQuyen.cs         # đọc JSON, từ chối dòng thiếu trường
│   └── Notifications/DarvasBreakoutAlertPublisher.cs  # AnalyzeFlatBox(dãy điều chỉnh)
├── StockRadar.Application/
│   └── DependencyInjection.cs                      # đăng ký nguồn + BoDieuChinh
│   └── Services/StockService.cs                    # flatBox / levels trên dãy chấm điểm; chart giữ thô
└── StockRadar.Tests/DieuChinhGia/                  # SSI + sự kiện thứ hai
```

**Structure Decision**: Chỉ backend. Điểm neo là dãy chấm điểm (`LayLichSuChamDiem`), không phải ghi đè OHLCV lưu kho. UI/chart tiếp tục nến thô.

## Complexity Tracking

Không có vi phạm hiến pháp cần biện minh.
