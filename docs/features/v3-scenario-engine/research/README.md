# Nghiên cứu backtest cho V3 — sổ kiến thức

Thư mục này ghi lại **toàn bộ** nghiên cứu dữ liệu nến dẫn tới bộ kịch bản V3 (P1++), kể cả những hướng đã thử mà không dùng.
Mục đích: phiên sau (người hoặc AI) đọc là nắm được đã biết gì, đã loại gì, số liệu ở đâu, và chạy lại được.

## Cách dùng sổ

| File | Nội dung | Khi nào cập nhật |
|---|---|---|
| [01-phuong-phap.md](01-phuong-phap.md) | Dữ liệu, bộ mô phỏng, giả định, định nghĩa chỉ báo, lỗi đã gặp | Đổi cách đo, thêm luật thực tế |
| [02-so-phat-hien.md](02-so-phat-hien.md) | Mỗi phát hiện một mục `PH-xx`: bằng chứng, mức tin cậy, trạng thái | Mỗi phát hiện mới, hoặc khi phát hiện cũ bị bác |
| [03-ket-qua.md](03-ket-qua.md) | Bảng số của từng kịch bản, danh mục, V2 mô phỏng | Mỗi lần chốt một phiên bản bộ kịch bản |
| [04-da-thu-khong-dung.md](04-da-thu-khong-dung.md) | Các hướng đã thử và lý do loại | Mỗi thử nghiệm thất bại — **đừng bỏ qua**, tránh thử lại |
| [05-quyet-dinh-va-cau-hoi-mo.md](05-quyet-dinh-va-cau-hoi-mo.md) | Quyết định của owner (có ngày) và câu hỏi chưa trả lời | Mỗi lần owner chốt / có câu hỏi mới |

Quy ước khi bổ sung:
- Ghi **ngày**, **dữ liệu dùng** (production hay local, tới ngày nào), **cách đo** (bộ mô phỏng nào).
- Phân biệt rõ trong mẫu (01/2023–06/2025) và ngoài mẫu (07/2025 trở đi). Phát hiện chỉ đúng ở một giai đoạn thì ghi rõ.
- Số đi kèm điều kiện: phí, T+2.5, cách tính thắng (lãi ròng > 0,05%).
- Không xoá mục cũ khi bị bác — đổi trạng thái thành "Bị bác" và ghi lý do.

## Chạy lại

Script ở [scripts/](scripts/). Dữ liệu **không** nằm trong repo (nặng, là dữ liệu production) — `scripts/.gitignore` chặn `*.pkl`, `*.txt`, `*.csv`, `*.json`.

1. Chép thư mục `scripts/` ra một thư mục làm việc ngoài repo (ví dụ `D:\sr-research\`). Mọi script coi thư mục chứa nó là nơi đọc/ghi dữ liệu.
2. Tạo môi trường Python riêng: `python -m venv venv` rồi `venv\Scripts\pip install numpy pandas scikit-learn`.
3. Xuất dữ liệu production (chỉ đọc): chạy `export_prod.sh` qua SSH như ghi chú đầu file, tải `sr_stocks.txt.gz` về, giải nén, đổi tên `prod_stocks.txt`, **xoá file tạm trên server**.
4. `venv\Scripts\python load.py <thư mục làm việc> [ngày-cắt]` → sinh `bars.pkl`, `meta.pkl`. Tham số thứ hai là ngày cắt dạng `YYYY-MM-DD`: **chỉ giữ nến trước ngày đó**, vì nến của chính ngày xuất là nến giữ chỗ (DB chép y nguyên nến hôm trước) hoặc phiên chưa xong. Không truyền thì mặc định lấy ngày sửa file `prod_stocks.txt` (tức ngày xuất). Muốn chạy lại đúng số của PH-22 đến PH-24 thì truyền `2026-10-23`.
5. Chạy script cần dùng: `venv\Scripts\python <script>.py <thư mục làm việc>`. Đặt `PYTHONUTF8=1` để in tiếng Việt.

Dữ liệu phụ (chỉ cần cho phần kiểm V2 và nến giờ):
- `prod_kq.txt`, `prod_forming.txt`: lệnh và FORMING thật của V2 trong bảng `KetQuaKichBan` (lấy bằng `sqlcmd` qua SSH, cách như `export_prod.sh`).
- `kbs60/*.json`: nến giờ từ KBS (`data_60P`), chỉ có từ 28/04/2026. Cần header User-Agent trình duyệt, không thì bị chặn.

## Bản đồ script

| Nhóm | Script | Việc |
|---|---|---|
| Nạp dữ liệu | `load.py`, `qa.py` | Đọc `prod_stocks.txt` → bảng nến; kiểm độ phủ, phiên vượt biên độ |
| Nền tảng | `bt.py` | Chỉ báo dùng chung, vũ trụ thanh khoản, chỉ số bình quân đều, `simulate()` bản đầu |
| Mô phỏng thực tế | `real.py` → `real2.py` | `simulate_real` (T+2 chỉ bán buổi chiều, sàn trắng), `simulate_real2` (mua ATC / bán ATO, cắt lỗ theo đóng cửa) |
| Tín hiệu | `sigs.py`, `newkb.py`, `step3.py`, `mix2.py` | Định nghĩa KB1–KB5, N1–N4, chọn top 5/ngày |
| Danh mục | `pfeng.py` | Danh mục lãi kép theo giá trị hằng ngày, tỉ trọng theo kịch bản, đỗ tiền nhàn rỗi |
| Quét giả thuyết | `features.py`, `combo.py`, `feat2.py`, `scan2.py`, `tree.py`, `events.py`, `events2.py`, `cal.py` | Thống kê theo nhóm đặc trưng, cây quyết định, hiệu ứng lịch, qua đêm/trong phiên |
| Thử kịch bản | `explore1–5.py`, `newkb2.py`, `step4.py` | Các vòng thử kịch bản và độ nhạy |
| Luật bán | `compare.py`, `diag.py`, `diagF.py`, `step1.py`, `step2.py`, `pm_load.py`, `rt.py` | So luật bán đóng cửa / realtime / cắt lỗ / thời gian giữ; kiểm bằng nến giờ |
| V2 mô phỏng | `v2sim.py`, `v2val*.py`, `v2run.py`, `v2opt.py`, `v2cmp.py` | Chép luật thật V2 sang Python, đối chiếu với lệnh thật, đo kết quả |
| Cải tiến P1 | `pf1–5.py`, `n3diag.py`, `kb4diag.py`, `kb4fix.py`, `idle.py`, `size.py`, `final2.py`, `final3.py` | Lọc N3, chặn lỗ KB4, vốn nhàn rỗi, tỉ trọng |
| Kịch bản tín hiệu dày (27/10) | `flex1.py` → `flex4.py` | Dò kịch bản ra tín hiệu nhiều ngày/tháng, hiệu ứng thứ trong tuần, ghép vào P1++ |
| Luật bán không dùng MA (27/10) | `flex5.py` (`sim_x`), `flex6.py`, `flex7.py` (độ nhạy phí), `flex8.py` (đợt sập 04/2025), `flex9.py`, `flex10.py` (`sim_mix`: luật bán riêng cho lệnh hoảng loạn sâu), `flex11.py` (PH-21 lãi 0%), `lot1.py` (tiền thật + lô 100 cổ phiếu) | Chốt lời cố định, trượt theo đỉnh, chốt lời động, chỉ giữ theo thời gian; ghép vào P1++ |
| Tỉ trọng và dừng lỗ KB4 (02/11) | `kb4size.py` (hệ số KB4 ×1/×0,75/×0,5/bỏ), `kb4early_thuong.py` + `kb4early3.py` (lệnh KB4 đang lỗ sau 1–2 phiên thì kết thúc thế nào), `kb4stop.py` (lưới dừng lỗ realtime × điều kiện thị trường), `kb4stop2.py` (độ bền: ô láng giềng, tách theo năm, biến thể chặt T+2.5) | PH-23, PH-24 — câu hỏi mở Q4 (rủi ro đuôi KB4) |
| Trần tỉ trọng theo thị trường (03/11) | `capvon.py` (lưới trần × ngưỡng chỉ số × có/không cho KB1 vượt trần), `capvon2.py` (so trần luôn bật, ngưỡng lân cận, vốn 100 triệu) | PH-25, Q15 |
| Chờ dừng rơi mới vào (03/11) | `chodung.py` (giữ tín hiệu, mua ATC phiên đầu tiên có dấu hiệu ngừng rơi trong W phiên; tham số thứ hai = W, mặc định 5) | PH-26 |
| Hỗ trợ / kháng cự kiểu đội tư vấn (03/11) | `doihtkc.py` (mua gần hỗ trợ N phiên, chốt kháng cự, cắt thủng hỗ trợ; đo 3 năm, so V3 cùng kỳ) | PH-27 |
| Theo dõi tháng gần nhất (03/11) | `thangqua.py <thư mục> [từ ngày]` (mọi tín hiệu V3 từ ngày đó, tính cả lệnh đang mở, danh mục bắt đầu trống) | PH-28, Q16 |
| Lọc tin xấu CafeF (03/11) | `dumpsig.py` (xuất mọi tín hiệu V3 ra `v3_sig.csv`) → `taitin.py` (tải tiêu đề tin theo mã từ CafeF, cache `tin/*.json`) → `loctin.py` / `loctin2.py` (gắn cờ theo nhóm từ khoá, cửa sổ 7/14/30 ngày, so danh mục) | PH-29 |
| Top V1 thật so với V3 (05/11) | `export_v1top.sh` (xuất `DailyOpportunities` từ production) → `v1top.py … atc` (mô phỏng mua ATC phiên Top, bán theo KB1/KB2, so V3 P1) → `v1rand.py … atc` (mốc chọn ngẫu nhiên) | PH-30 |
| Phát lại Top V1 2023–2026 (05/11) | `backend/StockRadar.Tests/Research/V1TopReplayTests.cs` (biến môi trường `V1_REPLAY_DIR`, `V1_REPLAY_STOCKS`, `V1_REPLAY_FROM`/`TO`, `V1_REPLAY_MODEL`, `V1_REPLAY_OUT`; cần `prod_stocks.txt`, `vnindex_hist.json` từ KBS, `appsettings.json`) → `khop.py` (so Top thật) → `v1rep.py` (mô phỏng, so V3 P1 và ngẫu nhiên) | PH-31 |
| Mọi họ cắt lỗ cho cả V3 (06/11) | `dumpparts.py` (dựng bảng lệnh gốc luật production qua chuỗi `kb4size.py`, lưu `parts.pkl`) → `catlo.py` (bộ đo: đi lại từng phiên của lệnh, áp luật cắt, ba cách tính nến ngày `tt`/`xau`/`tot`, danh mục 40 triệu lô 100) → `catlo2.py [tt,xau,tot]` (lưới 104 phương án, 16 họ, ghi `catlo2_<mode>.csv`) → `catlo3.py` (chọn tham số trong mẫu → kiểm ngoài mẫu; họ I8 học mức cắt từ MAE) → `catlo4.py` (đợt sụt tối đa gồm lệnh nào, lệnh bị cắt rồi đi đâu, chỉ KB4, lưới rộng họ I6) → `catlo5.py` (I6 tách theo kịch bản) | PH-32 |

Các script gọi nhau bằng `exec(open(<thư mục>/x.py).read().split(...)[0])` để lấy phần định nghĩa — đổi tên hay đổi dòng mốc trong file gốc có thể làm hỏng script phụ thuộc.
