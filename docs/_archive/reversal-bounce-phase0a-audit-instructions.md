# Phase 0A — Audit ReversalBounce: Hướng dẫn cho model thực thi

> **Mục đích:** File này dành cho một model nhỏ hơn thực hiện **audit đọc** codebase StockRadar, **trước khi** bất kỳ dòng code ReversalBounce nào được viết.
>
> **Đọc kỹ trước khi bắt đầu:**
> 1. `docs/features/reversal-bounce.md` — đặc tả đầy đủ tính năng.
> 2. `CLAUDE.md` — context cố định của dự án.
> 3. File này.

---

## 0. Quy tắc chung

### 0.1. Bạn đang làm gì?

- **Đọc và audit** codebase hiện tại.
- **KHÔNG** sửa file.
- **KHÔNG** tạo file mới trong `src/`, `backend/`, `mobile/`, `frontend/`.
- **KHÔNG** chạy `git commit`, `git push`, hay bất kỳ lệnh deploy nào.
- **KHÔNG** tạo migration database.
- **ĐƯỢC** tạo file báo cáo markdown trong `docs/features/phase0a-audit-reports/` (output duy nhất).

### 0.2. Worktree

- Bạn sẽ được cấp một worktree riêng để làm việc. Mọi đọc/grep chỉ thực hiện trong worktree này.
- Không thao tác ngoài worktree.

### 0.3. Format output

- **Trích đoạn tập trung + file:line.** Không dump full file trừ khi file < 300 dòng.
- Mỗi dòng trích dẫn phải có format `path/to/file.cs:123` để có thể click.
- Nếu cần thêm ngữ cảnh (DI, interface cha, biến toàn cục) → **dừng lại và hỏi**, không tự đoán.

### 0.4. Quy ước trích dẫn code

Khi trích một đoạn code, hãy:

```csharp
// path/to/file.cs:120-145
public async Task<...> DoSomething(...)
{
    var x = Compute(...);
    if (x.Threshold > 18.0m) { ... }   // ← đây là gate cần audit
    return ...;
}
```

- Luôn chỉ rõ **dòng bắt đầu – dòng kết thúc**.
- Chú thích inline (// ←) cho đoạn "đáng chú ý".
- Tối đa ~80 dòng / trích đoạn. Nếu dài hơn → chia nhỏ theo phương thức/region.

### 0.5. Ngôn ngữ báo cáo

- Tiếng Việt (technical Vietnamese, không dịch các thuật ngữ kỹ thuật).
- File báo cáo đặt tên theo schema:
  ```text
  docs/features/phase0a-audit-reports/0X.Y-topic.md
  ```

---

## 1. Quy trình chung cho mỗi ticket

Mỗi ticket (0A.1 → 0A.4) làm theo 6 bước:

1. **Khám phá phạm vi** — tìm file liên quan bằng `Grep` / `Glob` với các từ khoá phù hợp.
2. **Đọc chính xác** — đọc các file đã tìm được. Nếu file > 800 dòng, đọc theo `limit + offset`.
3. **Trích đoạn + file:line** — rút ra các đoạn logic cốt lõi.
4. **Mô tả logic hiện tại** — 1–2 đoạn văn ngắn, không diễn giải quá.
5. **Gap analysis** — so sánh với yêu cầu trong `reversal-bounce.md`. Bảng 2 cột: "Đặc tả yêu cầu" / "Hiện trạng".
6. **Đề xuất Ticket tiếp theo** — kết luận ngắn: `Giữ nguyên` / `Mở rộng` / `Tách riêng` / `Schema migration` / `Cần làm mới`.

---

## 2. Phase 0A.1 — Audit EarlyRecovery hiện tại

### 2.1. Mục tiêu

Xác định:
- File nào chứa logic Early Recovery (`BuildEarlyRecoveryRadar` hoặc tương đương).
- Bộ lọc đang dùng những indicator nào (MA20, RS, ATR, ...).
- Gate hiện tại lỏng hay chặt so với ý tưởng ReversalBounce.
- Có trùng logic với ReversalBounce ở mức nào.

### 2.2. Từ khoá tìm kiếm (Grep)

Chạy song song các pattern sau:

```text
EarlyRecovery
BuildEarlyRecovery
EarlyRecoveryRadar
/recovery
EarlyRecoveryScore
RsPercentileLoose
MA stack loose
```

### 2.3. File path thường gặp (chỉ để ưu tiên đọc trước)

- `backend/StockRadar.Application/Services/`
- `backend/StockRadar.Domain/Engines/`
- `backend/StockRadar.Application/Analyzers/`
- `backend/StockRadar.Infrastructure/Repositories/`
- Bất kỳ file nào có `Recovery` trong tên.

### 2.4. Khi tìm được file

Trích đoạn:

```text
- Phương thức xây dựng/ranking early recovery list
- Điều kiện gate: MA stack (Loose/Medium/Full), RS percentile, volume, ...
- Cách tính score (nếu có)
- Cách exclude (Restricted, low liquidity, ...)
- Đầu ra: kiểu dữ liệu, có lưu DB không, có alert không
```

### 2.5. Output báo cáo: `docs/features/phase0a-audit-reports/0A.1-early-recovery.md`

Template bắt buộc:

```markdown
# 0A.1 — Audit EarlyRecovery

## Tóm tắt 1 đoạn
...

## File liên quan
| Path | Vai trò |
|---|---|
| backend/.../Foo.cs | ... |

## Logic hiện tại

### Indicator đầu vào
- ...

### Gate / điều kiện
- ...

### Cách tính score
- ...

### Đầu ra
- ...

## Trích đoạn code chính

### Phương thức `BuildXxx` — file.cs:120-145
```csharp
...
```

### ... (các đoạn khác)

## Gap analysis vs ReversalBounce

| Yêu cầu trong reversal-bounce.md | Hiện trạng EarlyRecovery |
|---|---|
| Có Stage A/B/C riêng | KHÔNG — chỉ là danh sách 1 lần |
| Có Drawdown ≥ 18% + ATR multiple | ... |
| Có Demand confirmation | ... |
| Có Regime gate | ... |
| ... | ... |

## Đề xuất
- [ ] Giữ nguyên / Mở rộng / Tách riêng / Schema migration / Cần làm mới
- Lý do: ...

## Câu hỏi cần làm rõ (nếu có)
- ...
```

---

## 3. Phase 0A.2 — Audit Schema OHLCV

### 3.1. Mục tiêu

Xác định:
- Entity nào lưu OHLCV EOD.
- Dữ liệu hiện tại là **Adjusted** hay **Unadjusted**.
- Có cột `ReferencePrice`, `CeilingPrice`, `FloorPrice` chưa.
- Có bảng `CorporateAction` hay `ExchangeRules` chưa.
- Có cờ `AdjustedFactor` không.

### 3.2. Từ khoá tìm kiếm

```text
class StockHistory (hoặc StockPrice, DailyCandle, ...)
OpenAdj
AdjustedPrice
ReferencePrice
FloorPrice
CeilingPrice
CorporateAction
ExchangeRules
TickSize
PriceBand
```

### 3.3. File path thường gặp

- `backend/StockRadar.Domain/Entities/` (entity)
- `backend/StockRadar.Infrastructure/Persistence/` hoặc `Migrations/`
- `backend/StockRadar.Infrastructure/Repositories/`

### 3.4. Khi tìm được file

Trích đoạn:

```text
- Class/entity định nghĩa bảng OHLCV
- Cấu trúc cột (kiểu dữ liệu, nullable, index)
- Migration gần nhất có động vào bảng này không
- Cách dữ liệu được nạp (provider nào, có điều chỉnh không)
- Có bảng `CorporateAction` riêng không, có lưu ex-date không
```

### 3.5. Output báo cáo: `docs/features/phase0a-audit-reports/0A.2-ohlcv-schema.md`

Template bắt buộc (tóm tắt):

```markdown
# 0A.2 — Audit Schema OHLCV

## Tóm tắt 1 đoạn
...

## Bảng OHLCV chính
| Cột | Kiểu | Nullable | Ghi chú |
|---|---|---|---|
| Symbol | string | No | |
| TradingDate | date | No | |
| Open | decimal | No | unadjusted / adjusted? |
| ... | ... | ... | |

## Đối chiếu yêu cầu ReversalBounce

| Yêu cầu | Hiện trạng | Thiếu / Đủ |
|---|---|---|
| Có Open/High/Low/Close unadjusted | ... | ... |
| Có OpenAdj/CloseAdj | ... | ... |
| Có ReferencePrice | ... | ... |
| Có FloorPrice / CeilingPrice | ... | ... |
| Có AdjustedFactor | ... | ... |
| Có CorporateActionFlag / bảng riêng | ... | ... |
| Có ExchangeRules (PriceBand, TickSize) | ... | ... |

## Trích đoạn code chính
- Entity definition: file.cs:...-...
- Migration gần nhất: ...

## Đề xuất migration (nếu cần)
- [ ] Không cần — đủ dùng
- [ ] Cần migration: ... (mô tả ngắn các cột/bảng cần thêm)
```

---

## 4. Phase 0A.3 — Audit Backtester Fill Logic

### 4.1. Mục tiêu

Xác định:
- File engine backtest ở đâu.
- `FillPrice` lấy từ **Open(T+1)**, **Close(T)**, hay **VWAP**.
- Cơ chế xử lý T+3 (`MinTradingSessionsToSell`) hardcode thế nào.
- Có xử lý sàn / dư bán chưa (gợi ý: tìm `Floor`, `Locked`, `Ceiling`).
- Có mô phỏng slippage chưa.

### 4.2. Từ khoá tìm kiếm

```text
BacktestService
SmartMoneyBacktest
FillPrice
FillAtOpen
FillAtClose
MinTradingSessionsToSell
T+3
TPlus3
SellableSessions
Slippage
FloorLocked
```

### 4.3. File path thường gặp

- `backend/StockRadar.Application/Services/Backtest*.cs`
- `backend/StockRadar.Domain/Engines/Backtest*.cs`
- `backend/StockRadar.Api/Controllers/BacktestController.cs` (chỉ để tham chiếu endpoint)

### 4.4. Khi tìm được file

Trích đoạn:

```text
- Phương thức ExecuteTrade / FillOrder / MatchOrder
- Vòng lặp time-step (theo phiên? theo phút?)
- Cách lấy giá fill cho buy entry
- Cách lấy giá fill cho sell exit
- Có stop-loss / take-profit / time-stop logic ở đâu
- Có kiểm tra "có thể bán được không" (T+3, restricted, floor) không
- Slippage hardcode hay cấu hình
```

### 4.5. Output báo cáo: `docs/features/phase0a-audit-reports/0A.3-backtester.md`

Template bắt buộc (tóm tắt):

```markdown
# 0A.3 — Audit Backtester Fill Logic

## Tóm tắt 1 đoạn
...

## File liên quan
| Path | Vai trò |
|---|---|
| backend/.../BacktestService.cs | entry point |
| ... | ... |

## Fill logic hiện tại

### Entry
- FillPrice lấy từ: ... (Open T+1 / Close T / VWAP / ...)
- Trích đoạn: file.cs:...-...

### Exit
- FillPrice lấy từ: ...
- Trích đoạn: file.cs:...-...

### T+3 / MinTradingSessionsToSell
- Hardcode ở: file.cs:...
- Có tham số config không? ...

### Slippage
- ...

### Floor-lock / không bán được
- Hiện có: ... / Không có

## Đối chiếu yêu cầu ReversalBounce

| Yêu cầu trong reversal-bounce.md | Hiện trạng | Đạt? |
|---|---|---|
| Fill tại Open(T+1), không Close(T) | ... | ✅/❌ |
| Hard exit chỉ từ T+3 | ... | ✅/❌ |
| Floor-lock defer exit | ... | ✅/❌ |
| Slippage 2 tầng (base + gap) | ... | ✅/❌ |
| Gap-entry cancellation | ... | ✅/❌ |
| Không dùng High/Low T+1 để xác nhận tín hiệu | ... | ✅/❌ |

## Đề xuất
- [ ] Backtester hiện tại đủ dùng (sau khi đối chiếu chi tiết)
- [ ] Cần vá: ... (mô tả thay đổi cần thêm)
- [ ] Cần viết lại module fill riêng cho ReversalBounce
```

---

## 5. Phase 0A.4 — Audit Market Breadth / Regime

### 5.1. Mục tiêu

Xác định:
- Đã có service / entity nào tính **Market Breadth** (tỷ lệ mã trên MA20, số mã sàn, ...) chưa.
- Đã có **Regime engine** (Panic / Stabilizing / Rebound / Normal) chưa.
- Nếu có: nó được dùng ở đâu, có tham số hoá không, có lưu lịch sử không.
- Nếu không: xác nhận "trắng" để ticket 0B được sinh.

### 5.2. Từ khoá tìm kiếm

```text
MarketBreadth
Breadth
MarketRegime
PanicRegime
StabilizingRegime
ReboundRegime
AdvanceDecline
AdvanceDeclineLine
PctAboveMA20
FloorCount
```

### 5.3. File path thường gặp

- `backend/StockRadar.Application/Services/`
- `backend/StockRadar.Domain/Engines/`
- Có thể nằm trong `Market/`, `Index/`, `Universe/` namespace.

### 5.4. Khi tìm được file

Trích đoạn:

```text
- Cách tính các metric breadth (PctAboveMA20, FloorCount, ...)
- Cách phân loại regime (rule nào → regime nào)
- Có hysteresis không
- Có snapshot theo ngày không (entity, table)
- Có API endpoint nào expose regime không
```

### 5.5. Output báo cáo: `docs/features/phase0a-audit-reports/0A.4-breadth-regime.md`

Template bắt buộc (tóm tắt):

```markdown
# 0A.4 — Audit Market Breadth / Regime

## Tóm tắt 1 đoạn
- Trắng (chưa có gì) / Có sẵn 1 phần / Có đầy đủ

## File liên quan (nếu có)
| Path | Vai trò |
|---|---|
| ... | ... |

## Hiện trạng

### Breadth
- Có / Không
- Metric đang tính: ...
- Lưu snapshot: Có / Không

### Regime
- Có / Không
- Quy tắc phân loại: ...
- Hysteresis: Có / Không

## Trích đoạn code (nếu có)

## Đối chiếu yêu cầu ReversalBounce

| Yêu cầu | Hiện trạng | Đạt? |
|---|---|---|
| 8 metric breadth MVP (PctAboveMA20, ...) | ... | ✅/❌ |
| Regime: Panic / Stabilizing / Rebound / Normal | ... | ✅/❌ |
| Hysteresis stateless (2 phiên nâng, 1 phiên hạ) | ... | ✅/❌ |
| Snapshot theo ngày + Version | ... | ✅/❌ |
| API expose regime | ... | ✅/❌ |

## Đề xuất
- [ ] Trắng → Phase 0B phải code mới hoàn toàn
- [ ] Có một phần → Phase 0B bổ sung: ...
- [ ] Đủ dùng → Phase 0B chỉ cần wrap lại và bổ sung metric thiếu
```

---

## 6. Tổng hợp cuối Phase 0A

Sau khi 4 ticket xong, tạo file tổng hợp:

**`docs/features/phase0a-audit-reports/00-summary.md`**

Nội dung:

```markdown
# Phase 0A — Summary

## Trạng thái 4 ticket
| Ticket | Trạng thái | Quyết định |
|---|---|---|
| 0A.1 EarlyRecovery | DONE | Giữ/Mở rộng/Tách riêng |
| 0A.2 OHLCV Schema | DONE | Không cần migration / Cần migration: ... |
| 0A.3 Backtester | DONE | Tái sử dụng / Vá / Viết lại |
| 0A.4 Breadth/Regime | DONE | Code mới / Bổ sung / Đủ dùng |

## Tổng hợp gap ảnh hưởng đến kiến trúc
- ...

## Quyết định: CÓ proceed sang Phase 0B?
- [ ] CÓ — không có blocker kiến trúc
- [ ] CÓ — có blocker nhỏ, đã liệt kê và sẽ xử lý trong 0B
- [ ] KHÔNG — cần sửa đặc tả trước, lý do: ...

## Câu hỏi cần owner quyết định
- ...
```

---

## 7. Khi gặp vấn đề ngoài hướng dẫn

- **Không đoán.** Nếu thiếu ngữ cảnh cần thiết để audit (DI, interface, biến config) → ghi vào mục "Câu hỏi cần làm rõ" trong báo cáo và **dừng** ticket đó.
- Báo cáo trước khi sang ticket tiếp theo.
- Không tự ý mở rộng scope audit (ví dụ: đang làm 0A.1 mà phát hiện vấn đề 0A.3 → ghi nhận vào báo cáo 0A.1 mục "Phát hiện liên quan", không nhảy sang 0A.3).

---

## 8. Checklist trước khi báo cáo xong

Mỗi file báo cáo phải có:

- [ ] Mục "Tóm tắt 1 đoạn" đầy đủ.
- [ ] Bảng "File liên quan" (nếu có).
- [ ] Ít nhất 1 trích đoạn code kèm `file.cs:line-line`.
- [ ] Bảng "Đối chiếu yêu cầu" đầy đủ các dòng trong template.
- [ ] Mục "Đề xuất" với 1 lựa chọn rõ ràng.
- [ ] Mục "Câu hỏi cần làm rõ" nếu có.

Nếu thiếu bất kỳ mục nào → báo cáo chưa hoàn thành, không gửi owner.