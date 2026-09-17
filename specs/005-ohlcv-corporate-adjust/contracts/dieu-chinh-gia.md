# Phase 1 — Contracts: điều chỉnh giá theo quyền

Không thêm route `/api/v1`. Mobile/web không đổi DTO. Ba bề mặt nội bộ + ops.

## C-1. File seed `su-kien-quyen.json`

Đường dẫn mặc định: `Data/su-kien-quyen.json` dưới content root API (copy ra output).

```json
{
  "suKien": [
    {
      "ma": "SSI",
      "ngayKhongHuongQuyen": "2026-08-17",
      "tienMat": 1.0,
      "heSoPhaLoang": 1.2
    }
  ]
}
```

**Bất biến**:
- Key camelCase tiếng Việt như trên (file mới, chưa ship).
- `tienMat` cùng thang Close — **cấm** `1000` cho 1.000đ.
- Dòng thiếu `ma` / `ngayKhongHuongQuyen` hoặc `heSoPhaLoang` ≤ 0 → không nạp, log cảnh báo, các dòng khác vẫn dùng.
- File thiếu / JSON hỏng → nguồn rỗng, mọi mã = dãy thô, log lỗi (không crash job).

## C-2. `INguonSuKienQuyen`

**Đầu vào**: `ma`.

**Đầu ra**: danh sách `SuKienQuyen` đã validate cho mã đó (có thể rỗng).

**Bất biến**: không I/O trong vòng lặp nến; đọc file lúc khởi tạo (hoặc lần đầu) rồi cache memory. Không gọi HOSE.

## C-3. `ISignalAnalyzer.LayLichSuChamDiem` + `%` theo `Stock`

**Đầu vào**: `Stock` (có `Symbol` + `History` thô).

**Đầu ra**: `IReadOnlyList<OhlcvBar>` cùng số nến, cùng `Date`/`Volume`; OHLC đã nhân hệ số. Không sự kiện hợp lệ ⇒ dãy thô.

**Bất biến**:
- `history[^1].Open/High/Low/Close` bằng nến thô cuối.
- `GetChangePercent(Stock, days)` dùng dãy chấm điểm, không dùng `LastChangePercent` khi đủ `days+1` nến.
- `GetChangePercent(IReadOnlyList, days)` **không** tự tra quyền (giữ cho test/index).
- `GetVolumeRatio` / `GetAverageVolume` chỉ đọc Volume — kết quả như thô.

**Chỗ gọi bắt buộc cùng dãy**:
- `BuyDecisionEngine.Evaluate` — thay `History` bằng dãy chấm điểm trước khi detect/hộp/MA.
- `SmartMoneyOpportunitySelector.BuildSectorSnapshots` — `%` 1 phiên qua `GetChangePercent(Stock)`.
- `StockService` flatBox/levels chấm điểm (chart bars riêng, thô).
- `DarvasBreakoutAlertPublisher.AnalyzeFlatBox` trên dãy chấm điểm.

## C-4. Hiển thị (không đổi contract)

Giá last / OHLC chart / quote phiên đang khớp = nguồn thô. Không field `adjustedClose` trên DTO v1.
