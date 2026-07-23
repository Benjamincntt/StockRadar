# Hộp tích lũy phẳng (flatBox) & Darvas breakout

## Mục đích

Nhận diện **hộp tích lũy phẳng Darvas**, 4 gate phá vỡ có xác nhận dòng tiền, FOMO so đỉnh hộp, và phân biệt với `Breakout` 20 phiên / pipeline nền legacy.

API/UI dùng **`flatBox`** (`FlatBoxProfile` / `FlatBoxDto`). `flatBox: null` = không có hộp hợp lệ; `isBreakoutConfirmed: false` = có hộp nhưng chưa đủ 4 gate.

## Nguồn đối chiếu (code entry)

| Ưu tiên | File / entry | Vai trò |
|---------|--------------|---------|
| 1 | `DarvasBreakoutAnalyzer.cs` | `AnalyzeFlatBox`, 4 gate breakout, setup zone |
| 2 | `BuyDecisionEngine.cs` | Gate hộp / FOMO / điểm nền |
| 3 | `SignalAnalyzer.cs` | Tín hiệu `DarvasBreakout` |
| 4 | `DarvasBreakoutAlertPublisher.cs` | Alert sau Job 2 |
| 5 | `PriceRunupFilterOptions` / `Darvas` settings | Ngưỡng hộp & breakout |
| 6 | `BaseQualityEvaluator.cs` | Legacy VCP/Darvas/Spring — criterion nội bộ; **không** expose `basePrice` trên stock detail |

> Khi docs lệch code → **tin code trên disk**.

## Luật as-is

### Hộp & breakout (API living)

- Quét hộp trong cửa sổ config (thường **10–45** phiên); biên hộp Darvas breakout ≤ ~**10%**.
- **4 gate** phiên phá vỡ (tóm tắt): Close > max Close hộp; xung lực giá ≥ **2.5%**; KL ≥ **1.5×** TB hộp; râu trên ≤ **0.25**.
- Pass bất kỳ phiên sau hộp → `IsBreakoutConfirmed = true`.
- **FOMO Top**: gain từ đỉnh hộp trên nến cuối **> 10%** → fail.
- **Setup zone**: Top có thể chấp nhận re-test cạnh hộp (không bắt buộc breakout confirmed) — điểm nền thấp hơn breakout confirmed.
- `SignalType.DarvasBreakout` ≠ `SignalType.Breakout` (max High 20 phiên).

### Parallel gates nền (legacy criterion)

`BaseQualityEvaluator`: gate chung (gồm `HasPriorUptrend`) + OR hình thái VCP / Darvas / Spring. Nhiều mã VN fail `HasPriorUptrend` dù hộp Darvas đẹp — **flatBox path** không bắt buộc prior uptrend như pipeline `basePrice` cũ.

Giá OHLCV lưu **nghìn đồng** (25 = 25.000đ).

## Khoảng trống / mâu thuẫn

| ID | Mô tả | Ghi chú |
|----|--------|---------|
| G-FB-1 | Hai đường “nền”: flatBox API vs BaseQuality criterion — tên/đường dễ nhầm khi đọc transcript cũ `basePrice` | Living API = `flatBox` |
| G-FB-2 | Chưa ghi thêm mâu thuẫn vận hành mới ngoài map path | Cập nhật khi Spec Kit đổi gate |

## Tài liệu liên quan

- [`buy-decision.md`](./buy-decision.md)
- Stub cũ: [`../base-price-engine.md`](../base-price-engine.md)
- Index: [`../README.md`](../README.md)
