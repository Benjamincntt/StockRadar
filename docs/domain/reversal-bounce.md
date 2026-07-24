# Sóng hồi (ReversalBounce) — living rút gọn

## Mục đích

Chiến lược **counter-trend** tìm đáy kỹ thuật → sóng hồi: stage, điểm riêng, snapshot, shadow/backtest. **Không** sửa cổng `BuyDecisionEngine` / Top tăng trưởng để “lọt” mã bắt đáy.

AIUP: [`UC-004`](../use_cases/UC-004-find-rebound-opportunities.md). Spec dài: [`../features/reversal-bounce/`](../features/reversal-bounce/).

## Nguồn đối chiếu (code entry)

| Ưu tiên | File / entry | Vai trò |
|---------|--------------|---------|
| 1 | `MarketPhaseClassifier` | **Nhận định thị trường UI** — cùng Top / VNINDEX card |
| 2 | `MarketBreadthAnalyzer` / `MarketRegimeClassifier` | Breadth metrics + gate nội bộ (không hiện làm nhãn TT) |
| 3 | `ReversalBounceAnalyzer` / `CounterTrendDecisionEngine` | Stage + điểm + trade plan |
| 4 | `ReversalBounceAnalysisRunner` | Cuối `DailyAnalysisRunner` sau breadth |
| 5 | Controllers `/api/v1/reversal-bounce/*` | candidates, regime, backtest, shadow-report |
| 6 | Domain fill/shadow + Infra backtest runners | Đo outcome T+2.5 |

> Khi docs lệch code → **tin code trên disk**. Spec feature folder có thể cũ hơn code — tin code.

## Luật as-is

### Một nhận định thị trường (bắt buộc)

Toàn app **một** bộ nhận định pha TT từ `MarketPhaseClassifier` (FTD + MA20 + Higher Low):

| Enum | Nhãn VI (Top / sóng hồi / card) |
|------|----------------------------------|
| `Favorable` | TT thuận |
| `Neutral` | Nỗ lực hồi phục |
| `Unfavorable` | Điều chỉnh |

API sóng hồi (`GET .../market-regime`, field `regime` / `regimeLabel`, và `marketRegime` trên candidate) **trả pha này** — không còn nhãn breadth “Đang cân bằng / Hoảng loạn” trên UI “Thị trường”.

Breadth `MarketRegime` (`Panic` / `Stabilizing` / …) vẫn tính và lưu trong snapshot để **gate actionable nội bộ**; expose tùy chọn qua `breadthRegime` — **cấm** dùng làm nhận định TT cạnh pha Top.

### Điểm / stage (tách với Buy Score)

| Hệ | Enum / điểm | Dùng cho |
|----|-------------|----------|
| Tăng trưởng | Buy Score | Top cơ hội |
| Sóng hồi | Stage + `TotalScore` | Candidates / body chi tiết sóng hồi |

**Cấm** coi Buy Score ≡ điểm sóng hồi trên cùng pill.

### Luồng

OHLCV → breadth snapshot (gate) → analyzer stage (None → Capitulating → Stabilizing → Confirmed / Invalidated) → gate actionable + trade plan → snapshot idempotent (`TradingDate, Symbol, StrategyVersion, SetupId`).

UI: nhận định TT lấy từ pha Top. Home sóng hồi: **Tín hiệu mua** (`IsActionable`) + **Theo dõi** Stage A/B (không chỉ actionable).

Gate Stabilizing: `StabilizingMinDemand` = **13** (≤ max DemandScore 15; không dùng 18 lệch scale).

UI mobile: gạt Top cơ hội ↔ Top đánh sóng hồi; chi tiết gạt Theo tăng trưởng ↔ Theo sóng hồi.

### Nhãn

Tránh ngôn ngữ “đã tìm thấy đáy” tuyệt đối — dùng nhãn trung tính (`reversal_bounce_labels`). Stage mã vẫn có “Đang cân bằng” (stage B) — **khác** nhận định TT.

## Khoảng trống / mâu thuẫn

| ID | Mô tả | Ghi chú |
|----|--------|---------|
| G-RB-1 | File spec gốc `features/reversal-bounce/reversal-bounce.md` vẫn ghi “chưa audit / chưa code” ở header — **lệch** thực tế | Living = **file này** + code |
| G-RB-2 | Một số proxy MVP (RS VsSector, NearSupplyCluster = 0) | As-is MVP |
| G-RB-3 | Gate actionable vẫn map breadth `MarketRegime`; UI đã thống nhất pha Top | Có thể migrate gate sang pha sau |
| G-RB-4 | ~~StabilizingMinDemand=18 > Demand max 15~~ | **Resolved** — mặc định 13 |

## Tài liệu liên quan

- [`buy-decision.md`](./buy-decision.md) — không trộn cổng Buy Score
- [`ma-stack-and-market-phase.md`](./ma-stack-and-market-phase.md) — luật pha Top (nguồn nhận định TT)
- [`pipeline-jobs.md`](./pipeline-jobs.md) — thứ tự breadth → scan
- Archive audit: [`../_archive/phase0a-audit-reports/`](../_archive/phase0a-audit-reports/)
- Index: [`../README.md`](../README.md)
