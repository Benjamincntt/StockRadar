# Phase 0A — Summary

> Audit read-only codebase StockRadar cho tính năng **ReversalBounce** (tìm đáy & đánh sóng hồi). Không sửa code, không migration, không deploy. Nguồn đối chiếu: `docs/features/reversal-bounce.md`.

## Trạng thái 4 ticket

| Ticket | Trạng thái | Quyết định |
|---|---|---|
| 0A.1 EarlyRecovery | DONE | **Tách riêng** — không tái dùng/không mở rộng; giữ EarlyRecovery như feed tham chiếu |
| 0A.2 OHLCV Schema | DONE | **Cần migration** — thêm `ReferencePrice` (+ tính Floor/Ceiling runtime hoặc lưu cột); OHLCV đang là JSON blob, không adjusted |
| 0A.3 Backtester | DONE | **Viết lại module fill riêng** cho ReversalBounce (không sửa `SmartMoneyBacktestRunner`) |
| 0A.4 Breadth/Regime | DONE | **Code mới phần lớn** — breadth + regime 4-state + hysteresis + snapshot + endpoint (giữ nguyên `MarketWyckoffPhase` 3-mức) |

## Tổng hợp gap ảnh hưởng đến kiến trúc

1. **Không có bảng OHLCV chuẩn hóa** (JSON blob `HistoryJson` trong `Stocks`). Hệ quả:
   - Không query/index theo `(Symbol, TradingDate)` → breadth backfill (0A.4) và detector as-of (0C) phải deserialize toàn bộ history mỗi lần.
   - Thiếu `ReferencePrice/FloorPrice/CeilingPrice` → **floor-lock proxy (spec §6.5) và gap-entry (§7.1) chưa chạy được** cho đến khi 0A.2 xong.
   - **Blocker xác minh**: chưa rõ KBS trả giá adjusted hay unadjusted — ảnh hưởng độ đúng floor-lock.

2. **Backtester hiện tại quá lạc quan** (entry Close(T), exit Close(T+hold), không phí/slippage/T+3/floor-lock). ReversalBounce cần module fill riêng: Open(T+1), T+3 gate, floor-lock defer, slippage 2 tầng, gap-entry cancellation. **Phụ thuộc 0A.2** (cần FloorPrice).

3. **Chưa có Market Breadth & Regime 4-state.** Đã có `MarketWyckoffPhase` 3-mức thuần VNINDEX (đang dùng cho Buy Score/size/VIP) — **giữ nguyên**; regime ReversalBounce là hệ song song mới. Cần `MarketBreadthAnalyzer` + `MarketRegimeClassifier` (hysteresis) + snapshot bảng riêng + endpoint.

4. **EarlyRecovery không tái dùng được** (điều kiện giá ngược chiều: yêu cầu trên MA20). ReversalBounce là engine mới hoàn toàn, chỉ mượn pattern pipeline (quét universe cuối `DailyAnalysisRunner`) và `BuildRsPercentile`.

5. **Snapshot bất biến idempotent** (spec §6.3–6.4, theo `(Symbol, TradingDate, StrategyVersion, SetupId)`) khác pattern `ReplaceForDateAsync` (xóa-ghi cả ngày) hiện có → cần repo/entity mới.

### Thứ tự phụ thuộc đề xuất

```
0A.2 (ReferencePrice/Floor) ──► 0A.3 module fill (floor-lock, gap) ──► Phase 0D backtest
0A.4 (breadth+regime)       ──► Phase 0C analyzer (regime gate)    ──► Phase 0D
```

## Quyết định: CÓ proceed sang Phase 0B?

- [x] **CÓ — có blocker nhỏ, đã liệt kê và sẽ xử lý trong 0B/0D.**
  - Không có blocker kiến trúc chặn hẳn: mọi thành phần mới đều **tách biệt**, không đòi sửa `BuyDecisionEngine`/`SmartMoneyOpportunitySelector`/`DarvasBreakoutAnalyzer` (đúng ràng buộc cứng spec §1.3, §4.4).
  - Blocker cần xử lý sớm trong 0B/0D: (a) `ReferencePrice`/Floor (0A.2); (b) xác minh KBS adjusted/unadjusted; (c) module fill mới (0A.3).

## Câu hỏi cần owner quyết định

1. **KBS trả giá adjusted hay unadjusted?** (blocker độ đúng floor-lock — spec chọn unadjusted làm nguồn sự thật).
2. **OHLCV**: tách bảng `StockDailyBar` chuẩn hóa (Option A) hay giữ JSON blob + mở rộng record `OhlcvBar` (Option B)? Floor/Ceiling **tính runtime** từ `ReferencePrice + Exchange` hay **lưu cột**?
3. **Regime 4-state** (`Panic/Stabilizing/ReboundConfirmed/Normal`) chạy **song song** với `MarketWyckoffPhase` 3-mức (giữ nguyên cái cũ), đúng không?
4. **Snapshot ReversalBounce** dùng repo/entity mới idempotent theo `(Symbol, TradingDate, StrategyVersion, SetupId)` — xác nhận không hợp nhất với pattern `ReplaceForDateAsync` của EarlyRecovery.
5. Ngưỡng regime cụ thể (drawdown %, PctAboveMA20 %, FloorCount) — chốt ở 0B hay để tune ở phase sau?
