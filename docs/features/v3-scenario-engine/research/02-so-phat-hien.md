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

### PH-22 — Vốn nhỏ: làm tròn lô xuống làm mất ~1/3 lãi; làm tròn tới lô gần nhất thì giữ được
- Ngày: 2026-10-28. Dữ liệu: production tới 22/10/2026. Bộ đo: `lot1.py` — danh mục luật production (P1++ realtime + PH-21), tiền thật, lô 100 cổ phiếu, tiền mặt lãi 0%.
- Vốn 40 triệu: mua lẻ cổ phiếu (backtest cũ) +344%; làm tròn xuống, không đủ 1 lô thì bỏ mã +239% (bỏ 10 lệnh); làm tròn xuống, không đủ 1 lô thì mua 1 lô nếu đủ tiền +294%; **làm tròn tới lô gần nhất, ít nhất 1 lô, không vượt tiền mặt +347%**, sụt −16,6%. Vốn 100 triệu: +344% / +303% / +317% / +338%.
- Ý nghĩa: phần mất chủ yếu do làm tròn xuống để tiền nằm không (lệnh 8 triệu, giá 30 → 200 cổ phiếu thay vì 266), không phải do bỏ mã giá cao. Làm tròn gần nhất làm ~100 lệnh vượt tỉ trọng nhẹ (tối đa nửa lô).
- Trạng thái: **Đang dùng** — owner chọn làm tròn lô gần nhất 2026-10-28, spec 012 FR-020a, chờ AI thực hiện code.
- Tin cậy: trung bình — đúng cả trong/ngoài mẫu và ở các tham số lân cận, nhưng chỉ ~6 đợt độc lập. Trạng thái: **Đang dùng** — owner chốt 2026-10-27, spec 012 bổ sung v3 (FR-015a, FR-016a), chờ AI thực hiện code.

### PH-23 — Giảm tỉ trọng KB4 không làm danh mục an toàn hơn
- Ngày: 2026-11-02. Dữ liệu: production xuất 02/11/2026, nến tới 22/10/2026 do `load.py` cắt cứng ngày 23/10; DB production có nến tới 02/11. Bộ đo: `kb4size.py` — như `lot1.py` (luật production P1++ realtime + PH-21, tiền thật, lô 100 cổ phiếu, tiền mặt lãi 0%), chỉ đổi hệ số riêng của KB4; KB1/KB2/N1 vẫn ×1,5. Đo thêm phần mất của từng lệnh so với giá trị danh mục lúc vào lệnh, và tỉ trọng thực sau làm tròn lô.
- Lý do đo: lệnh live đầu tiên (FPT · KB4, 30/10/2026, vốn 40 triệu) làm tròn 1,5 lô lên 2 lô = 26,6% danh mục; owner hỏi 20% cho KB4 có quá cao không (câu hỏi mở Q4).
- Vốn 40 triệu, làm tròn lô gần nhất (đang dùng):

| KB4 | Tổng | Sụt tối đa | Tháng tệ nhất | Trong / ngoài mẫu | Lệnh tệ nhất mất (% DM) | Tỉ trọng thực lớn nhất của lệnh KB4 |
|---|---|---|---|---|---|---|
| ×1 (20%) | +347% | −16,6% | −10,5% | +58% / +184% | −5,7% (KB4) | 23,8% |
| ×0,75 (15%) | +326% | −17,5% | −10,5% | +53% / +179% | −4,7% (KB4) | 20,8% |
| ×0,5 (10%) | +310% | −17,3% | −10,6% | +56% / +163% | −4,2% (KB4) | 20,3% |
| Bỏ KB4 | +332% | −18,4% | −11,8% | +71% / +153% | −4,4% (N3) | — |

- Làm tròn xuống (ít nhất 1 lô / thiếu lô thì bỏ), vốn 40 triệu: ×1 +294% / +239%, ×0,75 +276% / +234%, ×0,5 +266% / +231%; sụt −15,5% đến −17,9%.
- Vốn 100 triệu, làm tròn gần nhất: ×1 +338% sụt −16,8%; ×0,75 +324% sụt −17,1%; ×0,5 +292% sụt −15,5%; bỏ KB4 +345% sụt −18,5%. Làm tròn xuống ít nhất 1 lô: ×0,5 +287% sụt −13,8% (thấp nhất bảng), ×1 +317% sụt −15,8%.
- Ý nghĩa:
  - Giảm KB4 chỉ bớt lệnh tệ nhất từ −5,7% xuống −4,2…−4,7% danh mục, nhưng mất 20–37 điểm lãi và **sụt tối đa không giảm** (vốn 40 triệu còn tăng nhẹ). Tiền và chỗ trống được KB4 nhả ra chuyển sang kịch bản khác, sụt giảm của danh mục không do KB4 quyết định.
  - Vốn 40 triệu: ×0,5 vẫn có lệnh KB4 chiếm 20,3% vì lệnh mong muốn 4 triệu nhỏ hơn 1 lô mã giá cao → hạ hệ số không kiểm soát được tỉ trọng thực khi vốn nhỏ.
  - KB4 kéo trong mẫu xuống (+71% → +58%) nhưng kéo ngoài mẫu lên (+153% → +184%) → đóng góp của KB4 không ổn định giữa hai giai đoạn.
  - Chỉ vốn 100 triệu + làm tròn xuống mới thấy sụt giảm bớt rõ (−15,8% → −13,8%), đổi lại mất 30 điểm lãi.
- Trạng thái: **Giữ nguyên KB4 ×1 (20%)** — owner chốt 2026-11-02 ("Giữ 20%, làm tiếp"). Giảm tỉ trọng KB4 không dùng. Rủi ro đuôi KB4 (Q4) vẫn mở, cần hướng khác ngoài tỉ trọng.
- Tin cậy: trung bình — một lần chạy, cùng bộ dữ liệu đã dùng để chọn tham số (Q10).

### PH-24 — Dừng lỗ realtime cho KB4 khi thị trường xấu: lệnh tệ nhất bớt rõ, nhưng sụt tối đa không giảm và lãi mất 7–49 điểm
- Ngày: 2026-11-02. Dữ liệu: production xuất 02/11/2026 (nến ngày, tới 22/10/2026 do `load.py` cắt cứng ngày 23/10). Bộ đo: `kb4stop.py` (lưới) và `kb4stop2.py` (độ bền), nối chuỗi qua `kb4size.py` → `lot1.py` → `flex10.py` → … → `bt.py`: đúng luật production (P1++ realtime + PH-21), tiền 40 triệu, lô 100, làm tròn lô gần nhất. Kiểm nền trước khi đo: không dừng lỗ ra **+347% / sụt −16,6% / lệnh tệ nhất −5,7% danh mục** — khớp PH-22/PH-23.
- Luật đo (chỉ lệnh KB4 **thường**, `deep == False`; lệnh hoảng loạn sâu đã có luật riêng PH-21): từ phiên e+2 (đúng T+2.5) tới trước phiên thoát gốc, nếu **giá thấp nhất phiên ≤ giá vào × (1 − X)** và **điều kiện thị trường ở giá đóng cửa phiên liền trước** (i−1, không dùng phiên i — tránh nhìn trước) đang đúng → bán ở mức dừng; mở cửa đã thấp hơn mức dừng thì bán ở mở cửa; phiên nằm sàn trắng (thấp = cao) không bán được, xét phiên sau. Phí khứ hồi suy từ dữ liệu: **0,40%** (script in ra dòng này).
- Số nền chưa từng ghi sổ (nguồn `kb4early_thuong.py`, `kb4early3.py`): 191 lệnh KB4 thường, thắng 67%, TB +0,84%, tệ nhất −31,2%, lỗ ≥10% 6%, MAE trung bình −3,7%.

| Nhóm "đang lỗ sau phiên 2" | Số lệnh | Thắng | TB nếu giữ | Lỗ ≥10% | Lỗ ≥20% | Tệ nhất | TB nếu bán ở đóng cửa phiên 2 |
|---|---|---|---|---|---|---|---|
| lỗ ≥ 3,0% | 32 | 25% | −5,89% | 31% | 9% | −31,2% | — |
| **lỗ ≥ 4,5%** | **20** | **25%** | **−6,88%** | **35%** | **10%** | **−31,2%** | **−7,3%** |
| lỗ ≥ 7,0% | 11 | 18% | −9,98% | 45% | 18% | −31,2% | — |
| 4,5% + chỉ số lẫn độ rộng phiên 2 cùng xấu hơn phiên vào | 16 | 25% | −6,95% | 31% | — | −31,2% | −7,5% |

- Phân phối kết quả cuối của nhóm 20 lệnh (đóng cửa so giá vào): trung vị **0,0%**, tứ vị 25% −14,5%, 90% +1,0%, lớn nhất +5,6% → nhóm này **hai cực**: phần lớn hồi về gần 0 rồi thoát chạm MA10, một số ít trượt sâu. Trong mẫu (11 lệnh) TB −10,46%, 55% lỗ ≥10%; ngoài mẫu (9 lệnh) TB −2,51%, 11% lỗ ≥10% → toàn bộ sức nặng của ý "cắt sớm" nằm ở trong mẫu (01/2023–06/2025).

**Lưới 4 điều kiện × 4 mức X** (từng lệnh → cả dòng; danh mục 40 triệu → các cột sau):

| Điều kiện (đóng cửa phiên trước) | X | Bị dừng | Thắng | TB | Tệ nhất | Lỗ ≥10% | Tổng | Sụt max | Tháng tệ | Trong / ngoài mẫu | Lệnh tệ nhất %DM | KB4 tệ nhất %DM |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| — (gốc, không dừng) | — | 0 | 67% | +0,84% | −31,2% | 6% | **+347%** | **−16,6%** | −10,5% | +58% / +184% | −5,7% | −5,7% |
| luôn đúng | 5% | 60 | 61% | +0,31% | −24,8% | 1% | +305% | −17,4% | −12,4% | +53% / +165% | −4,1% | −4,0% |
| luôn đúng | 7% | 47 | 63% | +0,37% | −24,8% | 1% | +312% | −17,5% | −11,1% | +52% / +170% | −4,1% | −4,0% |
| luôn đúng | 10% | 26 | 66% | +0,69% | −24,8% | 14% | +335% | −16,5% | −11,8% | +63% / +167% | −3,8% | −3,7% |
| luôn đúng | 15% | 13 | 67% | +0,73% | −24,8% | 8% | +332% | −16,0% | −14,5% | +60% / +170% | −3,9% | −3,8% |
| chỉ số < MA50 −5% | 5% | 49 | 62% | +0,35% | −24,8% | 4% | +314% | −16,3% | −12,5% | +52% / +172% | −4,1% | −4,1% |
| chỉ số < MA50 −5% | 7% | 39 | 64% | +0,31% | −24,8% | 4% | +317% | −16,3% | −12,5% | +52% / +175% | −4,1% | −4,0% |
| chỉ số < MA50 −5% | 10% | 22 | 66% | +0,61% | −24,8% | 13% | +332% | −16,7% | −11,9% | +61% / +168% | −3,9% | −3,8% |
| chỉ số < MA50 −5% | 15% | 12 | 67% | +0,71% | −24,8% | 8% | +339% | −16,0% | −14,6% | +63% / +169% | −4,6% | −3,8% |
| chỉ số xấu hơn lúc vào | 5% | 54 | 62% | +0,27% | −24,8% | 4% | +310% | −17,0% | −12,5% | +51% / +171% | −4,1% | −4,1% |
| chỉ số xấu hơn lúc vào | 7% | 42 | 64% | +0,28% | −24,8% | 5% | +309% | −17,1% | −12,5% | +50% / +174% | −4,2% | −4,0% |
| chỉ số xấu hơn lúc vào | 10% | 23 | 66% | +0,71% | −24,8% | 13% | +338% | −16,7% | −11,9% | +61% / +172% | −3,9% | −3,8% |
| chỉ số xấu hơn lúc vào | 15% | 12 | 67% | +0,75% | −24,8% | 8% | +339% | −16,0% | −14,6% | +63% / +169% | −4,6% | −3,8% |
| độ rộng < 0,20 | 5% | 46 | 62% | +0,41% | −24,8% | 4% | +312% | −16,3% | −12,5% | +52% / +170% | −4,1% | −4,1% |
| độ rộng < 0,20 | 7% | 38 | 64% | +0,33% | −24,8% | 4% | +310% | −16,3% | −12,5% | +52% / +170% | −4,1% | −4,0% |
| độ rộng < 0,20 | 10% | 21 | 66% | +0,67% | −24,8% | 12% | +340% | −16,7% | −11,9% | +61% / +173% | −3,9% | −3,8% |
| độ rộng < 0,20 | 15% | 10 | 67% | +0,72% | −24,8% | 8% | +332% | −16,2% | −15,1% | +60% / +170% | −3,9% | −3,8% |
| **đề xuất của AI**: chỉ số < MA50 **và** xấu hơn lúc vào | 5% | 54 | 62% | +0,27% | −24,8% | 4% | +310% | −17,0% | −12,5% | +51% / +171% | −4,1% | −4,1% |
| **đề xuất của AI** | 7% | 42 | 64% | +0,28% | −24,8% | 5% | +309% | −17,1% | −12,5% | +50% / +174% | −4,2% | −4,0% |
| **đề xuất của AI** | 10% | 23 | 66% | +0,71% | −24,8% | 13% | +338% | −16,7% | −11,9% | +61% / +172% | −3,9% | −3,8% |
| **đề xuất của AI** | 15% | 12 | 67% | +0,75% | −24,8% | 8% | +339% | −16,0% | −14,6% | +63% / +169% | −4,6% | −3,8% |

- Điều kiện "đề xuất của AI" (cửa này do AI thêm để kiểm, không phải luật owner chốt) cho số **y hệt** điều kiện "chỉ số xấu hơn lúc vào" ở cả 4 mức X. Lý do: cửa "dưới MA50" chỉ có tác dụng với lệnh vào lúc chỉ số **trên** MA50, mà trong lưới không có lệnh KB4 thường nào bị dừng ở hoàn cảnh đó → thêm cửa nữa cũng không cắt thêm lệnh nào. Điều kiện thị trường thứ hai là thừa.

**Độ bền** (`kb4stop2.py`, 39 ô: ngưỡng ± 2 điểm, X ± 2%, tách theo năm):

| Ô (X) | Tổng | Sụt max | Tệ nhất/lệnh | KB4 tệ nhất %DM | Ô láng giềng ngay cạnh |
|---|---|---|---|---|---|
| độ rộng < 0,18 (10%) | **+363%** | −16,2% | −25,2% | −3,7% | 8% → +337%, 12% → +361%; ngưỡng 0,20 → +340%, 0,22 → +332% |
| độ rộng < 0,20 (12%) | +347% | −15,7% | −24,8% | −3,7% | 10% → +340%, 8% → +313% |
| chỉ số < MA50 −7% (13%) | +355% | −15,7% | **−21,1%** | **−3,2%** | 15% → +346%, 17% → +348%; ngưỡng −5% → +347%, −3% → +345% |
| đề xuất AI +4 điểm (10%) | +353% | −15,7% | −27,3% | −4,1% | +2 điểm → +330%, +0 điểm → +338% |
| xấu hơn lúc vào (8%) | +317% | −17,5% | −24,8% | −3,8% | −2 điểm → +315%, +2 điểm → +298% |

- **Tách theo năm** (mọi dòng đều in kèm): nền 2023 +8%, 2024 +23%, 2025 +77%, **2026 +90%**. Không một ô dừng lỗ nào (20 ô lưới + 39 ô độ bền) đạt lại +90% của 2026 — cao nhất là +88% (chỉ số < MA50 −7%, dừng 17%), đa số +74…+84%, thấp nhất +68%. Vài ô vượt nền ở **tổng** là nhờ 2023–2025 (2023 có ô +16% so với +8%), còn năm mới nhất — gần lúc chạy thật nhất — luôn kém hơn nền.
- Biến thể "chặt T+2.5" (phiên e+2 chỉ được bán theo **giá đóng cửa**, vì cổ phiếu về tới buổi chiều phiên e+2 — `sim_mix` cũng làm vậy): 5% luôn đúng → 52 lệnh bị dừng, TB +0,67%, tệ nhất **−12,8%**, tổng +351%, sụt −17,1%, KB4 tệ nhất chỉ mất **−1,9%** danh mục. Riêng ô này đã chênh **+46 điểm lãi** (−12,8% so với −24,8% đuôi lệnh) chỉ vì cách xử lý **một phiên** — đúng cái nến ngày không trả lời được (Q7: chưa có nến giờ trước 28/04/2026).
- Ý nghĩa:
  - **Đuôi lệnh có chặn được**: tệ nhất mỗi lệnh −31,2% → −24,8% (dừng 5–15%), mất trên danh mục −5,7% → −3,7…−4,6%. Đây là kết luận duy nhất **đồng thuận ở mọi ô đã đo (59 ô, kể cả biến thể chặt T+2.5)**.
  - **Sụt tối đa của danh mục không giảm**: −15,7% đến −17,5% so với −16,6% gốc. Ô âm nhất (−17,5%) lại là ô dừng sát (7% luôn đúng). Giống PH-23: sụt giảm danh mục không do KB4 quyết định.
  - **Lãi giảm ở mọi ô dừng sát** (−30 đến −49 điểm ở X 5–7%), và số lệnh **lỗ ≥10% tăng** từ 6% lên 12–14% khi dừng ở 10–15%: KB4 thắng 67% nhờ hồi sau giảm, dừng giữa chừng biến lệnh đang hồi thành lệnh lỗ đóng sổ −10%.
  - **Điều kiện thị trường không chọn lệnh tốt hơn**: bốn điều kiện (luôn đúng, MA50 −5%, xấu hơn lúc vào, độ rộng 0,20) cho kết quả chênh nhau trong vòng ±4 điểm lãi ở cùng X — nhỏ hơn hẳn chênh giữa các X. Dừng lỗ "có điều kiện thị trường" không khá hơn dừng lỗ mù.
  - Vài ô vượt nền (+363%, +355%, +353%) nhưng **không hệ thống**: ngay ô bên cạnh rơi xuống +330…+347%, và có ô TB/lệnh thấp hơn nền mà tổng vẫn cao hơn nền (dòng +363%: TB +0,72% < +0,84%) → phần vượt đến từ **xáo trộn chỗ trống trong danh mục** (dừng sớm giải phóng 1 trong 5 slot cho tín hiệu khác), không phải từ bản thân luật dừng. Kết luận: **nhiễu**.
- Đề xuất của AI (không phải quyết định của owner): **không đưa luật dừng lỗ vào production**. Nếu owner chỉ mua sự an toàn cho đuôi lệnh và chấp nhận mất lãi, ô "dừng 15% khi chỉ số < MA50 −7%" là ô ít mất nhất (+346% tại X 15%, tệ nhất −21,1%, KB4 −3,6% DM) — nhưng chỉ cách nền 1 điểm lãi và đứng cạnh ô +355%, tức là không hơn nhiễu. Hướng còn lại để trả lời Q4: chờ dữ liệu **nến giờ/phút** tích lũy (Q7) rồi đo cắt lỗ trong phiên thật, thay vì đoán từ nến ngày.
- Trạng thái: **Chờ owner quyết** — chưa dùng luật dừng nào.
- Tin cậy: **thấp cho việc chọn ô, trung bình cho kết luận chung**. 191 lệnh nhưng chỉ 10–60 lệnh bị dừng/ô; nhóm "lỗ ≥ 4,5% sau 2 phiên" chỉ 20 lệnh (16 ở nhóm phụ) và đã lệch mạnh giữa trong/ngoài mẫu; X và ngưỡng đều dò trên chính dữ liệu này (Q10). Điều kiện thị trường dùng đóng cửa phiên i−1 nên không nhìn trước; nhưng giá khớp trong phiên (đặc biệt phiên e+2) chưa quan sát được (Q7) — số liệu dừng sát vì thế có độ không chắc lớn.
- Chưa kiểm được: hai lệnh KB4 live owner hỏi (FPT vào 30/10/2026, DRI vào 02/11/2026) **không có trong bộ nến đã nạp** — nguyên nhân là `load.py` cắt cứng trước 23/10/2026, còn DB production có nến tới 02/11. Nạp lại với ngày cắt mới (mặc định = ngày xuất file) là có, **không cần xuất lại qua SSH**. Cả hai lệnh còn đang giữ nên chưa có kết quả.

### PH-25 — Giữ lại tiền mặt khi chỉ số dưới MA50: bớt sụt giảm ~2–3 điểm, đổi lấy ~30–50 điểm lãi
- Ngày: 2026-11-03. Dữ liệu: bộ nến đã nạp ngày 02/11 (nến tới 22/10/2026 do `load.py` cắt cứng — xem PH-24). Bộ đo: `capvon.py` (lưới), `capvon2.py` (độ bền) — như `kb4size.py` (luật production P1++ realtime + PH-21, KB4 ×1, KB1/KB2/N1 ×1,5, tiền thật, lô 100, làm tròn lô gần nhất, tiền mặt lãi 0%), thêm trần: khi chỉ số (đóng cửa phiên trước) dưới MA50 quá ngưỡng thì tổng giá trị cổ phiếu đang giữ không vượt **trần × giá trị danh mục**; lệnh không đủ chỗ dưới trần thì mua bớt hoặc bỏ.
- Lý do đo: lệnh live 03/11 (TCB, PNJ KB2 ×1,5) đưa danh mục V3 lên ~97% vốn khi thị trường đang giảm; owner hỏi dồn hết tiền lúc chưa thấy đáy có ổn không. Giả thuyết của AI: giữ tiền mặt để còn tiền cho KB1 lúc hoảng loạn sâu.
- Kiểm nền: không trần → **+347% / sụt −16,6%** (khớp PH-22/23).
- Vốn 40 triệu:

| Cách | Tổng | Sụt tối đa | Tháng tệ nhất | Trong / ngoài mẫu | 2026 |
|---|---|---|---|---|---|
| Gốc (không trần) | +347% | −16,6% | −10,5% | +58% / +184% | +90% |
| Luôn trần 80% (không xét thị trường) | +247% | −15,5% | −9,2% | +51% / +129% | +67% |
| Luôn trần 90% | +284% | −15,7% | −9,8% | +57% / +146% | +71% |
| Chỉ số < MA50 −3%, trần 80% | +315% | −13,9% | −10,5% | +58% / +163% | +78% |
| Chỉ số < MA50 −3%, trần 90% | +346% | −14,8% | −10,7% | +62% / +174% | +82% |
| Chỉ số < MA50 −5%, trần 80% | +299% | −13,7% | −10,5% | +44% / +177% | +82% |
| Chỉ số < MA50 −7%, trần 80% | +314% | −14,3% | −10,5% | +50% / +175% | +85% |
| Chỉ số < MA50, trần 50% | +190% | −12,2% | −10,6% | +36% / +113% | +64% |
| Chỉ số < MA50, trần 80%, **KB1 được dùng phần giữ lại** | +287% | −16,9% | −10,6% | +66% / +133% | +58% |

- Vốn 100 triệu (gốc +338% / −16,8%): MA50 −3% trần 80% +304% / −14,4%; MA50 −5% trần 80% +296% / −13,9%; MA50 −7% trần 80% +301% / −14,1%; luôn trần 80% +237% / −15,7%.
- Ý nghĩa:
  - Trần **theo thị trường** hiệu quả hơn trần **luôn bật** (chỉ là dùng ít vốn): cùng mức sụt ~−14% thì giữ được nhiều lãi hơn hẳn (+315% so với khoảng +250%). Ngưỡng −3/−5/−7% và vốn 40/100 triệu cho kết quả gần nhau → không phải ô may mắn.
  - Đổi chác thật sự: bớt 2–3 điểm sụt tối đa, mất 30–50 điểm lãi; tháng tệ nhất **không đổi** (−10,5%); năm 2026 (gần chạy thật nhất) ô nào cũng kém gốc.
  - Giả thuyết "giữ tiền cho KB1" **bị bác**: cho KB1 dùng phần giữ lại thì sụt còn tệ hơn gốc (−16,9…−17,8%) và lãi vẫn mất.
- Trạng thái: **Không dùng** — owner chốt 2026-11-03 "Giữ nguyên luật" (không đặt trần tỉ trọng). Ô ổn định nhất nếu sau này xét lại: "chỉ số < MA50 −3% hoặc −5%, trần 80%".
- Tin cậy: trung bình — đúng ở nhiều ô lân cận và hai mức vốn, nhưng chỉ ~6 đợt giảm độc lập và tham số dò trên chính dữ liệu này (Q10).
