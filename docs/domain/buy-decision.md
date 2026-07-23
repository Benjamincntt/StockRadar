# Buy Score, cổng Top & hiển thị điểm

## Mục đích

Luật **tăng trưởng (pro-trend)**: tính Buy Score, cổng Top cơ hội (`PassesTopFilter` / `ResolveTopGateFailure`), hiển thị một điểm 0–100 Home↔detail, và VIP Master Alert gắn Top.

**Không** mô tả sóng hồi — xem [`reversal-bounce.md`](./reversal-bounce.md). Điểm sóng hồi (`ReversalBounce.totalScore`) là thang **khác**; **cấm gộp** hai hệ chấm điểm trên cùng UI/logic.

AIUP: [`UC-003`](../use_cases/UC-003-find-growth-opportunities.md).

## Nguồn đối chiếu (code entry)

| Ưu tiên | File / entry | Vai trò |
|---------|--------------|---------|
| 1 | `BuyDecisionEngine.cs` | Score 9 tiêu chí + gates |
| 2 | `SmartMoneyOpportunitySelector.cs` | Wrapper Top + `MinPassScore` + `ClassifyMarket` |
| 3 | `DailyAnalysisRunner.cs` | Persist `DailyOpportunities`, relaxed fallback |
| 4 | `StockService.cs` | Override BuyScore từ snapshot trên detail |
| 5 | `TopOpportunityVipAlertPublisher.cs` / `TopOpportunityVipAlertEvaluator.cs` | VIP trong phiên |

> Khi docs lệch code → **tin code trên disk**, rồi cập nhật doc này.

## Luật as-is

### Buy Score & Top

- Engine: `BuyDecisionEngine` — gates + điểm; Top strict qua selector + `SmartMoney.MinPassScore` (prod thường **62**).
- Cổng Top (`ResolveTopGateFailure`) gồm (tóm tắt): đủ lịch sử, thanh khoản TB, không phân phối, Darvas breakout **hoặc** setup zone, FOMO ≤10% so đỉnh hộp, **MA stack theo pha**, Unfavorable+RS, ngành yếu+RS, kích hoạt phiên / setup, RS âm, Buy Score ≥ MinPassScore.
- Khi pha **không** Favorable (Nỗ lực hồi phục / Điều chỉnh), lý do fail MA trên list được rewrite thành **Chờ xác nhận thị trường chung** (không đổ lỗi MA Full giả Favorable).
- Early Recovery: Loose nhưng thiếu RS → `GET /api/v1/early-recovery` (không vào Top).

### Hiển thị một điểm 0–100

- List: `OpportunityDto.score` = snapshot `DailyOpportunity.BuyScore`.
- Detail Top ngày active: override `score` / `buyDecision.buyScore` từ snapshot; `buyScoreSource` = `snapshot` | `live`.
- Mobile: một `ScorePill`; không P% / ActionScore cạnh Buy Score; DNA không bucket `· Điểm`; nhãn mức giá **Giá vào**.
- Điểm sóng hồi giữ riêng (cần gạt Home / body chi tiết).

### VIP / Master Alert (tóm tắt)

- Monitor ~60s: chỉ mã trong Top ngày → Entry Ready + Master buy/sell trong phiên.
- Bán vị thế Master: chỉ từ **T+3** (`MinTradingSessionsToSell=3`); T+0…T+2 chỉ cảnh báo rủi ro (không chữ Bán).
- Chi tiết ticks/vol: code `TopOpportunityVipAlert*`; kiến trúc [`architecture.md`](../architecture.md).

### MA stack

Xem [`ma-stack-and-market-phase.md`](./ma-stack-and-market-phase.md) — **không** nhân bản bảng Full/Medium/Loose ở đây.

### Hộp phẳng

Xem [`base-price-flatbox.md`](./base-price-flatbox.md).

## Khoảng trống / mâu thuẫn

| ID | Mô tả | Ghi chú |
|----|--------|---------|
| G-BD-1 | ~~Gap MA Favorable=Full khi index uptrend 1 phiên~~ | **Resolved** — xem `ma-stack-and-market-phase.md` (FTD+MA20+HL) |
| G-BD-2 | FE web ActionScore / PredictedHit chưa đồng bộ đợt hiển thị mobile | As-is; ưu tiên mobile đã làm |

## Tài liệu liên quan

- Domain: [`ma-stack-and-market-phase.md`](./ma-stack-and-market-phase.md), [`base-price-flatbox.md`](./base-price-flatbox.md), [`pipeline-jobs.md`](./pipeline-jobs.md)
- Rebound (tách): [`reversal-bounce.md`](./reversal-bounce.md)
- AIUP: UC-003
- Index: [`../README.md`](../README.md)
- Stub cũ: `../opportunity-scan-rules.md`, `../smartmoney-checklist.md`, `../buy-score-display.md`, `../telegram-vip-alerts-flow.md`
