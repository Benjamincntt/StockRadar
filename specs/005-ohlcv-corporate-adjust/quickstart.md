# Phase 1 — Quickstart: kiểm chứng điều chỉnh giá theo quyền

Hướng dẫn xác nhận P1. Không chứa thân hàm triển khai.

## Chuẩn bị

- Seed có SSI 17/08/2026: `tienMat` = 1.0, `heSoPhaLoang` = 1.2.
- Không cần migration SQL.
- API dev sau land: `backend/restart-api.ps1`.

## 1. Unit test — chạy trước

```powershell
dotnet test backend/StockRadar.Tests/StockRadar.Tests.csproj --filter "FullyQualifiedName~DieuChinhGia"
```

| Kịch bản | Kỳ vọng |
|----------|---------|
| SSI Close 14/08 = 24.5, 17/08 ≈ 19.8, có seed | `GetChangePercent(stock, 1)` ≈ **+1** (±1 điểm %), **không** ≈ −19 |
| Cùng cửa sổ, `giaThamChieu` | `(24.5 − 1.0) / 1.2` ≈ 19.58 |
| Không seed / mã khác | `%` trùng Close thô hiện tại |
| Seed `tienMat` = 1000 với Close 24.5 | Dòng từ chối hoặc không cho `(24.5 − 1000)` (fail rõ) |
| `LayLichSuChamDiem` nến cuối | OHLC = thô |
| Volume trước/sau điều chỉnh | Không đổi |
| Sự kiện thứ hai (fixture SC-005) | Gap thô ≠ lợi suất dãy chấm điểm |
| `heSoPhaLoang` ≤ 0 hoặc thiếu ngày | Không áp, dãy thô |

Hồi quy: `SectorWaveTests` vẫn pass trên fixture **không** sự kiện (hành vi cũ). Thêm case SSI kéo RS: cửa RS không fail **chỉ vì** gap thô −17%.

## 2. Không vỡ persist

Job 2 / `EntityMapper.ToEntity`: `HistoryJson` sau append vẫn Close khớp lệnh (SSI 17/08 ≈ 19.8, không 24.5×hệ số).

## 3. UI / API (thủ công)

- Chi tiết mã: last SSI = giá sàn cùng lúc.
- Chart nến lịch sử: vẫn khoảng trống GDKHQ (thô).
- Live Buy Score / RS trên engine: không còn −19% một phiên vì quyền.

## 4. Production ops

Khi HOSE công bố GDKHQ: thêm một object trong `suKien`, ship, restart. Không sửa engine.

Không cam kết nhãn **Sóng mạnh** phiên 21/08 — chỉ hết fail RS vì gap SSI.
