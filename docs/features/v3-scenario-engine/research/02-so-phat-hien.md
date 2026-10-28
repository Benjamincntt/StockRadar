# 02 — Sổ phát hiện

Mỗi mục: ngày, phát hiện, bằng chứng (số), mức tin cậy, trạng thái. Dữ liệu: production tới 22/10/2026 trừ khi ghi khác.
Trạng thái: **Đang dùng** (vào bộ kịch bản) · **Ghi nhận** (đúng nhưng chưa dùng) · **Bị bác**.

---

### PH-01 — Mua theo đà / breakout thua trong 2023–2026
- Ngày: 2026-10-23. Bộ đo: `simulate`.
- Bằng chứng (thắng trong mẫu / ngoài mẫu): breakout đỉnh 60 phiên + KL 42% / 34%; gần đỉnh 52 tuần + RS 33% / 32%; VCP 29% / 27%; pullback MA20 cổ phiếu mạnh 39% / 30%. Lãi TB đều âm.
- Ý nghĩa: các kịch bản V2 (Nổ hướng lên, Hồi về hỗ trợ) thuộc nhóm này.
- Tin cậy: cao (nhiều biến thể, cả hai giai đoạn). Trạng thái: **Ghi nhận** — lý do loại bỏ V2.

### PH-02 — Trạng thái toàn thị trường dự báo mạnh hơn đặc điểm từng mã
- Ngày: 2026-10-23. Bộ đo: thống kê lợi nhuận 10 phiên theo nhóm (`features.py`, `combo.py`).
- Bằng chứng: `BREADTH50` < 0,2 → thắng 10 phiên 71% / 61%, lãi TB +3,9% / +2,1%. Chỉ số dưới MA50 hơn 7% → 68% / 70%. Các đặc trưng từng mã (RS, KL, RSI2…) chênh giữa nhóm tốt và xấu chỉ ~1–2 điểm.
- Tin cậy: cao nhưng **ít đợt độc lập** (16 đợt hoảng loạn từ 2023). Trạng thái: **Đang dùng** (KB1, KB2).

### PH-03 — Bắt đáy chỉ có lãi khi cú giảm mang tính hệ thống
- Bằng chứng: mã giảm >12%/20 phiên khi thị trường ổn (`BREADTH50` > 0,5) → lãi TB −2,1% trong mẫu. Mua ngay phiên sau phiên nằm sàn → lãi TB −1,1% đến −2,0%. Ngược lại, cùng điều kiện nhưng cả ngành / cả thị trường yếu → lãi dương.
- Tin cậy: cao. Trạng thái: **Đang dùng** (KB4 lọc theo ngành và độ rộng).

### PH-04 — Thị trường bình thường không có lợi thế bền
- Ngày: 2026-10-26. Bằng chứng: khi `BREADTH50` ≥ 0,35, mã trung bình thắng 43%, lãi TB 10 phiên −0,59%. ~25 giả thuyết (ngành bùng nổ, PEAD, trần + KL, nến búa, pocket pivot, NR7, OBV, xoay vòng dẫn dắt, độ rộng bật mạnh, chống chịu khi thị trường giảm, trước nghỉ lễ…) không cái nào vượt 55% ở cả hai giai đoạn. Cây quyết định ở thị trường bình thường chỉ ra luật dính cụm ngày, không giữ ngoài mẫu.
- Trạng thái: **Ghi nhận**.

### PH-05 — Hiệu ứng đầu/cuối tháng
- Bằng chứng: chỉ số bình quân đều +0,153%/ngày trong cửa sổ "5 phiên cuối tháng + 2 phiên đầu tháng sau", −0,012% ngoài cửa sổ. Năm nào cũng trong > ngoài (2023: 0,133 vs 0,109; 2024: 0,112 vs 0,001; 2025: 0,220 vs −0,043; 2026: 0,145 vs −0,147).
- Trạng thái: **Đang dùng** (N3).

### PH-06 — Thị trường tăng qua đêm, giảm trong phiên
- Bằng chứng (TB/ngày, chỉ số bình quân đều): qua đêm 2023 +0,228%, 2024 +0,110%, 2025 +0,163%, 2026 +0,064%; trong phiên −0,104%, −0,064%, −0,109%, −0,101%.
- Hệ quả: mua ATC, bán ATO có lợi hơn mua mở cửa / bán đóng cửa — **trừ** ngày hoảng loạn (PH-07).
- Trạng thái: **Đang dùng** (mua ATC cho KB4, N1, N3; N3 bán ATO).

### PH-07 — Ngày hoảng loạn thường mở cửa giảm tiếp phiên sau
- Bằng chứng: KB1 mua ATC thay vì mở cửa phiên sau: thắng trong mẫu 76% → 62%, lãi TB +2,6% → +1,9%. KB2 tương tự.
- Trạng thái: **Đang dùng** (KB1, KB2 mua mở cửa phiên sau).

### PH-08 — Rút ngắn thời gian giữ làm tệ đi
- Bằng chứng (KB1, luật F): giữ tối đa 5 / 7 / 10 / 15 phiên → lãi TB +1,3% / +2,0% / +2,3% / +2,6%; lệnh tệ nhất −20,0% / −20,3% / −19,3% / −13,1%.
- Lý do: mua lúc bị bán quá mức cần thời gian để hồi. Trạng thái: **Đang dùng** (giữ 10–15 phiên).

### PH-09 — Cắt lỗ cứng không hợp với nhóm bắt đáy
- Bằng chứng: realtime 7% / 10%: tổng lãi/tổng lỗ KB3 giảm từ 1,9 xuống 1,1; KB5 xuống <1. Cắt lỗ theo đóng cửa (2–4 ATR, 8–12%) cho KB4: lệnh tệ nhất vẫn −19,8% đến −23%, lãi TB KB4 giảm từ +1,3% xuống +0,2–1,1%.
- Lý do: hai phiên chờ T+2.5 và mở cửa giảm sâu / nằm sàn làm giá rơi xuyên mức cắt; lệnh bị cắt đúng trước khi hồi.
- Trạng thái: **Ghi nhận** — không dùng cắt lỗ cứng.

### PH-10 — Bán realtime khi chạm MA dễ bán lỗ với mã mua lúc quá bán
- Bằng chứng: KB4 bán realtime chạm MA5: 99/114 lệnh lỗ có giá bán < giá mua. Thêm điều kiện "giá ≥ giá mua + phí" (luật F) sửa được: KB1/KB2 tổng lãi/tổng lỗ lên 5,3.
- Lưu ý: luật F tạo nhiều lệnh hoà vốn (13–26%) và lỗ trung bình sâu hơn (−8% đến −10%), do lỗ dồn vào lúc hết hạn giữ.
- Trạng thái: **Đang dùng** (luật bán của cả 4 kịch bản bắt đáy).

### PH-11 — N3 thua khi chỉ số nằm sát MA50
- Ngày: 2026-10-26. Bằng chứng: N3 vào lệnh lúc chỉ số cách MA50 từ −1,4% đến +0,4% → thắng 41% / 42%, lãi TB −3,1% / −0,5%. Bỏ N3 khi chỉ số trong ±1,5% của MA50: danh mục trong mẫu +43% → +50%, ngoài mẫu +153% → +178%, sụt giảm −23,4% → −16,2%. Độ rộng 1% / 2% cũng tốt; 3% làm ngoài mẫu giảm còn +64%.
- Tin cậy: trung bình (tìm từ chính dữ liệu, nhưng bền với tham số và đúng ở cả hai giai đoạn). Trạng thái: **Đang dùng**.

### PH-12 — Đỗ tiền nhàn rỗi vào cổ phiếu làm tệ đi
- Bằng chứng: mọi rổ (chỉ số, 10 mã thanh khoản nhất, 10 mã ít biến động, 10 mã mạnh nhất; có/không lọc MA50) cho tổng +103% đến +271%, sụt giảm −20% đến −38%, so với +318% / −16,2% khi để tiền mặt.
- Lý do: tiền nhàn rỗi rơi đúng lúc thị trường bình thường (PH-04); khi sập, rổ lỗ đúng lúc kịch bản bắt đáy cần tiền.
- Trạng thái: **Ghi nhận** — chỉ dùng tiền gửi / quỹ tiền tệ.

### PH-13 — Đặt lớn hơn cho kịch bản chất lượng cao giúp cả hai giai đoạn
- Bằng chứng: KB1, KB2, N1 ×1,5 (30% vốn/lệnh): trong mẫu +61% → +71%, ngoài mẫu +188% → +192%, sụt giảm giữ −15,4% (có giả định gửi tiền 4,5%).
- Trạng thái: **Đang dùng**.

### PH-14 — V2 mô phỏng thua lỗ; lịch sử 08:30 có nến giữ chỗ
- Bằng chứng: "Hồi về hỗ trợ" ~1.570 lệnh, thắng 34% / 27%, lãi TB −1,0%. Bỏ mua điểm 2: 41% / 34%, −0,3%. "Nổ hướng lên" 23 lệnh, "Quét thanh khoản" 11 lệnh trong ~4 năm. Danh mục V2: 2023 −1%, 2024 −26%, 2025 −34%, 2026 −39%. Lệnh thật 30/09–22/10/2026: 11 lệnh đã đóng, thắng 3, TB −1,79%.
- Phát hiện phụ: Pha 1 lúc 08:30 thấy nến hôm nay giữ chỗ trong `HistoryJson`. Mua điểm 2 tắt chốt lời 1/2 nên làm tệ đi. Đỉnh hộp của "Nổ hướng lên" dùng `MaxHigh` gồm cả nến dở dang (đọc code, **chưa kiểm trên hệ thống thật**).
- Trạng thái: **Ghi nhận** — căn cứ thay V2.

### PH-15 — N1 "giải chấp cạn kiệt" do cây quyết định tìm ra
- Bằng chứng: lá cây "ngành giảm ≥12%/20 phiên + mã có phiên sàn trong 20 phiên + gần cuối tháng" → lợi nhuận 10 phiên thắng 91% / 73%. Mô phỏng lệnh: thắng 69% / 75%, lệnh tệ nhất −11%. Đổi ngưỡng (ngành −10…−15%, còn 4–8 phiên tới cuối tháng) vẫn thắng 63–84%.
- Trạng thái: **Đang dùng**.

### PH-16 — Owner bác luật bán theo giá đóng cửa → ATO
- Ngày: 2026-10-26. Owner: luật bán phải realtime trên giá khớp.
- Ảnh hưởng: KB4 realtime (chạm MA10, giá ≥ giá vào + phí) lãi TB +0,94%/lệnh trong danh mục (bản đóng cửa → ATO: +1,30%), lệnh tệ nhất −31,2%. N1 realtime (chạm MA20) vẫn tốt: +3,1%. Danh mục: +399% → +352%.
- Trạng thái: **Đang dùng** (bản realtime).

### PH-17 — Hiệu ứng thứ trong tuần: thứ Tư tăng, thứ Sáu giảm, cả 4 năm
- Ngày: 2026-10-27. Dữ liệu: production tới 22/10/2026. Bộ đo: chỉ số bình quân đều (`flex2.py`, `flex3.py`).
- Bằng chứng (TB %/ngày): thứ Tư +0,54 / +0,18 / +0,27 / +0,44 (2023→2026); thứ Sáu −0,01 / −0,09 / −0,31 / −0,35. Thứ Tư tăng cả qua đêm (+0,18) lẫn trong phiên (+0,18); các thứ khác trong phiên đều âm.
- Thành kịch bản (mua ATC thứ Hai, bán ATO thứ Năm, top 5 beta cao, `TV20` > 50): TB +0,46%/lệnh, dương cả 4 năm, nhưng thắng chỉ 52–56%. Chỉ lấy tuần thị trường không hoảng loạn thì còn +0,16% → phần lớn lãi đến từ tuần thị trường yếu.
- Tin cậy: trung bình (bền 4 năm, nhưng tìm ra từ chính dữ liệu, lãi mỏng sát phí). Trạng thái: **Ghi nhận**.

### PH-18 — N3 là động cơ lãi chính lúc thị trường bình thường; chưa có kịch bản dày nào thay được
- Ngày: 2026-10-27. Dữ liệu: production tới 22/10/2026. Bộ đo: `simulate_real2` + danh mục P1++ realtime (`flex4.py`).
- Bằng chứng: bỏ N3 → danh mục +352% → +135%, số tháng không có lệnh mới 3 → 17 / 46. N3 trong danh mục: 103 lệnh, thắng 64%, TB +3,7%, giữ TB ~9 phiên. Thay N3 bằng các ứng viên tín hiệu dày (N4 với 3 luật bán, kịch bản theo tuần, ngành giảm ngắn hạn) đều chỉ còn +82% đến +202%.
- Ý nghĩa: N3 không phải "đầu tư dài hạn": mỗi tháng một ngày báo, tới 5 mã, giữ khoảng 2 tuần. Cả bộ P1++ trung bình ~6 lệnh/tháng.
- Trạng thái: **Ghi nhận** — giữ N3; xem 04 các hướng đã thử.

### PH-19 — Bỏ bám MA, thay bằng chốt lời cố định +3% cho 4 kịch bản bắt đáy: lãi ngang, thắng cao hơn
- Ngày: 2026-10-27. Dữ liệu: production tới 22/10/2026. Bộ đo: `sim_x` (realtime trên giá khớp, T+2.5, sàn trắng, hết hạn bán ATC) + danh mục P1++ (`flex5.py`, `flex6.py`).
- Bằng chứng từng lệnh (thắng trong mẫu / ngoài mẫu, TB): KB1 chạm MA 76% / 82%, +2,59% → chốt +3% 87% / 87%, +2,96%. KB4 68% / 71%, +1,55% → 76% / 84%, +1,38%. N1 78% / 84%, +3,52% → 87% / 89%, +2,40%. KB2 73% / 86%, +2,84% → 78% / 85%, +2,50%.
- Danh mục (KB1, KB2, KB4, N1 cùng chốt +3%, N3 giữ nguyên): tổng +345% (MA: +352%), thắng 75% (68%), sụt max −17,6% (−15,4%), trong mẫu +50% (+60%), ngoài mẫu +196% (+182%), 2026 +101% (+84%). Đồng loạt chốt +5%: +341%, sụt −17,6%.
- Trượt theo đỉnh (5%, 8%) và chốt lời động (lãi ≥3–5% thì trượt 2–3%) đều kém: danh mục +197% đến +223%. Chỉ giữ hết hạn: +295%.
- N3 không hợp chốt lời: +3% làm TB từ +3,21% xuống +1,09%.
- Phí (`flex7.py`): phí khứ hồi chiếm ~9% tổng lãi gộp ở cả luật MA lẫn chốt +3% (0,4%); lệnh thắng lãi ròng < 1%: MA 11%, chốt +3% 6%. Phí 0,6% / 0,8%: MA +304% / +260%, chốt +3% +297% / +254%, chốt +5% +298% / +258% — cả ba tụt gần như nhau.
- Ý nghĩa: luật MA không phải nguồn lợi thế; lợi thế nằm ở điểm mua. Chốt +3% đơn giản hơn (ngưỡng cố định lúc mua, không phải tính lại mỗi tối) nhưng đổi lại sụt giảm sâu hơn ~2 điểm và trong mẫu kém hơn.
- Tin cậy: trung bình (chọn từ 13 luật trên cùng dữ liệu; tổng danh mục chênh nhau trong vòng ±2%). Trạng thái: **Ghi nhận** — chờ owner chọn.

### PH-20 — Đợt sập 04/2025: mọi luật bán hiện có chỉ ăn được phần nhỏ cú hồi; chốt mỏng thiệt nhất
- Ngày: 2026-10-27. Dữ liệu: production tới 22/10/2026. Bộ đo: `flex8.py`.
- Bối cảnh: chỉ số bình quân đều −7,4% (03/04), −7,5% (08/04), thấp nhất −21% dưới MA50 ngày 09/04/2025; độ rộng 0,04.
- Bằng chứng (tín hiệu 03–05/2025, TB lãi ròng/lệnh): đỉnh trong 60 phiên sau giá vào TB +22% (KB2) đến +38% (N1). Ăn được: MA KB1 +2,1% / KB2 +3,2% / KB4 +2,3% / N1 +2,5%; chốt +3%: +2,6% / +2,3% / +1,6% / +1,9%; giữ tới hết hạn: +5,5% / +4,0% / +2,9% / +8,2%.
- Danh mục 03–06/2025: MA +16,7%, chốt +3% +7,6%, chốt +5% +14,8%, chốt +10% +17,3%. Cả năm 2025: MA +75%, chốt +3% +54%.
- Ý nghĩa: chốt +3% bằng MA trên cả giai đoạn nhờ thắng nhiều ở đợt nhỏ, nhưng thiệt nặng ở cú hồi chữ V lớn — đúng lúc lãi lớn nhất. Chưa có luật nào "để lãi chạy" sau đáy hoảng loạn.
- Trạng thái: **Ghi nhận** — đã thử tiếp ở PH-21.

### PH-21 — Lệnh mua lúc độ rộng < 0,15: chốt +15% realtime, giữ tới 40 phiên, tốt hơn bán chạm MA
- Ngày: 2026-10-27. Dữ liệu: production tới 22/10/2026. Bộ đo: `sim_mix` trong `flex10.py` — một lần mô phỏng, mỗi lệnh chọn luật bán theo trạng thái ngày tín hiệu; lệnh thường giữ luật chạm MA. Đã kiểm: không bật luật riêng thì ra đúng số lệnh và lãi TB như bản hiện tại.
- Áp cho KB1, KB2, KB4, N1 khi ngày tín hiệu có `BREADTH50` < 0,15 (29 phiên, ~6 đợt: 10/2023, 04/2024, 08/2024, 04/2025, 03/2026, 07/2026).
- Bằng chứng từng lệnh (lệnh hoảng loạn sâu): MA thắng 81%, TB +4,2%, giữ 6,5 phiên → chốt +15% giữ 40: thắng 86%, TB +9,8%, giữ 25 phiên. Mọi đợt đều tăng (đo với cửa −7%: 8/8 tháng có lệnh).
- Danh mục: +352% → +391%, sụt max giữ −15,4%, trong mẫu +60% → +69%, ngoài mẫu +182% → +191%; 2025 +75% → +85%, 2026 +84% → +89%; 2023–2024 không đổi. Lân cận cũng tốt: giữ 30 → +380%, giữ 60 → +402%, chốt +10% giữ 30 → +360%; chốt +20% kém hơn (+340%).
- Danh mục tăng ít hơn từng lệnh vì giữ lâu chiếm chỗ: ít lệnh hoảng loạn được vào hơn (131 → 119 tín hiệu thực khớp).
- Cửa khác: chỉ số < MA50 −10% (17 phiên) kết quả nhiễu (+349% đến +382%); chỉ số < MA50 −7% lẫn cả đợt điều chỉnh vừa, giữ lâu làm hỏng 2024 (+28% → +17–19%), sụt −16,9% đến −18,2%, lệnh tệ nhất −32% đến −56%.
- Lãi 0% (app không mô phỏng gửi tiền, `flex11.py`): +307% → +344%, sụt −16,6% không đổi.
- Tin cậy: trung bình — đúng cả trong/ngoài mẫu và ở các tham số lân cận, nhưng chỉ ~6 đợt độc lập. Trạng thái: **Đang dùng** — owner chốt 2026-10-27, spec 012 bổ sung v3 (FR-015a, FR-016a), chờ AI thực hiện code.
