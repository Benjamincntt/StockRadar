# Research: 002-confirmed-market-uptrend

## 1. Nguồn dữ liệu index cho Favorable

**Decision**: Dùng `MarketIndices.HistoryJson` (OHLCV + Volume) của `VNINDEX` — cùng nguồn `MarketBreadthRunner.LoadIndexHistoryAsync` / ReversalBounce.

**Rationale**: `MarketIndex` domain record hiện chỉ có `Price` / `ChangePercent` / `Trend` — không đủ MA20, FTD, Higher Low. HistoryJson đã có migration và đang được sync.

**Alternatives considered**:
- Chỉ dùng `ChangePercent5d` — vẫn quá thô, không FTD/volume.
- Gọi KBS live mỗi lần classify — chậm, không tái lập backtest.
- Universe proxy (% mã trên MA20) — đó là breadth/regime sóng hồi; **không** dùng làm Favorable tăng trưởng (hiến pháp tách hệ).

## 2. Giữ enum `MarketWyckoffPhase` vs enum 3 tên mới

**Decision**: **Giữ** `Favorable` / `Neutral` / `Unfavorable`; map nghiệp vụ:
- Favorable ← Confirmed Uptrend  
- Neutral ← Attempted Rally  
- Unfavorable ← Correction  

Cập nhật nhãn DNA/UI: Neutral → **“Nỗ lực hồi phục”** (thay hoặc bổ sung “TT trung tính”); Unfavorable giữ sắc thái điều chỉnh; Favorable giữ **“TT thuận”**.

**Rationale**: Tránh migration DB/`MarketPhase` string, ML DNA parser, và diff rộng. Spec cho phép map.

**Alternatives considered**:
- Thêm `AttemptedRally` vào enum — rõ nghĩa nhưng đụng mọi `switch` + persist string.
- Enum song song mới — trùng pha, dễ lệch.

## 3. Thay `ClassifyMarket(MarketIndex)` bằng classifier có history

**Decision**: Thêm `IMarketPhaseClassifier` (hoặc static `MarketPhaseClassifier`) trong Domain:
`Classify(IReadOnlyList<OhlcvBar> indexHistory, MarketPhaseThresholds settings) → MarketWyckoffPhase`  
(+ optional debug: `HasFtd`, `AboveMa20`, `HasHigherLow` cho log/tests).

`SmartMoneyOpportunitySelector.BuildContext` nhận thêm index history (hoặc load trước khi gọi). **Ngừng** dùng `index.Trend` / `ChangePercent > 0.5` làm điều kiện đủ Favorable cho Top/MA.

**Rationale**: Khớp FR-002/003; test được không cần DB.

**Alternatives considered**:
- Sửa `MarketTrend` khi sync KBS — vẫn một phiên; không đủ FTD.
- Classify trong Infra only — trái chỗ Domain engines.

## 4. Thuật toán FTD + ngày 1 đợt nỗ lực

**Decision** (khớp Assumptions spec):

1. **MA20 / slope**: Close cuối > SMA(Close,20); `Ma20Now >= Ma20 cách 3 phiên` (tái dùng tinh thần `SignalAnalyzer.Ma20SlopeNonNegative`).
2. **Ngày 1 đợt nỗ lực**: Trong lookback 20 phiên, tìm phiên đầu tiên Close tăng (`Close > Close[prev]`) sau chuỗi áp lực (ít nhất 1 phiên Close giảm hoặc Low tạo đáy cục bộ trong 20 phiên). Nếu không rõ, lấy phiên Close thấp nhất trong 20 rồi phiên tăng đầu tiên sau đó = ngày 1.
3. **FTD hợp lệ**: Tồn tại phiên `d` với `dayIndex` ∈ [4,7] kể từ ngày 1; `(Close-ClosePrev)/ClosePrev*100 >= 1.2`; `Volume > VolumePrev` và `Volume > AvgVolume(20)` (trên các phiên trước FTD hoặc cửa sổ 20 kết thúc tại FTD — **chọn: TB volume 20 phiên ngay trước FTD**).
4. **Higher Low**: Lookback 60 phiên; pivot đáy = Low thấp hơn Low của ±2 phiên mỗi bên (pivot 2); cần ≥1 cặp đáy pivot sau > đáy pivot trước.

**Favorable** chỉ khi (1)∧(3)∧(4) và Close > MA20 ∧ slope OK.  
**Unfavorable** nếu Close < MA20.  
Else **Neutral** (Attempted Rally).

**Rationale**: Đủ “lỳ”; pivot 2 đơn giản, test được; ngưỡng 1.2% theo spec.

**Alternatives considered**:
- FTD chỉ cần Close > MA20 + 1 phiên +1.5% — yếu hơn user yêu cầu.
- Bỏ Higher Low ở MVP — spec FR-003 bắt buộc; không bỏ trong plan này.
- O’Neil FTD đúng ngày 4–10 NYSE — user đã chốt 4–7.

## 5. MA mapping & fallback

**Decision**:
| Pha | `ResolveMaStackStrictness` | Fallback list |
|-----|----------------------------|---------------|
| Favorable | Full (`FavorableMode`) | Ít dùng; strict ưu tiên |
| Neutral (Attempted) | Medium | Cho phép relaxed; **rewrite** lý do MA |
| Unfavorable | Loose | Rất hạn chế (giữ config fallback hiện có hoặc siết `FallbackMaxResults` trong tasks nếu cần) |

Khi `GateFailure` chứa “MA stack” **và** `MarketPhase != Favorable`, reason hiển thị = **“Chờ xác nhận thị trường chung”** (FR-008). Khi Favorable, giữ câu MA (FR-009).

**Rationale**: Sau classify mới, false Favorable giảm; rewrite bảo hiểm list nới + Medium fail.

**Alternatives considered**:
- Tắt hẳn cổng MA khi Neutral — đổi quá nhiều điểm; Medium đủ.
- Chỉ đổi UX không đổi classify — không đóng G-MA-1.

## 6. `MarketTrend` trên card thị trường

**Decision**: **Giữ** `MarketTrend` từ % phiên cho hiển thị index ngắn hạn (không đổi KBS sync trong scope tối thiểu). **Pha Top/MA** chỉ từ classifier history. Log analysis ghi rõ `Phase` từ classifier.

**Rationale**: Tách “trend phiên” vs “pha chiến lược”; tránh phá dashboard.

**Alternatives considered**: Đồng bộ Trend = Favorable only — dễ nhầm UI “uptrend” với Favorable.

## 7. Callers cần parity

**Decision**: Mọi chỗ gọi `BuildContext` / `ClassifyMarket` cho Top phải truyền index history: `DailyAnalysisRunner`, `SmartMoneyBacktestRunner` (đã có index history bars), criterion path nếu dùng cùng pha Top (kiểm tra — `TrendSetupEvaluator.ClassifyMarketPhase` là đường **khác**, không đổi trừ khi tasks phát hiện dùng chung Top).

**Rationale**: Backtest lệch production nếu vẫn % một phiên.

## 8. Docs & gap

**Decision**: Cập nhật `docs/domain/ma-stack-and-market-phase.md` as-is mới; đánh dấu G-MA-1 **resolved**; stub/README nếu cần trỏ.

## Research outcome

Mọi điểm NEEDS CLARIFICATION kỹ thuật đã resolve bằng quyết định trên — sẵn sàng data-model / contracts / quickstart.
