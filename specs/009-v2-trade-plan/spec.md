# Feature Specification: V2 — Kế hoạch giao dịch đầy đủ trước khi mua + chuẩn hoá cò kích hoạt

**Feature Branch**: `009-v2-trade-plan`

**Created**: 2026-10-14

**Status**: Draft (mục 0 đã chốt 2026-10-14; phần 2 `v2-pha2-trigger-fix` đã chốt 2026-10-09)

**Input**: User description: "chạy speckit-specify" cho `docs/features/v2-trade-plan/spec.md` cùng phần 2 của `docs/features/v2-pha2-trigger-fix/spec.md`.

## Bối cảnh

Chủ sản phẩm đặt nguyên tắc (2026-10-09): **trước khi mua phải có hết kịch bản mua và bán**. Lệnh vào chỉ kích hoạt khi giá nằm trong vùng vào của kịch bản. Lệnh ra chỉ kích hoạt khi chạm mục tiêu hoặc quá điểm chịu đựng đã định sẵn. Không tính lại kế hoạch sau khi đã thấy giá.

Luồng bán V2 hiện tại (phương án A, B, C) được làm từng mảnh và lệch khỏi nguyên tắc này: cò mua không kiểm vùng vào (sự cố DCS 09/10 mua ở 700 với kế hoạch tính cho 610), khoảng cách dừng lỗ đuổi theo tính lại theo ATR mỗi phiên, Kiệt sức / Gãy nền bán theo chỉ báo phát sinh sau khi mua, quy tắc dời dừng lỗ và dừng theo thời gian không ghi trong kế hoạch.

Ngoài ra cò Nổ hướng lên không bao giờ kích hoạt được (đỉnh hộp gồm luôn phiên đang chạy) và khối lượng cộng dồn trong phiên bị so với trung bình cả ngày.

Tài liệu chi tiết: [`docs/features/v2-trade-plan/spec.md`](../../docs/features/v2-trade-plan/spec.md), [`docs/features/v2-pha2-trigger-fix/spec.md`](../../docs/features/v2-pha2-trigger-fix/spec.md) mục 2, 2b.

**Ngoài phạm vi:** phần 1 `v2-pha2-trigger-fix` (luồng bán chạy mọi lượt, đã ship 2026-10-09); V1 (Top, VIP); điều kiện bối cảnh / hình thái Pha 1; công thức đặt mức của từng kịch bản (giữ nguyên).

## Quyết định đã chốt

| # | Quyết định | Ai / khi nào |
|---|---|---|
| Q1 | Kiệt sức / Gãy nền chỉ cảnh báo, không phải lệnh bán | Chủ sản phẩm, 2026-10-14 |
| Q2 | Khoảng cách dừng lỗ đuổi theo = k × ATR tại lúc lập kế hoạch, không tính lại | như trên |
| Q3 | Bán 50% ở chốt lời 1 | như trên |
| Q4 | Lệnh chờ vào chỉ hiệu lực trong phiên của ngày đánh giá; hết phiên → huỷ `HetHanVao` | như trên |
| Q5 | Kế hoạch không hợp lệ → không vào trạng thái chờ vào, ghi lý do | như trên |
| Q6 | Lãi/lỗ tính theo giá khớp lúc kích hoạt (`GiaVaoThucTe`) | như trên |
| Q7 | 7 vị thế đang mở: bổ sung trường mới bằng giá trị mặc định, `GiaVaoThucTe = GiaVaoLenhMin`, đánh dấu là dữ liệu bổ sung | như trên |
| Q8 | Thêm lý do `HuyNgoaiVung`; cập nhật dòng DCS 3275 | như trên (UPDATE production vẫn cần duyệt riêng lúc chạy) |
| T-Q2 | Nến hôm nay khi kiểm cò dựng từ bảng giá realtime | Chủ sản phẩm, 2026-10-09 |
| T-Q3 | Khối lượng không còn là điều kiện bắt buộc của cò Nổ hướng lên / Quét thanh khoản; chỉ hiển thị và cộng điểm xếp hạng | như trên |
| T-Q4 | Giữ ngưỡng 1.5 / 2.0 lần, chỉ để hiển thị | như trên |
| T-Q5 | Không nới điều kiện hình thái Nổ hướng lên | như trên |
| C-0 | Chặn mua lặp ở cả Pha 1 (không tạo kịch bản cho mã đang có chu kỳ mở) và Pha 2 (không kích hoạt) — phương án C | Chủ sản phẩm, 2026-10-19 |
| C-1 | Mỗi mã một **chu kỳ giao dịch**: Mua điểm 1 → (có thể) Mua điểm 2 → chỉ chờ Bán 1 / Bán 2 theo kế hoạch → bắn tin, đóng chu kỳ → đánh giá lại mã. Tối đa 2 lần mua mỗi chu kỳ | như trên (lời chủ sản phẩm) |
| C-2 | Điểm mua 2 = mua thêm khi giá xác nhận đi đúng hướng, mức nằm sẵn trong kế hoạch lúc mua điểm 1, không tính sau | như trên (duyệt đề xuất) |
| C-3 | Tỷ lệ vốn 50% điểm 1 / 50% điểm 2 | như trên (duyệt đề xuất) |
| C-4 | Sau điểm 2: giá vào = bình quân hai lần mua; mức chốt lời / cắt lỗ giữ nguyên kế hoạch điểm 1 | như trên (duyệt đề xuất) |
| C-5 | Điểm 2 chờ tới khi bán 1 hoặc cắt lỗ; chưa đạt thì chu kỳ chỉ có điểm 1 | như trên (duyệt đề xuất) |
| C-6 | "Một mã" tính chung mọi loại kịch bản V2 | như trên (duyệt đề xuất) |
| C-7 | Đóng chu kỳ xong, mua lại được từ phiên sau (Pha 1 sáng hôm sau) | như trên (duyệt đề xuất) |
| C-8 | Bổ sung vào spec 009 rồi mới giao làm; tạm dừng Phase 4–5 | như trên |

| C-9 | Mức điểm mua 2: Hồi hỗ trợ và Quét thanh khoản = giá vào điểm 1 + 1×ATR (ATR lúc mua điểm 1); Nổ hướng lên = đỉnh hộp + 0.5×chiều cao hộp. Điểm 2 phải < chốt lời 1, nếu không kế hoạch không có điểm 2. Chỉ mua khi giá khớp trong [mức điểm 2, mức điểm 2 + 0.3×ATR] | Chủ sản phẩm, 2026-10-19 (duyệt đề xuất) |
| C-10 | Vị thế mua lặp đang mở (ABB 5 dòng, DCM 3 dòng): giữ dòng mua đầu tiên làm điểm 1; các dòng sau huỷ với lý do `HuyMuaLap`, không đo Pha 3, không tính là điểm 2. UPDATE production duyệt lúc chạy | như trên (duyệt đề xuất) |

| C-11 | Luật rút từ mốc, chạy realtime trên giá khớp, cho mọi vị thế V2: giảm **4%** từ mốc → bán một nửa; giảm **6%** từ mốc → bán hết. Mốc = giá cao nhất kể từ lúc mua; chưa lên đỉnh thì mốc = giá mua | Chủ sản phẩm, 2026-10-19 (lời chủ sản phẩm, xác nhận cách hiểu) |
| C-12 | 4% / 6% cố định cho mọi mã; luật này thay cách đếm "ngày giảm 2%" trong ví dụ đầu | như trên |
| C-13 | Luật rút từ mốc chạy cùng dừng lỗ ban đầu và dừng lỗ đuổi theo ATR; mức nào chạm trước thì bán | như trên |
| C-14 | Đã mua điểm 2: giá mua = bình quân hai lần; **bỏ chốt lời 1/2**, chỉ thoát theo dừng lỗ / rút từ mốc / thời gian / hạn | như trên |
| C-15 | Đã bán một nửa theo mốc 4%: không bán nửa lần nữa; chỉ chờ giảm 6% từ đỉnh (kể cả đỉnh mới) để bán hết | như trên |
| C-16 (Q-a) | Mỗi chu kỳ tối đa **một** lần bán nửa, bất kể nguồn (chốt lời 1 hay rút 4%); sau đó chỉ còn bán hết | Chủ sản phẩm, 2026-10-20 (duyệt đề xuất) |
| C-17 (Q-b) | Đã bán nửa (bất kỳ nguồn nào) thì không mua điểm 2 nữa | như trên |
| C-18 (Q-c) | Cùng lượt chạm điểm 2 và luật bán → luật bán thắng, không mua | như trên |
| C-19 (Q-d) | Chu kỳ đóng khi bán hết theo bất kỳ luật nào, kể cả hết hạn và dừng theo thời gian | như trên |
| C-20 (Q-e) | Dời cắt lỗ sau chốt lời 1 về giá điểm 1 (không dùng giá bình quân); theo C-17 trường hợp có điểm 2 rồi mới chốt lời 1 thì C-14 đã bỏ chốt lời | như trên |
| C-22 (Q-g) | Lưu điểm 2 bằng 2 cột trên dòng điểm 1 (giá khớp, thời điểm mua điểm 2); một dòng = một chu kỳ; không đổi unique index | Chủ sản phẩm, 2026-10-20 (duyệt đề xuất) |
| C-23 (Q-h) | Mua điểm 2 xong không tính lại mốc: đỉnh vẫn tính từ lúc mua điểm 1 | như trên |
| C-24 | Sửa `GiaVaoThucTe` của 7 vị thế đang mở mua ngoài vùng bằng giá khớp thật trong log "TRIGGERED": NTP 2114 = 52.80; ABB 4106 = 17.70, 5444 = 17.70, 5891 = 17.70; DCM 4619 = 33.90, 5957 = 33.90; QNS 5765 = 51.00. Chạy **cùng đợt** UPDATE `HuyMuaLap` (C-10); chủ sản phẩm chạy trên server. Lưu ý: theo C-10, ABB 4106/5444/5891 và DCM 4619/5957 là dòng mua lặp sẽ bị huỷ (dòng giữ lại ABB 3650, DCM 1916 mua trong vùng), nên thực chất chỉ NTP 2114 và QNS 5765 cần sửa | Chủ sản phẩm, 2026-10-20 (duyệt đề xuất) |
| C-25 | DCS 3275: `LyDoThoatHet = HuyNgoaiVung` (Q8), chạy ngay | như trên |
| C-26 | Lệnh đã đóng DGW 1472 (mua thật 45.80, vùng 44.59–45.04) và PVS 2168 (mua thật 33.60, vùng 32.82–33.15): giữ nguyên dữ liệu; kết quả Pha 3 "Thắng" của hai lệnh **không đáng tin** (giá vào thấp hơn giá mua thật + Pha 3 chấm một lần sau 4 ngày). Xem lại khi Pha 3 được làm lại (chưa chốt việc làm lại Pha 3) | như trên |
| C-27 | Làm lại Pha 3 trong spec 009: kết quả đo phải phản ánh lãi/lỗ thật của vị thế (hiện chấm một lần sau 4 ngày rồi giữ nguyên, kể cả khi vị thế còn mở hoặc đóng lỗ sau đó). Cách làm: P1 — chỉ chấm khi chu kỳ đóng (C-19), vị thế đang mở chưa có kết quả ("đang giữ"); P2 — lãi/lỗ cả chu kỳ: giá vào = điểm 1 (hoặc bình quân nếu có điểm 2), giá ra = bình quân có trọng số các lần bán (nửa 50% + phần còn lại 50%), ngưỡng Thắng/Thua/Ngang ±1% giữ nguyên; P3 — chấm lại các dòng đã đóng, dòng đang mở xoá kết quả cũ chờ đóng; ghi production cùng đợt script C-10/C-24 | Chủ sản phẩm, 2026-10-20 (duyệt đề xuất) |
| C-28 (Q-i) | Giữ nhãn "Chờ đo" cho chu kỳ đang mở trong đợt này; đổi nhãn web/mobile để sau | Chủ sản phẩm, 2026-10-20 (duyệt đề xuất) |
| C-31 | Mức điểm 2 cho 5 vị thế đang mở (C-21) tính bằng ATR14 = trung bình true range 14 phiên **trước** ngày mua, từ lịch sử giá production: ABB 3650 → 17.7471 / băng 0.0771; DCM 1916 → 34.0764 / 0.2839; NTP 2114 → 54.1786 / 0.4136; QNS 5765 → 52.3571 / 0.4071; FRT 6014 → 147.4186 / 1.5236 | Chủ sản phẩm, 2026-10-21 (duyệt đề xuất) |
| C-32 | Sửa `GiaVaoThucTe` của 2 lệnh đã đóng mua ngoài vùng trước khi Pha 3 chấm lại: DGW 1472 = 45.80, PVS 2168 = 33.60 (giá trong log "TRIGGERED"); thay cho phần "giữ nguyên dữ liệu" của C-26 | như trên |
| C-34 | Tín hiệu V2 chỉ dùng giá đã khớp thật: Pha 2 **không xét mua** (điểm 1 và điểm 2) khi mã chưa có khớp trong phiên (khối lượng phiên = 0) hoặc trước 09:15; **không xét bán** khi khối lượng phiên = 0 (không chặn theo giờ). Lý do: 8/20 lệnh mua V2 từ 05/10 ghi nhận lúc 09:00–09:02 với khối lượng 0 (giá tham chiếu, chưa khớp) | Chủ sản phẩm, 2026-10-21 (duyệt đề xuất) |
| C-33 | Chấm kết quả ngay khi đóng lệnh: Pha 2 bán hết → chấm `KetQuaDoLuong` trong cùng lượt bằng chung hàm chấm của Pha 3 (luật C-27); tin bán hết in Thắng/Thua/Ngang. Pha 3 16:00 vẫn chạy để chấm bù dòng đã đóng mà chưa có kết quả. Làm riêng, trước Phase 10 | Chủ sản phẩm, 2026-10-21 (chọn phương án A) |
| C-30 | Bỏ mọi mã quyết định nội bộ (C-x) khỏi nội dung **mọi** tin Telegram V2 | Chủ sản phẩm, 2026-10-20 |
| C-29 | Tin bán nửa: bỏ mã "(C-16)" khỏi nội dung tin; tin bán nửa theo chốt lời 1 dùng cùng format với tin bán nửa rút 4% | Chủ sản phẩm, 2026-10-20 (duyệt đề xuất) |
| C-21 (Q-f) | Vị thế ABB/DCM sau khi dọn (C-10): có mức điểm 2 nếu lấy lại được ATR lúc mua điểm 1, không thì không có điểm 2 | như trên |


## User Scenarios & Testing *(mandatory)*

### User Story 1 - Chỉ mua khi giá nằm trong vùng vào của kế hoạch (Priority: P1)

Người theo dõi nhận tin mua V2 chỉ khi giá khớp nằm trong vùng vào mà kịch bản đã tính, và kế hoạch còn đường ra (chốt lời 1 cao hơn vùng vào, dừng lỗ thấp hơn vùng vào). Giá chạy xa vùng vào thì không có tin mua.

**Why this priority**: Sự cố DCS cho thấy mua ngoài vùng làm toàn bộ lãi/lỗ, MAE/MFE và kết quả đo lường sai khoảng 15%, và tín hiệu bán bắn ngay cùng phút mua.

**Independent Test**: Dựng kịch bản Hồi hỗ trợ có vùng vào 607.66–613.77, chốt lời 1 = 700; cho giá khớp 700 với cò đạt → không kích hoạt. Cho giá về 610 ở lượt sau → kích hoạt.

**Acceptance Scenarios**:

1. **Given** cò đạt, giá khớp ngoài vùng vào, **When** Pha 2 quét, **Then** không kích hoạt, kịch bản vẫn đang hình thành, không gửi tin.
2. **Given** lượt trước giá ngoài vùng, **When** lượt sau giá quay về trong vùng (cùng phiên), **Then** kích hoạt.
3. **Given** kế hoạch có chốt lời 1 ≤ giá vào tối đa hoặc dừng lỗ ≥ giá vào tối thiểu, **When** cò đạt, **Then** không kích hoạt, bằng chứng ghi lý do `KeHoachKhongHopLe`.
4. **Given** Nổ hướng lên / Quét thanh khoản có vùng vào tính theo giá khớp, **When** cò đạt và kế hoạch hợp lệ, **Then** kích hoạt.
5. **Given** kịch bản chưa kích hoạt, **When** hết phiên của ngày đánh giá, **Then** huỷ với lý do `HetHanVao`.

---

### User Story 2 - Kế hoạch đầy đủ chốt lúc mua và in trong tin mua (Priority: P1)

Lúc kích hoạt, hệ thống lưu một kế hoạch gồm: vùng vào, hạn vào, dừng lỗ ban đầu, chốt lời 1 (kèm tỷ lệ bán 50%), chốt lời 2, có dời dừng lỗ về giá vào sau chốt lời 1, có dừng lỗ đuổi theo và khoảng cách (cố định), dừng theo thời gian (số phiên, ngưỡng lãi), hạn giữ tối đa, tỷ lệ lãi/lỗ kỳ vọng; cùng giá vào thực tế = giá khớp. Tin mua in đủ toàn bộ các mục này.

**Why this priority**: Đây là nguyên tắc gốc của chủ sản phẩm; người theo dõi phải biết trước mọi điểm ra.

**Independent Test**: Kích hoạt một kịch bản theo đà, đọc kế hoạch đã lưu và nội dung tin mua; đổi ATR các phiên sau → kế hoạch không đổi.

**Acceptance Scenarios**:

1. **Given** kịch bản kích hoạt ở giá khớp P, **When** lưu vị thế, **Then** giá vào thực tế = P và đủ các trường kế hoạch.
2. **Given** vị thế đã mở, **When** ATR các phiên sau thay đổi, **Then** khoảng cách dừng lỗ đuổi theo không đổi.
3. **Given** tin mua gửi đi, **Then** có đủ: giá vào, vùng vào, cắt lỗ (%), chốt lời 1 (%, bán 50%, dời cắt lỗ), chốt lời 2 (%), dừng lỗ đuổi theo (chỉ kịch bản theo đà), quy tắc thời gian, tỷ lệ lãi/lỗ, ghi chú chưa bán được trước T+2.5.

---

### User Story 3 - Lệnh ra chỉ theo mức trong kế hoạch (Priority: P1)

Khi giữ vị thế, mức cắt hiệu lực = cao nhất trong: dừng lỗ ban đầu; giá vào thực tế (nếu đã chốt lời 1 và kế hoạch cho dời); đỉnh từ lúc mua − khoảng cách đuổi (nếu kế hoạch bật). Bán khi chạm chốt lời 1 (bán theo tỷ lệ trong kế hoạch), chốt lời 2, mức cắt hiệu lực, dừng theo thời gian, hạn tối đa. Kiệt sức / Gãy nền chỉ gửi cảnh báo kèm mức cắt hiệu lực, không đổi trạng thái. Luật bán chạy realtime trên giá khớp.

**Why this priority**: Đảm bảo lệnh ra không phụ thuộc thông tin phát sinh sau khi mua.

**Independent Test**: Cho vị thế chạm chốt lời 1 rồi giá lùi về giá vào → bán phần còn lại tại mức giá vào. Cho Kiệt sức đạt → chỉ có cảnh báo.

**Acceptance Scenarios**:

1. **Given** chạm chốt lời 1, **Then** ghi bán 50% và mức cắt hiệu lực nâng lên giá vào thực tế.
2. **Given** kịch bản theo đà, đỉnh từ lúc mua = H, **When** giá ≤ H − khoảng cách, **Then** thoát.
3. **Given** Kiệt sức hoặc Gãy nền đạt, **Then** gửi cảnh báo, không ghi giá thoát, trạng thái không đổi.
4. **Given** hết N phiên chưa từng lãi X%, hoặc hết hạn tối đa, **Then** thoát với lý do tương ứng.
5. **Given** chưa đủ T+2.5 mà chạm mức ra, **Then** cảnh báo một lần, không đổi trạng thái.

---

### User Story 4 - Cò kích hoạt ổn định, không phụ thuộc job đồng bộ (Priority: P2)

Khi kiểm cò trong phiên, nến hôm nay luôn dựng từ bảng giá realtime; mọi mốc so sánh "N phiên trước" (đỉnh hộp 20 phiên, đáy nền, trung bình khối lượng 20 phiên) loại phiên hôm nay. Khối lượng không chặn kích hoạt Nổ hướng lên / Quét thanh khoản, chỉ ghi trong bằng chứng ("tham khảo, không bắt buộc").

**Why this priority**: Nổ hướng lên hiện không thể kích hoạt; kết quả cò thay đổi theo việc job đồng bộ có chạy hay không.

**Independent Test**: Các test 5–12 trong `docs/features/v2-pha2-trigger-fix/spec.md` mục 2.

**Acceptance Scenarios**:

1. **Given** lịch sử có nến hôm nay High = 105, đỉnh 20 phiên trước = 100, giá khớp 102, MACD tăng, **Then** Nổ hướng lên kích hoạt.
2. **Given** khối lượng chỉ 0.3 lần trung bình, giá và MACD đạt, **Then** vẫn kích hoạt; bằng chứng khối lượng ghi "chưa đạt (tham khảo)".
3. **Given** cùng lịch sử có hoặc không có nến hôm nay, **Then** kết quả RSI / MACD / cò giống nhau.

---

### User Story 5 - Dữ liệu cũ được bổ sung minh bạch (Priority: P3)

7 vị thế đang mở được bổ sung các trường kế hoạch mới bằng giá trị mặc định và đánh dấu là dữ liệu bổ sung. Vị thế DCS 3275 được gán đúng lý do `HuyNgoaiVung`.

**Independent Test**: Sau migration, đọc 7 vị thế: có đủ trường, `GiaVaoThucTe = GiaVaoLenhMin`, có dấu "bổ sung"; luồng bán tiếp tục xét chúng bình thường.

**Acceptance Scenarios**:

1. **Given** vị thế mở trước thay đổi, **When** migration chạy, **Then** kế hoạch có đủ trường mới và đánh dấu bổ sung.
2. **Given** báo cáo hiệu quả, **Then** có thể tách nhóm vị thế có kế hoạch gốc và nhóm bổ sung.

### Edge Cases

- Giá khớp đúng bằng biên vùng vào → tính là trong vùng.
- Thiếu giá mở cửa / cao / thấp trên bảng giá → dùng giá khớp cho phần thiếu.
- Chạm chốt lời 1 và mức cắt cùng lượt → [theo thứ tự hiện có của B; plan phải nêu rõ].
- Vị thế cũ không có `KhoangCachDuoi` nhưng là kịch bản theo đà → dùng giá trị bổ sung lúc migration, không tính lại sau đó.
- Đơn vị khối lượng lịch sử và bảng giá khác nhau → dừng và hỏi, không tự quy đổi.
- Bước mua lỗi không được chặn bước bán (giữ hành vi phần 1 đã ship).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Hệ thống PHẢI chỉ kích hoạt mua khi giá khớp nằm trong `[GiaVaoLenhMin, GiaVaoLenhMax]` của kế hoạch.
- **FR-002**: Hệ thống PHẢI từ chối kế hoạch có chốt lời 1 ≤ giá vào tối đa hoặc dừng lỗ ≥ giá vào tối thiểu, ghi lý do `KeHoachKhongHopLe`.
- **FR-003**: Kịch bản chưa kích hoạt đến hết phiên ngày đánh giá PHẢI bị huỷ với lý do `HetHanVao`.
- **FR-004**: Kế hoạch PHẢI chứa đủ các trường ở mục 1 tài liệu `v2-trade-plan` và được lưu trước / cùng lúc lệnh mua.
- **FR-005**: Lúc kích hoạt PHẢI lưu `GiaVaoThucTe` = giá khớp; lãi/lỗ, MAE/MFE, Pha 3 PHẢI dùng giá này khi có.
- **FR-006**: Sau khi mua, không trường nào của kế hoạch được tính lại.
- **FR-007**: Khoảng cách dừng lỗ đuổi theo PHẢI = k × ATR tại lúc lập kế hoạch (k = 2.5, ATR 14), chỉ bật cho Nổ hướng lên và Quét thanh khoản.
- **FR-008**: Mức cắt hiệu lực PHẢI = cao nhất trong ba mức định sẵn (mục 1 tài liệu `v2-trade-plan`).
- **FR-009**: Tỷ lệ bán ở chốt lời 1 (50%) và quy tắc dời dừng lỗ PHẢI đọc từ kế hoạch.
- **FR-010**: Dừng theo thời gian (5 phiên / 3%) và hạn tối đa (20 phiên) PHẢI đọc từ kế hoạch.
- **FR-011**: Kiệt sức / Gãy nền PHẢI chỉ gửi cảnh báo kèm mức cắt hiệu lực, không đổi trạng thái, không ghi giá thoát.
- **FR-012**: Tin mua PHẢI in đủ các mục ở mục 4 tài liệu `v2-trade-plan`.
- **FR-013**: Luật bán PHẢI chạy realtime trên giá khớp, không chờ giá đóng cửa.
- **FR-014**: Khi kiểm cò, nến hôm nay PHẢI dựng từ bảng giá realtime; mốc đỉnh hộp, đáy nền, trung bình khối lượng PHẢI loại phiên hôm nay.
- **FR-015**: Khối lượng KHÔNG được là điều kiện bắt buộc của cò Nổ hướng lên / Quét thanh khoản; vẫn ghi trong bằng chứng với ngưỡng 1.5 / 2.0 lần.
- **FR-016**: Hệ thống PHẢI có lý do thoát / huỷ `HetHanVao`, `HuyNgoaiVung`, `KeHoachKhongHopLe`.
- **FR-017**: Migration PHẢI bổ sung trường mới cho vị thế đang mở, `GiaVaoThucTe = GiaVaoLenhMin`, và đánh dấu dữ liệu bổ sung.
- **FR-018**: Cập nhật dòng DCS 3275 sang `HuyNgoaiVung` PHẢI là một bước riêng, chỉ chạy khi chủ sản phẩm duyệt lúc chạy.
- **FR-019**: Công thức đặt mức của từng kịch bản (mục 2 tài liệu `v2-trade-plan`) PHẢI giữ nguyên.

### Key Entities

- **Kế hoạch giao dịch**: toàn bộ mức vào / ra và quy tắc, chốt lúc lập; gắn với một kịch bản đã kích hoạt.
- **Vị thế kịch bản (KetQuaKichBan)**: thêm giá vào thực tế; trạng thái Đang hình thành → Đang giữ → Đã bán nửa → Đã thoát, hoặc Huỷ.
- **Sự kiện bán**: thêm lý do `HetHanVao`, `HuyNgoaiVung`, `KeHoachKhongHopLe`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% lần kích hoạt mới có giá khớp nằm trong vùng vào của kế hoạch (kiểm bằng truy vấn sau deploy).
- **SC-002**: 0 trường hợp mua và tín hiệu bán xảy ra trong cùng một phút do mua ngoài vùng.
- **SC-003**: 100% tin mua mới có đủ các mục ở FR-012.
- **SC-004**: Kế hoạch của mọi vị thế không thay đổi giữa lúc mua và lúc thoát (so ảnh chụp kế hoạch).
- **SC-005**: Nổ hướng lên có ít nhất một lần kích hoạt hợp lệ trong 10 phiên sau deploy, hoặc có giải thích bằng dữ liệu vì sao không.
- **SC-006**: Báo cáo hiệu quả tách được nhóm kế hoạch gốc và nhóm dữ liệu bổ sung.

## Assumptions

- Thông số chung (k = 2.5, ATR 14, 5 phiên / 3%, 20 phiên, 50%) là cấu hình, ghi vào từng kế hoạch lúc lập.
- Kế hoạch lưu trong JSON hiện có của vị thế, mở rộng không cần đổi cột; chỉ thêm cột giá vào thực tế.
- Phần 1 `v2-pha2-trigger-fix` đã chạy trên production (log 14/10 có "bước bán: 7 vị thế" mỗi phút).
- Điểm khối lượng trong xếp hạng giữ nguyên, chỉ ảnh hưởng thứ tự TopN tin.
- Chưa xác nhận: nến hôm nay có được ghi vào lịch sử trong phiên không; đơn vị khối lượng lịch sử và bảng giá có giống nhau không. Người implement phải kiểm và báo.
- Tài liệu `docs/domain/pipeline-jobs.md` cập nhật cùng change set.
