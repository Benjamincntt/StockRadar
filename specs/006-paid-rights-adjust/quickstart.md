# Phase 1 — Quickstart: quyền mua trả tiền

## Chuẩn bị

- Seed có SSI 17/08 và HCM 05/02 + 16/07 như contract.
- API: `backend/restart-api.ps1`.

## 1. Unit test

```powershell
dotnet test backend/StockRadar.Tests/StockRadar.Tests.csproj --filter "FullyQualifiedName~DieuChinhGia"
```

| Kịch bản | Kỳ vọng |
|----------|---------|
| SSI `TinhGiaThamChieu(24.5, 1, 1.2)` | ≈ 19.58 |
| HCM `TinhGiaThamChieu(26.95, 0.4, 1, 0.25, 10)` | **23.24 ± 0.02** |
| HCM nến 15/07=26.95, GDKHQ 16/07 (không nến), 17/07=25.4 | `GetChangePercent` ≈ **+9** (±1.5), không −6, không +19 |
| `tyLe` = 0 | Trùng công thức 005 |
| `soCoMoi` > 0, `soCoCu` = 0 | Không nạp / 400 |
| Nến cuối `LayLichSuChamDiem` | OHLC = thô |

## 2. API thủ công

```text
GET /api/v1/stocks/HCM/rights-events
```

Hai bản ghi; 16/07 có `oldShares` 4, `newShares` 1, `issuePrice` 10.

## 3. UI

Chi tiết HCM → Sự kiện quyền: thấy 2 dòng; form có tỷ lệ n:m + giá phát hành. Last HCM vẫn giá sàn.

Không cam kết HCM lọt Top.
