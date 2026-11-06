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

### PH-26 — Chờ dừng rơi mới vào làm kịch bản kém đi ở mọi dạng xác nhận
- Ngày: 2026-11-03. Dữ liệu: bộ nến đã nạp ngày 02/11 (tới 22/10/2026 — xem PH-24). Bộ đo: `chodung.py` — giữ nguyên tín hiệu KB1/KB2/KB4/N1 của luật production, nhưng chỉ mua **ATC ở phiên đầu tiên trong W phiên sau tín hiệu** có dấu hiệu ngừng rơi; không có thì bỏ lệnh. Luật bán, giữ tối đa (tính từ phiên mua), T+2.5, sàn trắng, lệnh hoảng loạn sâu (cờ theo ngày tín hiệu) giữ như `sim_mix`. N3 không đổi. Danh mục: tiền 40 triệu, lô 100, làm tròn lô gần nhất. Dấu hiệu xét trên giá đóng cửa phiên mua (gần đúng: thực tế quyết định lúc ~14:25).
- Lý do đo: owner trước nay không mua cổ phiếu đang rơi, chỉ vào khi đã dừng rơi; hỏi có nên lùi điểm vào như vậy không.
- Kiểm nền: mua ngay → **+347% / sụt −16,6%**.
- Từng kịch bản, chờ tối đa 5 phiên (thắng / lãi TB mỗi lệnh):

| Dấu hiệu chờ | KB1 | KB2 | KB4 | N1 |
|---|---|---|---|---|
| Mua ngay (gốc) | 80% / +3,94% | 76% / +3,57% | 72% / +2,91% | 78% / +4,09% |
| Đóng cửa tăng | 72% / +3,09% | 70% / +2,19% | 72% / +2,48% | 69% / +3,20% |
| Đóng cửa > MA5 | 60% / +2,64% | 62% / +1,42% | 57% / +1,66% | 66% / +1,97% |
| Đóng cửa > giá phiên tín hiệu | 69% / +3,03% | 66% / +1,90% | 68% / +2,40% | 70% / +3,02% |
| Tăng 2 phiên liên tiếp | 62% / +2,53% | 62% / +2,10% | 54% / +1,59% | 71% / +1,79% |

- Danh mục (chờ 3 / 5 / 10 phiên): đóng cửa tăng +277% / +267% / +267%, sụt −18,6…−19,1%; > MA5 +160% / +175% / +154%, sụt −14,6…−17,4%; > giá phiên tín hiệu +257% / +231% / +155%, sụt −18,1…−21,2%; tăng 2 phiên +128% / +92% / +96%, sụt −17,1…−21,4%.
- Ý nghĩa:
  - Mọi dạng chờ đều làm **giảm tỉ lệ thắng và lãi mỗi lệnh** ở cả 4 kịch bản: lãi của bộ này nằm ở cú hồi kỹ thuật ngay sau phiên bán ép; chờ xác nhận là mua sau khi đã mất một phần cú hồi, còn lệnh rơi tiếp vẫn bị bắt (lỗ TB không giảm).
  - Sụt tối đa **không giảm** (trừ dạng > MA5 nhưng lãi chỉ còn một nửa); lệnh tệ nhất trên danh mục bớt nhẹ (−5,7% → khoảng −4,3%).
  - Kết quả cùng hướng ở chờ 3, 5 và 10 phiên → không phải do chọn tham số.
- Trạng thái: **Ghi nhận** — không lùi điểm vào. Tin cậy: trung bình–cao cho kết luận chung (cùng hướng ở mọi dấu hiệu, mọi W, cả 4 kịch bản).

### PH-27 — Cách đánh "mua gần hỗ trợ, chốt kháng cự, cắt khi thủng hỗ trợ" (bản quy đổi thành luật máy) lỗ trong 3 năm
- Ngày: 2026-11-03. Dữ liệu: bộ nến đã nạp ngày 02/11 (tới 22/10/2026 — xem PH-24), kỳ đo **01/11/2023 – 22/10/2026**. Bộ đo: `doihtkc.py`.
- Lý do đo: owner gửi khuyến nghị của một đội tư vấn (mua thử gần hỗ trợ, mục tiêu kháng cự, cắt khi thủng hỗ trợ, kèm "R:R" 2–3,5 và "khả năng bật tăng" 45–65%), hỏi phương pháp này 3 năm qua ra sao.
- Quy đổi (giả định của AI — hỗ trợ/kháng cự của đội là vẽ tay nên đây **không** phải đo chính đội đó): hỗ trợ = thấp nhất 20 phiên; kháng cự = cao nhất 20 phiên trước; vào ATC khi giá cách hỗ trợ 2–7%, kháng cự cao hơn giá ≥ 5%, R:R ≥ 2, thanh khoản ≥ 20 tỷ, tối đa 3 mã/ngày (R:R cao nhất). Thoát: chạm kháng cự → chốt; thủng hỗ trợ → cắt (phiên T+2 chỉ xét đóng cửa); giữ tối đa 20 phiên; phí 0,4%. Danh mục 40 triệu, 5 chỗ × 20%, lô 100.
- Cùng kỳ: V3 (luật production) **+342%**, sụt −16,6%; chỉ số bình quân đều +26%, sụt −27,4%.
- Luật chuẩn, cắt trong phiên: 1.079 lệnh, **thắng 26%**, TB **−0,57%**/lệnh, lãi TB +9,1% / lỗ TB −3,9%, tổng lãi/tổng lỗ 0,80; **71% lệnh chạm cắt lỗ**, 11% chạm kháng cự, 18% hết hạn. Danh mục **−31%**, sụt −42,9%.
- Biến thể (danh mục): cắt theo đóng cửa −49%; đệm cắt 2% dưới hỗ trợ −43%; chỉ vào khi chỉ số trên MA50 −30%; đệm + lọc thị trường −47%; N = 10 / 40 phiên +2% / −24%; R:R ≥ 1 / ≥ 3 −35% / −36%; thanh khoản ≥ 100 tỷ −34%; giữ 10 / 40 phiên −43% / −52%. Lãi TB mỗi lệnh âm ở **mọi** biến thể; năm 2026 là năm tệ nhất (−1,1…−3,4%/lệnh).
- Ý nghĩa:
  - "R:R cao" lúc vào không có nghĩa là kỳ vọng dương: R:R trung bình lúc vào 5–9 nhưng chỉ 10–18% lệnh chạm kháng cự; hỗ trợ sát giá nên bị thủng rất thường xuyên (60–77% lệnh).
  - Cắt lỗ sát + T+2.5 làm hỏng nhóm bắt đáy — cùng hướng PH-09.
  - Tỉ lệ thắng thực của bản máy (22–33%) thấp xa con số "khả năng bật tăng 45–65%" đội đưa ra.
- Trạng thái: **Ghi nhận** — không đưa vào V3. Tin cậy: trung bình cho bản quy đổi (âm ở mọi biến thể, >500 lệnh/biến thể); **thấp** cho việc đánh giá chính đội đó, vì phần chọn mã bằng mắt (tin tức, mô hình nến, khung giờ) không mô phỏng được — muốn đánh giá đội phải ghi lại đủ khuyến nghị thật.

### PH-28 — Một tháng gần nhất (01/10 – 02/11/2026): chỉ KB4 ra lệnh, danh mục +0,6% khi chỉ số −5,2%; độ rộng thị trường của bộ nghiên cứu lệch production đúng ở ngưỡng KB1
- Ngày: 2026-11-03. Dữ liệu: production **xuất lại 03/11/2026**, nạp bằng `load.py <thư mục> 2026-11-03` → nến tới **02/11/2026**. Bộ đo: `thangqua.py` — luật production (P1++ realtime + PH-21), tính cả lệnh còn mở (định giá theo đóng cửa 02/11); danh mục 40 triệu bắt đầu trống ngày 01/10.
- Tín hiệu trong kỳ: chỉ **KB4** có lệnh đã vào (8 lệnh); KB1 và KB2 lần đầu bật ngày 02/11 (mua phiên 03/11, chưa có nến); N1, N3 không có lệnh.
  - Đã đóng 6: CEO +0,02%, CII +4,71%, NVL 0,00%, TCX 0,00%, VND +4,11% (cùng tín hiệu 02/10), FPT +2,23% (14/10). Thắng 3/6, TB +1,84%.
  - Đang mở 2 (khớp đúng hai lệnh live): FPT 30/10 −5,14%, DRI 02/11 −0,40%.
- Danh mục: **+0,59%**, sụt tối đa −2,47%; chỉ số bình quân đều cùng kỳ **−5,22%**.
- Đối chiếu production: lệnh KB4 FPT 30/10, DRI 02/11 và danh sách KB2 ngày 02/11 (TCB, PNJ trong top) **khớp** lệnh live. **Lệch:** bộ nghiên cứu tính BREADTH50 ngày 02/11 = **0,179** (< 0,20 → KB1 bật: GEE, KDH, NVL, PNJ, VSC), còn production lưu **0,2198** trong snapshot tín hiệu DRI → KB1 không bật, chỉ KB2 mua. Chỉ số so MA50 gần nhau (−5,91% so với −6,15%). Nguyên nhân (đã tìm, xem Q16): `bt.py` tính MA50 trên bảng pivot nên mã có phiên trống bị NaN và bị đếm là dưới MA50; tính đúng như production thì độ rộng 02/11 = 0,2120 → KB1 **không** bật, khớp production.
- Tin cậy: thấp (một tháng, 8 lệnh) — chỉ là theo dõi, không dùng để chỉnh luật.

### PH-29 — Lọc tin xấu CafeF trước khi vào lệnh: lọc theo cụm "rất xấu" chung không giúp; chỉ nhóm cảnh báo sàn / nợ / kiểm toán (+ báo lỗ) có ích, mẫu nhỏ
- Ngày: 2026-11-03. Dữ liệu: nến như PH-24 (tới 22/10/2026), 709 tín hiệu V3 từ 01/2023 (KB1 96, KB2 134, KB4 241, N1 96, N3 142; 160 mã). Tin: **53.090 tiêu đề** CafeF tải theo mã (khung tin trên trang mã, `Events_RelatedNews_New.aspx`, lùi tới 10–11/2022). Bộ đo: `dumpsig.py` → `taitin.py` → `loctin.py`, `loctin2.py`.
- Lý do đo: owner không mua mã có lãnh đạo dính vấn đề (PNJ — vụ kim cương 07/2026, gia đình Chủ tịch bị giải chấp 10–11/2026); hỏi lọc tin xấu khỏi rổ.
- Cách gắn cờ: tiêu đề khớp nhóm từ khoá trong W ngày trước giờ quyết định (KB1/KB2 08:15 ngày mua ATO; KB4/N1/N3 14:15 ngày mua ATC; tin chỉ có ngày chỉ tính khi trước ngày mua). Nhóm: pháp lý (khởi tố, bị bắt, điều tra, buôn lậu…), giải chấp, sàn GD (cảnh báo, kiểm soát, huỷ niêm yết, chậm công bố…), kiểm toán (ngoại trừ, từ chối ý kiến, hoạt động liên tục), nợ (chậm trả, vỡ nợ, phá sản), lãnh đạo (xin lỗi, từ nhiệm, bị bán…), báo lỗ. Bỏ cờ khi có "thoát diện", "giảm lỗ", "bác yêu cầu", "huỷ niêm yết trái phiếu"…
- Gốc: danh mục **+349%**, sụt −16,6%, 256 lệnh vào.

| Bỏ lệnh dính nhóm | W | Lệnh dính cờ (TB / thắng) | Danh mục | Sụt |
|---|---|---|---|---|
| Rất xấu (pháp lý+giải chấp+sàn+kiểm toán+nợ+lãnh đạo) | 7 / 14 / 30 | 26–60 lệnh, +0,5…+1,2%, thắng 62–65% | +335% / +357% / +335% | −16,0…−16,4% |
| Riêng giải chấp | 7 / 14 / 30 | 11–19 lệnh, **+3,4…+4,8%**, thắng 63–82% | +324% / +324% / +305% | −16,2…−17,0% |
| Riêng lãnh đạo | 7 / 14 / 30 | 10–14 lệnh, **+3,4…+4,1%** | +333% / +335% / +333% | −16,6% |
| Sàn + nợ + kiểm toán | 7 / 14 / 30 | 6 / 10 / 26 lệnh, **−8,3% / −8,0% / −1,9%** | +361% / **+378%** / +369% | −16,0…−16,1% |
| Sàn + nợ + kiểm toán + báo lỗ | 7 / 14 / 30 | 13 / 17 / 44 lệnh, −3,2% / −4,3% / +0,3% | +353% / **+378%** / +368% | **−12,1% / −13,1% / −13,8%** |

- Trường hợp PNJ 07/2026 (W = 30): KB1 20/07 **−12,5%**, KB4 22/07 **+19,7%**, KB2 23/07 **+10,2%** đều bị cờ → bỏ cả ba thì tránh được một lệnh lỗ nhưng mất gần 30% lãi. Tin giải chấp 02/11 19:30 và 03/11 07:58 có trước nhịp 08:30 ngày 03/11.
- Ý nghĩa:
  - Tin giải chấp / lãnh đạo bê bối **không** dự báo lệnh V3 xấu: lệnh dính cờ còn lãi hơn trung bình. Bộ này ăn cú hồi sau phiên bán ép, mà tin này thường chính là lý do bị bán ép — cùng hướng PH-26.
  - Tin "cấu trúc" (diện cảnh báo/kiểm soát, chậm trả nợ trái phiếu, kiểm toán nghi ngờ hoạt động liên tục) mới là tin làm hỏng lệnh (TAR −31%/−18%, NVL 09/2023 −12%/−17%, SMC −13%). Thêm nhóm báo lỗ giảm sụt từ −16,6% xuống khoảng −13%.
  - Mẫu rất nhỏ (6–44 lệnh dính cờ), kết quả nhảy theo W → **chưa đủ để chốt** tham số; +378% ở W = 14 có thể là may.
  - Nhiễu nguồn: CafeF gắn một bài cho nhiều mã (bài "'đế chế' của ông Phạm Nhật Vượng…" dính FPT, BID, VCB, HPG, MBB…); tiêu đề có dấu `"` bị cắt cụt; tin cũ chỉ có ngày, không có giờ. Khung tin là đường nội bộ, không phải API công bố.
- Trạng thái: **Không làm** — owner chốt 05/11/2026: "không cần thêm logic check tin nữa". Hướng từng cân nhắc: chỉ chặn nhóm sàn + nợ + kiểm toán (± báo lỗ), không chặn giải chấp / lãnh đạo. Muốn loại mã theo đánh giá lãnh đạo (kiểu PNJ) là lựa chọn của owner, backtest cho thấy nó **tốn** lãi chứ không thêm. Tin cậy: thấp–trung bình.
- **PH-29b (05/11/2026) — vào nửa tỉ trọng thay vì bỏ** (`loctin3.py`; owner hỏi sau khi bỏ PNJ 03/11 và PNJ lên trần 05/11). Danh mục tổng / sụt tối đa, gốc +349% / −16,6%:

| Nhóm dính cờ | W | Bỏ hẳn | Nửa tỉ trọng | 3/4 tỉ trọng |
|---|---|---|---|---|
| Giải chấp + lãnh đạo (kiểu PNJ) | 7 / 14 / 30 | +310% / +315% / +298%, sụt −17% | +326% / +326% / +326%, sụt −17% | +332% / +333% / +340%, sụt −17% |
| Rất xấu | 7 / 14 / 30 | +335% / +357% / +335% | +341% / +359% / +359%, sụt −15,8…−16,6% | +332% / +339% / +362% |
| Rất xấu + báo lỗ | 7 / 14 / 30 | +338% / +358% / +354%, sụt −11,9…−13,6% | **+346% / +365% / +360%, sụt −14,1% / −13,0% / −13,3%** | +339% / +351% / +362%, sụt −14,9…−15,7% |
| Sàn + nợ + kiểm toán | 7 / 14 / 30 | +361% / +378% / +369% | +351% / +367% / +368% | +349% / +361% / +353% |

  - Nhóm kiểu PNJ: nửa tỉ trọng đỡ tốn hơn bỏ hẳn nhưng vẫn kém gốc khoảng 23 điểm %, sụt không giảm → giảm tỉ trọng ở nhóm này chỉ là trả tiền cho sự yên tâm.
  - "Rất xấu + báo lỗ" nửa tỉ trọng là biến thể duy nhất **không kém gốc ở cả 3 cửa sổ** và giảm sụt (−16,6% → khoảng −13…−14%). Phần giảm sụt chủ yếu đến từ nhóm báo lỗ. Mẫu 33–76 lệnh dính cờ, lợi thế lãi chỉ +0…+16 điểm % trên 3 năm → tin cậy thấp.
  - Trạng thái: **Không làm** — owner chốt 05/11/2026: "Không, giữ luật".

- **PH-30 (05/11/2026) — Top 5 thật của V1 so với V3 cùng kỳ** (`export_v1top.sh` → `v1top.py <dữ liệu> <v1top.txt> atc`, `v1rand.py … atc`). Owner hỏi có nên gỡ Home Top V1 không: "Đo trước rồi quyết".
  - Nguồn: bảng `DailyOpportunities` trên production, 214 dòng, 59 phiên từ 25/06 tới 05/11/2026 (thiếu nhiều phiên; tháng 9 chỉ 7). Không có cách đo V1 đủ 3 năm từ dữ liệu thật.
  - **Mốc thời gian:** `GeneratedAt` lưu theo giờ UTC. 07:xx UTC là 14:xx giờ VN **trong chính phiên `ForTradingDate`**: bản Top lưu lại là lần làm mới trong phiên, đã thấy giá ngày đó. Vì vậy chỉ được giả định **mua ATC cùng phiên**. Bản đầu của PH-30 giả định mua giá mở cửa, tức là dùng thông tin từ tương lai, nên kết luận "V1 +18,5%, không ủng hộ gỡ" là **sai** và đã bỏ.
  - Luật bán cho V1 lấy y luật KB1/KB2: chạm MA10 và ≥ giá vào + phí, tối đa 15 phiên, phí 0,4%. Nến tới 02/11, nên 75 dòng Top cuối chưa đủ 15 phiên và không được tính.

| Nhóm (mua ATC phiên Top) | Lệnh | Thắng | TB/lệnh | PF | Tệ nhất |
|---|---|---|---|---|---|
| V1 Top, mọi dòng | 82 | 39% | **−1,14%** | 0,31 | −19,9% |
| V1 nhãn StrongBuy | 8 | 38% | −0,29% | 0,68 | −7,2% |
| V1 nhãn Watch | 68 | 41% | −1,23% | 0,32 | −19,9% |
| V1 chỉ hạng 1 | 29 | 31% | −1,60% | 0,22 | −19,9% |
| Ngẫu nhiên 5 mã/phiên (TV20 ≥ 20 tỷ), 30 lần | ~167 | 39% | **−1,60%** (−2,51…−0,80) | | |
| V3 mọi tín hiệu cùng kỳ | 71 | 62% | **+3,19%** | 5,17 | −21,5% |

  - Danh mục 5 vị thế × 20% vốn, lãi kép, từ 25/06: **V1 Top −13,3%**, sụt −15,6% (07/2026 −11,2%). **V3 P1 +2,3%**, sụt −11,2%. Chỉ số bình quân đều cùng kỳ −13,2%.
  - Ý nghĩa:
    - Top V1 nằm trong khoảng chọn ngẫu nhiên. Trong kỳ này chưa thấy lợi thế chọn mã. Danh mục V1 đi y như chỉ số.
    - Bản mua giá mở cửa từng cho V1 +1,12%/lệnh: lợi nhuận đó chính là đà tăng trong phiên mà Top đã nhìn thấy lúc 14:00.
  - Giới hạn: mẫu 4 tháng, 82 lệnh, thị trường giảm. Chưa điều chỉnh giá theo quyền. Luật bán của V1 là mượn. Tin cậy: thấp–trung bình.
  - Trạng thái: **chờ owner quyết**. Đang làm tiếp PH-31.

- **PH-31 (05/11/2026) — phát lại Top V1 2023–2026 bằng harness C#** (`backend/StockRadar.Tests/Research/V1TopReplayTests.cs`, chỉ chạy khi đặt `V1_REPLAY_DIR`; so khớp `khop.py`; mô phỏng `v1rep.py <dữ liệu> <csv phát lại>`). Owner: "backtest thử 3 năm xem", chọn harness C#, cho đọc cấu hình và model trên server.
  - Cách làm: cắt nến tới từng phiên; gọi đúng `BuyDecisionEngine`, `SmartMoneyOpportunitySelector` và `SectorWaveRegimeEngine` (sóng ngành giữ trong bộ nhớ). Các cổng, xếp hạng ML + cộng điểm ngành, hygiene và giới hạn ngành chép từ `DailyAnalysisRunner`. Bỏ cổng chốt quyền FireAnt vì không có lịch sử. Universe = giá > 8.000đ trong phiên. VNINDEX ngày lấy từ KBS, đặt O = H = L = C giống bảng `MarketIndices` (bảng này chỉ lưu từ 25/06/2026).
  - Cấu hình V1 trên server (MarketJobs, SmartMoney, PriceRunupFilter, OpportunityRanker) **giống repo**. Model ML trên server: hồi quy logistic 11 đặc trưng, học từ **45 mẫu**, độ chính xác tự báo 100%, ngày học 25/09/2026. Hồ sơ điểm và hồ sơ hiệu chỉnh dùng mặc định.
  - Độ khớp với Top thật cùng ngày: từ 25/09 (model hiện tại) **71%** số mã; trước 25/09 chỉ 14%, vì khi đó model và code V1 còn khác. Harness thể hiện **V1 hiện tại**. Trên 06–11/2026, Top phát lại cho −1,30%/lệnh, sát Top thật −1,14% (PH-30).
  - 943 phiên, 4.544 dòng Top. Pha thị trường: Neutral 597, Unfavorable 346, **không phiên nào Favorable**, vì VNINDEX O = H = L = C (giống production).

| Bán chạm MA10 ≥ giá vào + phí, tối đa 15 phiên, phí 0,4% | Lệnh | Thắng | TB/lệnh | 2023 | 2024 | 2025 | 2026 |
|---|---|---|---|---|---|---|---|
| V1 có ML, mua ATC phiên Top | 2.698 | 46% | **−0,24%** | −0,00% | −0,19% | −0,14% | −0,74% |
| V1 có ML, mua mở cửa phiên sau | 2.518 | 46% | −0,23% | +0,13% | −0,22% | −0,16% | −0,78% |
| V1 có ML, chỉ hạng 1 (ATC) | 684 | 47% | +0,05% | +0,20% | +0,27% | +0,06% | −0,49% |
| V1 có ML, nhãn StrongBuy (ATC) | 23 | 48% | +0,63% | | | | |
| V1 tắt ML, mua ATC | 3.127 | 49% | −0,20% | +0,04% | −0,31% | −0,05% | −0,58% |
| Ngẫu nhiên 5 mã/phiên TV20 ≥ 20 tỷ, 10 lần (ATC) | | | **−0,19%** (−0,26…−0,13) | +0,20% | −0,27% | −0,09% | −0,75% |

| Danh mục 5 vị thế × 20% vốn, lãi kép, từ 01/2023 | 2023 | 2024 | 2025 | 2026 | Tổng | Sụt tối đa |
|---|---|---|---|---|---|---|
| V1 có ML, ATC | −13,6% | +0,7% | +14,5% | −9,0% | **−9%** | −26,5% |
| V1 có ML, mở cửa | −3,6% | +6,1% | −0,8% | −11,9% | −11% | −24,7% |
| V1 tắt ML, ATC | −3,2% | −7,9% | −22,5% | −6,9% | −36% | −46,0% |
| **V3 P1** | +4,0% | +22,0% | +86,0% | +54,0% | **+264%** | −23,4% |
| Chỉ số bình quân đều | +24,1% | +8,8% | +8,9% | −14,2% | | |

  - Ý nghĩa:
    - Top V1 hiện tại **không có lợi thế chọn mã**: lãi mỗi lệnh nằm trong khoảng chọn ngẫu nhiên ở cả 4 năm, có ML hay không cũng vậy. Danh mục V1 kém cả chỉ số bình quân đều và kém V3 rất xa.
    - Hạng 1 và nhãn StrongBuy nhỉnh hơn một chút, nhưng mẫu nhỏ (StrongBuy 23 lệnh/3 năm) và vẫn quanh 0.
    - Model ML có giúp danh mục (−9% so với −36%), nhưng model chỉ học 45 mẫu cuối 2026; đoạn 2023–2025 là áp ngược, không phải dự báo thật.
  - Giới hạn: luật bán của V1 là mượn từ KB1/KB2. Không có cổng chốt quyền và không điều chỉnh giá theo quyền. Pha thị trường không bao giờ Favorable (giống production), nên chưa đo được V1 khi được phép breakout ở pha thuận. Hồ sơ hiệu chỉnh mặc định có thể khác bản production. Tin cậy: trung bình.
  - Trạng thái: **chờ owner quyết** giữ hay gỡ Home Top V1. Gỡ là đổi pipeline, phải qua Spec Kit.

### PH-32 — Mọi họ cắt lỗ cho cả bộ V3 (104 ô, 17 họ): không phương án nào hơn BASE ngoài mẫu; cắt lỗ không bớt được sụt tối đa vì đợt sụt là sập toàn thị trường rồi hồi
- Ngày: 2026-11-06. Dữ liệu: production xuất 03/11/2026, nạp bằng `load.py <thư mục> 2026-11-03` → nến tới **02/11/2026** (bộ của PH-28). Bộ đo: `dumpparts.py` → `catlo.py` (bộ đo) → `catlo2.py` (lưới) → `catlo3.py` (ngoài mẫu, họ MAE) → `catlo4.py`, `catlo5.py` (chẩn đoán).
- Lý do đo: owner muốn Home có danh sách mã V3 đang giữ kèm cột "giá cắt lỗ", nhưng V3 chưa có luật cắt lỗ. Owner: "Cần backtest tất cả các phương án cắt lỗ để xem cái nào hiệu quả. nếu cần thiết hãy sáng tạo ra các cách cắt lỗ chưa từng có và thử nghiệm xem mức độ hiệu quả đến đâu".
- Khác PH-09 / PH-24: PH-09 đo cắt cứng trên bộ mô phỏng cũ (trước luật realtime và PH-21); PH-24 chỉ đo KB4 thường. Lần này áp cho **cả 5 kịch bản** trên luật production hiện tại, thêm nhiều họ chưa đo, tính cả ba cách xử lý nến ngày, và có kiểm ngoài mẫu.
- **BASE** = luật production (P1++ realtime + PH-21, KB1/KB2/N1 ×1,5, 5 chỗ, vốn 40 triệu, lô 100, làm tròn lô gần nhất, tiền mặt lãi 0%): 709 lệnh, thắng 72%, TB +3,39%/lệnh, tệ nhất −31,2%. Danh mục **+349%, sụt tối đa −16,6%**, tháng tệ nhất −10,5%; 2023 +8% / 2024 +23% / 2025 +77% / 2026 +91%; trong mẫu +58% / ngoài mẫu +185%. Khớp PH-23/24 (+347% / −16,6% với nến tới 22/10). Số +264% / −23,4% ở PH-31 đến từ bộ mô phỏng khác (`v1rep.py`: 5 chỗ × 20%, không ×1,5), nên không dùng làm nền ở đây.
- Cách đo: lấy bảng lệnh gốc của BASE rồi đi lại từng phiên, từ phiên mua tới phiên bán gốc. Luật cắt chỉ làm lệnh kết thúc sớm hơn (hoặc bán một phần sớm hơn); phần còn lại giữ luật bán gốc. Sau đó chạy lại danh mục, vì cắt sớm giải phóng chỗ cho tín hiệu khác. Ràng buộc:
  - Realtime trên giá khớp: giá thấp nhất phiên chạm mức thì bán ở mức; mở cửa đã dưới mức thì bán ở giá mở cửa. Mức cắt tính từ dữ liệu tới đóng cửa phiên trước, không nhìn trước.
  - Sớm nhất phiên e+2 (T+2.5). Phiên sàn trắng không bán được. Phí 0,4%.
  - Họ "thời gian" bán ATC: quyết định lúc ~14:25 theo giá khớp, xấp xỉ bằng giá đóng cửa, giống bán hết hạn hiện nay. Không luật nào "đóng cửa rồi bán ATO hôm sau".
- **Thứ tự giá trong phiên** (nến ngày không cho biết). Mỗi ô tính ba cách:
  - `tt` (trung tính, số chính): phiên e+2 chỉ xét giá đóng cửa, vì cổ phiếu về buổi chiều (giống `sim_mix`). Ở phiên bán gốc, nếu bán gốc là chạm mức trong phiên thì coi **cắt lỗ chạm trước**.
  - `xấu`: mỗi lệnh lấy kết quả **thấp hơn** giữa các cách xử lý chỗ mơ hồ — phiên e+2 dùng cả phiên hay chỉ đóng cửa; ở phiên bán gốc, cắt trước hay chốt trước.
  - `tốt`: mỗi lệnh lấy kết quả **cao hơn**.
- Kiểm bộ đo: luật rỗng ra đúng BASE. Ô "KB4 thường, 5%, phiên e+2 chỉ đóng cửa" cho 55 lệnh bị cắt, tệ nhất −12,8%; PH-24 cho 52 lệnh, −12,8%. Hai chỗ lệch: nến dài thêm 8 phiên, và bộ mới xét cả phiên bán gốc.
- Sửa lỗi trong lúc đo: bản đầu của `catlo.py`, ở cách `tốt`, khi mức cắt cao hơn giá cao nhất phiên e+2 vẫn cho bán ở đúng mức cắt. Lỗi làm cách `tốt` lạc quan quá mức; với họ I3 (mức "bán ngay") nó còn treo bộ đo. Đã sửa: trường hợp này bán theo giá đóng cửa. Cách `tt` và `xấu` không bị ảnh hưởng (đã chạy lại, số y hệt), vì chúng luôn chọn kết quả thấp hơn.

**Các họ đã đo và giả thuyết.** x = mức cắt so với giá vào. "Giảm riêng" = mã kém chỉ số / ngành kể từ lúc vào, tính tới đóng cửa phiên trước.

| Họ | Tham số đã quét | Giả thuyết vì sao có thể giúp |
|---|---|---|
| A % cố định | 3, 5, 7, 10, 12, 15, 20, 25% | Chặn lệnh trượt sâu; đơn giản, có sẵn giá để hiện trên Home |
| B theo ATR (phiên tín hiệu) | 1; 1,5; 2; 3; 4 ATR | Mã biến động mạnh cần mức rộng hơn |
| C đáy N phiên trước khi mua | N 5/10/20, đệm 0/2/5% | Thủng đáy gần nhất = cú bán ép chưa xong |
| D theo MA (mức realtime) | MA20 −5/−10%, MA50 −10/−20% | Xa MA quá là xu hướng hỏng |
| E thời gian | ATC phiên e+3/5/7 nếu giá dưới giá vào 0/3/5% | Bộ này ăn cú hồi sớm; vài phiên chưa hồi là hỏng |
| F trượt từ đỉnh | 7, 10, 15, 20%, vẫn giữ chốt lời gốc | Giữ phần lãi đã có. Khác `flex5.py`: ở đây trượt chỉ là lưới cắt, không thay luật chốt |
| F hoà vốn | đỉnh +3/5/8% thì dời mức cắt lên giá vào + phí; có / không kèm cắt 10% | Lệnh đã có lãi thì không để thành lỗ |
| G theo kịch bản | chỉ KB4 / chỉ N3 / chỉ KB1+KB2+N1 / cả lệnh hoảng loạn sâu | Rủi ro đuôi tập trung ở KB4 (Q4) |
| H điều kiện thị trường | x 5/10/15% khi chỉ số xấu hơn lúc vào; x 5/10% khi độ rộng giảm 5/10 điểm so với ngày tín hiệu | Chỉ cắt khi thị trường vào "nhịp giảm thứ hai" |
| I1 (mới) giảm riêng so chỉ số | x 5/7/10% khi mã kém chỉ số 3/5/8% | Giảm vì cả thị trường thì hồi (PH-03); giảm vì riêng mã thì kéo dài |
| I2 (mới) giảm riêng so ngành | như I1, so chỉ số ngành (trung vị ngành) | Như I1, loại thêm được cú giảm của cả ngành |
| I3 (mới) sàn riêng lúc giữ | trong lúc giữ, mã có phiên giảm sát sàn, đang lỗ, thị trường hôm đó không giảm quá 2/4% (hoặc không điều kiện) → bán phiên sau | Sàn riêng thường là bán giải chấp, hay kéo dài |
| I4 (mới) cắt một nửa | bán 50% ở −5/7/10/15%, nửa còn lại giữ luật gốc | Bớt đuôi mà vẫn giữ một phần cú hồi |
| I5 (mới) siết dần | mức cắt 15→5%, 20→7%, 15→8%, 10→3% trong 6–10 phiên | Cú hồi phải tới sớm; giữ càng lâu càng bớt kiên nhẫn |
| I6 (mới) không hồi | sau n = 3/5/7 phiên mà đỉnh chưa từng vượt giá vào +2/3% thì bật mức cắt 3/5/8%. Lưới rộng thêm: n 4–8, ngưỡng hồi +2–4%, x 5–10% (`catlo4.py`) | Lệnh tốt hồi gần như ngay; lệnh không có cú hồi nào là lệnh hỏng |
| I7 (mới) thảm hoạ + không hồi | cắt 15/20% luôn bật + I6 (5 phiên, +2%, 5%) | Vừa chặn thảm hoạ vừa cắt lệnh hỏng |
| I8 (mới) học từ MAE | mức cắt riêng từng kịch bản = phân vị 80/90/95/100% mức sụt sâu nhất trong lúc giữ của lệnh **thắng trong mẫu**, cộng 0,5%; áp nguyên cho ngoài mẫu | Đặt mức cắt ở chỗ lệnh thắng gần như không bao giờ chạm tới |

Mặc định không áp cắt lỗ cho lệnh hoảng loạn sâu (PH-21, độ rộng < 0,15); họ G có biến thể áp cả cho nhóm này. Họ D bật rất sớm (222–369 lệnh bị cắt), vì bộ này mua dưới MA theo thiết kế (mua lúc quá bán).

**Bảng so sánh.** Mỗi họ lấy ô đại diện; đủ 104 ô nằm trong `catlo2_<mode>.csv`. "Cắt" = số lệnh bị cắt. Cột danh mục ghi ba cách tính `tt` / `xấu` / `tốt`; các cột khác theo `tt`.

| Phương án | Cắt | Thắng | TB/lệnh | Tệ nhất | Danh mục tt / xấu / tốt | Sụt max | Tháng tệ | Lệnh tệ nhất (% DM) | 2023 / 24 / 25 / 26 | Trong / ngoài mẫu |
|---|---|---|---|---|---|---|---|---|---|---|
| **BASE** | 0 | 72% | +3,39% | −31,2% | **+349%** | **−16,6%** | −10,5% | −5,7% | +8 / +23 / +77 / +91 | +58 / **+185** |
| A 5% | 175 | 67% | +2,82% | −25,2% | +262 / +203 / +316% | −20,1% | −11,7% | −3,7% | +10 / +20 / +70 / +60 | +59 / +128 |
| A 10% | 77 | 71% | +3,05% | −25,2% | +266 / +247 / +274% | −17,1% | −16,0% | −5,7% | +7 / +19 / +74 / +65 | +52 / +141 |
| A 15% | 40 | 72% | +3,12% | −25,2% | +266 / +266 / +274% | −17,7% | −15,2% | −8,3% | +9 / +18 / +78 / +61 | +51 / +142 |
| A 25% | 11 | 72% | +3,27% | −26,2% | +269 / +269 / +269% | −20,9% | −11,9% | −13,1% | +9 / +26 / +75 / +54 | +63 / +126 |
| B 3 ATR | 37 | 72% | +3,20% | −25,2% | +328 / +312 / +328% | −15,9% | −14,1% | −4,4% | +6 / +20 / +80 / +88 | +51 / +184 |
| C đáy 10 phiên −5% | 106 | 69% | +2,86% | −25,2% | +265 / +241 / +295% | −18,1% | −13,3% | −9,6% | +13 / +23 / +70 / +55 | +65 / +121 |
| D MA20 −5% | 369 | 62% | +2,64% | −25,2% | +325 / +265 / +375% | −19,3% | −15,2% | −3,5% | +16 / +23 / +74 / +70 | +68 / +153 |
| E ATC e+3 nếu dưới giá vào | 205 | 62% | +2,93% | −25,2% | +289 / +289 / +289% | −17,3% | −11,2% | −6,5% | +18 / +28 / +71 / +52 | +78 / +118 |
| E ATC e+7 nếu dưới giá vào −3% | 102 | 71% | +3,15% | −23,0% | +353 / +353 / +353% | −15,2% | −11,9% | −4,4% | +13 / +25 / +79 / +80 | +69 / +168 |
| F trượt 15% | 54 | 72% | +3,11% | −25,2% | +294 / +281 / +295% | −16,9% | −14,8% | −7,8% | +7 / +25 / +83 / +60 | +60 / +146 |
| F đỉnh +8% → hoà vốn | 13 | 71% | +3,35% | −31,2% | +344 / +344 / +344% | −15,8% | −10,5% | −5,7% | +8 / +28 / +78 / +80 | +65 / +168 |
| G chỉ KB4, 10% | 25 | 72% | +3,39% | −21,0% | +337 / +337 / +348% | −16,5% | −15,0% | −3,8% | +12 / +23 / +74 / +83 | +63 / +168 |
| G chỉ KB1/KB2/N1, 10% | 29 | 72% | +3,18% | −31,2% | +349 / +349 / +349% | −16,2% | −10,5% | −5,7% | +8 / +23 / +78 / +89 | +59 / +182 |
| G 10% kể cả lệnh hoảng loạn sâu | 112 | 67% | +2,03% | −25,2% | +194 / +170 / +204% | −18,8% | −16,0% | −3,8% | +2 / +20 / +38 / +74 | +22 / +142 |
| H 15% khi chỉ số xấu hơn lúc vào | 35 | 72% | +3,20% | −25,2% | +350 / +350 / +350% | −17,0% | −14,6% | −3,7% | +10 / +23 / +81 / +83 | +60 / +181 |
| H 10% khi độ rộng giảm 10 điểm | 32 | 72% | +3,29% | −23,3% | +319 / +310 / +333% | −17,4% | −15,4% | −4,1% | +6 / +21 / +71 / +91 | +53 / +174 |
| I1 10% khi kém chỉ số 5% | 60 | 71% | +3,16% | −25,2% | +306 / +302 / +306% | −15,7% | −12,5% | −5,2% | +12 / +28 / +77 / +61 | +70 / +139 |
| I2 10% khi kém ngành 3% | 59 | 72% | +3,19% | −25,2% | +331 / +326 / +332% | −15,7% | −14,1% | −5,1% | +14 / +28 / +76 / +68 | +75 / +147 |
| I3 sàn riêng (thị trường > −2%) | 19 | 72% | +3,36% | −31,2% | +360 / +360 / +360% | −15,7% | −10,2% | −5,6% | +10 / +28 / +79 / +83 | +67 / +176 |
| I4 bán 50% ở −10% | 77 | 71% | +3,22% | −21,6% | +323 / +310 / +324% | −16,1% | −10,8% | −3,4% | +8 / +26 / +75 / +77 | +63 / +160 |
| I5 20% → 7% sau 10 phiên | 67 | 72% | +3,08% | −25,2% | +295 / +282 / +295% | −17,2% | −13,5% | −8,4% | +13 / +22 / +79 / +60 | +63 / +143 |
| I6 5 phiên chưa +3% → cắt 8% | 58 | 72% | +3,24% | −21,0% | +342 / +342 / +350% | **−14,4%** | −11,2% | −4,4% | +13 / +25 / +77 / +78 | +68 / +163 |
| I6 5 phiên chưa +2% → cắt 5% | 78 | 71% | +3,21% | −27,3% | +314 / +314 / +319% | **−14,0%** | −10,9% | −4,6% | +12 / +19 / +76 / +76 | +63 / +155 |
| I7 20% + I6 | 85 | 71% | +3,11% | −25,2% | +251 / +251 / +255% | −19,3% | −10,9% | −11,0% | +12 / +21 / +76 / +47 | +66 / +112 |
| I8 MAE phân vị 95% (KB1 9,1 · KB2 10,0 · KB4 8,6 · N1 13,1 · N3 7,8%) | 93 | 71% | +2,98% | −25,2% | +249 / +228 / +249% | −19,1% | −11,9% | −4,1% | +14 / +18 / +69 / +53 | +59 / +119 |

- Trên 104 ô (`tt`): 6 ô có tổng ≥ BASE (+349…+360%: E e+7 ba ô, G chỉ KB1/KB2/N1 10%, H 15% khi chỉ số xấu hơn, I3). Cả 6 ô đều kém BASE ngoài mẫu (+168…+182%) và năm 2026 (+80…+89%). 35 ô có sụt tối đa thấp hơn BASE, phần lớn chỉ bớt dưới 1 điểm.

**Chia theo kịch bản** (TB/lệnh `tt`; BASE: KB1 +3,94% · KB2 +3,57% · KB4 +2,91% · N1 +4,09% · N3 +3,21%):
- A 10%: KB1 +3,58 · KB2 +3,35 · KB4 +2,90 · N1 +3,16 · N3 +2,60.
- I6 5 phiên +3% → 8%: KB1 +3,49 · KB2 +3,44 · **KB4 +3,04** · N1 +3,71 · N3 +2,90.
- KB4 là kịch bản duy nhất có lãi mỗi lệnh tăng ở nhiều ô (20 ô, tăng tối đa +0,16 điểm, ở I1 "7% khi kém chỉ số 5%"). N1 chỉ tăng ở 2 ô (tối đa +0,11, ở F hoà vốn +8%). KB1, KB2, N3 không tăng ở ô nào. N1 và N3 giảm nhiều nhất: N3 bán ATO theo lịch, nên cú hồi dồn vào cuối kỳ giữ.
- Riêng KB4 (`catlo4.py`, chọn **sau** khi xem lưới): I6 5 phiên +3% → 8% cho +356%, sụt −16,1%; "10% khi kém ngành 3%" cho +363%, sụt −15,8%. Ngoài mẫu vẫn thấp hơn BASE (+169% và +177%, so với +185%). Lệnh ngoài mẫu (mọi kịch bản): TB +3,89…+3,90% (BASE +3,92%), tệ nhất −14,0…−21,5% (BASE −21,5%).

**Ngoài mẫu** (`catlo3.py`): chọn ô tốt nhất **trong mẫu** (01/2023–06/2025, theo lợi nhuận danh mục), rồi xem đúng ô đó ở ngoài mẫu (07/2025–02/11/2026). BASE: trong mẫu +58%, ngoài mẫu +185% (sụt ngoài mẫu −10,8%).

| Họ | Ô được chọn trong mẫu | Trong mẫu | Ngoài mẫu (sụt) | Lệnh ngoài mẫu TB / tệ nhất (BASE +3,92% / −21,5%) |
|---|---|---|---|---|
| A % cố định | 7% | +65% | +116% (−13,4%) | +2,87% / −25,2% |
| B ATR | 1 ATR | +63% | +96% (−16,7%) | +2,71% / −25,2% |
| C đáy N phiên | đáy 10 phiên −5% | +65% | +121% (−16,2%) | +3,07% / −25,2% |
| D MA | MA20 −5% | +68% | +153% (−10,6%) | +2,77% / −25,2% |
| E thời gian | ATC e+3 nếu dưới giá vào | +78% | +118% (−13,5%) | +2,90% / −25,2% |
| F trượt | 10% | +64% | +104% (−14,4%) | +2,72% / −25,2% |
| F hoà vốn | đỉnh +8% | +65% | +168% (−15,3%) | +3,82% / −21,5% |
| G theo kịch bản | chỉ KB4 10% | +63% | +168% (−11,8%) | +3,81% / −14,4% |
| H thị trường | 15% khi chỉ số xấu hơn | +60% | +181% (−12,3%) | +3,75% / −25,2% |
| I1 giảm riêng (chỉ số) | 7% khi kém 3% | +72% | +115% (−10,9%) | +2,91% / −25,2% |
| I2 giảm riêng (ngành) | 10% khi kém 3% | +75% | +147% (−13,7%) | +3,47% / −25,2% |
| I3 sàn riêng | thị trường > −4% | +70% | +153% (−12,0%) | +3,62% / −25,2% |
| I4 cắt một nửa | 50% ở −10% | +63% | +160% (−15,8%) | +3,60% / −18,9% |
| I5 siết dần | 15% → 8% sau 6 phiên | +63% | +139% (−14,6%) | +3,15% / −25,2% |
| I6 không hồi | 7 phiên chưa +3% → 8% | +70% | +160% (−10,5%) | +3,62% / −23,0% |
| I7 thảm hoạ + không hồi | 20% + I6 | +66% | +112% (−19,3%) | +3,32% / −25,2% |
| I8 MAE | học trong mẫu, phân vị 80–100% | +54…+64% | +116…+145% | +2,72…+3,53% |

- Cách `xấu`: ô được chọn đổi ở vài họ, kết luận không đổi (ngoài mẫu +109…+181%).
- Chọn ngược (ô tốt nhất ngoài mẫu, đem xem trong mẫu): cao nhất +184% (B 3 ATR), vẫn dưới BASE; trong mẫu ô đó chỉ +51% (BASE +58%).

**Chẩn đoán** (`catlo4.py`, `catlo5.py`):
- **Đợt sụt tối đa của BASE (−16,6%) kéo từ 28/08/2024 tới 09/04/2025.** Đáy là cú sập tháng 4/2025: chỉ số bình quân đều −21,5% trong cùng khoảng. Ở đáy 09/04, 20 lệnh mua 03–08/04 đang lỗ từ −3% tới −19%, phần lớn quanh −13%. Sau đó **tất cả đều quay về lãi** (+3,4% tới +16,7%), trừ CTR KB4 −1,4%. Mọi mức cắt ≤ 13% đều bán đúng đáy những lệnh này. Vì thế cắt lỗ **không** bớt được sụt tối đa: đợt sụt do cả thị trường sập rồi hồi, không do vài mã hỏng.
- Lệnh bị cắt rồi giá đi đâu (`tt`):

| Luật | Lệnh bị cắt | TB sau khi cắt | TB nếu giữ theo luật gốc | Cắt đúng (giữ còn tệ hơn) | Tổng chênh |
|---|---|---|---|---|---|
| A 10% | 77 | −11,3% | −8,2% | 30% | −241 điểm % |
| I6 5 phiên +3% → 8% | 58 | −10,2% | −8,3% | 36% | −109 điểm % |
| E ATC e+7 dưới giá vào −3% | 102 | −8,8% | −7,2% | 25% | −169 điểm % |

  Chỉ 25–36% lệnh bị cắt là cắt đúng. Số còn lại hồi một phần sau khi đã bị cắt.
- **Cắt lỗ làm lệnh tệ nhất tệ hơn.** Phần lớn các ô có lệnh tệ nhất −25,2%, trong khi BASE KB1 tệ nhất chỉ −12,5%. Đó là **PNJ KB1, mua 21/07/2026** ở giá 40,90: phiên e+2 (23/07) **sàn trắng** nên không bán được; ngày 24/07 mở cửa 30,75 (−24,8%) và lệnh cắt khớp ở giá đó. Nếu giữ theo luật gốc thì giá hồi lên, bán sau 14 phiên ở −12,5%. Do T+2.5 và biên độ sàn, giá **nhảy qua** mức cắt. Mức cắt chỉ là giá mong muốn, không phải giá khớp được.
- **I6 (không hồi)** là họ duy nhất bớt sụt tối đa **một cách đều đặn**: 27 ô lân cận với n = 5–7 phiên đều cho sụt −14,0…−15,7% (BASE −16,6%), danh mục +298…+362%. Nhưng:
  - Phần bớt sụt không cộng dồn theo kịch bản: chỉ N3 −15,5%, chỉ KB4 −16,1%, chỉ KB1/KB2/N1 −16,9%, tất cả −14,4%. Phần bớt chủ yếu đến từ việc đổi chỗ trống trong danh mục cuối 2024 (giống PH-24), không phải vì luật cắt làm từng lệnh tốt hơn.
  - Đáy vẫn là 09/04/2025. Tháng tệ nhất **xấu đi**: −10,5% → −10,6…−14,6%.
  - **Ngoài mẫu và năm 2026 luôn kém BASE**: ngoài mẫu +149…+182% (BASE +185%), 2026 +71…+87% (BASE +91%). Trong mẫu thì luôn hơn: +60…+73% (BASE +58%). Lợi thế chỉ nằm ở 2023–2024.
  - n = 8 cho tổng cao hơn BASE (+355…+387%) nhưng sụt không giảm (−15,6…−16,7%).
- Thứ tự giá trong phiên quan trọng với họ cắt chặt (5–7%, đáy N phiên, MA20 −5%): chênh giữa `xấu` và `tốt` tới hơn 100 điểm % (A 5%: +203…+316%; MA20 −5%: +265…+375%). Cắt càng chặt thì kết quả càng phụ thuộc vào thứ mà nến ngày không trả lời được (Q7). Họ cắt rộng và họ I6 gần như không đổi giữa ba cách.

- Ý nghĩa:
  - **Không có luật cắt lỗ nào đáng đưa vào V3**, xét theo lợi nhuận ngoài mẫu. Tính cả 17 họ, 104 ô, lưới I6 rộng và các biến thể chỉ KB4: ô chọn trong mẫu của **mọi** họ đều kém BASE ngoài mẫu (+96…+181% so với +185%). Năm 2026, gần lúc chạy thật nhất, không ô nào vượt BASE. Ô cao nhất chỉ bằng BASE (+91%, H "10% khi độ rộng giảm 10 điểm"), nhưng tổng của ô đó chỉ +319%.
  - Cùng hướng PH-09, PH-24, PH-26, PH-27: lợi thế của V3 là cú hồi sau bán ép. Cắt lỗ bán đúng phần đáy của cú hồi, nhất là khi T+2.5 và sàn trắng làm giá nhảy qua mức cắt.
  - Sụt tối đa của V3 là rủi ro **toàn thị trường** (04/2025), không phải rủi ro từng mã. Muốn giảm sụt phải giảm tổng tỉ trọng (PH-25: bớt 2–3 điểm sụt, mất 30–50 điểm lãi). Cắt lỗ từng lệnh không làm được việc này.
  - Nếu owner vẫn muốn cột "giá cắt lỗ" trên Home làm mức **tham khảo / cảnh báo** (không tự bán), có hai ô ít tốn nhất mà có giá biết trước:
    - **I6 "5 phiên chưa hồi +3% → cắt 8%"**: mất 7 điểm lãi, sụt −14,4%, ngoài mẫu +163%. Giá cắt chỉ hiện từ phiên thứ 5, khi lệnh chưa có cú hồi nào.
    - **"Chỉ KB4, 10%"**: mất 12 điểm lãi, sụt không đổi. Lệnh KB4 tệ nhất −31% → −14%; lệnh tệ nhất mất trên danh mục −5,7% → −3,8%. Giá cắt hiện ngay lúc mua.

    Cả hai đều là **trả tiền cho sự yên tâm**: kém BASE ngoài mẫu 17–22 điểm, và phần lợi chưa vượt nhiễu tham số.
- Đề xuất của AI (không phải quyết định của owner): giữ V3 **không** cắt lỗ tự động. Nếu cần cột này trên Home, ghi rõ đó là mức tham khảo, kèm cảnh báo rằng T+2.5 và sàn trắng có thể làm giá khớp thấp hơn mức đó rất nhiều (PNJ 07/2026: mức −10%, khớp −25%). Đổi luật bán phải qua Spec Kit.
- Trạng thái: **Chờ owner quyết**.
- Tin cậy: **cao** cho kết luận "cắt lỗ không giúp V3": 104 ô cộng lưới rộng, cả ba cách tính nến ngày, kiểm ngoài mẫu hai chiều, khớp PH-09/PH-24. **Thấp** cho việc chọn ô I6 hay "chỉ KB4": phần lợi nằm trong vùng nhiễu, tham số dò trên chính dữ liệu này (Q10), và chỉ có khoảng 6 đợt hoảng loạn độc lập.
- Chưa kiểm được:
  - Giá khớp thật trong phiên, nhất là buổi chiều phiên e+2 (Q7: nến giờ chỉ có từ 28/04/2026).
  - Trượt giá khi bán cắt lỗ lúc thanh khoản cạn (Q1). Số liệu ở đây coi như khớp đúng mức cắt hoặc giá mở cửa, nên đã là cận lạc quan cho phía cắt lỗ.
  - Cắt sớm thì mã đó có thể ra tín hiệu mới trong khoảng lẽ ra còn giữ. Bảng lệnh gốc chặn mã đang giữ, nên các tín hiệu này bị bỏ qua (hiệu ứng nhỏ, chưa đo).
  - Độ rộng thị trường của `bt.py` lệch production (Q16), nên KB1, KB4 và hoảng loạn sâu bật nhiều hơn thực tế. Mọi ô dùng cùng dữ liệu nên so sánh vẫn nhất quán, nhưng số tuyệt đối có thể đổi.
