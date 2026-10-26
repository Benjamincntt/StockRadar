# 03 — Kết quả

Dữ liệu production tới 22/10/2026. Phí khứ hồi 0,4%. Thắng = lãi ròng > 0,05%. Chưa tính trượt giá.

## Bộ V3 đang chọn: P1++ bản realtime (2026-10-26)

### Luật từng kịch bản

| Mã | Điều kiện báo | Xếp hạng (top 5/ngày) | Mua | Bán (realtime) | Giữ tối đa |
|---|---|---|---|---|---|
| KB1 Hoảng loạn | (`BREADTH50` < 0,20 hoặc chỉ số dưới MA50 hơn 7%) và `TV20` > 30 | `RS126` thấp nhất | Mở cửa phiên sau | Giá khớp ≥ max(TB 9 đóng cửa trước, giá vào × 1,004) | 15 phiên |
| KB2 Chỉ số thủng MA50 | Chỉ số dưới MA50 hơn 5% và `TV20` > 100 | `RSI14` thấp nhất | Mở cửa phiên sau | Như KB1 | 15 phiên |
| KB4 Ngành bán quá mức | `SECR20` ≤ −10%, `RET20` < `SECR20`, `TV20` > 20, `BREADTH50` < 0,35 | `RET20` thấp nhất | ATC phiên tín hiệu | Giá khớp ≥ max(TB 9 đóng cửa trước, giá vào × 1,004) — chạm MA10 | 10 phiên |
| N1 Giải chấp cạn kiệt | `FLOOR20` ≥ 1, hôm nay không sàn, `SECR20` ≤ −12%, `TV20` > 10, `LEFT` ≤ 6 | `SECR20` thấp nhất | ATC phiên tín hiệu | Giá khớp ≥ max(TB 19 đóng cửa trước, giá vào × 1,004) — chạm MA20 | 15 phiên |
| N3 Đón dòng tiền đầu tháng | `LEFT` = 5, `TV20` > 30, bỏ nếu chỉ số cách MA50 trong ±1,5% | `RS63` cao nhất | ATC phiên tín hiệu | Lệnh ATO phiên `TDM` = 2 tháng sau | 12 phiên |

Chung: hết hạn giữ thì bán ATC; T+2.5 (sớm nhất bán chiều T+2); sàn trắng không bán được; mua ATC bỏ nếu đóng cửa kịch trần, mua mở cửa bỏ nếu mở cửa kịch trần.
Danh mục: 5 vị thế, ưu tiên KB1 > KB2 > KB4 > N1 > N3, không trùng mã; mỗi lệnh = giá trị danh mục / 5, KB1/KB2/N1 × 1,5; tiền nhàn rỗi gửi lấy lãi.

### Từng kịch bản (mọi tín hiệu, trước khi qua danh mục)

| Mã | Lệnh | Thắng trong / ngoài mẫu | Thắng quý 4 | Năm thấp nhất | Lãi TB | Lỗ TB | Tổng lãi/tổng lỗ | Tệ nhất | Giữ TB |
|---|---|---|---|---|---|---|---|---|---|
| KB1 | 114 | 75,7% / 81,8% | 81,1% | 75,0% | +2,59% | −8,64% | 4,80 | −13,1% | 5,6 |
| KB2 | 139 | 72,6% / 85,5% | 80,9% | 73,5% | +2,84% | −7,73% | 5,65 | −18,8% | 6,3 |
| KB4 | 256 | 67,6% / 71,1% | 71,6% | 65,5% | +1,55% | −9,76% | 2,16 | −31,2% | 6,0 |
| N1 | 97 | 78,0% / 84,2% | 81,0% | 77,8% | +3,52% | −7,71% | 5,43 | −11,2% | 9,5 |
| N3 | 142 | 57,0% / 69,0% | 48,5% | 50,0% | +3,21% | −5,51% | 2,51 | −21,0% | 9,0 |

### Danh mục (lãi kép theo giá trị hằng ngày, từ 01/2023)

| Tiền nhàn rỗi | 2023 | 2024 | 2025 | 2026* | Tổng | Sụt giảm tối đa | Tháng tệ nhất | Trong / ngoài mẫu | Thắng |
|---|---|---|---|---|---|---|---|---|---|
| Lãi 0% | +7% | +24% | +71% | +80% | +307% | −16,6% | −10,9% | +49% / +173% | 68% |
| Gửi 3%/năm (giả định) | +9% | +27% | +74% | +82% | +337% | −15,8% | −10,8% | +57% / +179% | 68% |
| Gửi 4,5%/năm (giả định) | +10% | +28% | +75% | +84% | +352% | −15,4% | −10,7% | +60% / +182% | 68% |

\*2026 tới 22/10. 280 lệnh. Lệnh tệ nhất làm mất 6,2% tổng vốn (KB4 −31,2% × 20%).
Chỉ số bình quân đều cùng kỳ: 2023 +24,1%, 2024 +8,3%, 2025 +9,2%, 2026 −9,5%, tổng +33%, sụt giảm −27,4%.

## Lịch sử các phiên bản danh mục

| Phiên bản | Thay đổi | Tổng | Sụt giảm tối đa | Ghi chú |
|---|---|---|---|---|
| Bộ 5 KB đầu (KB1–KB5) | — | — | — | Chỉ đo cộng dồn, chưa đo sụt giảm |
| P1 | KB1, KB2 (luật F), KB4, N1, N3, mua ATC / bán ATO cho KB4 N1 N3 | +262% | −23,4% | Lãi kép hằng ngày |
| P1+ | + lọc N3 vùng ±1,5% MA50 | +318% | −16,2% | PH-11 |
| P1++ | + gửi tiền 4,5% + KB1/KB2/N1 × 1,5 | +399% | −15,4% | KB4, N1 bán theo đóng cửa → ATO |
| P1++ an toàn | + KB4 × 0,5 | +363% | −12,5% | Lựa chọn giảm rủi ro |
| **P1++ realtime** | KB4 chạm MA10, N1 chạm MA20, realtime | **+352%** | **−15,4%** | Owner bác đóng cửa → ATO (PH-16) |
| P3 (ưu tiên thắng) | KB1 KB2 KB4 N1 + N4 thay N3 | cộng dồn 2023–26: +23 / +3 / +27 / +18% | — | Thắng 68–75% mỗi năm, lãi thấp hơn |

## V2 mô phỏng (luật thật, đã đối chiếu lệnh thật)

| | Lệnh | Thắng trong / ngoài mẫu | Lãi TB | Tổng lãi/tổng lỗ |
|---|---|---|---|---|
| Hồi về hỗ trợ (có mua điểm 2) | ~1.570 | 34% / 27% | −1,0% | 0,57 |
| Hồi về hỗ trợ (bỏ điểm 2) | ~1.565 | 41% / 34% | −0,3% | 0,86 |
| Nổ hướng lên | 23 | 30% / 33% | −1,2% | 0,35 |
| Quét thanh khoản | 11 | 33% / 20% | −6,0% | 0,06 |
| Danh mục 5 vị thế (cộng dồn) | 538 | 2023 −1,4%, 2024 −26,4%, 2025 −34,1%, 2026 −38,9% | | |

Lý do thoát (có điểm 2): rút mốc 6% 35%, dừng lỗ 33%, hết thời gian 25%, chốt lời 2 chỉ 3%.
