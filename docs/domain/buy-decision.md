# Buy Score, cổng Top & hiển thị điểm

## Mục đích

Luật **tăng trưởng (pro-trend)**: tính Buy Score, cổng Top cơ hội (`PassesTopFilter` / `ResolveTopGateFailure`), hiển thị một điểm 0–100 Home↔detail, và VIP Master Alert gắn Top.

**Không** mô tả sóng hồi — xem [`reversal-bounce.md`](./reversal-bounce.md). Điểm sóng hồi (`ReversalBounce.totalScore`) là thang **khác**; **cấm gộp** hai hệ chấm điểm trên cùng UI/logic.

AIUP: [`UC-003`](../use_cases/UC-003-find-growth-opportunities.md) (Top), [`UC-005`](../use_cases/UC-005-manage-watchlist.md) / BR-019 (watchlist cùng Buy Score).

## Nguồn đối chiếu (code entry)

| Ưu tiên | File / entry | Vai trò |
|---------|--------------|---------|
| 1 | `BuyDecisionEngine.cs` | Score 9 tiêu chí + gates |
| 2 | `SmartMoneyOpportunitySelector.cs` | Wrapper Top + `MinPassScore` + `ClassifyMarket` |
| 3 | `DailyAnalysisRunner.cs` | Persist `DailyOpportunities`, relaxed fallback |
| 4 | `StockService.cs` | Override BuyScore từ snapshot trên detail |
| 5 | `WatchlistService.cs` | Watchlist ScorePill = Buy Score (snapshot Top / live) |
| 6 | `TopOpportunityVipAlertPublisher.cs` / `TopOpportunityVipAlertEvaluator.cs` | VIP trong phiên |

> Khi docs lệch code → **tin code trên disk**, rồi cập nhật doc này.

## Luật as-is

### Buy Score & Top

- Engine: `BuyDecisionEngine` — gates + điểm; Top strict qua selector + `SmartMoney.MinPassScore` (prod thường **62**).
- Cổng Top (`ResolveTopGateFailure`) gồm (tóm tắt): đủ lịch sử, thanh khoản TB, không phân phối, Darvas breakout **hoặc** setup zone, FOMO ≤10% so đỉnh hộp, **MA stack theo pha**, Unfavorable+RS, ngành yếu+RS, kích hoạt phiên / setup, RS âm, Buy Score ≥ MinPassScore.
- Khi pha **không** Favorable (Nỗ lực hồi phục / Điều chỉnh), lý do fail MA trên list được rewrite thành **Chờ xác nhận thị trường chung** (không đổ lỗi MA Full giả Favorable).
- Early Recovery: Loose nhưng thiếu RS → `GET /api/v1/early-recovery` (không vào Top).
- **Top hygiene (DailyAnalysisRunner):** loại `AwaitingTrigger` khỏi Top (`ExcludeAwaitingTriggerFromTop`); gate breakout theo pha (Neutral chỉ Actionable; Unfavorable cần Actionable + BuyScore ≥ `UnfavorableMinBuyScore`); tắt relaxed fallback trên `RelaxedFallbackDisabledPhases` (mặc định `Unfavorable`).

### Hiển thị một điểm 0–100

- List: `OpportunityDto.score` = snapshot `DailyOpportunity.BuyScore`.
- Detail Top ngày active: override `score` / `buyDecision.buyScore` từ snapshot; `buyScoreSource` = `snapshot` | `live`.
- Watchlist: cùng Buy Score — snapshot Top ngày active; mã ngoài Top → live `BuyDecisionEngine` (không dùng Criterion CompositeScore).
- Mobile: một `ScorePill`; không P% / ActionScore cạnh Buy Score; DNA không bucket `· Điểm`; nhãn mức giá **Giá vào**.
- Điểm sóng hồi giữ riêng (cần gạt Home / body chi tiết).

### VIP / Master Alert (tóm tắt)

- Monitor ~60s: chỉ mã trong Top ngày → Entry Ready + Master buy/sell trong phiên.
- **BuyPoint:** `% từ Open phiên` (3%/6%) **hoặc** pullback sát MA10/MA20 khi uptrend dài hạn (chỉ Buy1). Prefetch MA từ history, fail-closed nếu thiếu. Spec: [`features/vip-buy-trigger-open-pullback/spec.md`](../features/vip-buy-trigger-open-pullback/spec.md).
- **ML gate + đo:** `MlGateEnabled` + `MinMlProbToFire` theo pha; log fire → `VipAlertFires`; KPI `GET /performance/vip-alert-accuracy`. Spec: [`features/vip-intraday-ml-accuracy/spec.md`](../features/vip-intraday-ml-accuracy/spec.md).
- **LLM veto (A / ShopAIKey Claude):** sau rule+ML, gửi hồ sơ đầy đủ mã → ALLOW/BLOCK; mặc định `ShadowMode=true`. Spec: [`features/vip-deepseek-veto/spec.md`](../features/vip-deepseek-veto/spec.md).
- Bán vị thế Master: chỉ từ **T+3** (`MinTradingSessionsToSell=3`); T+0…T+2 chỉ cảnh báo rủi ro (không chữ Bán).
- **Hai chế độ thoát** (chốt lúc mở vị thế / phân loại lười vị thế cũ):
  - **UnderBase** — còn hộp nền Darvas phía trên giá vào (biên độ ≤15%, ≥20 phiên): bán 1 nửa gần cạnh dưới nền; bán hết khi bị đẩy ngược; vượt cạnh trên → chuyển **BlueSky**.
  - **BlueSky** — mốc = `max(High)` 20 phiên gần nhất, không lùi xa hơn ngày mua; bán 1 nửa khi giảm ≥4% so mốc, bán hết ≥6% (nhân hệ số pha); thủng `EntryBarLow` → bán hết ngay. Không còn gate “phải lãi ≥3%”.
- Hệ số pha (chợ xấu bán sớm): Favorable **1.25** / Neutral **1.0** / Unfavorable **0.75**.
- Chi tiết ticks/vol: code `TopOpportunityVipAlert*`; kiến trúc [`architecture.md`](../architecture.md); Spec Kit `specs/003-regime-aware-sell-exits/`.

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
