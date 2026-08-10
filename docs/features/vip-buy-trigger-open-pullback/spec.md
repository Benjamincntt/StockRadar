# Spec — VIP Buy: % từ mở cửa + pullback MA (uptrend)

**Status:** Implemented  
**Scope:** Telegram VIP Master Buy only (`TopOpportunityVipAlert*`). Không đổi Buy Score / Top selection / bán.  
**Related living docs:** `docs/domain/buy-decision.md` + `docs/architecture.md`.

---

## Goal

1. **Ý 1:** Thay điều kiện kích hoạt Mua điểm 1/2 từ `% tăng so BaseHigh` → `% tăng so giá mở cửa phiên` (`KbsBoardRow.Open`).
2. **Ý 2:** Thêm nhánh **OR** — mã uptrend dài hạn, giá live hồi sát MA10 hoặc MA20 → ưu tiên bắn **Mua điểm 1**.

`BaseHigh` vẫn dùng cho Entry Ready / hiển thị tham chiếu; **không** còn là ngưỡng BuyPoint.

---

## Data availability & anti-fake (bắt buộc)

| Dữ liệu | Nguồn | Có sẵn hôm nay? | Rủi ro fake | Guard bắt buộc |
|---------|--------|-----------------|-------------|----------------|
| `Open`, `Close`, `SessionVolume` | `KbsBoardRow` | ✅ | `Open <= 0` → chia 0 / % vô nghĩa | `Open <= 0` hoặc `Close <= 0` → **không** evaluate buy |
| `AverageDailyVolume` | `DailyOpportunityRecord` | ✅ | 0 → paced ratio 0 | Giữ `PassesVolumeGate` hiện tại |
| `Entry.IsActionable` | `EntryPointJson` | ✅ | null/false | Giữ guard Actionable |
| MA10 / MA20 / MA50 | `Stock.History` (OHLCV) | ❌ **chưa có trên path VIP** | Nếu default dist=0 hoặc MA=Close → mọi mã “sát MA” | Prefetch history cho Top; thiếu &lt; 50 bar → **tắt nhánh pullback** (fail-closed). Không bịa MA. |
| Bar hôm nay trong History | Job 2 / sync | Có thể đã có / chưa | Tính MA lẫn giá intraday → méo | MA tính trên history **kết thúc phiên trước** (loại bar `Date >= sessionDate`) |

**Kết luận:** Ý 1 đủ data (Open). Ý 2 **thiếu** history trên monitor — implement phải load; không được stub 0.

---

## Behavior

### Prefetch (mỗi scan / mỗi phiên)

Trong `OpportunityIntradayMonitorRunner` hoặc `TopOpportunityVipAlertPublisher`:

- Load `IJobStockRepository.GetBySymbolAsync` (hoặc batch) cho symbols trong Top map.
- Cache in-memory theo `sessionDate`: `(Ma10, Ma20, Ma50, UptrendLong)` tính 1 lần / mã / ngày.
- Công thức MA: SMA đơn giản trên Close các bar `Date < sessionDate`.
- `UptrendLong` = `lastClose > Ma50 && Ma20 >= Ma50 && Ma20SlopeNonNegative` (lookback 3 — tái dùng tinh thần `MarketPhaseClassifier` / SignalAnalyzer nếu có helper sẵn; không copy sai).

### Nhánh Breakout phiên (Buy1 + Buy2)

```
gainFromOpen = (Close - Open) / Open * 100
```

| Signal | Điều kiện giá | Volume | Ticks |
|--------|---------------|--------|-------|
| BuyPoint1 | `BuyPoint1MinChangePercent <= gainFromOpen < BuyPoint2MinChangePercent` | paced ≥ `MinVolumeRatioPaced` (+ floor) | ≥ `RequiredConfirmationTicks` |
| BuyPoint2 | `gainFromOpen >= BuyPoint2MinChangePercent` | paced ≥ `BuyPoint2MinVolumeRatio` | ≥ same |

Config số giữ mặc định 3 / 6 nhưng **đổi ngữ nghĩa** trong comment Options + appsettings.

### Nhánh Pullback MA (chỉ BuyPoint1)

Tất cả phải đúng:

1. `UptrendLong == true` (đã prefetch; nếu không tính được → false).
2. `nearMa = min(|Close-Ma10|/Ma10, |Close-Ma20|/Ma20) * 100 <= PullbackNearMaPercent` (default **1.5**). Chỉ xét MA > 0.
3. `gainFromOpen >= PullbackMinGainFromOpenPercent` (default **0.5**) — tránh bắn khi đang đỏ sâu trong phiên.
4. Volume + ticks **cùng BuyPoint1**.

Không kích hoạt BuyPoint2 qua nhánh pullback.

### OR logic trong `EvaluateMasterSignal`

```
buy1Eligible = breakoutBand || pullbackBranch
buy2Eligible = breakoutStrong   // chỉ % mở cửa
```

Giữ reset confirm ticks khi rơi khỏi điều kiện tương ứng.  
Giữ: skip nếu đã fired; hydrate từ SQL; cooldown.

### Entry Ready

Không đổi (`IsPriceInEntryZone` + Actionable).

### Telegram copy

- Breakout: nêu `+X% từ mở cửa` (không còn “so đỉnh nền” làm điều kiện).
- Pullback: nêu `hồi sát MA10/MA20 trong uptrend dài hạn` + `+X% từ mở cửa`.
- Có thể vẫn hiện `BaseHigh` như thông tin phụ (optional).

---

## Config (`MasterAlerts`)

Thêm:

```json
"PullbackNearMaPercent": 1.5,
"PullbackMinGainFromOpenPercent": 0.5,
"PullbackRequireUptrendLong": true
```

Đổi comment (không đổi default số trừ khi cần):

- `BuyPoint1MinChangePercent` / `BuyPoint2MinChangePercent` → **% từ Open phiên**, không phải BaseHigh.

---

## Files to change

1. `backend/StockRadar.Application/Options/MasterAlertOptions.cs` — props + XML docs  
2. `backend/StockRadar.Api/appsettings.json` (+ Production nếu override)  
3. `backend/StockRadar.Infrastructure/Notifications/TopOpportunityVipAlertEvaluator.cs` — `GainFromOpen`, OR branches, signature nhận Open + pullback context  
4. `backend/StockRadar.Infrastructure/Notifications/TopOpportunityVipAlertPublisher.cs` — truyền Open + MA context; reasoning  
5. `backend/StockRadar.Infrastructure/MarketData/OpportunityIntradayMonitorRunner.cs` — prefetch/cache MA cho Top (hoặc helper mới cùng folder Notifications)  
6. `backend/StockRadar.Infrastructure/Notifications/VipTelegramMessageFormatter.cs` — wording  
7. Tests (nếu có / thêm unit cho evaluator): breakout open, pullback near MA, fail-closed thiếu history, Open=0  
8. Docs: `docs/domain/buy-decision.md` (VIP tóm tắt), `docs/architecture.md` (MasterAlerts JSON)

---

## Acceptance

- [ ] Mã Top Actionable tăng ≥3% từ Open, đủ vol/ticks → Buy1 dù vẫn dưới BaseHigh  
- [ ] ≥6% từ Open → Buy2  
- [ ] Open=0 hoặc thiếu Open → không buy  
- [ ] Uptrend + giá trong ±1.5% MA10/20 + gainFromOpen≥0.5% + vol/ticks → Buy1  
- [ ] Không đủ history / không uptrend → nhánh pullback im lặng; breakout vẫn chạy  
- [ ] Không có MA giả (0) làm mọi mã “near”  
- [ ] Entry Ready / sell / risk không đổi hành vi  
- [ ] Sau deploy: Telegram lý do phản ánh nhánh nào đã kích hoạt  

---

## Out of scope

- Đổi Buy Score, Top hygiene, SessionRadar  
- Lưu MA vào `DailyOpportunities` DB (không bắt buộc nếu prefetch đủ)  
- HPO / OpportunityRanker  

---

## Implement notes for agent

- Fail-closed luôn thắng fail-open.  
- Tái dùng helper MA/slope nếu đã có trong Domain; tránh duplicate logic lệch.  
- Không hỏi lại user về BaseHigh vs Open — spec này là source of truth.  
- Sau code: `backend/restart-api.ps1` (local) hoặc ship theo quy trình repo nếu user yêu cầu deploy.
