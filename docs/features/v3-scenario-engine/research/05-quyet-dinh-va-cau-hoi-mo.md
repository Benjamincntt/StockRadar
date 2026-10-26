# 05 — Quyết định của owner và câu hỏi mở

## Quyết định (ghi đúng lời owner, có ngày)

| Ngày | Quyết định | Mức |
|---|---|---|
| 2026-10-23 | Nghiên cứu phải dùng DB production, không dùng DB local | Chốt |
| 2026-10-23 | Không bó buộc khuôn khổ cũ; nếu chưa ưng thì tự sáng tạo kịch bản theo dữ liệu | Chỉ đạo |
| 2026-10-26 | Chọn hướng **P1 — ưu tiên lãi** (không chọn P3 ưu tiên tỉ lệ thắng) | Chốt |
| 2026-10-26 | Chọn **P1++**, bỏ tất cả phần không dùng của V2, làm thành **V3** | Chốt |
| 2026-10-26 | Luật bán phải **realtime trên giá khớp**; bác "xét giá đóng cửa rồi bán ATO phiên sau" | Chốt |
| 2026-10-26 | V3 **bắn Telegram thay thế luôn** V2 (V2 đã cũ) | Chốt |
| 2026-10-26 | Ghi lại toàn bộ nghiên cứu vào repo để liên tục bổ sung | Chốt |

## Câu hỏi mở / việc nên nghiên cứu tiếp

| # | Câu hỏi | Vì sao quan trọng |
|---|---|---|
| Q1 | Trượt giá thực tế khi mua ATC / mở cửa các mã 10–30 tỷ/phiên? | Chưa tính trong mọi số liệu |
| Q2 | So với VNINDEX thật | DB chỉ có VNINDEX từ 25/06/2026 |
| Q3 | Lãi thật của "tiền nhàn rỗi sinh lời" rút được ngay | Số danh mục dùng giả định 3–4,5% |
| Q4 | Rủi ro đuôi của KB4 (tệ nhất −31%, mất 6,2% tổng vốn) | Chưa chặn được; nghĩ thêm cách khác cắt lỗ cứng |
| Q5 | N3 quý 4 chỉ thắng 48,5% | Quý 4 là quý định áp dụng |
| Q6 | Chạy thật (forward) có khớp backtest không | Cần đo bằng dữ liệu V3 thật sau khi chạy |
| Q7 | Nến giờ chỉ có từ 28/04/2026 | Không mô phỏng được trong phiên cho giai đoạn dài; nên lưu nến phút/giờ từ nay |
| Q8 | Phân ngành `Stocks.Sector` có đủ đúng cho `SECR20` không | KB4, N1 phụ thuộc trung vị ngành |
| Q9 | Lệch thời điểm dữ liệu (nến giữ chỗ lúc 08:30, nến dở dang trong phiên) | Làm tín hiệu lệch một ngày nếu không xử lý |
| Q10 | Thiên lệch chọn tham số | Bộ lọc N3, tỉ trọng ×1,5, chọn MA10/MA20 đều tìm từ chính dữ liệu này |
