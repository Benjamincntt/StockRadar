# Đề xuất gộp trạng thái mua: từ 2 bộ logic → 1 `TradeState`

Tài liệu tổng hợp để đánh giá / hỏi ý kiến.  
Ngữ cảnh: app JUICE / StockRadar — list **Cơ hội tốt nhất**, chi tiết CP, mobile + web.

---

## 1. Vấn đề hiện tại

Trên UI (đặc biệt mobile) một mã có thể hiển thị **hai badge cùng lúc**, ví dụ:

| Badge | Nguồn | Ví dụ ORS |
|-------|--------|-----------|
| **Theo dõi** (tím) | `BuyRecommendation.Watch` (sau khi list ghi đè) | Có |
| **Watch** (xanh) | `EntryPointStatus.Watch` (tiếng Anh, chưa dịch trên mobile) | Có |

User hiểu nhầm là hai “trạng thái” khác nhau, trong khi thực chất là **hai pipeline** + **một lớp ghi đè riêng cho list**.

### Ba lớp logic (không phải hai)

| Lớp | Enum / field | Câu hỏi trả lời |
|-----|----------------|-----------------|
| **Top gate** | `gateFailure`, `passesTopFilter` | Có đạt chuẩn SmartMoney đầy đủ không? |
| **Khuyến nghị** | `BuyRecommendation` (Avoid / Watch / StrongBuy) | Mức “quan tâm” theo điểm + gate |
| **Điểm vào** | `EntryPointStatus` (Ready / Watch / Late / Invalid) | Có vào lệnh **ngay** không? |
| **+ List riêng** | `OpportunityListRecommendation` | Ghi đè khuyến nghị trên list — gần như không hiển thị Tránh |

**Kết luận:** Không phải thiết kế “user cần 2 trạng thái”, mà là **nợ kỹ thuật**: list marketing tách khỏi timing vào lệnh, thêm lớp ghi đè, không có **một nguồn sự thật**.

---

## 2. Luồng hiện tại (chi tiết)

### 2.1. Pipeline tổng

```
BuyDecisionEngine.Evaluate()
  ├─ Buy Score + breakdown
  ├─ BuildEntry()           → EntryPointStatus
  ├─ ResolveTopGateFailure() → gateFailure?
  ├─ AlignEntryWithTopGate() → có thể hạ Ready → Watch
  └─ ResolveRecommendation() → BuyRecommendation

DailyAnalysisRunner (chỉ list)
  ├─ Chọn mã: strict HOẶC relaxed fallback
  ├─ OpportunityListRecommendation.Resolve() → ghi đè recommendation
  └─ Lưu entry nguyên từ engine (không ghi đè)

UI
  ├─ List: recommendation (đã ghi đè) + entry.status
  └─ Chi tiết CP: recommendation gốc + entry gốc
```

### 2.2. Bộ 1 — `EntryPointStatus` (`BuildEntry`)

**File:** `BuyDecisionEngine.BuildEntry()` → `AlignEntryWithTopGate()`

**Tiền điều kiện (trước BuildEntry):**

```text
hasBreakoutEntry = (Breakout || DarvasBreakout)
    && meetsSessionBar          // phiên > MinSessionChange% & KL ≥ MinSessionVolume
    && (DarvasBreakout || volRatio ≥ BreakoutMinVolumeRatio)

hasShakeoutEntry = flatBox hợp lệ && IsShakeoutFromBase && meetsSessionBar
hasMaStack       = HasBullishMaStack(...)
```

**Luồng BuildEntry (dừng sớm):**

| Bước | Điều kiện | Kết quả |
|------|-----------|---------|
| 1 | Lịch sử < MinHistoryDays | `Invalid` |
| 2 | Phân phối | `Invalid` |
| 3 | Không có nền giá | `Invalid` |
| 4 | Chưa phá vỡ nền | `Watch` — chờ phá vỡ |
| 5 | FOMO (gain > MaxGainFromBase) | `Late` |
| 6 | `entryType ≠ None` **và** RS OK **và** thanh khoản | `Ready` |
| 7 | Còn lại (đã có nền + breakout) | `Watch` — chờ kích hoạt phiên |

`entryType` = Breakout nếu `hasBreakoutEntry`, Shakeout nếu `hasShakeoutEntry`, else None.

**Lưu ý:** MA stack chỉ là **dòng checklist** trong BuildEntry; **không** chặn Ready ở bước này.

**AlignEntryWithTopGate** (chỉ khi đang `Ready`):

```text
if gateFailure != null && entry.Status == Ready
  → hạ xuống Watch
  → Headline = gateFailure
  → IsActionable = false
```

Nếu BuildEntry đã là `Watch`, gate **không** đổi entry status.

### 2.3. Bộ 2 — `BuyRecommendation` + Top gate

**ResolveTopGateFailure** (fail theo thứ tự, dừng sớm):

1. Thiếu lịch sử  
2. Thanh khoản thấp  
3. Phân phối  
4. Chưa phá vỡ nền giá  
5. FOMO so đỉnh nền  
6. **Chưa MA stack / xu hướng dài hạn**  
7. Thị trường xấu + RS < 1  
8. Ngành yếu + RS < 2  
9. Chưa breakout/shakeout phiên (khi chưa có hasBreakoutEntry/hasShakeoutEntry)  
10. RS âm && !hasBreakoutEntry  
11. Buy Score < MinPassScore (mặc định **60**)

`passesTopFilter` = `gateFailure == null`.

**ResolveRecommendation:**

| Điều kiện | Kết quả |
|-----------|---------|
| Entry == `Late` | `Avoid` |
| `gateFailure != null` | `Avoid` |
| Score ≥ **80** và Entry == `Ready` | `StrongBuy` |
| Score ≥ 70, hoặc ≥ 60, hoặc Entry Watch/Ready (và không gate) | `Watch` |
| Mặc định | `Watch` |

### 2.4. Bộ 3 — `OpportunityListRecommendation` (chỉ list)

**Ai lên list:**

- **Strict:** `SmartMoney.PassesFilter` = pass gate + score ≥ MinPassScore → `strictPass = true`
- **Relaxed fallback** (khi strict = 0 mã): score ≥ FallbackMinScore (**45**), loại FOMO/phân phối, **bỏ qua gate** → `strictPass = false`

**Ghi đè `recommendation` khi lưu DB:**

| Chế độ | Logic |
|--------|--------|
| `ResolveStrict` | Giữ StrongBuy/Watch; Avoid → ép **Watch** |
| `ResolveRelaxed` | Score ≥ 80 và Entry Ready → StrongBuy; **còn lại → Watch** |

**NormalizeStored** khi đọc list: `Avoid` trong DB → đổi thành StrongBuy (score≥80) hoặc **Watch**.

**`entry`:** lưu nguyên từ engine — **không** qua bộ 3.

### 2.5. Khác biệt List vs Chi tiết CP

| | List “Cơ hội tốt nhất” | Chi tiết CP |
|--|-------------------------|-------------|
| `recommendation` | Ghi đè bởi bộ 3 | `BuyRecommendation` gốc |
| `entry.status` | Gốc engine | Gốc engine |
| Relaxed + fail gate | Thường **Theo dõi** | Có thể **Tránh** |

### 2.6. Ví dụ ORS (minh họa lệch badge)

Giả định: nền đã phá, phiên hồi, score ~60, lên list qua **relaxed**:

```text
hasBreakoutEntry     = false   // phiên chưa đủ % + KL
flatBox confirmed    = true
hasMaStack           = false

BuildEntry           → Watch ("Chờ kích hoạt — có nền giá, chưa đủ điều kiện phiên")
AlignEntryWithTopGate → không đổi (không phải Ready)

gateFailure          → "Chưa đạt MA stack / xu hướng dài hạn"
ResolveRecommendation → Avoid

ResolveRelaxed       → Watch → UI "Theo dõi"

UI list              → Theo dõi + Watch
```

---

## 3. Ma trận tổ hợp (để đánh giá gộp)

| Entry | gateFailure | strict list | Recommendation (engine) | List badge | Entry badge | User hiểu |
|-------|-------------|-------------|-------------------------|------------|-------------|-----------|
| Ready | null | strict | StrongBuy (score≥80) | Mua mạnh | Ready/Vào ngay | ✅ Rõ |
| Ready | null | strict | Watch (score 60–79) | Theo dõi | Ready | ⚠️ Mâu thuẫn |
| Ready | MA stack | strict | Avoid | Theo dõi* | Watch** | ❌ Rối |
| Watch | MA stack | relaxed | Avoid | Theo dõi | Watch | ❌ Rối (ORS) |
| Watch | null | strict | Watch | Theo dõi | Watch | ⚠️ Trùng nghĩa |
| Late | — | — | Avoid | Tránh/hidden | Late | OK |
| Invalid | — | — | Avoid | — | — | OK |

\* Sau ResolveStrict/Relaxed ép Watch  
\** Sau AlignEntryWithTopGate nếu từng Ready

---

## 4. Mục tiêu tích hợp

> **Một cổ phiếu = một `TradeState`**, sinh **một lần** trong engine, dùng **mọi nơi** (list, chi tiết, alert).

- `EntryPoint` vẫn giữ **dữ liệu**: giá vào, SL, trigger, checklist — không còn là “trạng thái thứ hai” trên UI.
- Xóa `OpportunityListRecommendation` (list dùng cùng `TradeState`).
- Deprecate `BuyRecommendation` trên API (giữ map ngược tạm thời nếu cần).

---

## 5. So sánh phương án

### Phương án A — Thang trạng thái thống nhất (đề xuất)

**Mô tả:** Enum mới `StockTradeState` + một `TradeStateResolver` trong `BuyDecisionEngine`.

**Đề xuất 5 mức:**

| TradeState | Tiếng Việt UI | Ý nghĩa |
|------------|---------------|---------|
| `Avoid` | Tránh | Không xem xét / FOMO / phân phối / Late |
| `Watchlist` | Theo dõi | Setup sơ bộ, điểm yếu, chưa có kế hoạch vào rõ |
| `AwaitingTrigger` | Chờ kích hoạt | Setup đủ (nền, breakout…), thiếu phiên / MA / gate cụ thể |
| `Actionable` | Vào ngay | Ready + pass gate chính |
| `StrongBuy` | Mua mạnh | Actionable + Buy Score ≥ 80 + confidence cao |

*(Có thể gộp `Actionable` + `StrongBuy` thành 4 mức nếu muốn UI tối giản.)*

**Resolver đề xuất (ưu tiên):**

```text
1. Late / Invalid / phân phối nặng     → Tránh
2. gateFailure FOMO / thanh khoản…     → Tránh
3. Ready + passesTop + score ≥ 80      → Mua mạnh
4. Ready + passesTop                   → Vào ngay
5. Ready nhưng gateFailure (vd. MA)     → Chờ kích hoạt (+ lý do)
6. Entry Watch + đủ điểm list          → Chờ kích hoạt hoặc Theo dõi (tùy score)
7. Còn lại trên opportunity list       → Theo dõi
8. Còn lại                             → Tránh
```

| Ưu | Nhược |
|----|--------|
| Rõ cho user, một badge | Refactor backend + web + mobile |
| Một logic, bỏ list ghi đè | Cần migration API / DB |
| Dễ giải thích ORS: một dòng + lý do phụ | Phải thống nhất strict vs relaxed trong resolver |

---

### Phương án B — Chỉ giữ `EntryPointStatus`, xóa `BuyRecommendation`

**Mô tả:** `StrongBuy` = Ready + score ≥ 80; list không ghi đè.

| Ưu | Nhược |
|----|--------|
| Ít enum mới | Mất nhãn “Theo dõi list” khi chưa có entry rõ |
| Entry đã có checklist + headline | “Theo dõi” vs “Chờ kích hoạt” vẫn cần map từ Watch |
| | Relaxed list khó biểu diễn |

---

### Phương án C — Chỉ giữ `BuyRecommendation`, gộp Ready vào StrongBuy/Watch

**Mô tả:** Bỏ badge entry trên list; chi tiết vẫn có checklist.

| Ưu | Nhược |
|----|--------|
| Ít thay đổi API surface | Mất chi tiết timing (Ready vs Watch) trên list |
| | Vẫn còn OpportunityListRecommendation |
| | Không giải quyết gate vs list |

---

### Phương án D — Chỉ ghép UI (không đổi engine)

**Mô tả:** Ẩn badge trùng; dịch `Watch` → “Chờ kích hoạt”; ẩn khi recommendation = Watch và entry = Watch.

| Ưu | Nhược |
|----|--------|
| Nhanh, ít risk | **Không sửa** mâu thuẫn list vs chi tiết |
| | Ba pipeline vẫn tồn tại |
| | ORS vẫn Avoid ở chi tiết + Theo dõi ở list |

**Không khuyến nghị** làm giải pháp dài hạn.

---

### Bảng so sánh tóm tắt

| Tiêu chí | A Thang 5 mức | B Chỉ Entry | C Chỉ Recommendation | D Ghép UI |
|----------|---------------|-------------|----------------------|-----------|
| Một trạng thái UI | ✅ | ⚠️ | ⚠️ | ❌ |
| Sửa mâu thuẫn list/chi tiết | ✅ | ⚠️ | ❌ | ❌ |
| Effort backend | Cao | Trung bình | Thấp | Thấp |
| Effort client | Trung bình | Thấp | Thấp | Rất thấp |
| Dễ giải thích user | ✅ | Trung bình | Thấp | Thấp |
| Bỏ OpportunityListRecommendation | ✅ | ⚠️ | ❌ | ❌ |

---

## 6. Lộ trình triển khai đề xuất (phương án A)

### Phase 1 — Engine

- Thêm `TradeStateResolver.Resolve(decision, listContext?)` trong Domain.
- Trả thêm: `TradeState`, `TradeStateReason` (chuỗi ngắn), `TradeStateDetail` (gate hoặc checklist item fail đầu tiên).

### Phase 2 — API

- `buyDecision.tradeState` + `tradeStateLabelVi`
- Map `recommendation` cũ từ `tradeState` (tương thích mobile/web cũ).
- List lưu `tradeState` thay vì ghi đè `recommendation` riêng.

### Phase 3 — UI

- Home / list: **một** `TradeStateBadge`.
- Chi tiết CP: badge = cùng `tradeState`; checklist + giá giữ nguyên.

### Phase 4 — Dọn

- Xóa `OpportunityListRecommendation`.
- Xóa `BuyRecommendation` khỏi API khi client đã migrate.

---

## 7. Ví dụ sau tích hợp (phương án A)

| Mã | Hiện tại (list) | Sau tích hợp |
|----|-----------------|--------------|
| ORS | Theo dõi + Watch | **Chờ kích hoạt** · *Chưa MA stack* |
| Score 85, Ready, pass gate | Mua mạnh + Ready | **Mua mạnh** |
| Có nền, chưa breakout | Theo dõi + Watch | **Theo dõi** · *Chưa phá vỡ nền giá* |
| FOMO | Tránh / ẩn | **Tránh** · *FOMO +12% so đỉnh nền* |

---

## 8. Câu hỏi mở (để hỏi ý kiến)

1. **4 mức hay 5 mức?** Có tách **Mua mạnh** khỏi **Vào ngay** trên list không?
2. **Relaxed fallback:** Mã fail gate (vd. MA) còn trên list không? Nếu có → `AwaitingTrigger` hay `Watchlist`?
3. **Chi tiết CP:** Khi `TradeState = Tránh`, có ẩn khỏi list hay vẫn hiện với lý do?
4. **Backward compatibility:** Giữ field `recommendation` / `entry.status` bao lâu?
5. **Alert / Zalo:** Push theo `TradeState` đổi (vd. Watchlist → AwaitingTrigger → StrongBuy)?

---

## 9. File code liên quan

| Thành phần | Đường dẫn |
|------------|-----------|
| Engine chính | `backend/StockRadar.Domain/Services/BuyDecisionEngine.cs` |
| Ghi đè list | `backend/StockRadar.Application/Services/OpportunityListRecommendation.cs` |
| Job lưu list | `backend/StockRadar.Infrastructure/MarketData/DailyAnalysisRunner.cs` |
| Đọc list API | `backend/StockRadar.Application/Services/MarketService.cs` |
| Strict filter | `backend/StockRadar.Domain/Services/SmartMoneyOpportunitySelector.cs` |
| Mobile list UI | `mobile/lib/screens/home_screen.dart` |
| Web list UI | `frontend/src/pages/HomePage.tsx` |

---

## 10. Kết luận ngắn

- Hiện có **3 lớp** (gate + recommendation + entry) và **ghi đè list** → UI hiển thị **2 badge** dễ nhầm.
- **Điểm mua đúng** theo engine gốc: **StrongBuy + Ready + không gate**; list relaxed làm **mềm** khuyến nghị nhưng **không** mềm entry.
- **Khuyến nghị:** Phương án **A** — một `TradeState` trong engine, không chỉ ghép UI (phương án D).

*Tài liệu tạo: 2026-07-06 — phiên bản 1.0*
