# 01 — Phương pháp

Cập nhật lần cuối: 2026-10-26.

## Dữ liệu

| Mục | Giá trị |
|---|---|
| Nguồn | Bảng `Stocks`, cột `HistoryJson` trên **DB production** (owner yêu cầu 2026-10-23: không dùng DB local) |
| Số mã | 1.135 (961 đang hoạt động); DB local chỉ có 526 mã — không dùng để kết luận |
| Khoảng thời gian | Lịch sử từ 2016; nghiên cứu dùng 06/2021 (làm nóng chỉ báo) → **22/10/2026** |
| Đơn vị giá | Nghìn đồng, giá đã điều chỉnh quyền (HPG 2016 ≈ 3,0) |
| Phiên dở dang | Bỏ phiên 23/10/2026 (đang giao dịch lúc xuất) |
| Phiên bất thường | Lợi nhuận ngày vượt biên độ + 1,5% (HOSE 7%, HNX 10%, UPCOM 15%) bị đánh dấu `BAD`; lệnh dính phiên này bị loại |
| Vũ trụ thanh khoản `UNI` | Giá trị giao dịch TB 20 phiên ≥ 5 tỷ và giá ≥ 5 (nghìn). Khoảng 166–211 mã mỗi cuối năm |
| Nến giờ | KBS `data_60P`, chỉ có từ **28/04/2026** — chỉ đủ để kiểm cách xấp xỉ, không đủ để backtest |
| VNINDEX | DB chỉ có từ 25/06/2026 → **không so được với VNINDEX**; dùng chỉ số bình quân đều tự dựng |

## Chia giai đoạn

- Trong mẫu (dò ý tưởng): 01/01/2023 → 30/06/2025.
- Ngoài mẫu (kiểm lại): 01/07/2025 → 22/10/2026.
- Báo thêm theo từng năm và riêng quý 4 (2023–2025).

## Định nghĩa chỉ báo (bản nghiên cứu, khác `IndicatorMath` ở vài chỗ)

| Tên | Cách tính |
|---|---|
| `TV20` | TB 20 phiên của close × volume / 10⁶ → tỷ đồng |
| Chỉ số bình quân đều (`MKT`) | Lợi nhuận ngày = TB lợi nhuận các mã thuộc `UNI` phiên trước, mỗi mã kẹp ±16%; cộng dồn. MA20/MA50 tính trên chỉ số này |
| `BREADTH50` | Tỉ lệ mã `UNI` có close > MA50 của chính nó |
| `RS63`, `RS126` | Hạng phần trăm trong `UNI` của close/close[−63] (hoặc [−126]) |
| `RSI14` | Làm mượt Wilder (alpha 1/14). **Khác** `IndicatorMath.Rsi` (cộng dồn 14 phiên) |
| MA | Trung bình đơn giá đóng cửa |
| `ATR` | Wilder (alpha 1/14) trong `bt.py`; trung bình đơn 14 phiên trong `v2sim.py` (giống app) |
| `RET20` | close/close[−20] − 1 |
| `SECR20` | Trung vị `RET20` của các mã `UNI` cùng `Stocks.Sector` |
| Phiên sàn | Lợi nhuận ngày ≤ −(biên độ − 0,5%). `FLOOR20` = số phiên sàn trong 20 phiên |
| `LEFT`, `TDM` | Số phiên còn lại tới cuối tháng (0 = phiên cuối); thứ tự phiên trong tháng (0 = phiên đầu) |
| Chạm MAn realtime | Giá khớp ≥ TB (n−1) giá đóng cửa trước — khi đó MAn tính cả giá hiện tại bằng đúng giá này |

## Các bộ mô phỏng (theo thứ tự ra đời)

| Bộ | File | Mô tả | Còn dùng? |
|---|---|---|---|
| `simulate` | `bt.py` | Mua mở cửa phiên sau; sớm nhất bán phiên t+3 **cả ngày**; một vị thế mỗi mã | Chỉ để dò nhanh |
| `simulate_real` | `real.py` | Phiên T+2 chỉ bán buổi chiều (xấp xỉ bằng giá đóng cửa); sàn trắng (H=L, đóng ở sàn) không bán được | Có |
| `simulate_real2` | `real2.py` | Thêm mua ATC phiên tín hiệu, bán ATO theo điều kiện hôm trước, cắt lỗ theo giá đóng cửa → ATO | **Chính** |
| `sell_v2` | `v2sim.py` | Chép luật bán Pha 2 của V2 | Chỉ cho V2 |
| `run_pf` | `pfeng.py` | Danh mục lãi kép, giá trị theo giá đóng cửa mỗi ngày, N vị thế, ưu tiên, tỉ trọng theo kịch bản, đỗ tiền nhàn rỗi | **Chính** |

Giả định chung:
- Phí + thuế khứ hồi **0,4%**, trừ một lần khi bán.
- **Chưa tính trượt giá**, khớp đúng giá mở cửa / đóng cửa / mức chạm.
- Mua ATC giả định khớp đúng giá đóng cửa; app thật phải báo lúc ~14:25 bằng giá trong phiên.
- Thắng = lãi ròng > 0,05%. Từ −0,05% đến +0,05% là **hoà vốn**, không tính thắng.
- Trong một nến ngày không biết giá nào đến trước: bộ V2 xét chiều giảm trước (bi quan); đã thử chiều lạc quan, kết quả gần như không đổi.

## Kiểm cách xấp xỉ

| Kiểm | Kết quả |
|---|---|
| "Phiên T+2 chỉ bán buổi chiều" xấp xỉ bằng giá đóng cửa vs nến giờ thật 13:00–14:45 (81 lệnh, 05–10/2026) | Bán theo đóng cửa: 79/79 trùng. Luật F: 69/81 trùng, chênh TB +0,02%/lệnh, lệch lớn nhất 0,9% |
| Bỏ hẳn phiên T+2 (cận bi quan) | Kết quả đổi rất ít |
| V2 mô phỏng vs lệnh thật V2 (30/09–22/10/2026) | 13/22 lệnh trùng đúng ngày, 17/22 trong ±3 ngày |
| Pha 1 V2 mô phỏng vs FORMING thật | Khớp tốt nhất khi giả định lúc 08:30 lịch sử **đã có nến hôm nay giữ chỗ** (giá tham chiếu, KL 0): "Hồi về hỗ trợ" 44/49 (không giả định: 19/49) |

## Lỗi đã gặp (đừng lặp lại)

| Lỗi | Hậu quả | Sửa |
|---|---|---|
| pandas 3 `stack()` giữ ô trống | Đếm cả ô không có tín hiệu → tỉ lệ thắng nền 8%, bảng quét sai | Luôn `.stack().dropna()` |
| Lệnh bán đúng giá mua + phí có lãi ròng ≈ 1e−16 | Hoà vốn bị đếm là thắng → luật F báo thắng 86–96% (thật 66–79%) | Thắng = lãi > 0,05%, tách cột hoà vốn |
| Lợi nhuận năm của chỉ số tính sai (chia cho đầu năm sau) | Báo chỉ số 2024 +34% (thật +8%) | Năm = cuối năm / cuối năm trước − 1 |
| Phanh danh mục "ngừng mua khi sụt X%" | Ngừng mua thì đỉnh không bao giờ lập lại → phanh không nhả | Bỏ ý tưởng hoặc phanh có thời hạn |
| Lấy `R` làm tên biến tạm, đè lên lợi nhuận ngày | Proxy VN30 ra 0% | Không đặt tên trùng biến dùng chung trong `bt.py` |
