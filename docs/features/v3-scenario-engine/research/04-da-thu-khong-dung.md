# 04 — Đã thử, không dùng

Ghi lại để không thử lại. Số là danh mục / kịch bản trên dữ liệu production tới 22/10/2026 trừ khi ghi khác.

## Kịch bản

| Ý tưởng | Kết quả | Lý do loại |
|---|---|---|
| Breakout đỉnh 60 phiên, gần đỉnh 52 tuần, VCP, pullback MA20 cổ phiếu mạnh | Thắng 25–42% | PH-01 |
| RSI2 < 10 trên MA200 (Connors) | Thắng 54–60%, lãi TB ≈ 0 | Không đủ lãi sau phí |
| Mã giảm >12%/20 phiên khi thị trường ổn | Lãi TB −2,1% | PH-03 |
| Mua sau phiên nằm sàn KL lớn | Lãi TB −1,1% đến −2,0% | PH-03 |
| Theo sau FTD (chỉ số lấy lại MA20 sau hoảng loạn) | Thắng 22–56%, lệch giữa giai đoạn | Không bền |
| KB3 mã khoẻ (trên MA200) RSI14 < 35 khi độ rộng < 0,35 | Thắng 71% / 70%, nhưng realtime / cắt lỗ chỉ còn lãi ≈ 0 | Yếu hơn, trùng nhiều với KB4 |
| KB5 bluechip thủng Bollinger dưới khi chỉ số dưới MA50 3% | Thắng 66% / 70%, realtime + cắt lỗ thì âm | Yếu |
| N2 rung lắc cuối tháng trong xu hướng tăng | ~11 đợt | Quá ít mẫu |
| N4 ngày chỉ số giảm ≥1,5% (thị trường bình thường), mua bluechip giảm mạnh nhất | Thắng 69% / 73%, tệ nhất −29,5% | Giữ làm phương án P3 (ưu tiên thắng); thêm vào P1 làm giảm lãi vì tranh chỗ |
| Trước kỳ nghỉ lễ dài | 13 ngày tín hiệu, thắng 38–80% lệch | Ít mẫu, không bền |
| PEAD (nhảy giá + KL mùa báo cáo) | 23 lệnh, lãi âm | Ít mẫu, âm |
| Ngành bùng nổ: mua mã tụt hậu / mã dẫn đầu | Thắng 34–47% | Âm |
| Trần + KL khi thị trường trên MA50 | Thắng 31–47% | Âm |
| Nến búa đảo chiều trong xu hướng | Ngoài mẫu −6% | Âm |
| Giữ tín hiệu KB1/KB2/KB4/N1 nhưng chờ dừng rơi mới vào (đóng cửa tăng, > MA5, > giá phiên tín hiệu, tăng 2 phiên; chờ 3–10 phiên) (2026-11-03) | Thắng và lãi mỗi lệnh giảm ở cả 4 kịch bản; danh mục +92…+277% so với +347%, sụt không giảm | PH-26 |
| Mua gần hỗ trợ 20 phiên, chốt kháng cự, cắt khi thủng hỗ trợ (cách đánh của một đội tư vấn, quy đổi thành luật máy; 2026-11-03) | Thắng 22–33%, TB âm mọi biến thể, danh mục +2…−52% trong 3 năm (V3 +342%) | PH-27 |
| Pocket pivot, NR7, tích luỹ OBV biên hẹp | Thắng 36–48% | Âm |
| Chống chịu: thị trường −1,5%, mã tăng | Ngoài mẫu −3,4% | Âm |
| Xoay vòng top 30 thanh khoản theo RS63 đầu tuần | Thắng 48% / 55% | Yếu |
| Độ rộng bật 40% → 60%, mua RS cao | 32 ngày, lệch mạnh | Ít mẫu |

### Vòng kịch bản tín hiệu dày (2026-10-27, `flex1–4.py`, realtime, T+2.5)

Mục tiêu: kịch bản báo nhiều ngày/tháng cho thị trường bình thường/tăng, thay hoặc bổ sung N3. Thắng ghi trong mẫu / ngoài mẫu.

| Ý tưởng | Kết quả từng lệnh | Trong danh mục P1++ (+352%, sụt −15,4%) | Lý do loại |
|---|---|---|---|
| A1 chỉ số giảm ≥1%/phiên, bluechip trên MA200 giảm mạnh nhất (1,5 ngày/tháng) | Thắng 58–69% / 64–70%, TB −0,04% đến +0,71% | — | Lãi ≈ 0 |
| A2 = N4 (giảm ≥1,5%), mua mở cửa, chốt +5% realtime, hết 10 phiên bán ATO | 65% / 67%, Q4 93%, TB +0,98%, tệ nhất −23,5% | Thay N3: +202%; thêm cạnh N3: +282%, sụt −19,3% | Kém P1++ |
| A2, mua mở cửa, bán ATO sau 8 phiên | 63% / 58%, TB +1,78% | Thay N3: +191%; thêm: +178%, sụt −24,5% | Kém P1++ |
| A2, luật F MA10 giữ 15 | 68% / 77%, TB +0,96%, lỗ TB −12,4% | Thay N3: +174%; thêm: +245%, sụt −25,1% | Kém P1++ |
| A3 chỉ số giảm ≥2,5%/3 phiên, mã trên MA50 RSI2 thấp | 55–64% / 65–86%, TB ≤ +0,17% | — | Lãi ≈ 0 |
| B1/B2 RSI2 của chỉ số < 15, mã mạnh RSI2 thấp | 51–65% / 47–62%, TB −0,59% đến +0,24% | — | Âm / ≈ 0 |
| C1 ngành giảm ≥5%/5 phiên, mã yếu hơn ngành (2,7 ngày/tháng) | 51–63% / 60–72%, TB âm | — | Âm |
| C2 ngành giảm ≥4%/5 phiên khi chỉ số trên MA50, mã trên MA50 | 58–68% / 56–69%, TB +0,17% đến +0,47% | Thay N3: +82%; thêm: +163% | Kém P1++ |
| D1 mã mạnh (RS126 > 0,7) RSI2 < 10, báo gần mỗi ngày (7,5 ngày/tháng) | 53–69% / 47–61%, TB âm | — | Âm |
| Bán theo thời gian (ATO sau 3/5 phiên) cho A1/A2/C2/D1 | TB −0,3% đến +0,2% | — | Giữ ngắn tệ hơn (khớp PH-08) |
| W theo tuần: mua ATC thứ Hai, bán ATO thứ Năm, beta cao (PH-17) | 52% / 56%, TB +0,46%, dương cả 4 năm | Thay N3: +173%, sụt −24,2%; thêm cạnh N3: +423% nhưng sụt −26,9%, tháng tệ −16,2%, W thắng 50% / TB +0,1% | Lãi tăng không đến từ W (W ≈ 0), sụt giảm tăng gần gấp đôi; ngoài mẫu gần như không đổi (+182% → +184%) |
| Các cặp thứ khác (mua T6 bán T3, mua T3 bán T6) | TB âm | — | Âm |

## Luật bán

| Ý tưởng | Kết quả | Lý do loại |
|---|---|---|
| Cắt lỗ cứng realtime 5–10% | Lãi giảm mạnh, sụt giảm không bớt | PH-09 |
| Cắt lỗ theo đóng cửa → ATO (2–4 ATR, 8–12%) cho KB4 | Tệ nhất vẫn −19,8%, lãi TB KB4 giảm | PH-09 |
| Rút ngắn thời gian giữ (5, 7, 10 phiên) | Lãi giảm, tệ nhất xấu hơn | PH-08 |
| Bán realtime chạm MA không cần ≥ giá vào | Bán lỗ nhiều | PH-10 |
| Chốt lời realtime +3/4/5% cho N3 | Thắng tăng (56% → 62–67%), lãi TB giảm +1,7% → +0,8–1,0% | Đổi lãi lấy tỉ lệ thắng; owner chọn ưu tiên lãi |
| KB1, KB2 bán chạm MA20 thay MA10, giữ 20 | Trong mẫu +41% vs +50% | Kém hơn |
| Bán theo đóng cửa → ATO (KB4 MA5, N1 MA10) | Tốt nhất về số (+399%) | Owner bác: bán phải realtime (PH-16) |
| Mua ATC cho KB1, KB2 | Kém hơn mua mở cửa phiên sau | PH-07 |
| Trượt 5% / 8% từ đỉnh (realtime), cả 4 KB bắt đáy | Thắng 41–58%, danh mục kém xa | Bị rung ra trước khi hồi (2026-10-27, `flex5.py`) |
| Chốt lời động: lãi ≥3% trượt 2%, lãi ≥5% trượt 3% / 5%, có / không giữ gấp đôi | Danh mục +197% đến +223%, sụt −18,6% đến −26,1% | Kém chạm MA và kém chốt cố định (`flex6.py`) |
| Chỉ giữ tới hết hạn, không luật giá | Danh mục +295%, sụt −18,2% | Kém |
| Chốt +7% / +10% cho cả 4 KB | Danh mục +302% / +307% | Kém chốt +3% / +5% |
| Chọn luật tốt nhất cho từng KB (KB1 +3%, KB2 trượt, KB4 +5%, N1 +10%) | +369%, sụt −16,0% | Chọn theo kết quả quá khứ, hơn chốt +3% đồng loạt không đáng kể |
| Chốt lời +3/5/7/10% hoặc trượt cho N3 | TB +0,75% đến +1,90% (bản hiện tại +3,21%) | N3 cần giữ tới lịch ATO |
| Luật bán không-MA cho A2, C2, W | TB ≤ +1,7%, không cải thiện | Điểm mua yếu, đổi luật bán không cứu được |
| Lệnh hoảng loạn sâu: trượt từ đỉnh (lãi ≥10% trượt 7–10%, giữ 40–60) | Danh mục +270% đến +313% (`flex9.py`) | Kém chốt +15% |
| Lệnh hoảng loạn sâu: bán nửa theo MA, nửa giữ 30 / trượt | +267% đến +307% | Kém (mô phỏng gộp, thận trọng) |
| Lệnh hoảng loạn sâu theo cửa chỉ số < MA50 −7%, giữ dài | 2024 tụt, sụt tới −18,2%, lệnh tệ nhất −56% | Cửa quá rộng, lẫn đợt điều chỉnh vừa (PH-21) |
| Tách tín hiệu hoảng loạn / thường thành hai lần mô phỏng | Mốc MA lệch +352% → +304–353% | Lỗi phương pháp: hai bộ không chặn trùng mã của nhau; dùng `sim_mix` |
| Dừng lỗ realtime KB4 thường, có điều kiện thị trường (X 5–15% × 4 điều kiện + 1 đề xuất AI = 20 ô, thêm 19 ô láng giềng) | Đuôi lệnh bớt: tệ nhất −31,2% → −24,8%, mất trên danh mục −5,7% → −3,7…−4,6%. Nhưng tổng +347% → +305…+340%, sụt tối đa **không giảm** (−15,7…−17,5%), số lệnh lỗ ≥10% **tăng** 6% → 12–14% ở X 10–15% | Không ô nào ổn định: láng giềng X ± 2% / ngưỡng ± 2 điểm đảo chiều (+313% → +363%); **năm 2026 ô nào cũng kém nền** (+90% → +68…+88%); điều kiện thị trường không khá hơn dừng mù; vài ô vượt nền là xáo trộn slot, không phải lãi của luật (PH-24) |
| Cắt sớm lệnh KB4 thường đang lỗ ≥4,5% sau 2 phiên (bán ở đóng cửa phiên 2) | TB −7,3% so với −6,9% nếu giữ (chưa trừ phí); vẫn 35% lỗ ≥10% | Bán theo đóng cửa — owner bác; trung vị nhóm này 0,0% (hồi về huề rồi thoát chạm MA10), cắt sớm biến lệnh huề thành lệnh lỗ (PH-24) |
| Dừng 5% "chặt T+2.5" (phiên e+2 chỉ được bán theo đóng cửa) | Đuôi đẹp nhất lưới: tệ nhất −12,8%, KB4 chỉ mất −1,9% danh mục, tổng +351% | Chênh +46 điểm lãi so với bản dùng giá thấp nhất cũng chỉ vì cách xử lý **một phiên** — nến ngày không trả lời được (Q7) |
| Cắt lỗ cho cả V3 (2026-11-06, `catlo*.py`, 104 ô / 17 họ, ba cách tính nến ngày): % cố định 3–25%, ATR 1–4, đáy 5/10/20 phiên, MA20/MA50, thời gian (ATC e+3/5/7), trượt từ đỉnh 7–20%, hoà vốn sau +3/5/8%, theo kịch bản, theo chỉ số / độ rộng, giảm riêng so chỉ số / ngành, sàn riêng lúc giữ, cắt một nửa, siết dần, không hồi sau n phiên, thảm hoạ + không hồi, học từ MAE lệnh thắng | BASE +349% / sụt −16,6% / ngoài mẫu +185%. Ô chọn trong mẫu của mọi họ: ngoài mẫu +96…+181%. 6 ô có tổng ≥ BASE (+349…+360%) đều kém ở ngoài mẫu và năm 2026. Chỉ 25–36% lệnh bị cắt là cắt đúng | PH-32: đợt sụt tối đa là cú sập 04/2025, sau đó mọi lệnh hồi về lãi, nên cắt là bán đúng đáy. T+2.5 + sàn trắng làm giá nhảy qua mức cắt (PNJ 07/2026: mức −10%, khớp −25%) |
| Cắt "không hồi" (sau 5–7 phiên chưa vượt giá vào +2–4% thì bật mức cắt 3–10%) — họ tốt nhất của PH-32 | Sụt −16,6% → −14,0…−15,7% đều ở 27 ô lân cận; tổng −0…−51 điểm | Ngoài mẫu và 2026 luôn kém BASE, tháng tệ nhất xấu đi. Phần bớt sụt là do đổi chỗ trống trong danh mục cuối 2024, không cộng dồn theo kịch bản (PH-32). Chỉ giữ làm phương án "mức tham khảo" nếu owner muốn |
| Cắt lỗ cả lệnh hoảng loạn sâu (PH-21) ở 10% / 15% | Danh mục +194% / +237%, 2025 tụt từ +77% xuống +38% | Cắt đúng đáy cú sập 04/2025 (PH-32) |

## Danh mục

| Ý tưởng | Kết quả | Lý do loại |
|---|---|---|
| 3, 4, 6, 8, 10 vị thế | 5 vị thế tốt nhất | — |
| Tối đa 2 mã/ngành | Lãi giảm, sụt giảm tăng nhẹ | — |
| Tối đa 3 lệnh mới/ngày | Lãi giảm mạnh | — |
| N3 tối đa 2–3 chỗ; N3 × 0,75 | Lãi giảm | — |
| Chia vốn theo độ biến động (ATR mục tiêu 2,5–3,5%) | Sụt giảm giảm nhưng lãi giảm nhiều hơn tỉ lệ | Chỉ là dùng ít vốn hơn |
| Phanh: ngừng mua khi danh mục sụt 5–12% (toàn bộ hoặc chỉ N3) | Không giúp / phanh không nhả | Thiết kế sai (xem 01) |
| Giữ tiền mặt cho KB1: trần tỉ trọng khi chỉ số < MA50 nhưng KB1 được vượt trần (2026-11-03) | Sụt tối đa tệ hơn gốc (−16,9…−17,8%), lãi vẫn mất | PH-25 |
| Đỗ tiền nhàn rỗi vào rổ cổ phiếu (6 biến thể) | Tổng +103% đến +271%, sụt −20% đến −38% | PH-12 |
| Lọc N3 thêm độ rộng không sụt mạnh | Không hơn lọc MA50 | — |
| Lọc N3 chỉ khi chỉ số trên MA50 / MA20 / MA20 đang lên | Kém hơn lọc vùng ±1,5% | — |
| Lọc KB4 theo phiên sàn gần đây, ATR cao, giảm 5 phiên mạnh | Nhóm lỗ nặng cũng là nhóm lãi cao nhất | Lọc mất lệnh thắng |
| KB4 × 0,75 / × 0,5 / bỏ KB4, vốn 40 và 100 triệu, ba cách làm tròn lô (2026-11-02) | Lãi giảm 20–37 điểm, sụt tối đa không giảm, lệnh tệ nhất chỉ bớt từ −5,7% xuống −4,2% danh mục | PH-23 |
