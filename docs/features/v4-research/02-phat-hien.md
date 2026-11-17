# Phát hiện V4

Mọi số: dữ liệu xuất 16/11/2026, nến tới 13/11/2026, phí 0,6%, trừ khi ghi khác. "Trong mẫu" = 2017–2022, "ngoài mẫu" = 2023 → 11/2026. Danh mục bắt đầu 01/2017.

### P4-01 — Dữ liệu production có 39.170 giá lẫn đơn vị
- Ngày: 16/11/2026. Script: `v4core.py`, `soi*.py` (không giữ).
- Mã giá < 1.000đ có nến lưu đồng lẫn nghìn đồng. Không sửa thì backtest ra lãi ảo (+35% đến +925%/lệnh TB). 23 mã không sửa được, đã loại.
- Ảnh hưởng ngoài nghiên cứu: biểu đồ / chi tiết mã trên app có thể hiện giá sai ở các mã này. **Chưa kiểm trên app.**
- Trạng thái: **Đã xử lý trong bộ đo**.

### P4-02 — Giai đoạn thị trường có ý nghĩa
- Lãi chỉ số 20 phiên sau theo giai đoạn: Hoảng loạn +1,9%, Hồi phục +1,1%, Tăng mạnh +1,0%, Điều chỉnh +0,8%, **Giảm giá −1,0%** (2016 → 2026).
- Tin cậy: trung bình. Trạng thái: **Dùng làm nền**.

### P4-03 — 9 họ tín hiệu × 7 kiểu thoát: gần hết âm ngoài mẫu
- Script: `v4quet.py`. Họ: phá đỉnh 55 phiên, nhịp chỉnh trong xu hướng (RSI2 < 10, trên MA200), quá bán, mua mã mạnh khi hoảng loạn, động lượng RS126, nền chặt rồi phá, phiên bùng nổ, trở lại xu hướng, sát đỉnh 1 năm. Thoát: giữ 10 phiên, ATR cắt/chốt, trượt ATR, MA10, MA5.
- Chỉ "mua mã mạnh khi hoảng loạn" dương ngoài mẫu (+0,4 … +2,3%/lệnh) nhưng gần 0 hoặc âm trong mẫu (−0,7 … +0,4%). "Nền chặt + trượt 3 ATR" dương cả hai nhưng nhỏ (+1,6% / +0,2%). Còn lại âm ngoài mẫu.
- Trạng thái: **Ghi nhận** — tín hiệu đơn lẻ không đủ.

### P4-04 — Giai đoạn quyết định nhiều hơn chọn chiến lược
- Script: `v4o.py`. TB mỗi lệnh gộp mọi cấu hình:

| Giai đoạn | Trong mẫu | Ngoài mẫu |
|---|---|---|
| Hoảng loạn | −0,2 … +0,6% | +0,6 … +1,3% |
| Hồi phục | −1,0 … −1,9% | +0,2 … +0,8% |
| Tăng mạnh | +0,2 … +0,3% | −0,6 … −1,7% |
| Điều chỉnh | +0,1 … +1,0% | −0,1 … −1,7% |
| Giảm giá | −0,6 … −1,8% | −1,0 … −1,2% |

- Trạng thái: **Ghi nhận**.

### P4-05 — Đặc tính cổ phiếu: RS, ngành, thanh khoản tách được lệnh tốt/xấu (xét từng lệnh)
- Script: `v4dt.py`. Chênh lãi TB nhóm cao − nhóm thấp, cùng dấu trong lẫn ngoài mẫu: **RS63 cao tốt hơn ở cả 5 giai đoạn**; ngành mạnh tốt hơn ở 4/5; thanh khoản cao tốt hơn ở Tăng mạnh / Điều chỉnh / Hoảng loạn; biến động cao tốt hơn khi Hoảng loạn, xấu hơn khi Điều chỉnh / Giảm giá.
- Nhưng P4-07: ưu tiên RS khi chọn 5 mã cho danh mục lại **kém hơn chọn ngẫu nhiên**.
- Trạng thái: **Ghi nhận** — có ích ở cấp lệnh, không chuyển thành lãi danh mục.

### P4-06 — Cá nhân hoá luật theo từng mã: không giữ được
- Script: `v4ca.py`. Đầu mỗi năm, mỗi mã chọn cấu hình tốt nhất trong quá khứ của chính nó (co về trung bình nhóm, k = 10).
- TB lệnh 2023–2026: cá nhân hoá −0,87 … −1,02%; mọi lệnh −0,68%. Kém hơn không cá nhân hoá.
- Owner 16/11: "khớp quá khứ cũng tốt. không sao cả" — vẫn ghi tách trong / ngoài mẫu; kết quả cuốn chiếu ở trên là ngoài mẫu.
- Trạng thái: **Loại**.

### P4-07 — Danh mục từ tín hiệu lệnh: thua chỉ số
- Script: `v4dm.py`, `v4rr.py`. Mốc chỉ số đều trọng số: +109%, sụt −62%.
- Nhịp chỉnh khi Hoảng loạn / Hồi phục, RS cao: −11%, sụt −75% (2022: −68%). Chọn ngẫu nhiên cùng tín hiệu: +116% TB.
- Thêm thoát khi Giảm giá + cắt 2 ATR, chọn ngẫu nhiên: +67%, sụt −53%.
- **Chỉ số ra tiền mặt khi Giảm giá hoặc Hoảng loạn: +101%, sụt −28%** (lãi / sụt 0,27 so với 0,13). 2023–2026: −8%.
- Ý nghĩa: lãi từng lệnh dương không thành lãi danh mục 5 chỗ vì dồn vào đợt sập liên tiếp. Né giai đoạn xấu giảm sụt mạnh.
- Trạng thái: **Ghi nhận**.

### P4-08 — Giỏ 5 mã ít biến động: phương án vững nhất
- Script: `v4xv.py`, `v4ben.py`, `loi2326.py`. Top 100 thanh khoản, 5 mã biến động 120 phiên thấp nhất, cân lại 21 phiên.

| Cách chọn giỏ | Tổng | Sụt | Lãi/sụt | Trong mẫu | Ngoài mẫu |
|---|---|---|---|---|---|
| Chỉ số đều trọng số | +109% | −62% | 0,13 | +52% | +32% |
| **Ít biến động, đang trong xu hướng (giá > MA200, MA50 > MA200)** | **+210…+217%** | **−31%** | **0,39–0,40** | +95% | +62% (sụt −17%) |
| Ít biến động, mọi mã | +158% | −32% | 0,32 | +49% | +72% |
| Ít biến động trong xu hướng, ra tiền mặt khi Giảm giá / Hoảng loạn (10 mã, `v4ghep.py`) | +151% | −21% | 0,47 | +116% | +16% |
| Ngẫu nhiên cùng tập mã | +22% / −4% | −66% / −62% | | | |
| Động lượng 6-1 tháng | −85% | −89% | | | |
| Ngành mạnh nhất | −74% | −78% | | | |
| Gần đỉnh 1 năm | +102% | −59% | | +156% | −22% |
| Thanh khoản cao nhất | −5% | −68% | | | |
| Mã giảm mạnh 1 tháng | −71% | −75% | | | |

- **Độ bền:** lưới 108 ô (biến động 60/120/250 phiên × top 50/100/200 × 5/10 mã × cân lại 10/21/63 phiên × có/không lọc xu hướng). Với top 100: **36/36 ô dương cả hai giai đoạn**, tổng +97 … +246%, sụt −21 … −42%. Top 50 yếu hơn (+8 … +116%); top 200 cần lọc xu hướng.
- Giá mã được chọn: trung vị 25 nghìn đồng, 6,8% lần chọn có giá > 80 nghìn (1 lô > 1/5 vốn 40 triệu).
- Năm 2022: −12 … −27%, các năm khác dương hoặc gần 0.
- Tin cậy: **cao** cho "ít biến động trong top 100 thanh khoản thắng mốc và thắng ngẫu nhiên". Trạng thái: **Ứng viên lõi V4** — chưa chốt.

### P4-09 — Đổi giỏ theo giai đoạn: làm hỏng danh mục
- Script: `v4gd.py`, `v4ghep.py`. Xét lãi 21 phiên TB, beta cao / biến động cao có vẻ tốt khi Hoảng loạn, Hồi phục (ngoài mẫu +2 … +6%).
- Danh mục đổi sang beta cao khi Hoảng loạn / Hồi phục: +89 … +137%, **sụt −71%** (2022: −56%), vì Hoảng loạn 2022 kéo dài.
- "Ít biến động mọi giai đoạn": +225%, sụt −29%.
- Ý nghĩa: không có căn cứ cho 10 bộ kịch bản theo giai đoạn. Trạng thái: **Loại**.

### P4-10 — Minervini và Livermore: tốt 2017–2022, yếu 2023–2026
- Script: `v4mm.py`. Minervini M1 = Trend Template 8 điều kiện + phá đỉnh 20 phiên với KL ≥ 1,5× TB 50; M2 = M1 + nền co hẹp (biên 20 phiên ≤ 15%, ATR10/ATR50 ≤ 0,75, KL 10 < KL 50). Livermore L1 = phá đỉnh 1 năm sau 30 phiên biên ≤ 20%, KL lớn. Chưa mô phỏng nhồi lệnh.
- TB lệnh trong / ngoài mẫu: M1 +1,6 … +6,8% / −1,8 … −0,0%; **M2 +0,9 … +14% / −0,0 … +8,1%**; L1 +1,6 … +8,2% / −1,6 … +0,9%.
- Danh mục tốt nhất từng họ: M1 thị trường tăng, cắt 10%/thủng đáy 20 phiên +230% sụt −49%; M2 cùng thoát +173% sụt −37%; L1 cắt 7%/thủng MA50 +239% sụt −44%, nhưng ngoài mẫu chỉ +21%.
- **Chốt lời +20% (Minervini "bán vào sức mạnh") là kiểu thoát tệ nhất ở mọi họ.**
- Trạng thái: **Ghi nhận**.

### P4-11 — Lõi ít biến động + vệ tinh VCP / Livermore: không cải thiện ổn định
- Script: `v4ghep2.py`, `v4vcp.py`, `v4vcp2.py`.
- VCP xét từng lệnh: lưới 96 ô (nền 12–25%, ATR 0,65–0,85, KL cạn có/không, đỉnh 20/30, Trend Template đầy đủ / nới) — **96/96 ô dương trong mẫu, ngoài mẫu, và khi bỏ 2021**. TB ngoài mẫu trung vị +4,6% (Trend Template đầy đủ +5,9%, ~44 lệnh; nới +3,4%, ~100 lệnh). Chỉ ~1/3 lệnh thắng, lệnh trung vị −6 … −10%; lãi đến từ vài siêu cổ phiếu (VOS +356% 2021, MCH +165% 2024, VIX +153% 2025).
- Danh mục: lõi riêng +217% / −32%. Lõi + 1/2/3 chỗ VCP gốc +211% / +193% / +347%. Bỏ năm 2021: lõi +133%, lõi + 3 VCP +130%. Bỏ 2021 + 2025: lõi +80%, lõi + 3 VCP +49%.
- VCP nới (30–89 tín hiệu/năm): +1 chỗ ≈ lõi; +2–3 chỗ kém hơn (+101 … +184%), bỏ 2021 + 2025 chỉ +6 … +87%.
- Lõi + 2 Livermore: +235%, sụt −31%, lãi/sụt 0,42, nhưng ngoài mẫu +21%.
- Ý nghĩa: VCP có kỳ vọng dương thật ở cấp lệnh, nhưng với 5 chỗ thì phương sai quá lớn, không làm lõi tốt hơn đáng tin. Trạng thái: **Ghi nhận** — không đưa vào V4 ở cấu hình 5 chỗ.

### P4-12 — So V3 cùng bộ đo: V3 khớp 2023–2026, lỗ 2017–2022
- Ngày: 16/11/2026. Script: `v3_lam_sach.py`, `v3so.py` — chuỗi luật production của sổ V3 (`kb4size.py`, P1++ realtime + PH-21, KB1/KB2/N1 ×1,5, 5 chỗ, 40 triệu, lô 100) đổi ngày bắt đầu 2023 → 2017, dữ liệu đã làm sạch như P4-01.

| | Tổng 2017–26 | Lãi/năm | Sụt | 2017–22 | 2023–26 | Sụt 2023–26 |
|---|---|---|---|---|---|---|
| V3, phí 0,4% | +174% | 10,9% | −70% | −34% | +306% | −18% |
| V3, phí 0,6% | +97% | 7,2% | −70% | −42% | +234% | −20% |
| Giỏ ít biến động (P4-08), phí 0,6% | +217% | 12,5% | −32% | +95% | +62% | −17% |

- V3 theo năm (phí 0,6%): 2017 −29, 2018 −5, 2019 −2, 2020 −15, 2021 +55, 2022 −35, 2023 +5, 2024 +10, 2025 +58, 2026 +76 (%). ~60 lệnh/năm.
- Ý nghĩa: luật V3 được chọn trên dữ liệu 2023–2026 và rất mạnh ở đó; ở 2017–2022 (chưa từng thấy) thua chỉ số. Chưa tách được: thị trường đổi tính chất hay luật khớp quá sát.
- Giới hạn: giỏ ít biến động dùng tỷ trọng lẻ, V3 làm tròn lô; vũ trụ 2017–2020 hẹp.
- Trạng thái: **Ghi nhận** — đã tách lý do ở P4-13 (cùng ngày).

### P4-13 — V3 lỗ 2017–2022: lỗ rải nhiều năm, nhiều kịch bản; bỏ pha xấu chỉ vớt 17 điểm
- Ngày: 16/11/2026. Script: `v3vi_sao.py` (mới, log `v3vi_sao.log`) — chạy trong thư mục V3 đã chép (`kb4size.py` + chuỗi script sổ V3, mọi `'2023-01-01'` đã đổi thành `'2017-01-01'`), đọc thêm nhãn giai đoạn `PHA` của `v4core.py` (`D:\sr-research\v4`). Phí 0,6% như P4-12. Đóng góp = cột `mat` của `run_kb4` (cost × lãi / giá trị danh mục hôm trước), cộng dồn ra điểm % của danh mục.
- Nền tái lập trước khi đo, lệch **0 điểm** so P4-08 / P4-12 (log `v3so_repro.log`, `ghep2_repro.log`): V3 phí 0,6% **+97% / −70,1% / −42% / +234%**; V3 phí 0,4% +174% / −70,4% / −34% / +306%; giỏ ít biến động +217% / −31,9% / +95% / +62%.
- Lệnh vào được 600, tín hiệu bị bỏ vì không đủ lô 151, lệnh thiếu nhãn giai đoạn 0. Tổng đóng góp +109,7 điểm trên danh mục +97%.

**Kịch bản × năm — đóng góp vào danh mục (điểm %)**

| Kịch bản | 2017 | 2018 | 2019 | 2020 | 2021 | 2022 | 2023 | 2024 | 2025 | 2026 | Cả kỳ |
|---|---|---|---|---|---|---|---|---|---|---|---|
| KB1 | — | +13,4 | +1,5 | +4,3 | — | −9,7 | +2,2 | +8,6 | +6,5 | +2,1 | +28,9 |
| KB2 | — | — | — | +1,4 | — | +5,4 | +8,0 | +0,1 | +2,4 | +8,3 | +25,6 |
| KB4t | — | −0,9 | +0,3 | −2,3 | +8,2 | −21,1 | −7,6 | +2,5 | +19,3 | +11,4 | +9,8 |
| N1t | −26,8 | −12,9 | +4,5 | −24,7 | −1,1 | +1,4 | +3,6 | −1,8 | +6,5 | +1,3 | −50,2 |
| N3t | −0,8 | −6,0 | −4,9 | +11,9 | +43,3 | −5,7 | +2,0 | +0,7 | +16,7 | +38,5 | +95,5 |
| **Tổng** | **−27,6** | **−6,4** | **+1,4** | **−9,4** | **+50,4** | **−29,7** | **+8,3** | **+10,1** | **+51,3** | **+61,5** | **+109,7** |

**Kịch bản — tổng hợp (thắng = lãi ròng > 0,05%)**

| Kịch bản | Lệnh | Thắng | Lãi TB/lệnh | Góp 2017–22 | Góp 2023–26 | Góp cả kỳ |
|---|---|---|---|---|---|---|
| KB1 | 60 | 73% | +1,90% | +9,6 | +19,4 | +28,9 |
| KB2 | 37 | 65% | +1,65% | +6,8 | +18,8 | +25,6 |
| KB4t | 175 | 62% | +0,42% | −15,8 | +25,6 | +9,8 |
| N1t | 62 | 65% | −2,41% | −59,7 | +9,5 | −50,2 |
| N3t | 266 | 55% | +1,57% | +37,7 | +57,8 | +95,5 |
| **Tổng** | **600** | **60%** | **+0,86%** | **−21,4** | **+131,1** | **+109,7** |

**Giai đoạn thị trường tại ngày tín hiệu (nhãn `PHA` của bộ đo V4, chỉ đọc)**

| Giai đoạn | 2017–22: lệnh | thắng | TB/lệnh | góp | 2023–26: lệnh | thắng | TB/lệnh | góp |
|---|---|---|---|---|---|---|---|---|
| Hoảng loạn | 111 | 59% | −1,05% | −5,2 | 53 | 70% | +3,81% | +40,5 |
| Tăng mạnh | 101 | 59% | +1,65% | +31,3 | 62 | 71% | +4,20% | +56,5 |
| Hồi phục | 40 | 32% | −3,48% | −35,2 | 26 | 54% | −0,65% | −3,3 |
| Điều chỉnh | 33 | 67% | +1,23% | +4,3 | 46 | 72% | +2,31% | +20,3 |
| Giảm giá | 66 | 52% | −0,88% | −16,6 | 62 | 66% | +1,18% | +17,1 |
| **Tổng** | **351** | | | **−21,4** | **249** | | | **+131,1** |

| Kịch bản | 2017–22: lệnh / TB / thắng | 2023–26: lệnh / TB / thắng |
|---|---|---|
| KB1 | 44 / +0,70% / 68% | 16 / +5,21% / 88% |
| KB2 | 16 / −1,93% / 50% | 21 / +4,37% / 76% |
| KB4t | 67 / −1,14% / 57% | 108 / +1,39% / 65% |
| N1t | 49 / −3,85% / 57% | 13 / +3,03% / 92% |
| N3t | 175 / +0,90% / 51% | 91 / +2,86% / 63% |

**Phép thử chẩn đoán — bỏ tín hiệu theo pha (không phải đề xuất luật)**

| Bộ lọc | Tổng | Sụt | 2017–22 | 2023–26 | Lệnh | Thắng | TB/lệnh |
|---|---|---|---|---|---|---|---|
| V3 gốc | +97% | −70,1% | −42% | +234% | 600 | 60% | +0,86% |
| Bỏ khi Giảm giá | +199% | −69,9% | −20% | +261% | 503 | 62% | +1,43% |
| Bỏ khi Hoảng loạn | +27% | −66,7% | −47% | +140% | 476 | 59% | +0,70% |
| Bỏ cả hai | +42% | −62,6% | −25% | +90% | 324 | 58% | +1,10% |

- **Tần suất tín hiệu (thô / lệnh vào được):** KB1 0/0 năm 2017 và 2021, dồn 61/21 năm 2018 và 72/14 năm 2022. KB2 0 tín hiệu 2017–2019 và 2021, dồn 99/15 năm 2022. KB4t 1/0 năm 2017, 157/29 năm 2022. N1t 5/5 năm 2017 và 111/8 năm 2022. N3t đều 33–59 tín hiệu/năm. Tức 2017–22 không phải "quá ít tín hiệu" — có năm dày hơn 2023–26 (2022), chỉ là tín hiệu hỏng.
- **Kết luận (c): lẫn cả hai, nhưng (b) là chính.**
  - (b) bằng số: 4/5 kịch bản lãi TB/lệnh âm ở 2017–22 (KB2 −1,93%, KB4t −1,14%, N1t −3,85%; KB1 +0,70% và N3t +0,90% gần 0) và **cả 5 dương ở 2023–26**. Năm 2017 — năm thị trường tăng, không phải đợt sập — là năm lỗ nặng thứ hai (−27,6 điểm, 46 lệnh) vì 5 lệnh N1t lãi TB −17,4%.
  - (a) bằng số: 2018 + 2020 + 2022 góp −45,5 điểm trong tổng −21,4 (phần còn lại +24,2). Nhưng pha Hoảng loạn trong mẫu chỉ −5,2 điểm; lỗ nằm ở pha Hồi phục (−35,2 điểm, 40 lệnh, thắng 32%) — tức mua sau sập, không phải lúc sập.
  - Bỏ tín hiệu Giảm giá + Hoảng loạn vớt 17 điểm (−42% → −25%), vẫn cách giỏ ít biến động (+95% trong mẫu) 120 điểm; sụt vẫn −62,6% so −32%. **Suy luận của AI:** lọc pha không cứu được V3; phần lớn khoảng cách còn lại là luật tự thân không hợp 2017–22, không phải do chọn sai pha.
  - Cách đọc: 2017–22 là **ngoài mẫu thật của V3** (V3 chọn luật trên 2023–26). Nên (b) ở đây nghĩa là "luật chỉ mô tả đúng tính chất 2023–26", không phải overfit trong mẫu.
- Tin cậy: **cao** cho bảng đóng góp theo kịch bản và năm (cùng engine, tái lập đúng `v3so.py`, số lệnh mỗi năm trùng khít). **Trung bình** cho bảng giai đoạn — nhãn `PHA` của bộ đo V4 gán sang V3 chỉ để so, và ô nhỏ nhất chỉ 1 lệnh (vài ô kịch bản × pha).
- Trạng thái: **Ghi nhận** — không đề xuất luật. Chưa đo: lỗ do cỡ vị thế ×1,5 của N1t riêng ra bao nhiêu (N1t chiếm −59,7 / −21,4 điểm của cả giai đoạn 2017–22).

### P4-14 — Giỏ ít biến động với lô 100 và mua ATO: giữ kết quả, mất ~1 điểm lãi/năm
- Ngày: 16/11/2026. Script: `v4lo.py` (mới, log `v4lo.log`), **không sửa** `v4ghep2.py`. Lõi như P4-08: top 100 thanh khoản, giá > MA200 và MA50 > MA200, 5 mã biến động 120 phiên thấp nhất, cân lại 21 phiên (118 lần). Phí + trượt 0,3% mỗi chiều. Vị thế giữa hai lần cân lại định giá theo chuỗi `ADJ`; giá thô `C` chỉ dùng để tính số cổ.
- Kiểm engine trước khi đo: nhánh tỷ trọng lẻ + ATC ra **+216% / −31,9% / +94% / +62%**, lệch 1 điểm so mốc +217% / −31,9% / +95% / +62% → đạt.
- Lô 100: số cổ cho **cả vị thế** = làm tròn tới 100 gần nhất của (giá trị chỗ ÷ giá), ít nhất 1 lô, không vượt tiền mặt; giao dịch chỉ là phần chênh với số cổ đang giữ. Bán hết mã rời giỏ **trước** rồi mới mua (làm ngược thì danh mục kẹt tiền mặt: bản nháp đầu ra +125% thay vì +217%).

| Kiểu khớp | Vốn | Tổng | Lãi/năm | Sụt | Lãi/sụt | 2017–22 | 2023–26 | Sụt 2023–26 |
|---|---|---|---|---|---|---|---|---|
| ATC cùng phiên | tỷ trọng lẻ | +216% | 12,5% | −31,9% | 0,39 | +94% | +62% | −17,2% |
| ATC cùng phiên | lô 100, 40 triệu | +201% | 11,9% | −31,7% | 0,38 | +88% | +60% | −17,1% |
| ATC cùng phiên | lô 100, 100 triệu | +215% | 12,5% | −32,1% | 0,39 | +93% | +62% | −17,4% |
| ATO phiên sau | tỷ trọng lẻ | +191% | 11,6% | −32,3% | 0,36 | +91% | +52% | −21,7% |
| ATO phiên sau | lô 100, 40 triệu | +176% | 11,0% | −32,3% | 0,34 | +80% | +52% | −21,4% |
| ATO phiên sau | lô 100, 100 triệu | +191% | 11,6% | −32,6% | 0,35 | +92% | +51% | −21,7% |

| Theo năm (%) | ATC lẻ | ATC 40tr | ATC 100tr | ATO lẻ | ATO 40tr | ATO 100tr |
|---|---|---|---|---|---|---|
| 2017 | +53 | +47 | +52 | +52 | +46 | +51 |
| 2018 | +6 | +6 | +7 | +6 | +6 | +7 |
| 2019 | +12 | +12 | +12 | +12 | +12 | +13 |
| 2020 | +10 | +11 | +10 | +10 | +10 | +10 |
| 2021 | +35 | +34 | +34 | +34 | +33 | +33 |
| 2022 | −27 | −27 | −27 | −28 | −28 | −27 |
| 2023 | +8 | +8 | +8 | +8 | +11 | +8 |
| 2024 | +16 | +16 | +16 | +17 | +16 | +16 |
| 2025 | +30 | +29 | +30 | +29 | +28 | +29 |
| 2026 | −1 | −2 | −1 | −7 | −8 | −7 |

| Bộ đếm | ATC lẻ | ATC 40tr | ATC 100tr | ATO lẻ | ATO 40tr | ATO 100tr |
|---|---|---|---|---|---|---|
| Tiền mặt nhàn rỗi TB | 2,4% | 4,4% | 3,3% | 2,5% | 4,6% | 3,3% |
| Lần cân lại bỏ ≥ 1 mã vì không đủ 1 lô | 0/118 | 0/118 | 0/118 | 0/118 | 0/118 | 0/118 |
| Lần mua vượt quá 2% giá trị chỗ (do luật "ít nhất 1 lô") | 0 | 203 | 164 | 0 | 210 | 171 |
| Mã không có giá mở cửa phiên t+1 (không mua) | — | — | — | 0 | 0 | 0 |
| Chênh mở cửa t+1 so đóng cửa t: mua / bán | — | — | — | +0,052% / −0,075% | +0,077% / −0,064% | +0,075% / −0,039% |

- **Lô 100 ở vốn 40 triệu:** −16 điểm tổng so mốc P4-08 (−15 điểm so bản tỷ trọng lẻ cùng kiểu khớp), lãi/năm 11,9% so 12,5%, **sụt không đổi** (−31,7% so −31,9%). Chi phí làm tròn lô ≈ 0,6 điểm lãi/năm. Ở vốn 100 triệu gần như không mất (−1 điểm) vì mỗi lần mua rơi vào chỗ 20 triệu, một lô chỉ còn chiếm 1/2,5 tỷ lệ so vốn 40 triệu.
- **Tiền nhàn rỗi** 4,4% (40 triệu) và 3,3% (100 triệu) so 2,4% (lẻ): phần lớn là tiền thừa do làm tròn, lãi 0%. Đây là nguồn mất lãi chính, không phải phí.
- **Không lần nào bỏ mã vì không đủ 1 lô** (0/118): vũ trụ yêu cầu giá ≥ 3 nghìn đồng, 1 lô ≥ 300 nghìn = 3,75% chỗ 8 triệu. Bẫy "không đủ 1 lô" chỉ xuất hiện nếu hạ vốn dưới ~5 triệu hoặc cho phép giá dưới 3 nghìn.
- **ATO so ATC:** 2017–2025 gần trùng (lệch ≤ 1 điểm/năm), toàn bộ chênh 25 điểm nằm ở 2026 (−7% so −1%). Chênh giá khớp đo được mỗi chiều chỉ +0,05 … +0,08% khi mua và −0,04 … −0,08% khi bán. **Suy luận của AI:** ATO không phải chi phí hệ thống lớn; chênh tổng chủ yếu do năm cuối ngắn và vài lệnh lệch. Muốn chắc phải đo ở khung khác.
- ATO làm đường vốn gập ghềnh hơn ngoài mẫu: sụt 2023–26 −21,7% so −17,2% dù tổng gần bằng.
- **Kết luận:** giỏ ít biến động **vẫn giữ** khi đặt lệnh thật — 40 triệu, 5 chỗ, lô 100, mua ATO phiên sau: **+176%, sụt −32,3%, lãi/sụt 0,34, cả hai giai đoạn dương (+80% / +52%)**, vẫn cách xa V3 (+97% / −70,1%). Chưa chốt phương án V4.
- Giới hạn: không bán lô lẻ (số cổ đang giữ làm tròn về lô 100 gần nhất, hiếm khi lệch do chia tách); chuỗi `ADJ` coi cổ tức tiền mặt là tái đầu tư; lệnh treo ở phiên cuối không khớp; chưa mô phỏng trần/sàn khi khớp ATO.
- Tin cậy: **cao** cho chi phí lô 100 (đơn điệu theo vốn, sụt không đổi, tái lập được). **Trung bình** cho chênh ATO (dồn vào 2026). Trạng thái: **Ghi nhận**.

### P4-15 — Cổ tăng sốc, cách 1: giữ mã lõi đang lãi lớn thay vì bán khi cân lại — giúp nhờ 2021, không bền
- Ngày: 16/11/2026. Script: `v4soc.py` (bộ đo: tỷ trọng lẻ, quyết + khớp ATC, định giá `ADJ`, phí 0,6%). Lý do: owner (Q4-2) muốn tránh chốt lời quá non — giỏ ít biến động bán mã đang tăng sốc vì biến động của nó tăng lên.
- Luật: khi cân lại, mã lõi sắp rời giỏ mà đã lãi ≥ X% từ giá vào thì giữ, thoát khi trượt Y% từ đỉnh (hoặc đóng cửa < MA50); vẫn chiếm 1 trong 5 chỗ.

  | Biến thể | Tổng | Sụt | Lãi/sụt | 2017–22 | 2023–26 | Bỏ 2021 | Bỏ 2021+2025 |
  |---|---|---|---|---|---|---|---|
  | Lõi riêng (mốc) | +217% | −31,9% | 0,39 | +95% | +62% | +133% | +80% |
  | X 10%, trượt 25% hoặc MA50 | +316% | −34,3% | 0,46 | +202% | +37% | +127% | +88% |
  | X 30%, trượt 25% hoặc MA50 | +311% | −31,9% | 0,49 | +172% | +51% | +112% | +74% |
  | X 30%, trượt 15% | +277% | −33,9% | 0,43 | +140% | +56% | +90% | +55% |
  | X 20%, trượt 25% | +216% | −36,1% | 0,35 | +90% | +65% | +54% | +27% |
  | X 10%, trượt 15% | +212% | −38,5% | 0,32 | +120% | +41% | +71% | +43% |

- Ý nghĩa: các bản tốt nhất hơn lõi nhờ 2021 (+82 … +109% so với +35%); ngoài mẫu và khi bỏ 2021 đều kém hoặc ngang lõi. Trạng thái: **Ghi nhận** — không đủ bằng chứng để dùng.

### P4-16 — Cổ tăng sốc, cách 2: vệ tinh 2 chỗ mua cổ tăng sốc, để lãi chạy — vượt trội cả khi đặt lệnh thật
- Ngày: 16/11/2026. Script: `v4soc.py`, `v4soc2.py` (lưới), `v4soc3.py` (điều kiện đặt lệnh thật).
- **Tín hiệu (đóng cửa phiên t):** tăng ≥ 25% trong 20 phiên, đóng cửa vượt đỉnh đóng cửa 60 phiên trước, KL ≥ 2 × TB 50 phiên, nằm trong top 100 thanh khoản. Nhiều tín hiệu cùng phiên: ưu tiên RS126 cao.
- **Thoát:** trượt 20% từ đỉnh (theo chuỗi `ADJ`, xét đóng cửa) hoặc đóng cửa < MA50. Tối đa 2 chỗ; thiếu tiền thì bán mã lõi biến động cao nhất.
- **Từng lệnh (độc lập, `mo_phong`, mua ATC, trượt 20% hoặc MA50):** 457 lệnh, thắng 40%, TB +5,63%, trung vị −4,84%, giữ TB 41 phiên. Bỏ 10 lệnh lãi nhất: TB +1,09%. Lệnh lớn nhất: CEO +501% (2021), VIC +447% (2025), PDR +214% (2020), HAR +193% (2017), VGI +150% (2024). Năm 2018 (−6,3%/lệnh) và 2019 (−2,3%) âm.
- **Lưới độ bền (bộ đo ATC, tỷ trọng lẻ; lõi riêng +217%):**
  - Ngưỡng tăng 15/20/25/30/40% × cửa sổ 10/20/40 phiên: **11/15 ô hơn lõi** (tổng +83 … +907%; sụt −33 … −54%). Ô yếu: 30%/10p +113%, 40%/10p +83%, 20%/40p +186%, 30%/40p +184%.
  - KL ×1 / ×1,5 / ×3: +577% / +691% / +306%.
  - Thoát trượt 10 / 15 / 25 / 30%, có hoặc không MA50: +462 … +754%, sụt −30 … −40%.
  - Số chỗ 1–5: +266% / +654% / +733% / +890% / +983%, sụt −40% / −35% / −43% / −43% / −48% — **2 chỗ cho lãi/sụt cao nhất (0,66)**.
- **Điều kiện đặt lệnh thật (`v4soc3.py`):** khớp ATO phiên sau; bỏ mua nếu mở cửa sát trần; lô 100, vốn 40 triệu. Mọi dòng cùng bộ đo:

  | Phương án | Tổng | Sụt | Lãi/sụt | 2017–22 | 2023–26 | Bỏ 2021+2025 |
  |---|---|---|---|---|---|---|
  | Lõi riêng, ATO, lô 100 | +156% | −32,1% | 0,31 | +70% | +49% | +52% |
  | Lõi + 2 tăng sốc, ATC, tỷ trọng lẻ | +694% | −34,5% | 0,68 | +190% | +170% | +170% |
  | Như trên, ATO, không chặn trần | +507% | −36,7% | 0,55 | +156% | +133% | +122% |
  | Như trên, ATO, chặn trần (4 lệnh bị chặn) | +572% | −36,7% | 0,59 | +183% | +133% | +146% |
  | **Lõi + 2 tăng sốc, ATO, lô 100, 40 triệu** | **+580%** | **−38,7%** | **0,56** | +178% | +141% | **+153%** |
  | Như trên, tăng ≥ 20% / 20p | +539% | −36,1% | 0,58 | +194% | +114% | +89% |
  | Như trên, tăng ≥ 30% / 20p | +545% | −40,6% | 0,52 | +175% | +130% | +92% |
  | Như trên, tăng ≥ 15% / 40p | +385% | −40,7% | 0,43 | +158% | +85% | +65% |
  | 1 chỗ vệ tinh | +240% | −40,4% | 0,33 | +107% | +62% | +92% |
  | 3 chỗ vệ tinh | +351% | −47,0% | 0,35 | +126% | +96% | +61% |

- Lõi riêng trong `v4soc3.py` thấp hơn `v4lo.py` (+156% so với +176%) vì bộ đo này không bơm thêm tiền vào mã lõi đang giữ khi cân lại; so sánh trong bảng vẫn cùng bộ đo.
- Khác P4-03 (họ "phiên bùng nổ", âm ngoài mẫu) và P4-11 (vệ tinh VCP không bền): tín hiệu đo đà 20 phiên chứ không một phiên, chỉ top 100 thanh khoản, thoát để lãi chạy (trượt 20% / MA50) thay vì cắt sớm. **Suy luận của AI**, chưa tách riêng từng yếu tố.
- Giới hạn: **thiếu mã đã huỷ niêm yết** — rủi ro lớn nhất với cổ tăng sốc (tăng rồi sập rồi huỷ không có trong dữ liệu); lãi dồn vào ít siêu cổ phiếu (40% lệnh thắng); khối lượng khớp ATO không kiểm; thoát xét theo đóng cửa và bán theo bộ đo đơn giản (`v4soc3.py` bán ATO phiên sau).
- Tin cậy: **trung bình–cao** cho "vệ tinh tăng sốc 2 chỗ làm danh mục tốt hơn lõi riêng" (bền qua lưới, qua ATO + lô 100, qua bỏ 2021+2025); **thấp** cho mức lãi tuyệt đối (+580%). Trạng thái: **Ứng viên kịch bản cổ tăng sốc cho V4** — chờ owner.

### P4-17 — Ghép đủ hệ V4: lõi ít biến động + 2 chỗ vệ tinh tăng sốc + lớp tiền mặt — chốt lớp tiền mặt chỉ lõi, chỉ Giảm giá (D4)
- Ngày: 16/11/2026. Script: `v4he.py` (mới, log `he.log`). **Không sửa** `v4soc3.py`, `v4core.py`, `v4soc.py` — ba script đó là nguồn số của P4-14 và P4-16.
- **Vì sao đo:** owner đã chốt ba thành phần của V4 (lõi P4-08, vệ tinh 2 chỗ P4-16, lớp tiền mặt) nhưng ba thành phần **chưa từng chạy chung**. Đây là lần đầu.
- **Cách đo:** `v4he.py` nạp phần đầu `v4soc3.py` rồi định nghĩa `chay_he()` = `chay()` + `ra_tien` (`None` / `'loi'` / `'ca'`) + `pha_ra` (tập giai đoạn bật lớp). Mọi dòng: ATO phiên sau, lô 100, vốn 40 triệu, phí + trượt 0,3% mỗi chiều, 5 chỗ, cân lại 21 phiên; vệ tinh `tin(0.25, 20, 2.0)`, `S=2`, trượt 20%, thoát dưới MA50.
- **Luật lớp tiền mặt:** xét `PHA` tại **đóng cửa phiên d**. Thuộc tập bật → bán mọi mã **lõi** (khớp ATO phiên d+1), không mua lõi mới. Vệ tinh giữ luật thoát riêng và **vẫn được mua mới** (`'loi'`). `'ca'` = thêm bán mọi vệ tinh, không mua vệ tinh mới. Rời tập bật → cân lại lõi **ngay phiên đó**, không chờ đủ 21 phiên; nếu chờ, danh mục kẹt tiền mặt tới 20 phiên sau khi thị trường đã hồi — lỗi của bộ đo, không phải của luật. Quyết ở phiên d, khớp phiên d+1: không nhìn trước. Vị thế định giá theo chuỗi `ADJ`, không dùng giá thô `C`.
- **Nền tái lập, lệch 0 điểm** so P4-16: `ra_tien=None` cho lõi riêng **+156% / −32,1% / +70% / +49%** và lõi + 2 vệ tinh **+580% / −38,7% / +178% / +141%**.

**Bảng 1 — hệ và lớp tiền mặt**

| Phương án | `ra_tien` | Tổng | Lãi/năm | Sụt | Lãi/sụt | 2017–22 | 2023–26 | sụt 23–26 | Bỏ 2021 | Bỏ 2021+2025 |
|---|---|---|---|---|---|---|---|---|---|---|
| A — lõi riêng | — | +156% | 10,1% | −32,1% | 0,31 | +70% | +49% | −20,4% | +98% | +52% |
| B — lõi riêng | `'loi'`, Hoảng loạn + Giảm giá | +62% | 5,1% | −25,3% | 0,20 | +48% | +10% | −23,8% | +46% | +32% |
| C — lõi + 2 vệ tinh | — | **+580%** | 21,7% | −38,7% | **0,56** | +178% | +141% | −27,7% | +373% | +153% |
| D — lõi + 2 vệ tinh | `'loi'`, Hoảng loạn + Giảm giá | +291% | 15,0% | −34,4% | 0,44 | +117% | +79% | −31,4% | +184% | +59% |
| E — lõi + 2 vệ tinh | `'ca'`, Hoảng loạn + Giảm giá | +58% | 4,8% | **−40,1%** | 0,12 | +41% | +12% | −31,6% | +28% | +21% |

**Bảng 2 — độ nhạy: chỉ bật khi Giảm giá (`PHA == 4`)**

| Phương án | `ra_tien` | Tổng | Lãi/năm | Sụt | Lãi/sụt | 2017–22 | 2023–26 | sụt 23–26 | Bỏ 2021 | Bỏ 2021+2025 |
|---|---|---|---|---|---|---|---|---|---|---|
| **D4 — lõi + 2 vệ tinh** | `'loi'`, chỉ Giảm giá | **+453%** | **19,1%** | **−34,7%** | **0,55** | +151% | +117% | **−32,1%** | +294% | +111% |
| E4 — lõi + 2 vệ tinh | `'ca'`, chỉ Giảm giá | +130% | 8,9% | **−46,2%** | 0,19 | +23% | +85% | −28,0% | +82% | +53% |

**Bảng 3 — đếm**

| Phương án | Lệnh vệ tinh | Ra / vào lại | Phiên ngoài | Bán lõi | Bán vệ tinh | Tiền mặt TB |
|---|---|---|---|---|---|---|
| A | 0 | — | 0 | 0 | 0 | 4,7% |
| B | 0 | 59 / 58 | 790 | 284 | 0 | 35,7% |
| C | 105 | — | 0 | 0 | 0 | 7,6% |
| D | 104 | 59 / 58 | 790 | 192 | 0 | 27,8% |
| E | 105 | 59 / 58 | 790 | 218 | 55 | 37,1% |
| D4 | 104 | 56 / 55 | 468 | 193 | 0 | 18,6% |
| E4 | 105 | 56 / 55 | 468 | 227 | 37 | 25,6% |

**Bảng 4 — từng năm (% vốn đầu năm)**

| Phương án | 17 | 18 | 19 | 20 | 21 | 22 | 23 | 24 | 25 | 26 |
|---|---|---|---|---|---|---|---|---|---|---|
| A | +46 | +2 | +12 | +9 | +28 | −27 | +6 | +14 | +30 | −6 |
| B | +41 | −17 | +9 | +14 | +12 | −8 | +1 | +2 | +10 | −3 |
| C | +71 | +21 | +7 | +26 | +36 | −27 | +5 | +56 | +87 | −24 |
| D | +66 | −1 | −0 | +23 | +27 | −18 | −8 | +48 | +78 | −29 |
| E | +57 | **−30** | +4 | −3 | +23 | **+3** | −4 | +13 | +6 | −3 |
| D4 | +66 | +11 | −1 | +30 | +29 | −21 | +2 | +57 | +87 | −30 |
| E4 | +57 | **−24** | +2 | −6 | +24 | −13 | +3 | +34 | +19 | +13 |

- **Đợt sụt sâu nhất của từng bản** (đỉnh → đáy, theo chuỗi `ADJ`): A −32,1% (2022-04-06 → 2023-04-25, 384 ngày), C −38,7% (2021-11-18 → 2023-04-25, 523 ngày), D −34,4% (2021-11-18 → 2023-11-27, 739 ngày), **E −40,1% (2017-12-14 → 2020-08-12, 972 ngày)**, D4 −34,7% (2018-08-03 → 2020-03-25, 600 ngày), **E4 −46,2% (2018-04-09 → 2020-07-27, 840 ngày)**. Đợt sụt của E và E4 nằm **hoàn toàn ngoài cú sập 2022**. Đúng cú sập 2022 thì lớp tiền mặt lại làm tốt: E **+3%** so C **−27%**.
- **Độ dài các đợt pha xấu:** 59 đợt trong cửa sổ chạy, TB 15,5 phiên, **trung vị 4 phiên**; 52,5% đợt ≤ 5 phiên, 72,9% ≤ 10 phiên, chỉ 7 đợt > 40 phiên. Năm 2018 có 130 phiên pha xấu và 19 lần đổi pha. `soi8.py` — script chẩn đoán, **đã xoá**, không phải một phần của bộ đo — cho thêm: lãi TB mỗi phiên của danh mục C trên phiên pha xấu **+0,041%**, trên phiên pha tốt +0,110%; pha xấu tốt hơn pha tốt ở **6/10 năm** (2017, 2018, 2019, 2023, 2024, 2025). Số của `soi8.py` chỉ dùng để **chẩn đoán**, không phải số của hệ.
- **Ý nghĩa:** lớp tiền mặt làm nông sụt khi đúng cú sập kéo dài (2022: E +3% so C −27%) nhưng bật tắt theo nhiễu ở năm giằng co — trung vị 4 phiên một đợt là quá ngắn. Mỗi đợt ra-vào tốn ~0,6% phí hai chiều trên toàn danh mục; phần lớn khoảng cách năm 2018 (E −30% so C +21%) là **bán rẻ mua đắt**, không phải phí (**suy luận của AI**). Bỏ Hoảng loạn khỏi điều kiện bật gần như miễn phí về sụt (D4 −34,7% so D −34,4%) mà **+162 điểm lãi**; chuyển từ `'loi'` sang `'ca'` thì mất 233 điểm lãi (D 291% → E 58%) vì bán đúng cái máy tạo lãi là vệ tinh tăng sốc.
- **Giới hạn:** ngoài mẫu 2023–2026, D4 sụt **−32,1%**, sâu hơn C (−27,7%) — lớp tiền mặt chưa chứng minh được lợi thế sụt ở giai đoạn mới nhất. Tiền mặt nhàn rỗi TB 18,6% (D4). D4 bán lõi 193 lần, thiếu tiền 5 lần, bỏ 4 lệnh vì mở cửa sát trần. Toàn bộ vẫn chịu giới hạn chung: thiếu mã đã huỷ niêm yết (Q4-6), khối lượng khớp ATO không kiểm, 2026 mới 214 phiên đến 13/11.
- Tin cậy: **cao** cho số của từng biến thể (nền tái lập lệch 0 điểm, hai bản không lớp khớp P4-16 và P4-14); **trung bình** cho kết luận "D4 hơn D nhờ bỏ Hoảng loạn" vì chênh lệch sụt chỉ 0,3 điểm, phần lớn là chênh lãi; **thấp** cho mức lãi tuyệt đối. Trạng thái: **Chốt dùng D4** (owner 16/11).

### P4-18 — Mùa cuối năm và Tết âm lịch: 4 tháng 16/11 → 16/03 thường tăng; mua một lần nhóm mã gần đỉnh 1 năm rồi giữ là cách vững nhất
- Ngày: 16/11/2026. Dữ liệu như P4-01 (nến tới 13/11/2026). Phí 0,6%. Script: `v4q4.py`, `v4q4l.py`, `v3q4.py` (quý 4); `v4tet.py`, `v3tet.py` (quanh Tết); `v4t4.py`, `v4t4b.py`, `v3t4.py` (16/11 → 16/03).
- Lý do đo: owner hỏi kịch bản nào lãi nhiều nhất cuối năm để "ăn Tết"; sau đó owner nói tiền không cần rút giai đoạn này, có thể mua chờ.
- Hai cách đo: (a) danh mục chạy liên tục cả năm, cắt lãi theo cửa sổ (vị thế mang sang vẫn tính); (b) mua một lần ở ngày đầu cửa sổ các mã trong top 100 thanh khoản theo một cách chọn, giữ nguyên tới cuối cửa sổ, lãi theo chuỗi `ADJ`.
- Ngày Tết dùng: 16/02/2018, 05/02/2019, 25/01/2020, 12/02/2021, 01/02/2022, 22/01/2023, 10/02/2024, 29/01/2025, 17/02/2026.

**1. Quý 4 (01/10 → 31/12, 2017–2025, kiểu a):** quý 4 không tự tăng — chỉ số đều trọng số TB +0,6%, trung vị −2,6%, dương 4/10 năm.

| Phương án | TB | Trung vị | Năm dương | Tệ nhất |
|---|---|---|---|---|
| V4 lõi + 2 tăng sốc (ATO, lô 100) | +7,3% | +4,0% | 7/9 | −16,5% |
| Giỏ ít biến động, trong xu hướng | +3,6% | +0,3% | 8/9 | −14,6% |
| V3 | +2,9% | −0,8% | 4/9 | −16,1% |
| Chỉ số đều trọng số | +0,6% | −2,6% | 4/10 | −23,4% |

- Cấp lệnh, tín hiệu tháng 10–12: họ theo xu hướng (sát đỉnh năm, phá đỉnh) +3 … +3,5%/lệnh (tháng 1–9 ~0%) nhưng chỉ dương 3–5/9 năm. V3: N1 +5,6%/lệnh (5/6 năm), KB2 +2,1% (5/5).

**2. Quanh Tết (kiểu a, 9 mùa):**

| Cửa sổ | Chỉ số | V3 | Giỏ ít biến động (top100) | Ghi chú |
|---|---|---|---|---|
| 15/12 → phiên cuối trước Tết | +0,5% (4/9) | −0,3% (4/9) | **+6,0% (6/9)**; trong xu hướng +5,6% (7/9) | Động lượng / ngành mạnh −2 … −5% |
| 20 phiên trước Tết | **−2,0% (3/9)** | −1,8% (6/9) | +0,9 … +1,3% (5–6/9) | Thị trường hay giảm trước Tết |
| 20 phiên sau Tết | **+4,7% (6/9)** | +4,5% (6/9) | ~+1% | Động lượng +4 … +5% |

- V3 cấp lệnh, tín hiệu 15/12 → trước Tết: KB4 +5,2%/lệnh (6/6 năm), N1 +4,0% (7/9), KB1 +2,7% (4/4) — lệnh giữ sang sau Tết.

**3. 16/11 → 16/03 năm sau (qua Tết, 9 mùa; cột = năm kết thúc mùa):**

| Cách | 18 | 19 | 20 | 21 | 22 | 23 | 24 | 25 | 26 | TB | Trung vị | Dương | Tệ nhất |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **(b) 10 mã gần đỉnh 1 năm, mua 16/11** | +34 | +17 | −22 | +72 | +15 | +7 | +16 | +10 | +9 | **+17,5** | **+14,9** | **8/9** | −22 |
| (b) 20 mã gần đỉnh 1 năm | +26 | +12 | −24 | +55 | +6 | +8 | +15 | +8 | +6 | +12,3 | +8,4 | 8/9 | −24 |
| (b) 5 mã beta cao | +20 | +7 | −27 | +73 | +19 | +53 | +8 | +18 | −9 | +18,0 | +18,4 | 7/9 | −27 |
| (a) Giỏ ít biến động | +24 | +17 | −16 | +27 | −5 | +1 | +12 | +9 | +15 | +9,3 | +12,4 | 7/9 | −16 |
| (a) Chỉ số đều trọng số | +11 | +8 | −23 | +43 | +3 | +22 | +11 | +9 | −4 | +9,0 | +9,3 | 7/9 | −23 |
| (b) 10 mã ít biến động | +25 | +18 | −19 | +31 | −8 | +1 | +10 | +8 | +6 | +7,9 | +7,7 | 7/9 | −19 |
| (b) 5 mã ngẫu nhiên (TB 20 lần) | +8 | +6 | −27 | +35 | +3 | +22 | +17 | +6 | 0 | +8,0 | +6,4 | 8/9 | −27 |
| (a) V4 lõi + 2 tăng sốc | +27 | +11 | −17 | +24 | −7 | −3 | +22 | +3 | +7 | +7,3 | +6,8 | 6/9 | −17 |
| (a) V3 | −4 | 0 | −24 | −10 | +24 | +62 | +1 | −3 | +79 | +14,0 | −0,2 | 4/9 | −24 |

- Mùa 2019/20 có cú sập COVID tháng 3/2020 — mọi cách đều lỗ.
- **Độ bền (`v4t4b.py`, 5/10/20 mã × mua 01/11, 16/11, 01/12):** gần đỉnh 1 năm cả 9 tổ hợp TB +6,6 … +18,7%, trung vị +7 … +15%, dương 6–8/9; beta cao tốt khi mua tháng 11 (+11 … +24%) nhưng mua 01/12 chỉ +3 … +5%; ít biến động +4 … +9%, năm tệ nhất nhẹ hơn. Mua tháng 11 tốt hơn mua 01/12 ở cả ba cách.
- Ý nghĩa: (1) quý 4 và tháng cuối trước Tết không tự tăng; (2) 4 tháng qua Tết thường tăng (chỉ số dương 7/9 mùa); (3) với tiền không cần rút, mua một lần nhóm mã gần đỉnh 1 năm trong top 100 thanh khoản vào tháng 11 rồi giữ tới giữa tháng 3 là cách vững nhất qua các biến thể; (4) V3 thất thường trong mọi cửa sổ mùa vụ (trung vị ~0).
- Giới hạn: 9 mùa; TB bị kéo bởi mùa 2021 (+55 … +82%); rủi ro sụt lớn vẫn còn (−22 … −27% mùa 2020); thiếu mã đã huỷ niêm yết; chỉ nến ngày; danh mục (a) phụ thuộc vị thế mang sang. Kết quả backtest, không phải lời khuyên đầu tư.
- Tin cậy: **trung bình** cho "4 tháng qua Tết thường tăng" và "gần đỉnh 1 năm vững nhất trong các cách đã thử"; **thấp** cho mức lãi tuyệt đối. Trạng thái: **Ghi nhận**.

### P4-19 — Kịch bản "Mùa Tết — mã khỏe": 10 mã gần đỉnh 1 năm, mua giữa tháng 11, giữ qua Tết, cắt cả giỏ khi giảm 10%
- Ngày: 17/11/2026. Dữ liệu như P4-01 (nến tới 13/11/2026). Phí + trượt 0,6% mỗi lần đổi giỏ. Lãi theo chuỗi `ADJ`. 10 mùa 16/11 → 16/03 (2016/17 → 2025/26; cột = năm kết thúc mùa). Script: `v4mua.py` (12 kịch bản), `v4mua2.py` (độ bền + cắt lỗ), `v4mua3.py` (điểm chọn mã, số mã, ngành, độ dài mùa), `v4mua4.py` (thêm cổ tăng sốc), `hientai.py` (trạng thái thị trường hiện tại).
- Lý do đo: owner thấy V3 không hợp giai đoạn này, yêu cầu tìm / sáng tạo kịch bản hiệu quả cao nhất cho mùa cuối năm qua Tết, tiền không cần rút.
- **Trạng thái thị trường lúc đo (13/11/2026, `hientai.py`):** giai đoạn Giảm giá 10 phiên liên tiếp (71% số phiên năm 2026); chỉ số đều trọng số −12,9% từ đầu năm, −17,2% so với đỉnh 1 năm; dưới MA50 −2,4%, dưới MA200 −8,1%, MA50 dưới MA200 −5,8%; độ rộng 0,35. Lịch sử từ giai đoạn Giảm giá trong tháng 11–1: lãi chỉ số 20 / 60 / 80 phiên sau +0,7% (46% đi lên) / +0,6% (63%) / +2,0% (68%); mọi tháng: −0,9% / −1,0% / −0,7% (36–46%). Từ Hồi phục: 60–80 phiên sau +3,1 … +4,7% (66–70%). Từ Hoảng loạn trong tháng 11–1: +6,4 … +7,6% (76–84%, ~67 phiên).

**1. 12 kịch bản (`v4mua.py`, không cắt lỗ):**

| Kịch bản | TB | Trung vị | Mùa có lãi | Tệ nhất |
|---|---|---|---|---|
| **A. 10 mã gần đỉnh 1 năm, mua giữ** | **+17,3%** | +15,3% | **9/10** | −22,2% |
| B. A + RS63 top 30% + trên MA200 | +16,8% | +17,8% | 8/10 | −26,3% |
| F. A, làm mới mỗi 21 phiên | +14,6% | +14,3% | 9/10 | −26,1% |
| G. Xoay: gần đỉnh → ít biến động (15/12) → beta cao (ngày Tết) | +12,0% | +15,1% | 8/10 | −27,3% |
| D. 10 mã beta cao | +12,3% | +12,1% | 8/10 | −36,6% |
| H. A, nghỉ 20 phiên trước Tết rồi mua lại | +11,6% | +8,4% | 8/10 | −25,3% |
| E. 5 gần đỉnh + 5 ít biến động | +11,4% | +7,6% | 8/10 | −18,8% |
| I. 50% mua 16/11, 50% chờ Hoảng loạn hoặc Tết | +11,0% | +10,1% | 9/10 | −22,6% |
| J. 10 mã động lượng 6-1 | +7,5% | +8,8% | 7/10 | −36,0% |
| C. 10 mã ít biến động | +6,6% | +6,7% | 7/10 | −18,9% |
| Mốc: 10 mã ngẫu nhiên top 100 (20 lần) / chỉ số đều trọng số | +9,7% / +9,1% | +10,4% / +9,8% | 8/10 | −26,5% / −22,7% |

**2. Độ bền của A (`v4mua2.py`):** 5/10/15/20/30 mã × mua 01/11, 16/11, 01/12 — TB +6,2 … +17,4%, 6–9/10 mùa có lãi. Mua 16/11 cao nhất ở mọi số mã; mua 01/11 hoặc 01/12 với 10 mã chỉ +10,7 … +10,8%.

**3. Lớp cắt lỗ (A, 10 mã, 16/11):** không cắt +17,3% / tệ nhất −22,2%; **cắt cả giỏ khi giá trị ≤ 90% vốn: +18,4% / −11,8%** (chỉ bật ở mùa 2019/20 COVID); cắt từng mã −10%: +16,6%, trung vị tụt còn +10,0%.

**4. Biến thể tìm lãi cao hơn (`v4mua3.py`, đều cắt cả giỏ 10%):**

| Biến thể | TB | Trung vị | Mùa có lãi | Tệ nhất |
|---|---|---|---|---|
| **10 mã, tối đa 2 mã/ngành** | **+18,7 … +19,0%** | **+16,3 … +16,6%** | 9/10 | −10,6 … −10,8% |
| 7 mã | +19,9% | +14,9% | 9/10 | −10,8% |
| Giữ tới 31/03 | +20,7% | +15,9% | 9/10 | −11,8% |
| Giữ tới 15/04 / 15/05 | +17,4% / +17,6% | +7,4% / +12,0% | 8/10 | −11,8% / −14,0% |
| **Bán 16/02 (ngay sau Tết)** | +12,9 … +13,3% | +8,5 … +9,9% | **10/10** | **+3,7 … +4,2%** |
| 3 mã | +20,2% | +7,1% | 6/10 | −12,6% |
| Điểm kết hợp gần đỉnh với ít biến động / RS126 / beta / chia biến động | +5,3 … +16,2% | | 6–8/10 | |
| Tối đa 1 / 3 mã/ngành | +15,9% / +18,8% | | 9/10 | −14,1% |

**5. Thêm cổ tăng sốc trong mùa (`v4mua4.py`, tín hiệu P4-16, mỗi chỗ vệ tinh là một túi tiền riêng):** 8 gần đỉnh + 2 tăng sốc +16,5% (16/03) / +11,1% (16/02); 7 + 3 hoặc 5 + 2 đều thấp hơn không thêm. Bản nháp đầu ra +35 … +44% do **lỗi tạo vốn** (mỗi lệnh mới được cấp tiền mới không lấy từ lệnh trước); đã sửa trước khi ghi.

- Ý nghĩa: (1) đơn giản nhất thắng — mua một lần 10 mã gần đỉnh 1 năm, giữ qua Tết; các cách xoay giỏ, canh Tết, chờ hoảng loạn, thêm tăng sốc đều không hơn; (2) cắt cả giỏ ở −10% gần như miễn phí trong 10 mùa và giảm mùa tệ nhất từ −22% xuống −11%; (3) giới hạn 2 mã/ngành nhích nhẹ lãi và giảm tệ nhất; (4) bán ngay sau Tết đổi lãi lấy sự chắc chắn (10/10 mùa dương); (5) không có biến thể vững nào đạt +20%; các bản vượt +20% đến từ dồn ít mã (trung vị thấp) hoặc nhiễu ngày bán.
- Kịch bản đề xuất (của AI, chờ owner): **10 mã gần đỉnh 1 năm trong top 100 thanh khoản, tối đa 2 mã/ngành, mua giữa tháng 11, giữ tới giữa tháng 3, bán cả giỏ nếu giá trị ≤ 90% vốn**; bản an toàn: bán giữa tháng 2.
- Giới hạn: 10 mùa; ngày mua 16/11 là ngày được chọn để đo (lệch ngày thường thấp hơn vài điểm); mùa 2021 kéo TB lên (bỏ 2021 còn +11 … +13%); mùa 2026/27 bắt đầu ở Giảm giá — chỉ 3 mùa tương tự (2018/19 +17%, 2019/20 −22% không cắt lỗ, 2024/25 +10%); thiếu mã đã huỷ niêm yết; chỉ nến ngày; không làm tròn lô. Kỳ vọng thực tế **+10 … +15%**. Kết quả backtest, không phải lời khuyên đầu tư.
- Tin cậy: **trung bình** cho "mua giữ mã gần đỉnh 1 năm qua mùa Tết hơn mốc và hơn các cách phức tạp"; **thấp** cho mức lãi tuyệt đối. Trạng thái: **Ghi nhận** — chưa đưa vào app.
