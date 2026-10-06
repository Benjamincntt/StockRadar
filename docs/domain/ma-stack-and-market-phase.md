# MA stack & pha thị trường (tăng trưởng)

## Mục đích

Mô tả **as-is** cách hệ tăng trưởng chọn độ chặt MA stack theo pha (`MarketWyckoffPhase`) sau khi xác nhận uptrend (FTD + MA20 + Higher Low), và luật phân loại pha thị trường.

> **Cập nhật 2026-09 (gate cleanup):** MA stack **không còn là cổng Top** — `ResolveTopGateFailure` chỉ còn 3 cổng (FOMO / Unfavorable / sóng ngành). MA stack giờ chỉ còn: (1) dòng checklist "Xếp lớp MA" trong `BuildEntry`, (2) cờ `HasMaStack` expose cho playbook classifier. Luật độ chặt Full/Medium/Loose giữ nguyên cho checklist.

**Không** dùng bất kỳ nhãn pha song song nào trên UI — `MarketPhaseClassifier` là nguồn **duy nhất** cho nhãn nhận định thị trường (Top, VNINDEX card). `MarketRegime` breadth đã gỡ bỏ (spec `008-remove-reversal-bounce`).

## Nguồn đối chiếu (code entry)

| Ưu tiên | File / entry | Vai trò |
|---------|--------------|---------|
| 1 | `MarketPhaseClassifier.Classify` | Favorable / Neutral / Unfavorable từ VNINDEX HistoryJson |
| 2 | `SmartMoneyOpportunitySelector.BuildContext` | Gắn `MarketPhase` + `PhaseDetail` |
| 3 | `BuyDecisionEngine.ResolveMaStackStrictness` | Map pha → Full / Medium / Loose (cho checklist MA) |
| 4 | `SignalAnalyzer.HasBullishMaStack` | Luật MA theo độ chặt |
| 5 | `SmartMoney:MarketPhase` / `MarketPhaseThresholds` | FTD 1.2%, ngày 4–7, HL 60 |

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

### Vai trò hiện tại của MA stack (2026-09)

- **Không chặn Top** — fail MA không còn loại mã khỏi `DailyOpportunities` (không còn rewrite "Chờ xác nhận thị trường chung" — `RewriteMaGateForUnconfirmedMarket` đã xóa).
- Chỉ hiện ở **checklist điểm vào**: mục `ma` "Xếp lớp MA" (✓/✗) — ảnh hưởng `Confidence` (%) của entry, không ảnh hưởng vào/top.
- Pha thị trường vẫn dùng thật ở: Buy Score component `market` (Favorable 5 / Neutral 2 / Unfavorable 0), cổng Unfavorable + RS, gate breakout theo pha trong `ApplyTopHygiene`.  

### `MarketTrend` trên card index

Vẫn có thể derive từ % phiên (hiển thị ngắn hạn) — **pha Top/MA** chỉ từ `MarketPhaseClassifier`.

## Khoảng trống / mâu thuẫn

| ID | Mô tả | Ghi chú |
|----|--------|---------|
| G-MA-1 | ~~Uptrend 1 phiên → Favorable → Full~~ | **Resolved (2026-07-23)** — feature `002-confirmed-market-uptrend` |
| G-MA-2 | Tên `MarketWyckoffPhase` vs `TrendSetupEvaluator.ClassifyMarketPhase` (criterion) | Hai đường khác nhau; Top dùng `MarketPhaseClassifier` |
| G-MA-3 | Comment trong `BuildScore` vẫn ghi "MA stack chỉ còn là gate cứng (ResolveTopGateFailure)" — code thật không còn check MA trong gate | Comment stale; tin code chạy, không tin comment |

## Tài liệu liên quan

- [`buy-decision.md`](./buy-decision.md)
- Spec: `specs/002-confirmed-market-uptrend/`
- Index: [`../README.md`](../README.md)
