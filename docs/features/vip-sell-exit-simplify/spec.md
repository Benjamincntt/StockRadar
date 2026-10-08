# VIP V1 — Rút gọn logic bán (bỏ phủ nhận nến, bỏ UnderBase, bỏ hệ số pha)

Trạng thái: **ĐÃ COMMIT 2026-10-08 (`baa7315`, sửa thêm trong `9abe799`)**. Chưa xác nhận deploy, chưa chạy live trong phiên. Các câu hỏi mục 0 đã chốt theo cột mặc định. Kết quả ở mục 7, bài học ở mục 8.
Phạm vi: chỉ luồng VIP alert **V1**. V2 (Pha 1/2/3, `KichBan*`) **không** dùng code này. Đã grep xác nhận ngày 2026-10-08.

## 0. Câu hỏi cần chủ sản phẩm chốt trước

| # | Câu hỏi | Mặc định đang ghi trong tài liệu |
|---|---|---|
| Q1 | Luật 2b (BlueSky: rút từ mốc ≥ 4% bán nửa, ≥ 6% bán hết) **giữ nguyên** không? | Giữ. Sau thay đổi, 2b áp cho **mọi** vị thế. |
| Q2 | Bỏ 2a thì bỏ luôn **việc phân loại UnderBase/BlueSky** (dò nền phía trên, chuyển chế độ, dòng "Chế độ:" trong tin)? | Bỏ hẳn. Không giữ phần phân loại chỉ để hiển thị. |
| Q3 | Các cột DB `ExitRegime`, `OverheadBaseLow`, `OverheadBaseHigh`, `EntryBarLow` trên bảng vị thế VIP: drop hay để nguyên? | Để nguyên, không viết migration, chỉ ngừng ghi/đọc. Drop ở một change set riêng sau khi chạy ổn. |
| Q4 | Bỏ hệ số pha thì các ngưỡng 4% / 6% dùng **giá trị gốc trong config** cho mọi pha? | Đúng: `SellPoint1DropFromAnchorPercent`, `SellPoint2DropFromAnchorPercent` dùng thẳng. |

## 1. Bối cảnh

Ngày 07/10/2026, mã VIB bắn tin "Mua 1 nửa" lúc 14:15. Hai phút sau (14:17), hệ bắn tiếp "Cảnh báo rủi ro T+0". Nguyên nhân là nhánh UnderBase: hàm dò nền phía trên tìm ra một "nền" có đáy ≈ giá mua. Biên độ nền cho phép tới 15%, hệ quét lùi tới 250 phiên và chọn nền sát giá mua nhất. Vì vậy điều kiện chốt nửa đúng ngay từ phút mua. Chủ sản phẩm quyết định rút gọn logic bán như dưới đây.

## 2. Logic bán hiện tại (code ngày 2026-10-08)

Hàm `TopOpportunityVipAlertEvaluator.EvaluatePositionSignal`:

| Thứ tự | Luật | Quyết định |
|---|---|---|
| 1 | Đóng cửa < `EntryBarLow` (giá thấp nhất của nến lúc mua) → `SellAll` | **BỎ** |
| 2a | UnderBase: Close ≥ `OverheadBaseLow × (1 − buffer)` → `SellPoint1Half`; đã bán nửa mà Close < `OverheadBaseLow` → `SellAll` | **BỎ** |
| 2b | BlueSky: rút từ mốc ≥ stop2 → `SellAll`; ≥ stop1 → `SellPoint1Half` | **GIỮ** (Q1) |
| 3 | Phân phối (`IsDistributionScan`): peakGain ≥ `CutAllMinPeakGainPercent` → `SellAll`; ≥ `CutLoss1MinPeakGainPercent` → `SellPoint1Half` | **GIỮ** |
| — | `MarketPhaseMultipliers[phase]` nhân vào stop1, stop2 và buffer nền | **BỎ** |
| — | Chưa đủ T+2.5 → hạ cấp thành `RiskWarningIntraday` (hàm `Emit`, nhánh `severe`) | Giữ nguyên |

## 3. Logic bán sau thay đổi

```
peakGain           = (max(PeakPriceSinceEntry, row.High) - EntryPrice) / EntryPrice * 100
drawdownFromAnchor = max(0, (anchor - Close) / anchor * 100)
stop1 = cfg.SellPoint1DropFromAnchorPercent      // không nhân hệ số pha
stop2 = cfg.SellPoint2DropFromAnchorPercent

candidate = null
if drawdownFromAnchor >= stop2                 → SellAll
else if !soldHalf && drawdownFromAnchor >= stop1 → SellPoint1Half
if candidate == null && IsDistributionScan(scan):
    peakGain >= CutAllMinPeakGainPercent       → SellAll
    !soldHalf && peakGain >= CutLoss1MinPeakGainPercent → SellPoint1Half
if candidate != null → Emit(canSell, riskAlready, candidate)
if !canSell && !riskAlready && (IsDistributionScan(scan) || drawdownFromAnchor >= RiskWarningDrawdownFromPeakPercent)
    → RiskWarningIntraday
return null
```

Tham số `marketPhase` của hàm không còn được dùng cho logic bán. Nếu không còn chỗ nào dùng thì xoá khỏi chữ ký hàm và bên gọi.

## 4. Danh sách việc cho người thực hiện

### 4.1 Evaluator — `backend/StockRadar.Infrastructure/Notifications/TopOpportunityVipAlertEvaluator.cs`
- Xoá khối "Phủ nhận cây vượt đỉnh" (`EntryBarLow`).
- Xoá nhánh `IsUnderBase(...)`. Nhánh BlueSky trở thành logic mặc định duy nhất.
- Xoá đoạn tra `MarketPhaseMultipliers` và `mult`.

### 4.2 Publisher — `backend/StockRadar.Infrastructure/Notifications/TopOpportunityVipAlertPublisher.cs`
- Xoá `EnsureExitRegimeAsync` (lazy classify) và khối chuyển `UnderBase → BlueSky` (quanh dòng 565–591). Nếu Q2 = bỏ hẳn: vị thế mới không gọi `FindOverheadBox` nữa (quanh dòng 460).
- Hàm dựng nội dung tin bán (quanh dòng 1225–1290):
  - bỏ tra `MarketPhaseMultipliers`;
  - bỏ dòng `Chế độ: {regime}`, bỏ "Đã chạm vùng mục tiêu nền", bỏ "Mục tiêu cạnh dưới nền";
  - bỏ "Phủ nhận cây vượt đỉnh".
- `AnchorWindowStart`: giữ nguyên cách tính mốc hiện tại. Cửa sổ mốc tính từ ngày mua và không còn bị đặt lại khi chuyển chế độ.

### 4.3 Code chỉ phục vụ UnderBase (nếu Q2 = bỏ hẳn)
- `VipPositionHistoryCache.FindOverheadBox` → xoá.
- `DarvasBreakoutAnalyzer.FindNearestOverheadBox` → xoá. **Không** đụng `Analyze` / `Evaluate` / `TryFindBoxWindow`, vì các hàm này dùng cho luồng breakout.
- `MasterAlertExitRegimes` → xoá nếu không còn ai dùng.
- `MasterAlertOptions` + `appsettings.json`: xoá `OverheadBoxMinSessions`, `OverheadBoxMaxHeightPercent`, `OverheadBaseMaxAgeSessions`, `OverheadBaseBufferPercent`, `MarketPhaseMultipliers`. Grep trước, chỉ xoá khi không còn chỗ dùng.
- `VipLlmContextBuilder`: bỏ các trường `OverheadBaseLow/High`, `EntryBarLow` khỏi context gửi AI.
- Repository / DTO (`IPerformanceServices`, `EfPerformanceRepositories`, `PerformanceDtos`): ngừng ghi/đọc các trường trên theo Q3.
- **Lưu ý:** `appsettings.Production.json` trên server khác bản trong repo. Phải báo người vận hành xoá các key tương ứng trên server, không sửa file trong repo rồi coi như xong.

### 4.4 Test — `backend/StockRadar.Tests/SellExit/`
- `UnderBaseExitTests.cs`, `OverheadBoxTests.cs` → xoá (nếu Q2 = bỏ hẳn).
- `BlueSkyStopTests.cs`: ca "dưới EntryBarLow → SellAll" phải đổi kỳ vọng thành **không bán** (giảm 2.5% < stop).
- `BlueSkyThresholdTests.cs` / `SellExitFixtures.cs`: bỏ `MarketPhaseMultipliers`. Thêm ca: cùng mức rút 4%, pha `Unfavorable` và `Favorable` cho cùng kết quả.
- `DarvasRegressionTests.cs`: đang dùng helper `OverheadBoxTests.BuildBoxThenBreak`. Chuyển helper sang `SellExitFixtures` trước khi xoá file.
- Thêm ca hồi quy cho VIB: vị thế mới mua, giá = giá mua, chưa đủ T+2.5, không có phân phối, rút từ mốc < 4% → **không** trả `RiskWarningIntraday`.

### 4.5 Tài liệu
- `docs/domain/buy-decision.md`: cập nhật mục logic bán VIP.
- `specs/003-regime-aware-sell-exits/`: ghi chú các FR đã bị thay thế (UnderBase, FR-005a, phủ nhận nến, hệ số pha) và trỏ sang tài liệu này.
- Theo constitution, đây là thay đổi cổng VIP nên cần đi qua Spec Kit (`/speckit-specify`).

## 5. Tiêu chí hoàn thành
- `dotnet build` sạch; `dotnet test --filter SellExit` xanh.
- Grep `UnderBase|OverheadBase|FindNearestOverheadBox|MarketPhaseMultipliers|EntryBarLow` trong `backend/` (trừ Migrations và cột DB giữ theo Q3) không còn kết quả.
- Không file V2 nào bị sửa.
- Báo cáo kèm: file đã sửa, test đã chạy, phần chưa kiểm được (ví dụ chưa chạy live trong phiên).

## 6. Rủi ro đã biết
- Bỏ luật 1 thì vị thế breakout thất bại (đóng cửa thủng đáy nến mua) chỉ bị bán khi rút đủ 6% từ mốc. Lỗ có thể sâu hơn trước.
- Bỏ 2a thì mất mục tiêu chốt lời tại vùng cản. Chốt lời chỉ còn dựa vào luật rút từ đỉnh và luật phân phối.
- Bỏ hệ số pha thì ở pha xấu, ngưỡng cắt không còn được siết lại.

## 7. Kết quả implement (2026-10-08)

- **Đã đổi:** logic bán chỉ còn (a) rút từ mốc ≥ 4% / ≥ 6%, (b) luật phân phối, (c) cảnh báo T+0 khi chưa được bán.
- **Đã xoá:**
  - `FindNearestOverheadBox`, `FindOverheadBox`, `MasterAlertExitRegimes`
  - `EnsureExitRegimeAsync`, `UpdateExitRegimeAsync`
  - 5 option config
  - các dòng "Chế độ:", "Mục tiêu cạnh dưới nền", "Phủ nhận cây vượt đỉnh" trong tin Telegram
  - `ExitRegime` khỏi context gửi AI judge; tham số `Branch` của tin bán truyền `null`
- **Giữ lại:**
  - các cột DB `ExitRegime`, `OverheadBaseLow`, `OverheadBaseHigh`, `EntryBarLow` (Q3)
  - `RealizedTradeDto.ExitRegime`, vì mobile đọc `json['exitRegime']` (`mobile/lib/core/models/models.dart`). Vị thế mới trả `null`.
- **Kiểm bằng:** `dotnet build` sạch; `dotnet test --filter SellExit` 16/16 pass, có ca hồi quy VIB trong `SellWindowTests`.
- **Chưa kiểm:**
  - chạy live trong phiên
  - vị thế cũ còn `ExitRegime = UnderBase`: giờ đi theo luật rút từ mốc. `AnchorWindowStart` của chúng có thể đã bị đặt lại vào ngày vượt nền.
- **Khi ship:** xoá trên server các key `OverheadBoxMinSessions`, `OverheadBoxMaxHeightPercent`, `OverheadBaseMaxAgeSessions`, `OverheadBaseBufferPercent`, `MarketPhaseMultipliers` trong `appsettings.Production.json`. File trên server khác bản trong repo.

## 8. Không lặp lại — vì sao bỏ, cần gì nếu muốn đưa lại

**Sự cố gốc:** VIB ngày 07/10/2026. Tin mua lúc 14:15, hai phút sau đã có tin cảnh báo bán.

| Luật đã bỏ | Lỗi thực tế | Điều kiện tối thiểu nếu muốn đưa lại |
|---|---|---|
| UnderBase: chốt nửa tại cạnh dưới nền trên | Hàm dò nền quá lỏng: biên độ cho phép 15%, quét lùi 250 phiên, chọn nền có đáy **sát giá mua nhất**. Kết quả là "nền" không thấy trên biểu đồ, đáy nền ≈ giá mua, và điều kiện chốt nửa đúng ngay phút mua. | (1) Mục tiêu phải cách giá mua tối thiểu X% (chủ sản phẩm chốt X). (2) Nền phải nhìn thấy được trên biểu đồ: kiểm bằng ảnh chụp trên vài mã thật trước khi bật. (3) Có test: giá mua nằm sát hoặc trên ngưỡng chốt thì không được bắn tin. |
| Phủ nhận cây vượt đỉnh (`EntryBarLow`) | Chủ sản phẩm quyết định bỏ. Không có sự cố riêng được ghi nhận. | Cân nhắc rủi ro ở mục 6: breakout thất bại giờ chỉ bị bán khi rút đủ 6%. |
| Hệ số pha nhân vào ngưỡng | Chủ sản phẩm quyết định bỏ. Hệ số pha còn làm vùng đệm nền co giãn theo pha, khó đoán hành vi. | Backtest so sánh có / không có hệ số trước khi đưa lại. |

**Bài học chung:**
- Mỗi luật bán phải có test cho trường hợp **vừa mua xong**: giá = giá mua, chưa đủ T+2.5 → không được bắn tin.
- Spec 003 có FR-010 nói bộ dò hộp sẽ "phân biệt nền đi ngang với một đoạn xu hướng". Ca VIB cho thấy giả định này **chưa được kiểm trên dữ liệu thật**: không có test nào chạy trên lịch sử giá thật. Spec mới về dò nền phải có kiểm chứng trên lịch sử giá thật, không chỉ chuỗi giả.
- `ShadowMode = true` của AI judge: AI ghi "BLOCK" nhưng tin vẫn gửi. Nhãn này dễ khiến người đọc tưởng tin đã bị chặn. Chưa sửa. Đây là quyết định riêng của chủ sản phẩm.
