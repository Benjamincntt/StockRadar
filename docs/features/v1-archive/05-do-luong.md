# V1 — đo lường và kết luận gỡ

Phạm vi: cách đã đo Top 5 của V1, cách chạy lại phép đo, và kết quả dẫn tới quyết định gỡ (spec 016).
Số liệu gốc và diễn giải đầy đủ: [PH-30, PH-31 trong sổ nghiên cứu](../v3-scenario-engine/research/02-so-phat-hien.md).

## 1. Hai phép đo

| | PH-30 — Top thật | PH-31 — phát lại 3 năm |
|---|---|---|
| Dữ liệu Top | Bảng `DailyOpportunities` trên production (214 dòng, 59 phiên, 25/06–05/11/2026) | Harness C# `V1TopReplayTests` chạy lại V1 hiện tại cho từng phiên 2023–2026 (943 phiên, 4.544 dòng Top) |
| Nến | `Stocks.HistoryJson` production, cắt tới 02/11/2026 | Như PH-30 |
| Cách vào lệnh | Mua ATC ngay phiên của Top | Mua ATC phiên Top; thêm bản mua mở cửa phiên sau để so |
| Luật bán (mượn của KB1/KB2) | Chạm MA10 và giá ≥ giá vào + phí; tối đa 15 phiên; phí 0,4% | Như PH-30 |
| Mốc so | Chọn ngẫu nhiên 5 mã/phiên có GTGD TB20 ≥ 20 tỷ; V3 cùng kỳ | Như PH-30; thêm danh mục V3 P1 và chỉ số bình quân đều |

Vì sao mua ATC: `GeneratedAt` của Top lưu giờ UTC. Bản Top lưu lại là lần làm mới lúc khoảng 14:00 giờ VN **trong chính phiên** `ForTradingDate`, đã thấy giá phiên đó. Giả định mua giá mở cửa là dùng thông tin tương lai. Bản đầu của PH-30 mắc lỗi này và cho kết luận ngược (V1 +18,5%); bản đó đã bỏ.

## 2. Kết quả

### PH-30 — Top thật, 4 tháng (mua ATC)

| Nhóm | Lệnh | Thắng | TB/lệnh |
|---|---|---|---|
| V1 Top, mọi dòng | 82 | 39% | **−1,14%** |
| V1 chỉ hạng 1 | 29 | 31% | −1,60% |
| Ngẫu nhiên 5 mã/phiên, 30 lần | ~167 | 39% | **−1,60%** (−2,51…−0,80) |
| V3 mọi tín hiệu cùng kỳ | 71 | 62% | **+3,19%** |

Danh mục 5 vị thế × 20% vốn từ 25/06: V1 −13,3%, V3 P1 +2,3%, chỉ số bình quân đều −13,2%.

### PH-31 — phát lại 2023–2026

| Mỗi lệnh | Lệnh | Thắng | TB/lệnh | 2023 | 2024 | 2025 | 2026 |
|---|---|---|---|---|---|---|---|
| V1 có ML, mua ATC | 2.698 | 46% | **−0,24%** | −0,00% | −0,19% | −0,14% | −0,74% |
| V1 có ML, mua mở cửa phiên sau | 2.518 | 46% | −0,23% | +0,13% | −0,22% | −0,16% | −0,78% |
| V1 có ML, chỉ hạng 1 | 684 | 47% | +0,05% | +0,20% | +0,27% | +0,06% | −0,49% |
| V1 có ML, nhãn StrongBuy | 23 | 48% | +0,63% | | | | |
| V1 tắt ML | 3.127 | 49% | −0,20% | +0,04% | −0,31% | −0,05% | −0,58% |
| Ngẫu nhiên, 10 lần | | | **−0,19%** (−0,26…−0,13) | +0,20% | −0,27% | −0,09% | −0,75% |

| Danh mục 5 × 20%, lãi kép, từ 01/2023 | 2023 | 2024 | 2025 | 2026 | Tổng | Sụt tối đa |
|---|---|---|---|---|---|---|
| V1 có ML, ATC | −13,6% | +0,7% | +14,5% | −9,0% | **−9%** | −26,5% |
| V1 tắt ML, ATC | −3,2% | −7,9% | −22,5% | −6,9% | −36% | −46,0% |
| V3 P1 | +4,0% | +22,0% | +86,0% | +54,0% | **+264%** | −23,4% |
| Chỉ số bình quân đều | +24,1% | +8,8% | +8,9% | −14,2% | | |

Độ khớp harness với Top thật cùng ngày: **71%** số mã từ 25/09/2026 (model ML hiện tại); trước 25/09 chỉ 14% vì model và code V1 khi đó khác. Harness thể hiện **V1 tại commit gỡ**, không phải V1 của từng thời điểm quá khứ.

## 3. Kết luận

- Top V1 **không có lợi thế chọn mã**: lãi mỗi lệnh nằm trong khoảng chọn ngẫu nhiên ở cả 4 năm, có ML hay không cũng vậy.
- Danh mục V1 kém chỉ số bình quân đều và kém V3 rất xa.
- Hạng 1 và StrongBuy nhỉnh hơn một chút nhưng mẫu nhỏ và vẫn quanh 0.
- ML giúp danh mục (−9% so với −36%), nhưng model chỉ học 45 mẫu cuối 2026; đoạn 2023–2025 là áp ngược, không phải dự báo thật.
- Owner quyết gỡ V1 (spec 016), chỉ giữ cổng sắp chốt quyền và chuyển sang V3 (spec 015 User Story 6).

## 4. Giới hạn của phép đo

- Luật bán của V1 là mượn từ KB1/KB2; V1 gốc không có luật bán tự động cho Top.
- Harness bỏ cổng sắp chốt quyền (không có lịch sử FireAnt) và không điều chỉnh giá theo quyền.
- Pha thị trường không bao giờ Favorable, giống production (VNINDEX lưu O = H = L = C). Chưa đo được V1 khi được phép breakout ở pha thuận.
- Hồ sơ điểm và hồ sơ hiệu chỉnh trong harness dùng mặc định, có thể khác production.
- Mức tin cậy: trung bình.

## 5. Cách chạy lại

Phải chạy ở commit còn code V1 (xem [README](./README.md)).

1. **Nến:** xuất `Stocks` production theo `research/scripts/export_prod.sh` (chỉ đọc, qua SSH), đổi tên thành `prod_stocks.txt`. Mỗi dòng có dạng `Symbol|Exchange|Sector|IsActive|<HistoryJson>`; harness bỏ nến trước 2020.
2. **VNINDEX:** lấy nến ngày từ KBS
   `https://kbbuddywts.kbsec.com.vn/iis-server/investment/index/VNINDEX/data_day?sdate=dd-MM-yyyy&edate=dd-MM-yyyy`,
   ghi thành `vnindex_hist.json` (mảng `OhlcvBar`, camelCase). Muốn giống production thì đặt O = H = L = C = giá đóng cửa.
3. **Cấu hình:** chép `appsettings.json` (và nếu có, `appsettings.Production.json` của server) vào cùng thư mục. Harness đọc các mục `MarketJobs`, `SmartMoney`, `PriceRunupFilter`, `OpportunityRanker`.
4. **Model ML (tuỳ chọn):** tệp model của server (`Data/opportunity-ranker-model.json`). Bản 25/09/2026 đã chép đủ tham số trong [03-xep-hang-ml.md](./03-xep-hang-ml.md). Không đặt thì harness tắt ML và xếp theo `PredictedHitPercent`.
5. **Chạy harness** (PowerShell):
   ```powershell
   $env:V1_REPLAY_DIR = "D:\du-lieu\v1replay"
   $env:V1_REPLAY_STOCKS = "D:\du-lieu\prod_stocks.txt"
   $env:V1_REPLAY_MODEL = "D:\du-lieu\v1replay\ranker_model.json"
   $env:V1_REPLAY_FROM = "2023-01-01"
   $env:V1_REPLAY_OUT = "v1_replay_ml.csv"
   dotnet test backend/StockRadar.Tests --filter "FullyQualifiedName~V1TopReplayTests"
   ```
   Không đặt `V1_REPLAY_DIR` thì test trả về ngay (không chạy). `V1_REPLAY_TO` để cắt ngày cuối. Kết quả là CSV với các cột `asof,rank,symbol,sector,buy_score,trade_state,legacy_rec,ml_prob,phase,candidates,universe`.
6. **So khớp và mô phỏng** (Python, môi trường của sổ nghiên cứu, xem `research/README.md`):
   - `load.py <thư mục> [ngày-cắt]` → `bars.pkl`.
   - `khop.py` so CSV phát lại với Top thật (Top thật xuất bằng `export_v1top.sh`).
   - `v1rep.py <thư mục dữ liệu> <csv phát lại> [nhãn]` → bảng mỗi lệnh, ngẫu nhiên, danh mục so V3 P1.
   - PH-30: `v1top.py <thư mục> <v1top.txt> atc` và `v1rand.py … atc`.
