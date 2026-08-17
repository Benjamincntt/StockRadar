# VIP bull-trap deferral — chờ chiều xác nhận trước khi bắn Buy1

**Trạng thái:** slice 1 (deferral + pin + checkpoint). Hysteresis, foreign-13:00, window-integrity = slice sau.

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

## Ngoài phạm vi (slice sau)

- **Window-integrity** vs endpoint: cờ "đã thủng ngưỡng trong cửa sổ 13:00→checkpoint" —
  state phiên riêng (KHÔNG tái dụng `SessionHighSinceBuy1`; `UpdateHigh` chỉ chạy *sau* Buy1).
  Index: cờ trên cache; từng mã: cờ trên session tracker. Endpoint qua được dead-cat nảy đúng
  lúc đọc; integrity trượt reclaim thật sau rung. Knob precision/recall, không mặc định.
- **Hysteresis distance dưới P1** (bật env ≤1.5%, tắt khi bò lại >~2.5–3%): chữa nhiễu mép 1.773.
  Độc lập với pin.
- **Snapshot foreign 13:00**: gate phân phối khối ngoại chiều (khác `SessionForeignNet` lũy kế
  từ ATO). Đắt hơn, cuối cùng.
- **Gap-open trên đỉnh:** nếu index nhảy qua đỉnh ngay ATO, env không kịp "lần đầu bật" →
  pin không set → breakout không bị hoãn. Hiếm; ghi nhận, xử lý sau nếu cần.

## Ba số để backtest

`BullTrapDeferralCheckpointHour/Minute` (14:00), `BullTrapDeferralCloseWithinHighPercent` (1.5),
và (slice sau) hysteresis band.

## File chạm (slice 1)

- `MasterAlertOptions.cs` — config deferral.
- `VipVnIndexPeakCache.cs` — `_trapPeakPinned`, `TrapContextActive`, `LiveAbovePin`.
- `TopOpportunityVipAlertEvaluator.cs` — 3 tham số mới + cổng deferral + `IsCloseNearSessionHigh`.
- `TopOpportunityVipAlertPublisher.cs` — tính `trapContextActive`/`liveAbovePin`/`pastCheckpoint`, truyền vào.
- `docs/domain/buy-decision.md` — ngữ nghĩa deferral.
