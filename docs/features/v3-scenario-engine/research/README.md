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
4. `venv\Scripts\python load.py <thư mục làm việc>` → sinh `bars.pkl`, `meta.pkl`.
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

Các script gọi nhau bằng `exec(open(<thư mục>/x.py).read().split(...)[0])` để lấy phần định nghĩa — đổi tên hay đổi dòng mốc trong file gốc có thể làm hỏng script phụ thuộc.
