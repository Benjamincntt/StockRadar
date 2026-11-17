# Quyết định và câu hỏi mở — V4

## Quyết định của owner

| Ngày | Owner nói / chọn | Mức |
|---|---|---|
| 2026-11-16 | "bác bỏ tất cả các quan điểm cũ. tôi muốn hệ thống lại app lên v4. phần cũ tạm dừng." Yêu cầu 10 bộ kịch bản theo giai đoạn thị trường và đặc tính biến động từng cổ phiếu; backtest thận trọng; đơn giản nhưng hiệu quả cao | Chỉ đạo |
| 2026-11-16 | "Phần cũ tạm dừng" = V3 vẫn chạy production, chỉ dừng phát triển (spec 017 nến phút, spec 019 sửa luồng bán tạm dừng) | Chốt (chọn đáp án AI đề xuất) |
| 2026-11-16 | Bỏ hết chỉ đạo cũ cho V4 (kể cả "bán phải realtime", "không chốt cố định +3%", "không cắt lỗ"); chỉ giữ ràng buộc thật: phí, T+2.5, biên độ, lô 100, thanh khoản | Chốt |
| 2026-11-16 | Tiêu chí chọn: lãi / sụt tối đa, phải đạt cả trong lẫn ngoài mẫu | Chốt |
| 2026-11-16 | Backtest: vốn 40 triệu, 5 chỗ, lô 100 | Chốt |
| 2026-11-16 | "thêm yếu tố đặc tính của cổ phiếu để cố cách hành xử với từng cổ phiếu khác nhau được không" | Hỏi → AI làm P4-05, P4-06 |
| 2026-11-16 | "khớp quá khứ cũng tốt. không sao cả" | Chỉ đạo — AI vẫn ghi tách trong / ngoài mẫu |
| 2026-11-16 | Chọn "Tìm tiếp cách chọn mã" (thay vì dựng lớp né giai đoạn trước) | Chọn hướng |
| 2026-11-16 | "chưa chốt gì hết. thử thêm các cách của Jesse Livermore, Mark Minervini" | Chỉ đạo — **chưa chốt phương án V4 nào** |
| 2026-11-16 | Chọn "Thử ghép" lõi + vệ tinh, rồi "Kiểm bản VCP kỹ hơn", "Thử bản nới", "So V3 cùng bộ đo", "Ghi sổ V4" | Chọn hướng |
| 2026-11-16 | Q4-1: "có lấy giỏ ít biến động làm lõi" | **Chốt** — lõi V4 = giỏ ít biến động (P4-08). Lớp ra tiền mặt khi Giảm giá / Hoảng loạn: **chưa quyết** |
| 2026-11-16 | Q4-2: "không nhất thiết nhưng hãy có bộ kịch bản cho cổ tăng sốc tăng mạnh để tránh chốt lời quá non" | **Chốt** — không bắt buộc 10 bộ; phải có kịch bản cho cổ tăng sốc. Owner chọn thử **cả hai** cách: (1) giữ mã lõi đang tăng sốc thay vì bán khi cân lại; (2) kịch bản riêng mua cổ tăng sốc, để lãi chạy |
| 2026-11-16 | Dùng vệ tinh tăng sốc 2 chỗ (P4-16) cho V4 | **Chốt** |
| 2026-11-16 | Có lớp ra tiền mặt khi thị trường ở giai đoạn Giảm giá / Hoảng loạn | **Chốt** — phạm vi (cả hệ hay chỉ lõi) chưa chốt |
| 2026-11-16 | Lớp tiền mặt chỉ áp cho lõi, chỉ bật khi Giảm giá (D4, P4-17); bỏ Hoảng loạn khỏi điều kiện | **Chốt** — thay phạm vi của dòng trên |

## Câu hỏi mở

| # | Câu hỏi | Hiện trạng |
|---|---|---|
| Q4-1 | V4 dùng giỏ ít biến động (P4-08) làm lõi? Có thêm lớp ra tiền mặt khi Giảm giá / Hoảng loạn không (sụt −31% → −21%, lãi thấp hơn)? | Đã trả lời 16/11 — lõi chốt; lớp tiền mặt chỉ lõi, chỉ Giảm giá |
| Q4-2 | Giữ yêu cầu "10 bộ kịch bản theo giai đoạn" không, khi P4-09 cho thấy đổi giỏ theo giai đoạn làm hỏng danh mục? | Đã trả lời 16/11 — không bắt buộc; cần kịch bản cổ tăng sốc (Q4-8) |
| Q4-8 | Kịch bản cổ tăng sốc: giữ mã lõi tăng sốc (trượt theo đỉnh) và / hoặc mua riêng cổ tăng sốc — cách nào, ngưỡng nào làm danh mục tốt hơn lõi? | Đã trả lời 16/11 — dùng P4-16 (2 chỗ) |
| Q4-3 | Vì sao V3 lỗ 2017–2022 (P4-12): kịch bản nào, giai đoạn nào? Thị trường đổi tính chất hay luật khớp quá sát 2023–2026? | Đã đo — xem P4-13 |
| Q4-4 | Giỏ ít biến động khi làm tròn lô 100 với 40 triệu, và khi khớp ATO phiên sau thay vì ATC cùng phiên, còn giữ kết quả không? | Đã đo — xem P4-14 |
| Q4-5 | Có ETF / chỉ số mua được (VN30, VNDiamond) để làm mốc thật thay chỉ số đều trọng số 300 mã không? Cần giá lịch sử | Chưa có dữ liệu |
| Q4-6 | Thiếu mã đã huỷ niêm yết làm kết quả đẹp lên bao nhiêu? | Chưa đo — DB không có mã đã huỷ |
| Q4-7 | 39.170 giá lẫn đơn vị trên DB production (P4-01) có làm sai biểu đồ / chi tiết mã trên app không? | Chưa kiểm trên app |
| Q4-9 | Lớp tiền mặt áp cho cả lõi và vệ tinh hay chỉ lõi? | Đã trả lời 16/11 — chỉ lõi, chỉ Giảm giá (D4) |
