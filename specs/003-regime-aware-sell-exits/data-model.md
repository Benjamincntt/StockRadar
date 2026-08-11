# Phase 1 — Data Model

## 1. `MasterAlertPositions` — cột mới

Bảng đã tồn tại (migration `20260714120000_AddMasterAlertPositions`). Bổ sung 5 cột, tất cả nullable để migration chạy được trên dữ liệu sẵn có và để phục vụ phân loại lười ở R-8.

| Cột | Kiểu SQL | Null | Ý nghĩa |
|-----|----------|------|---------|
| `ExitRegime` | `nvarchar(16)` | có | `UnderBase` \| `BlueSky`; `NULL` = chưa phân loại (vị thế cũ) |
| `OverheadBaseLow` | `decimal(18,2)` | có | Cạnh dưới nền trên — mục tiêu chốt lãi; chỉ có khi `ExitRegime = UnderBase` |
| `OverheadBaseHigh` | `decimal(18,2)` | có | Cạnh trên nền trên — mốc để xác định "vượt nền" và chuyển chế độ |
| `EntryBarLow` | `decimal(18,2)` | có | Giá thấp nhất phiên mở vị thế — mốc phủ nhận cây vượt đỉnh |
| `AnchorWindowStart` | `date` | có | Phiên sớm nhất được phép tính vào mốc tham chiếu; bằng `EntryDate` khi mở vị thế |

Cột hiện có giữ nguyên ngữ nghĩa, riêng `PeakPriceSinceEntry` đổi cách dùng: vẫn là giá cao nhất đã ghi nhận nhưng chỉ hợp lệ trong cửa sổ tính từ `AnchorWindowStart`; khi cửa sổ trượt quá 20 phiên, mốc được dựng lại từ lịch sử thay vì đọc cột này.

**Ràng buộc**:
- `ExitRegime = 'UnderBase'` ⇒ `OverheadBaseLow > 0` và `OverheadBaseHigh > OverheadBaseLow`.
- `ExitRegime = 'BlueSky'` ⇒ hai cột nền để `NULL`.
- `AnchorWindowStart >= EntryDate` luôn đúng; chỉ tiến lên khi cửa sổ 20 phiên trượt.

**Index**: không thêm. Truy vấn vẫn theo `Symbol` + `IsClosed` như hiện tại.

**Migration**: một migration `AddSellRegimeColumns` với `AddColumn` cho 5 cột, cộng cập nhật `ApplicationDbContextModelSnapshot`. Không backfill.

## 2. Chuyển trạng thái chế độ

```text
                 mở vị thế
                     │
        ┌────────────┴────────────┐
   có nền trên hợp lệ        không có
        │                         │
   ┌────▼─────┐              ┌────▼────┐
   │UnderBase │──vượt nền───►│ BlueSky │
   └──────────┘   kèm vol    └─────────┘
        ▲                         │
        └──────── không cho ──────┘
```

Chuyển chiều `UnderBase → BlueSky` xoá `OverheadBaseLow` / `OverheadBaseHigh` và đặt `AnchorWindowStart` về phiên vượt nền. Chiều ngược lại bị cấm (FR-003).

## 3. Bản ghi ứng dụng

`MasterAlertPositionRecord` trong `IPerformanceServices.cs` nhận thêm 5 thuộc tính tương ứng. Đây là `record` positional nên mọi nơi khởi tạo phải cập nhật; các trường mới đặt cuối và có giá trị mặc định để hạn chế lan diff.

`IMasterAlertPositionRepository` cần:
- `UpsertOnBuyAsync` nhận thêm chế độ, hai cạnh nền, `EntryBarLow`.
- Một phương thức cập nhật chế độ cho vị thế đã tồn tại, phục vụ phân loại lười (R-8) và chuyển chế độ (FR-003).

## 4. Nền giá bên trên (giá trị tính toán, không lưu bảng riêng)

Kết quả của entry point mới trên `DarvasBreakoutAnalyzer`:

| Trường | Nguồn |
|--------|-------|
| `BoxLow` / `BoxHigh` | Giá đóng cửa thấp nhất / cao nhất trong hộp, như `FlatBoxProfile` hiện tại |
| `StartDate` / `EndDate` | Phiên đầu và cuối của hộp |
| `SessionDays` | Độ dài, phải ≥ `OverheadBoxMinSessions` |

Điều kiện chọn: `BoxLow > giá vào`, `EndDate` cách phiên hiện tại không quá `OverheadBaseMaxAgeSessions`, và trong các hộp đạt chuẩn thì lấy hộp có `BoxLow` nhỏ nhất — tức nền gần giá nhất (FR-008).

Cache theo phiên trong `VipOverheadBaseCache`, khuôn theo `VipPullbackMaCache`: prefetch một lần cho các mã đang có vị thế mở, `Unavailable` khi thiếu lịch sử.

## 5. Tham số mới trong `MasterAlertOptions`

| Khoá | Mặc định | Ghi chú |
|------|----------|---------|
| `MarketPhaseMultipliers` | `Favorable 1.25` / `Neutral 1.0` / `Unfavorable 0.75` | **Đảo chiều** giá trị hiện hành `0.8 / 1.0 / 2.25` |
| `SellPoint1DropFromAnchorPercent` | `4` | Thay vai trò `BaseTrailingStopPercent1` |
| `SellPoint2DropFromAnchorPercent` | `6` | Thay vai trò `BaseTrailingStopPercent2` |
| `AnchorLookbackSessions` | `20` | Cửa sổ mốc tham chiếu |
| `OverheadBoxMinSessions` | `20` | Độ dài nền tối thiểu |
| `OverheadBoxMaxHeightPercent` | `15` | Biên độ nền; **tách khỏi** `BreakoutMaxBoxHeightPercent = 10` |
| `OverheadBaseMaxAgeSessions` | `250` | Nền cũ hơn mốc này coi như hết hiệu lực |
| `OverheadBaseBufferPercent` | `0.5` | Đệm chốt trước cạnh dưới nền, nhân nghịch hệ số pha |
| `SellConfirmationTicks` | `2` | Số vòng quét xác nhận trước khi phát |

`TrailingStopMinPeak` bị loại bỏ (FR-015). `BaseTrailingStopPercent1/2` giữ lại như alias đọc-tương-thích hay xoá hẳn là quyết định của `/speckit-tasks`; mặc định đề xuất là xoá vì không còn nơi dùng.

## 6. Log đối chứng

`VipAlertFires` đang phục vụ KPI cho tín hiệu mua. Với cảnh báo bán cần ghi thêm chế độ, mốc tham chiếu, ngưỡng đã dùng và pha (FR-025). Hai hướng để `/speckit-tasks` chọn: thêm cột nullable vào bảng này, hoặc ghi vào một cột JSON bối cảnh. Ưu tiên hướng cột JSON vì tránh nở bảng KPI của luồng mua.
