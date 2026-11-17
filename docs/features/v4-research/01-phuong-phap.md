# Phương pháp V4

## Dữ liệu

- Nguồn: `Stocks.HistoryJson` trên DB production, xuất **16/11/2026** (chỉ đọc qua SSH), nạp bằng `v4load.py … 2026-11-16`.
- Kết quả: 1.135 mã, 1.884.817 nến ngày, **28/09/2015 → 13/11/2026**. Sổ V3 chỉ dùng từ 06/2021, nên thiếu các đợt sập 2018, 2020, 2022.
- Chỉ có **nến ngày**. Không có nến trong phiên cho giai đoạn này.
- **Thiếu mã đã huỷ niêm yết** (DB chỉ giữ mã còn trong danh sách). Kết quả vì thế thiên về phía tốt.

## Làm sạch (trong `v4core.py`)

- **Lẫn đơn vị giá:** mã giá dưới 1.000đ có nến lưu bằng đồng (970) lẫn nghìn đồng (0,97). Lần quét đầu ra lãi ảo tới +900%/lệnh vì lỗi này. Quy tắc sửa: mã có trung vị giá đóng cửa < 100 (nghìn đồng) mà một giá ≥ 500 thì chia 1000. Sửa **39.170** giá.
- **Mã hỏng không sửa được:** giá cao nhất gấp > 200 lần thấp nhất → loại khỏi vũ trụ (23 mã, vd PXM, DCS, VHG).
- **Phiên vượt biên độ** (|lãi ngày| > biên độ sàn + 1,5 điểm, thường là ngày quyền chưa điều chỉnh): bộ đo lệnh bỏ lệnh có phiên đó; bộ đo giỏ tính lãi phiên đó bằng 0.

## Vũ trụ

- **300 mã thanh khoản nhất mỗi ngày** (giá trị giao dịch TB 20 phiên ≥ 1 tỉ, giá ≥ 3 nghìn, đã niêm yết ≥ 250 phiên).
- Mức cố định "≥ 5 tỉ/phiên" của sổ V3 bị bỏ vì năm 2016–2019 chỉ còn 5–70 mã.
- Số mã trung bình mỗi năm: 2017 100, 2018 122, 2019 153, 2020 200, từ 2021 ~270–300.
- Bộ đo giỏ (P4-08 trở đi) dùng **top 100 thanh khoản** trong vũ trụ này.

## Bộ đo lệnh (`mo_phong`)

- Tín hiệu tại đóng cửa phiên t. Mua 'open' = giá mở cửa t+1 (ATO); 'close' = đóng cửa t (ATC). Bỏ nếu giá mua sát trần.
- T+2.5: phiên e+2 chỉ bán theo giá đóng cửa; từ e+3 bán cả phiên. Sàn trắng không bán được.
- Cùng phiên chạm cả cắt lỗ và chốt lời: coi **cắt lỗ chạm trước**. Mở cửa đã vượt mức thì khớp ở giá mở cửa.
- Cắt lỗ / chốt lời / trượt theo bội số ATR14 tại phiên tín hiệu, hoặc theo % (thêm ở vòng 8).
- `exit_flag`: đúng ở đóng cửa phiên k thì bán ATO phiên k+1.
- **Phí + thuế 0,4% khứ hồi + trượt giá 0,1% mỗi chiều = 0,6%.** Sổ V3 chỉ trừ 0,4%.

## Bộ đo danh mục

- **Lệnh** (`v4dm.py`): 5 chỗ, vốn 40 triệu, lô 100, làm tròn lô gần nhất, tiền mặt lãi 0%, ưu tiên theo RS (hoặc ngẫu nhiên làm mốc).
- **Giỏ** (`v4xv.py`): giữ N mã bằng tỷ trọng, cân lại mỗi `ky` phiên; quyết ở đóng cửa t, khớp xấp xỉ ATC t; phí 0,3% mỗi chiều trên phần đổi. **Tỷ trọng lẻ, chưa làm tròn lô 100.**
- **Lõi + vệ tinh** (`v4ghep2.py`): lõi định giá theo chuỗi giá đã điều chỉnh; vệ tinh theo giá thô điền tiếp.
- **Mốc:** chỉ số đều trọng số của 300 mã (thay VNINDEX, vì DB chỉ có VNINDEX từ 06/2026). Mốc này **không mua được thật** với 40 triệu.

## Giai đoạn thị trường (tại đóng cửa phiên t)

Tính trên chỉ số đều trọng số MKT, MA50/MA200 của nó, độ rộng (tỉ lệ mã trên MA50), sụt từ đỉnh 20 phiên:

| Giai đoạn | Điều kiện (xét theo thứ tự) |
|---|---|
| Hoảng loạn | sụt 20 phiên ≤ −8% **hoặc** độ rộng < 0,15 |
| Tăng mạnh | MKT > MA50 và MA50 > MA200 |
| Hồi phục | MKT > MA50 và MA50 ≤ MA200 |
| Điều chỉnh | MKT ≤ MA50 và MKT > MA200 |
| Giảm giá | còn lại |

Kiểm nhãn (`v4pha.py`): 2021 có 82% phiên Tăng mạnh; 2022 có 52% phiên Hoảng loạn. Lãi chỉ số 20 phiên sau: Hoảng loạn +1,9%, Hồi phục +1,1%, Tăng mạnh +1,0%, Điều chỉnh +0,8%, Giảm giá −1,0%.

## Đặc tính cổ phiếu (chỉ dùng dữ liệu trước phiên tín hiệu)

| Đặc tính | Đo |
|---|---|
| Biến động | Độ lệch chuẩn lãi ngày 120 phiên (bỏ phiên vượt biên độ) |
| Kiểu đi | Tỉ số phương sai 10 phiên, 250 phiên (> 1 bám xu hướng, < 1 hay đảo chiều) |
| Beta | Hệ số theo MKT, 250 phiên |
| Sức bật sau hoảng loạn | Lãi 10 phiên sau các phiên Hoảng loạn trước đây của chính mã |
| Thanh khoản | Giá trị giao dịch TB 20 phiên |
| Ngành | Lãi 63 phiên của ngành (TB đều) trừ thị trường |
| RS | Thứ hạng lãi 63 / 126 phiên trong vũ trụ |

Mỗi đặc tính chia ba nhóm theo thứ hạng trong vũ trụ ngày hôm đó.

## Chia giai đoạn kiểm

- **Trong mẫu 2017–2022** (gồm sập 2018, 2020, 2022 và sóng 2021). **Ngoài mẫu 2023 → 13/11/2026.**
- Cá nhân hoá theo mã (P4-06) dùng **cuốn chiếu theo năm**: đầu năm Y chỉ học từ lệnh đã đóng trước Y.
