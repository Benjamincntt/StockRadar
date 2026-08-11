# Lợi nhuận thực (Realized P&L)

## Mục đích

Đo hiệu quả **một lệnh đã đóng** bằng % lợi nhuận thật (giá tại tín hiệu Bán nửa/Bán hết, trừ phí + thuế, trọng số theo tỷ trọng thực bán) — khác với **T+2.5** (`ForwardPriceT25`/`ForwardReturnPercent`), vốn chỉ đo "setup mua có tốt không" bằng giá đóng cửa T+2/T+3, bỏ qua toàn bộ luật bán.

Hai bộ số **sống song song**, không thay thế nhau: `HitCalibrationService`, `FalsePositiveMiningService`, `ShadowAnalysisService`, `EntryTimingService`, `OpportunityNorthStarQueryService` vẫn phụ thuộc T+2.5 nguyên trạng. Realized là bộ dữ liệu mới, chỉ tính khi vị thế đã đóng (`IsClosed = true`).

## Nguồn đối chiếu (code entry)

| Ưu tiên | File / entry | Vai trò |
|---------|--------------|---------|
| 1 | `RealizedPnlMath.cs` (Domain) | Math thuần: `NetLegReturnPercent`, `Compute`, `Classify` — cạnh `TradingSessionMath` |
| 2 | `RealizedPnlService.cs` (Application) | Đo vị thế đã đóng, ghi `MasterAlertPositionEntity.Realized*` |
| 3 | `RealizedPnlBackfillService.cs` (Application) | Gắn `PositionId` cho `SetupTracks` cũ + dựng leg cho vị thế đóng trước khi có `PositionSellLegs` |
| 4 | `PositionSellLegEntity` (`PerformanceEntities.cs`) | 1 dòng = 1 nhịp bán (`BanNua`/`BanHet`), có `PriceSource` để audit |
| 5 | `TopOpportunityVipAlertPublisher.cs` — `RecordSellHalfAsync`/`CloseAsync` | Đường ghi giá bán thật khi bắn tín hiệu VIP |
| 6 | `OpportunityPerformanceRunner.cs` | Gọi `RealizedPnlService` **sau cùng**, ngoài block đo T+2.5 |
| 7 | `PerformanceController.cs` | `GET /realized-trades`, `POST /measure-realized`, `POST /backfill-realized` |
| 8 | Mobile: `performance_screen.dart`, `alert_history_screen.dart` | Card "Lợi nhuận thực" + toggle T+2.5 ↔ Realized |

> Khi docs lệch code → **tin code trên disk**.

## Luật as-is

### Công thức (`RealizedPnlMath`)

```
buyCost       = entryPrice × (1 + BuyFeePercent/100)
netProceeds_i = sellPrice_i × (1 − (SellFeePercent + SellTaxPercent)/100)
R_i           = (netProceeds_i − buyCost) / buyCost × 100

WeightedReturnPercent   = Σ (SoldSize_i × R_i)         ← đóng góp NAV, KHÔNG normalize theo tổng size
ReturnOnDeployedPercent = WeightedReturnPercent / Σ SoldSize_i
GrossReturnOnDeployedPercent = như trên, tính với FeeProfile.Zero (không phí) — để so lệch phí ăn vào lãi
```

`Classify` (Good/Flat/Failed) dùng **`ReturnOnDeployedPercent`**, không dùng `WeightedReturnPercent` — nếu dùng Weighted thì lệnh size 0.5 bị so cùng thang với lệnh size 1.0, sai khi ngưỡng Win khác 0.

Ví dụ (bỏ phí cho gọn): size 1.0, bán nửa 0.5 @R1=+10%, bán hết 0.5 @R2=+5% → Weighted **+7.5%**, OnDeployed **+7.5%**. Size 0.5 (bán nửa 0.25 + bán hết 0.25, cùng R1/R2) → Weighted **+3.75%**, OnDeployed **+7.5%** — cùng chất lượng lệnh nhưng đóng góp NAV thấp hơn vì mua ít.

### Phí + ngưỡng Win

Default (`RealizedPnlOptions`, section `RealizedPnl`): phí mua **0.15%**, phí bán **0.25%**, thuế bán **0.1%**, `WinThresholdPercent = 0` — tức **Win = lãi ròng > 0%** trên `ReturnOnDeployedPercent` (không có vùng Flat rộng như T+2.5's `≥1%`/`0…<1%`). Đổi phí trong config sẽ **tự động recompute**: `RealizedPnlService` so `RealizedFeeProfile` (khoá `FeeProfile.Key()`, InvariantCulture) đã lưu trên vị thế với khoá hiện tại; lệch → đo lại.

### Trọng số theo size thực bán

Quyết định chốt: trọng số mỗi nhịp bán là **size thực bán tuyệt đối** (không normalize về tổng 1.0). `WeightedReturnPercent` dùng để cộng dồn "đóng góp NAV" giữa nhiều lệnh (`TotalWeightedReturnPercent` ở summary); `ReturnOnDeployedPercent` dùng để so sánh **chất lượng** giữa các lệnh khác size.

### Lệnh còn mở

Vị thế chưa `BanHet` (`IsClosed = false`) → **không** mark-to-market, **không** tính realized. Vẫn hiển thị/đo bằng T+2.5 như trước. Vì vậy `RealizedPnlSummary`/`AlertHistoryResponse` luôn có `openTrades`/`totalOpenTrades` tách riêng khỏi `closedTrades`.

### 3 giá trị `status` (`RealizedStatusNames`)

| Giá trị | Ý nghĩa |
|---------|---------|
| `Measured` | Toàn bộ leg có `PriceSource = "Fire"` — giá bán thật từ tín hiệu VIP (`VipAlertFires`) |
| `Approximate` | Ít nhất 1 leg là giá backfill (`ForwardT25`/`OhlcvClose`) — suy diễn, không phải giá bắn noti thật |
| `MissingSellPrice` | Không dựng được leg nào, hoặc `entryPrice <= 0` — các cột % để `null`, đánh dấu `RealizedMeasured = true` để không quét lại vô hạn |

Vị thế đóng **trước khi** `PositionSellLegs`/`VipAlertFires` tồn tại (backfill lịch sử) hầu hết rơi vào `Approximate`, vì nguồn giá duy nhất dựng được là T+2.5 tính từ ngày mua (1 leg `BanHet` duy nhất, `SoldSize = MaxPositionSize` — không suy diễn được nhịp bán nửa vì không biết ngày bán nửa thật).

`IncludeApproximateInAggregates = true` (default): lệnh `Approximate` vẫn gộp vào số liệu tổng (vì proxy là chính T+2.5 — thước đo hiện đang dùng), nhưng UI **luôn** hiện breakdown "N lệnh giá bán thật / M lệnh gần đúng (T+2.5)" + badge trên từng dòng, để số liệu trung thực.

### Chống đếm trùng

Mọi con số realized tổng hợp (`RealizedPnlSummaryDto`, `AlertHistoryResponseDto.TotalClosedTrades`/`RealizedWinCount`/…) tính từ **`MasterAlertPositions`** — 1 dòng = 1 lệnh. Một vị thế có thể sinh 2 `SetupTrack` (`MuaDiem1` + `MuaDiem2`), nhưng chỉ đếm là **1 lệnh**. Nếu aggregate nhầm từ `SetupTracks` thì lệnh có cả 2 track bị đếm 2 lần, phồng win rate.

Trend bucket (`AlertHistoryTrendBucketDto`) vẫn group theo `EntryDate` (giống cột T+2.5) để so trực tiếp cùng kỳ — trade-off: P&L thực hiện (theo `ClosedDate`) có thể thuộc kỳ sau kỳ vào lệnh.

### Vì sao win rate realized thường THẤP HƠN T+2.5

1. **Ngưỡng khắt khe hơn**: Win T+2.5 = lãi ≥1% (gross, giá đóng cửa cố định T+2/T+3); Win realized = lãi ròng >0% sau phí mua 0.15% + phí bán 0.25% + thuế 0.1% (~0.5% round-trip ăn thẳng vào lãi).
2. **Luật bán chốt sớm**: `BanNua`/`BanHet` có thể chốt lời trước khi giá chạy hết tiềm năng mà T+2.5 "nhìn thấy", hoặc cắt lỗ ở mức tệ hơn giá đóng cửa T+2.5 nếu thị trường đảo chiều nhanh.
3. **Dữ liệu `Approximate`** dùng chính T+2.5 làm proxy cho vị thế lịch sử — không thêm nhiễu, nhưng không cải thiện gì so với T+2.5 gốc.

Vì lý do trên, UI luôn hiện song song 2 chỉ số (toggle, không thay thế), kèm `MethodologyNote` giải thích công thức ngay trên card.

## API / Model

Xem chi tiết field trong `PerformanceDtos.cs` (`RealizedPnlSummaryDto`, `RealizedTradeDto`, mở rộng `OpportunityPerformanceSummaryDto.Realized`, `AlertHistoryResponseDto`, `AlertHistoryItemDto`, `AlertHistoryTrendBucketDto`) và model Dart tương ứng trong `mobile/lib/core/models/models.dart` (`RealizedPnlSummary`, `RealizedTrade`). Toàn bộ field mới là **append-only, nullable/có default** — client cũ (mobile/web bản trước) gọi API mới vẫn chạy bình thường; API cũ chưa deploy thì mobile mới coi `realized == null` là empty state.

Endpoints: `GET /performance/realized-trades?days=180&limit=100`, `POST /performance/measure-realized`, `POST /performance/backfill-realized?days=365&dryRun=true`. `POST /performance/measure` (T+2.5) giữ nguyên semantics.

## Khoảng trống / mâu thuẫn

| ID | Mô tả | Ghi chú |
|----|--------|---------|
| G-RPNL-1 | Web (`PerformancePage.tsx`) chưa hiển thị realized | Scope Đợt 3 chỉ backend + mobile; API append-only nên web cũ vẫn chạy |
| G-RPNL-2 | Trend bucket group theo `EntryDate`, không phải `ClosedDate` | Trade-off có chủ đích để so cùng kỳ với T+2.5 — xem "Chống đếm trùng" |
| G-RPNL-3 | `WeeklyOpportunityReviewEntity` chưa có cột realized | Aggregate on-the-fly ở query service, chưa denormalize |

## Tài liệu liên quan

- [`buy-decision.md`](./buy-decision.md) — luật bán 2 nhịp `BanNua`/`BanHet` sinh ra leg cho realized
- [`pipeline-jobs.md`](./pipeline-jobs.md) — `RealizedPnlService` chạy cuối `OpportunityPerformanceRunner`, sau toàn bộ luồng T+2.5
- Index: [`../README.md`](../README.md)
