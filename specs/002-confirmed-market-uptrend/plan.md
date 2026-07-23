# Implementation Plan: Xác nhận Uptrend thị trường (ClassifyMarket)

**Branch**: `002-confirmed-market-uptrend` | **Date**: 2026-07-23 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-confirmed-market-uptrend/spec.md`

## Summary

Thay phân loại pha Top tăng trưởng từ **% một phiên index** (`ChangePercent > 0.5` → Favorable → MA Full) bằng bộ điều kiện **xác nhận uptrend**: Close > MA20, slope MA20 không xuống, Follow-Through Day (ngày 4–7), Higher Low. Map nghiệp vụ Correction / Attempted Rally / Favorable lên enum hiện có `Unfavorable` / `Neutral` / `Favorable`. Chỉ Favorable mới MA **Full**; Attempted → Medium + copy UX “Chờ xác nhận thị trường chung”; không đụng `MarketRegime` sóng hồi. Cập nhật living `docs/domain/ma-stack-and-market-phase.md` cùng change set.

## Technical Context

**Language/Version**: C# (.NET 8) Domain/Infra/Api; Dart (Flutter) chỉ copy nhãn nếu cần; docs Markdown  

**Primary Dependencies**: Stack hiện có — không thêm NuGet/package mới  

**Storage**: Tận dụng `MarketIndices.HistoryJson` (OHLCV + Volume VNINDEX) đã có; không migration schema bắt buộc  

**Testing**: `backend/StockRadar.Tests` (xUnit) — classifier thuần Domain + regression message/MA mapping  

**Target Platform**: StockRadar API (dev `:5280` / prod) + mobile Home Top (đọc `tradeStateReason`)  

**Project Type**: Monorepo — Domain engine + Infra wiring (`DailyAnalysisRunner` / backtest) + docs  

**Performance Goals**: Phân loại pha O(n) trên ~60–200 nến index mỗi lần analysis — không đáng kể so quét universe  

**Constraints**: Hiến pháp II/IV — Spec Kit đã có; tối thiểu xâm lấn; Wyckoff phase ≠ Reversal regime; restart API sau backend  

**Scale/Scope**: 1 classifier Domain mới (hoặc mở rộng selector); sửa `BuildContext` / callers có index history; TradeState/gate message; docs; tests. Không đổi Buy Score 9 tiêu chí, không đổi ReversalBounce.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*  
*Source: `.specify/memory/constitution.md` v1.0.1*

- [x] **I. Code as truth**: Entry: `SmartMoneyOpportunitySelector.ClassifyMarket`, `BuyDecisionEngine.ResolveMaStackStrictness` / `ResolveTopGateFailure`, `TradeStateResolver`, `DailyAnalysisRunner.BuildContext`, `MarketBreadthRunner.LoadIndexHistoryAsync` (pattern HistoryJson), `SignalAnalyzer.Ma20SlopeNonNegative`, `docs/domain/ma-stack-and-market-phase.md`
- [x] **II. Spec-first**: `specs/002-confirmed-market-uptrend/spec.md` + checklist PASS trước plan này
- [x] **III. Minimal surface**: Không rename enum hàng loạt; không refactor ReversalBounce; không đổi VIP T+3
- [x] **IV. Domain gates**: Đổi pha→MA + living docs + specs cùng change set; không gộp `MarketRegime`
- [x] **V. Simplicity**: Một classifier Domain thuần; config ngưỡng trong Options hiện có / mở rộng `SmartMoney` — không service song song vô ích (xem Complexity nếu thêm options class)
- [x] **Stack**: Domain logic → Infra load history → Api/options; mobile chỉ nếu copy cứng; `restart-api.ps1` sau ship backend

**Post–Phase 1**: Vẫn PASS — contracts chỉ pha/nhãn/MA mapping; không API route mới bắt buộc.

## Project Structure

### Documentation (this feature)

```text
specs/002-confirmed-market-uptrend/
├── plan.md              # This file
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1
└── tasks.md             # /speckit-tasks (chưa tạo)
```

### Source Code (repository root)

```text
backend/StockRadar.Domain/
  Services/          # MarketPhaseClassifier (mới) + SmartMoneyOpportunitySelector + BuyDecisionEngine + TradeStateResolver
  ValueObjects/      # SmartMoneySettings / thresholds nếu cần
backend/StockRadar.Application/Options/   # SmartMoneyOptions ngưỡng FTD/HL (optional)
backend/StockRadar.Infrastructure/MarketData/
  DailyAnalysisRunner.cs
  SmartMoneyBacktestRunner.cs            # BuildIndexAt / ClassifyMarket parity
backend/StockRadar.Tests/                # MarketPhaseClassifierTests (+ message tests)
docs/domain/ma-stack-and-market-phase.md
docs/domain/buy-decision.md              # chéo UX Attempted nếu cần
mobile/lib/                              # chỉ nếu nhãn DNA “TT trung tính” → “Nỗ lực hồi phục” (tuỳ tasks)
```

**Structure Decision**: Logic phân loại pha + FTD/HL nằm **Domain**; Infra chỉ nạp `HistoryJson` VNINDEX vào `BuildContext`; UI mobile tái sử dụng `tradeStateReason` từ API (đổi chuỗi backend là đủ cho FR-008 trừ khi DNA label cần đổi client-side).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Thêm type/classifier Domain riêng thay vì nhồi hết vào `ClassifyMarket` 10 dòng | FTD + Higher Low + ngày 1 đợt hồi cần test thuần và tái dùng backtest | Inline 100+ dòng trong selector khó test và vi phạm V nếu phình selector |
| Options ngưỡng FTD trong `SmartMoney` (hoặc nested) | Spec cho phép tinh chỉnh 1.2% / cửa sổ mà không hard-code rải rác | Chỉ hard-code constants — OK MVP; Options nếu tasks chọn cấu hình hóa |
