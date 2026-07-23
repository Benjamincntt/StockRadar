# MA stack & pha thị trường (tăng trưởng)

## Mục đích

Mô tả **as-is** cách hệ tăng trưởng chọn độ chặt MA stack theo pha (`MarketWyckoffPhase`) sau khi xác nhận uptrend (FTD + MA20 + Higher Low).

**Không** dùng `MarketRegime` (Panic / Stabilizing / …) của sóng hồi — hệ **độc lập**.

## Nguồn đối chiếu (code entry)

| Ưu tiên | File / entry | Vai trò |
|---------|--------------|---------|
| 1 | `MarketPhaseClassifier.Classify` | Favorable / Neutral / Unfavorable từ VNINDEX HistoryJson |
| 2 | `SmartMoneyOpportunitySelector.BuildContext` | Gắn `MarketPhase` + `PhaseDetail` |
| 3 | `BuyDecisionEngine.ResolveMaStackStrictness` | Map pha → Full / Medium / Loose |
| 4 | `BuyDecisionEngine.RewriteMaGateForUnconfirmedMarket` | UX Attempted/Correction |
| 5 | `SignalAnalyzer.HasBullishMaStack` | Luật MA theo độ chặt |
| 6 | `SmartMoney:MarketPhase` / `MarketPhaseThresholds` | FTD 1.2%, ngày 4–7, HL 60 |

> Khi docs lệch code → **tin code trên disk**.

## Luật as-is

### Ba pha (map enum)

| Nghiệp vụ | Enum | DNA / UI | MA |
|-----------|------|----------|-----|
| Correction | `Unfavorable` | TT bất lợi | Loose |
| Attempted Rally | `Neutral` | **Nỗ lực hồi phục** | Medium |
| Confirmed Uptrend | `Favorable` | **TT thuận** | Full |

### Favorable (đủ cả bộ)

1. Index Close > MA20  
2. Slope MA20 không xuống (MA20[t] ≥ MA20[t−3])  
3. Follow-Through Day: gain ≥ **1.2%**, vol > prev & > TB20, trong ngày **4–7** của một đợt nỗ lực (quét lookback)  
4. Higher Low (pivot radius 2) trong 60 phiên  

**Không** còn gắn Favorable chỉ vì `ChangePercent > 0.5` một phiên.

### Close &lt; MA20 → Unfavorable

Else (trên/quanh MA20 nhưng thiếu FTD/HL/slope) → Neutral (Attempted Rally).

### UX cổng MA

- Phase ≠ Favorable + fail MA → **`Chờ xác nhận thị trường chung`**  
- Phase = Favorable + fail MA → **`Chưa đạt MA stack / xu hướng dài hạn`**  
- Correction: fallback Top **không** nhồi mã fail MA  

### `MarketTrend` trên card index

Vẫn có thể derive từ % phiên (hiển thị ngắn hạn) — **pha Top/MA** chỉ từ `MarketPhaseClassifier`.

## Khoảng trống / mâu thuẫn

| ID | Mô tả | Ghi chú |
|----|--------|---------|
| G-MA-1 | ~~Uptrend 1 phiên → Favorable → Full~~ | **Resolved (2026-07-23)** — feature `002-confirmed-market-uptrend` |
| G-MA-2 | Tên `MarketWyckoffPhase` vs `TrendSetupEvaluator.ClassifyMarketPhase` (criterion) | Hai đường khác nhau; Top dùng `MarketPhaseClassifier` |

## Tài liệu liên quan

- [`buy-decision.md`](./buy-decision.md)
- Spec: `specs/002-confirmed-market-uptrend/`
- Rebound: [`reversal-bounce.md`](./reversal-bounce.md)
- Index: [`../README.md`](../README.md)
