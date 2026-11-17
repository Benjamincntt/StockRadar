# Nghiên cứu V4 — sổ kiến thức

Sổ này ghi lại nghiên cứu backtest cho V4, bắt đầu ngày 16/11/2026. Mục đích: phiên sau (người hoặc AI) đọc là biết đã đo gì, kết quả ra sao, hướng nào đã loại, và chạy lại được.

Sổ V3 cũ ở [`../v3-scenario-engine/research/`](../v3-scenario-engine/research/README.md). Hai sổ dùng hai bộ đo khác nhau, đừng so số giữa hai sổ trừ khi mục đó ghi rõ là so cùng bộ đo (xem [02-phat-hien.md](02-phat-hien.md) mục P4-12).

## Cách dùng sổ

| File | Nội dung |
|---|---|
| [01-phuong-phap.md](01-phuong-phap.md) | Dữ liệu, cách làm sạch, bộ đo, giả định, giai đoạn thị trường, đặc tính cổ phiếu |
| [02-phat-hien.md](02-phat-hien.md) | Mỗi phát hiện một mục `P4-xx`: bằng chứng, mức tin cậy, trạng thái. Gồm cả hướng thất bại |
| [03-quyet-dinh-va-cau-hoi-mo.md](03-quyet-dinh-va-cau-hoi-mo.md) | Quyết định của owner (có ngày, đúng mức hỏi / đề xuất / chốt) và câu hỏi chưa trả lời |

Quy ước khi bổ sung: ghi ngày, dữ liệu dùng (xuất ngày nào, nến tới ngày nào), script; tách trong mẫu (2017–2022) và ngoài mẫu (2023 → nay); không xoá mục cũ — đổi trạng thái và ghi lý do.

## Chạy lại

Script ở [scripts/](scripts/). Dữ liệu không nằm trong repo (`scripts/.gitignore`).

1. Thư mục làm việc ngoài repo, ví dụ `D:\sr-research\`. Môi trường Python: `python -m venv venv` rồi `venv\Scripts\pip install numpy "pandas<3" scikit-learn`. Script chạy với pandas 2 (pandas 3 đổi kiểu chuỗi làm script cũ lỗi).
2. Xuất nến production (chỉ đọc) bằng `../v3-scenario-engine/research/scripts/export_prod.sh` qua SSH, tải `sr_stocks.txt.gz`, giải nén thành `prod_stocks.txt`, **xoá file tạm trên server**.
3. `venv\Scripts\python v4load.py <prod_stocks.txt> <thư mục v4> <ngày cắt YYYY-MM-DD>` → `bars.pkl`, `meta.pkl`, nến từ 2015, bỏ nến từ ngày cắt (ngày xuất là phiên chưa xong).
4. Chép `scripts/*.py` vào thư mục v4, chạy `venv\Scripts\python <script>.py <thư mục v4>`, đặt `PYTHONUTF8=1`.
5. Riêng phép so V3 (P4-12): thư mục riêng chứa bản chép script của sổ V3 (`../v3-scenario-engine/research/scripts/`), đổi mọi `'2023-01-01'` thành `'2017-01-01'`, chạy `v3_lam_sach.py` (đọc `../v4/bars.pkl`) rồi `v3so.py <thư mục>`.

## Bản đồ script

| Vòng | Script | Việc | Mục |
|---|---|---|---|
| Nền | `v4load.py`, `v4core.py`, `v4pha.py` | Nạp nến từ 2015; làm sạch đơn vị giá; bộ đo lệnh (`mo_phong`); giai đoạn thị trường; nhóm biến động; kiểm nhãn giai đoạn | P4-01, P4-02 |
| 1 | `v4quet.py` | 9 họ tín hiệu × 7 kiểu thoát, ghi `quet.pkl` | P4-03 |
| 2 | `v4o.py` | Chia lệnh theo ô giai đoạn × nhóm biến động, chọn bằng trong mẫu | P4-04 |
| 3 | `v4dt.py`, `v4ca.py`, `v4loc.py` | 7 đặc tính cổ phiếu; cá nhân hoá theo mã (cuốn chiếu năm); lọc theo đặc tính | P4-05, P4-06 |
| 4 | `v4dm.py`, `v4rr.py` | Danh mục 5 chỗ, 40 triệu, lô 100; lớp né giai đoạn; mốc chỉ số | P4-07 |
| 5 | `v4xv.py`, `v4ben.py`, `loi2326.py` | Chọn giỏ 5 mã cân lại định kỳ (9 cách); lưới độ bền 108 ô của giỏ ít biến động | P4-08 |
| 6–7 | `v4gd.py`, `v4ghep.py` | Đặc tính tốt nhất theo giai đoạn; danh mục đổi giỏ theo giai đoạn | P4-09 |
| 8 | `v4mm.py` | Minervini (Trend Template, VCP) và Livermore (điểm xoay) | P4-10 |
| 9–11 | `v4ghep2.py`, `v4vcp.py`, `v4vcp2.py` | Lõi ít biến động + vệ tinh; lưới 96 ô VCP; VCP nới | P4-11 |
| So V3 | `v3_lam_sach.py`, `v3so.py` | Luật V3 production chạy từ 2017 trên cùng dữ liệu, phí 0,4% và 0,6% | P4-12 |
| Q4-3 | `v3vi_sao.py` | Tách lỗ V3 2017–22 theo kịch bản × năm, × giai đoạn thị trường; phép thử bỏ tín hiệu theo giai đoạn. Nhận 2 đối số: thư mục V3 đã chép, thư mục dữ liệu V4 | P4-13 |
| Q4-4 | `v4lo.py` | Giỏ ít biến động × {ATC, ATO} × {tỷ trọng lẻ, lô 100 với 40 / 100 triệu}; đo tiền mặt nhàn rỗi, số lần bỏ mã vì thiếu lô, chênh giá mở cửa t+1 | P4-14 |
| Q4-8 | `v4soc.py`, `v4soc2.py`, `v4soc3.py` | Cổ tăng sốc: giữ mã lõi lãi lớn; vệ tinh mua cổ tăng sốc; lưới độ bền; kiểm ATO + chặn trần + lô 100 | P4-15, P4-16 |
| Q4-9 | `v4he.py` | Hệ V4 chạy chung: lõi ít biến động + 2 chỗ vệ tinh tăng sốc + lớp tiền mặt (`None` / `'loi'` / `'ca'`, tập giai đoạn bật lớp); đếm lần ra / vào lại, tiền mặt nhàn rỗi, phiên đứng ngoài; bảng độ nhạy chỉ bật khi Giảm giá | P4-17 |
| Mùa vụ | `v4q4.py`, `v4q4l.py`, `v3q4.py`, `v4tet.py`, `v3tet.py`, `v4t4.py`, `v4t4b.py`, `v3t4.py` | Quý 4; quanh Tết âm lịch (15/12 → Tết, 20 phiên trước / sau Tết); 16/11 → 16/03 kiểu chạy liên tục và mua một lần rồi giữ; độ bền theo số mã và ngày mua | P4-18 |
| Kịch bản mùa Tết | `v4mua.py`, `v4mua2.py`, `v4mua3.py`, `v4mua4.py`, `hientai.py` | 12 kịch bản 16/11 → 16/03; độ bền số mã × ngày mua; cắt lỗ cả giỏ / từng mã; điểm chọn mã, giới hạn ngành, độ dài mùa; thêm cổ tăng sốc (túi tiền riêng); trạng thái thị trường hiện tại và xác suất lịch sử | P4-19 |
