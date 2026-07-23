# Sóng hồi (ReversalBounce) — living rút gọn

## Mục đích

Chiến lược **counter-trend** tìm đáy kỹ thuật → sóng hồi: stage, điểm riêng, regime thị trường 4 trạng thái, snapshot, shadow/backtest. **Không** sửa cổng `BuyDecisionEngine` / Top tăng trưởng để “lọt” mã bắt đáy.

AIUP: [`UC-004`](../use_cases/UC-004-find-rebound-opportunities.md). Spec dài: [`../features/reversal-bounce/`](../features/reversal-bounce/).

## Nguồn đối chiếu (code entry)

| Ưu tiên | File / entry | Vai trò |
|---------|--------------|---------|
| 1 | `MarketBreadthAnalyzer` / `MarketRegimeClassifier` | Breadth → regime |
| 2 | `ReversalBounceAnalyzer` / `CounterTrendDecisionEngine` | Stage + điểm + trade plan |
| 3 | `ReversalBounceAnalysisRunner` | Cuối `DailyAnalysisRunner` sau breadth |
| 4 | Controllers `/api/v1/reversal-bounce/*` | candidates, regime, backtest, shadow-report |
| 5 | Domain fill/shadow + Infra backtest runners | Đo outcome T+2.5 |

> Khi docs lệch code → **tin code trên disk**. Spec feature folder có thể cũ hơn code — tin code.

## Luật as-is

### Hai hệ độc lập (bắt buộc)

| Hệ | Enum / điểm | Dùng cho |
|----|-------------|----------|
| Tăng trưởng | `MarketWyckoffPhase` + Buy Score | Top cơ hội, MA stack |
| Sóng hồi | `MarketRegime` (`Panic` / `Stabilizing` / `ReboundConfirmed` / `Normal`) + `TotalScore` | Candidates / UI sóng hồi |

**Cấm gộp** regime ↔ pha Wyckoff, và **cấm** coi Buy Score ≡ điểm sóng hồi trên cùng pill.

### Luồng

OHLCV → breadth snapshot → regime → analyzer stage (None → Capitulating → Stabilizing → Confirmed / Invalidated) → gate actionable + trade plan → snapshot idempotent (`TradingDate, Symbol, StrategyVersion, SetupId`).

Panic: thường chỉ watchlist, không alert mua (theo gate config).

UI mobile: cần gạt Top cơ hội ↔ Top đánh sóng hồi; chi tiết cần gạt Theo tăng trưởng ↔ Theo sóng hồi.

### Nhãn

Tránh ngôn ngữ “đã tìm thấy đáy” tuyệt đối — dùng nhãn trung tính (`reversal_bounce_labels`).

## Khoảng trống / mâu thuẫn

| ID | Mô tả | Ghi chú |
|----|--------|---------|
| G-RB-1 | File spec gốc `features/reversal-bounce/reversal-bounce.md` vẫn ghi “chưa audit / chưa code” ở header — **lệch** thực tế (0B–0D + shadow + mobile đã có) | Living = **file này** + code; header spec dài = lịch sử thiết kế |
| G-RB-2 | Một số proxy MVP (RS VsSector, NearSupplyCluster = 0) | As-is MVP |

## Tài liệu liên quan

- [`buy-decision.md`](./buy-decision.md) — không trộn cổng
- [`pipeline-jobs.md`](./pipeline-jobs.md) — thứ tự breadth → scan
- [`ma-stack-and-market-phase.md`](./ma-stack-and-market-phase.md) — pha tăng trưởng ≠ regime
- Archive audit: [`../_archive/phase0a-audit-reports/`](../_archive/phase0a-audit-reports/)
- Index: [`../README.md`](../README.md)
