# VIP bull-trap deferral — chờ chiều xác nhận trước khi bắn Buy1

**Trạng thái:** slice 1 (deferral + pin + checkpoint) **và** slice 2 (hysteresis mặc định bật;
window-integrity + foreign-hold: opt-in, mặc định tắt) — đã code, build + test xanh. **Chưa
backtest 3 số** (checkpoint time, integrity threshold, hysteresis band) — dùng default đề xuất
trong thread thiết kế, chưa có dữ liệu thực tế xác nhận.

## Vấn đề

Trong env bull-trap (sát đỉnh kháng cự VNINDEX + pha ≠ Favorable), VIP bắn Buy1 ngay khi
tín hiệu đủ điều kiện *trong phiên* — mà điều kiện "phiên xanh" chỉ là một dòng:
`IsDipBounceBuy1Eligible` loại khi `Close <= Open && gainFromOpen <= 0`. Tức **bất kỳ** nến
xanh so Open nào lúc 10:30 cũng đủ mồi. Đây đúng là cái bẫy tháng 8: xanh đầu phiên → xả chiều.
`RequiredConfirmationTicks` (~3 phút) là chống nhiễu tick, **không** phải chờ chiều.

Hai lỗ cùng bản chất — tin một tín hiệu trong phiên mà buổi chiều chưa xác nhận:

1. **close>open buổi sáng** (env bật): dip-bounce bắn 10:30, 14:00 đảo.
2. **xuyên đỉnh buổi sáng** (env *tắt*): `IsNearPriorPeak` trả false khi `live >= peak`
   (VnIndexPriorPeakAnalyzer:61). Xuyên P1 → env tắt → cửa breakout mở đúng nhịp break hụt.
   Sát ATH (~1.800) thường không còn P2 → `FindActiveResistance` trả null → env tắt hẳn.

Hai trigger sống ở **hai trạng thái env ngược nhau** — nên deferral **không được** treo lên
`isBullTrapEnv` từng vòng; xuyên đỉnh sẽ thoát cùng nhịp env tắt.

## Thiết kế slice 1

**Trap-context theo phiên (pin), không phải boolean vòng hiện tại.**
`VipVnIndexPeakCache._peak`/`_livePrice` bị ghi đè mỗi `PrefetchAsync` (~60s) — ephemeral,
biến mất/xoay P2 đúng lúc xuyên. Nên ghim một mốc bền theo phiên:

- `_trapPeakPinned` (decimal): ghi **một lần** khi env lần đầu bật trong phiên
  (`IsNearPriorPeak(peak, live, band)` = true), xoá theo session rollover.
- **Không cần luật release trong phiên:** deferral chỉ gate *trước checkpoint*; tín hiệu
  buổi chiều đã qua checkpoint không bị pin cản. Pin độc lập với hysteresis (slice sau).

**Deferral trigger = đang trong trap-zone** (không phải chỉ "pin đã set"):

```
trapDeferralActive = isBullTrapEnv                         // env bật, sát đỉnh dưới band
                     || (trapContextActive && live >= pin) // đã xuyên pin (env tắt)
```

Khi live rút về dưới band (vd 1.740, ~3.3% dưới 1.800) → cả hai vế false → pin "im" →
luật nhánh thường quyết. Vế xuyên dùng **pin bền**, sống sót khi `_peak` xoay/null.

**Checkpoint muộn trong chiều — knob, KHÔNG phải mép 13:00.**
`SessionElapsedFraction` mở chiều 13:00 = điều kiện cần (qua nghỉ trưa), *không* phải xác nhận.
Sóng cung tháng 8 nằm 13:00→14:00, đỉnh ~14:00. Confirm 13:01 chỉ dời lệnh 30' và ăn nguyên
cú 14:00. Checkpoint mặc định **14:00** (khe ~13:45–14:15; trần muộn ~14:30 khớp liên tục HOSE
rồi ATC 14:30–14:45). Backtest để chốt số.

**Shape khi tới checkpoint (endpoint, slice 1):**
- Mã còn sát high phiên: `(High - Close)/High ≤ BullTrapDeferralCloseWithinHighPercent`.
- (Xuyên đỉnh: điều kiện index-còn-trên-pin đã nằm trong `trapDeferralActive`.)

```
deferralBlocks = BullTrapDeferralEnabled
                 && trapDeferralActive
                 && (!pastCheckpoint || !closeNearHigh)
```

Áp vào **Buy1** (dip-bounce env-on + breakout/pullback khi xuyên) và **Buy2 breakoutStrong**
(một cú xuyên mạnh có thể bắn thẳng Buy2+Buy1, phải chặn cùng cổng). **Buy2 scale-in +10%**
trong env giữ nguyên slice này — nếu Buy1 chiều xác nhận rồi kéo, scale-in vẫn bắn ngay; đó là
cửa thứ hai, xử lý ở slice sau.

## Slice 2 — đã code

- **Hysteresis distance dưới P1** (`BullTrapHysteresisEnabled`, mặc định **bật**): approach từ
  dưới, bật ở `BullTrapNearPeakBandPercent` (1.5%), chỉ tắt khi lùi xa hơn
  `BullTrapNearPeakExitBandPercent` (3%). Chữa nhiễu mép 1.773. Chỉ áp dụng khi `live < peak` —
  live đã xuyên (≥ peak) là chuyện của pin, không phải hysteresis (`IsNearPriorPeakWithHysteresis`
  trả false khi pierced, để pin/trap-context xử lý). Độc lập hoàn toàn với pin — không share state.
  Pure function trong `VnIndexPriorPeakAnalyzer`, có unit test.
  `VipVnIndexPeakCache.IsNearPriorPeak()` giờ trả giá trị đã hysteresis-hoá (không còn tính raw
  distance trực tiếp) — mọi caller (kể cả `isBullTrapEnv`) tự động ổn định theo.

- **Window-integrity** (`BullTrapDeferralRequireWindowIntegrity`, mặc định **tắt**): thay đọc
  shape một lần tại checkpoint (endpoint) bằng "chưa từng thủng ngưỡng suốt cửa sổ 13:00→checkpoint".
  Hai cờ tách biệt, KHÔNG tái dụng `SessionHighSinceBuy1` (chỉ chạy sau Buy1):
  - Mã: `MasterAlertSessionTracker.SymbolMasterState.AfternoonShapeIntegrityBroken` — set ngay khi
    Close rời `IsCloseNearSessionHigh` từ 13:00, cập nhật vô điều kiện (trước cả guard actionable)
    để quan sát suốt cửa sổ chứ không chỉ lúc đủ điều kiện bắn.
  - Index: `VipVnIndexPeakCache._pinWindowIntegrityBroken` — set khi `live < pin` từ 13:00 trở đi
    (đã ghim). Chỉ có ý nghĩa khi đang trong nhánh xuyên (`liveIndexAbovePin`); nếu không xuyên,
    cờ này không chặn gì (`indexShapeOk` vacuously true).
  Đổi precision/recall: dodge dead-cat bounce (nảy kỹ thuật đúng lúc đọc endpoint), đánh đổi mất
  vài reclaim thật sau một nhịp rung giữa cửa sổ. Vì vậy **opt-in**, không mặc định.

- **Foreign-hold** (`BullTrapDeferralRequireForeignHold`, mặc định **tắt**): snapshot
  `SessionForeignNet` lúc bước sang 13:00 (`SessionFlowTracker`, một lần/phiên/mã, field
  `ForeignNetAtAfternoonStart` + `AfternoonSnapshotTaken`), export `ForeignNetSinceAfternoon` =
  hiệu số. Null nghĩa là chưa qua 13:00 hoặc thiếu orderflow (không phải "0") → fail-open. Sensor
  mới, chưa backtest — khác `SessionForeignNet` là tổng lũy kế từ ATO không tách được pha chiều.

- **Gap-open trên đỉnh** (chưa xử lý, hiếm): nếu index nhảy qua đỉnh ngay ATO, env không kịp
  "lần đầu bật" trước khi live đã ≥ peak → pin không set → breakout không bị hoãn. Ghi nhận, để sau.

## Ba số cần backtest (chưa chốt, đang dùng default đề xuất)

- `BullTrapDeferralCheckpointHour/Minute` = 14:00 (khe hợp lý ~13:45–14:15).
- `BullTrapDeferralCloseWithinHighPercent` = 1.5%.
- `BullTrapNearPeakExitBandPercent` = 3% (hysteresis).

## File chạm

- `MasterAlertOptions.cs` — config deferral (slice 1) + hysteresis/integrity/foreign-hold (slice 2).
- `VnIndexPriorPeakAnalyzer.cs` — `IsNearPriorPeakWithHysteresis` (pure, unit test).
- `VipVnIndexPeakCache.cs` — `_trapPeakPinned`, `TrapContextActive`, `LiveAbovePin`,
  `_hysteresisActive`, `_pinWindowIntegrityBroken`, `PinWindowIntegrityHeld`.
- `SessionFlowTracker.cs` — snapshot foreign 13:00, `ForeignNetSinceAfternoon` trên
  `SessionFlowSnapshot`.
- `MasterAlertSessionTracker.cs` — `AfternoonShapeIntegrityBroken` per-mã.
- `TopOpportunityVipAlertEvaluator.cs` — tham số mới + cổng deferral (3 gate: close-shape,
  index-shape, foreign-hold) + `IsCloseNearSessionHigh`.
- `TopOpportunityVipAlertPublisher.cs` — tính các cờ, truyền vào, log `VIP deferred_checkpoint`.
- `docs/domain/buy-decision.md` — ngữ nghĩa deferral + slice 2.
- `StockRadar.Tests/VipAlerts/VnIndexPriorPeakAnalyzerTests.cs` — test hysteresis.
