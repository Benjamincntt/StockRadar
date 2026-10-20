# V2 — Kế hoạch giao dịch đầy đủ trước khi mua

Trạng thái: **Mục 0 đã chốt (2026-10-14). Mục 8 — luật chu kỳ C-0→C-15 — chủ sản phẩm chốt thêm 2026-10-19; Phase 4–5 của Spec Kit 009 tạm dừng để cập nhật thiết kế (xem `specs/009-v2-trade-plan/`).**

## Vì sao làm lại

Chủ sản phẩm đặt nguyên tắc (2026-10-09):

> **Trước khi mua phải có hết kịch bản mua và bán.** Lệnh vào chỉ kích hoạt khi giá nằm trong vùng vào của kịch bản. Lệnh ra chỉ kích hoạt khi chạm mục tiêu hoặc quá điểm chịu đựng đã định sẵn. Không tính lại kế hoạch sau khi đã thấy giá.

Các phương án A, B, C, 2b được làm từng mảnh và lệch khỏi nguyên tắc này:

| Mảnh | Lệch ở đâu |
|---|---|
| Cò mua (mọi kịch bản) | Không kiểm giá khớp có nằm trong vùng vào → DCS mua ở 700 với kế hoạch tính cho 610 |
| Giá vào lưu trong kế hoạch | Lưu `GiaVaoLenhMin`, không lưu giá khớp thật lúc mua → lãi/lỗ, MAE/MFE, Pha 3 lệch |
| Dời dừng lỗ về giá vào sau chốt lời 1 (B) | Quy tắc đúng, nhưng không ghi trong kế hoạch, không in trong tin mua |
| Dừng lỗ đuổi theo bằng ATR (C) | ATR tính lại mỗi phiên → khoảng cách cắt thay đổi sau khi mua; không ghi trong kế hoạch |
| Dừng lỗ theo thời gian, hết hạn 20 phiên (C) | Không ghi trong kế hoạch, không in trong tin mua |
| Kiệt sức / Gãy nền (A) | Lệnh ra dựa trên chỉ báo phát sinh sau khi mua, không phải mức định sẵn |
| Đóng tay DCS | Gán nhãn `HetHanTheoDoi` — sai sự thật, thực chất là huỷ lệnh vào ngoài vùng |

Tài liệu này **thay** cách tiếp cận từng mảnh bằng **một kế hoạch đầy đủ, chốt lúc mua**. Code A/B/C đã ship được giữ làm nền, sửa cho khớp kế hoạch.

## 0. Câu hỏi đã chốt

Chủ sản phẩm trả lời "chốt" ngày 2026-10-14: **duyệt cả 8 đề xuất dưới đây, không thay đổi**.

| # | Câu hỏi | Đáp án đã chốt |
|---|---|---|
| Q1 | Kiệt sức / Gãy nền (chỉ báo) làm gì? | **Chỉ cảnh báo**, không phải lệnh bán. Lệnh ra chỉ theo mức trong kế hoạch. |
| Q2 | Dừng lỗ đuổi theo: khoảng cách chốt lúc mua? | **Có.** `KhoangCachDuoi = k × ATR tại lúc mua`, lưu trong kế hoạch, không tính lại. Mức cắt = đỉnh từ lúc mua − khoảng cách đó (đỉnh thay đổi, công thức và khoảng cách cố định). |
| Q3 | Bán bao nhiêu ở chốt lời 1? | **50%.** Ghi trong kế hoạch. |
| Q4 | Lệnh chờ vào hiệu lực bao lâu? | **Chỉ trong phiên của ngày đánh giá.** Hết phiên chưa vào → huỷ, lý do `HetHanVao`. |
| Q5 | Kế hoạch không hợp lệ (chốt lời 1 ≤ giá vào tối đa, dừng lỗ ≥ giá vào tối thiểu) | **Không đưa vào trạng thái chờ vào**; ghi lý do trong bằng chứng. |
| Q6 | Giá vào dùng để tính lãi/lỗ | **Giá khớp lúc kích hoạt** (nằm trong vùng vào), lưu cột riêng `GiaVaoThucTe`. |
| Q7 | 7 vị thế đang mở (không có các trường mới) | Bổ sung các trường mới bằng giá trị mặc định lúc migration, `GiaVaoThucTe = GiaVaoLenhMin`. Ghi rõ trong tài liệu là dữ liệu bổ sung, không phải kế hoạch gốc. |
| Q8 | Nhãn huỷ của DCS | Thêm lý do `HuyNgoaiVung`, cập nhật dòng 3275 (đang ghi sai `HetHanTheoDoi`). |

## 1. Kế hoạch giao dịch đầy đủ (chốt lúc lập kế hoạch, trước khi mua điểm 1)

Mỗi kịch bản mua sinh ra **một** kế hoạch gồm đủ các phần sau. Kế hoạch được lưu và **in toàn bộ trong tin mua**. Sau khi mua không trường nào được tính lại (FR-006).

| Phần | Trường | Nghĩa |
|---|---|---|
| **Vào** | `GiaVaoLenhMin`, `GiaVaoLenhMax` | Vùng vào điểm 1. Chỉ mua khi giá khớp nằm trong vùng. |
| | `HetHanVao` | Hạn chờ vào (Q4) |
| **Cắt lỗ** | `GiaDungLo` | Điểm chịu đựng ban đầu |
| **Chốt lời** *(chỉ áp dụng khi chưa mua điểm 2 — C-14)* | `GiaChotLoi1`, `TyLeBanChotLoi1` | Mục tiêu 1, bán bao nhiêu (Q3, C-16: tối đa một lần) |
| | `GiaChotLoi2` | Mục tiêu 2, bán phần còn lại |
| **Điểm mua 2** *(C-9)* | `GiaMuaDiem2` | Mức điểm 2, chốt lúc mua điểm 1. Null nếu ≥ CL1 (kế hoạch không có điểm 2) |
| | `BandRongDiem2` | 0.3 × ATR lúc mua điểm 1 — chỉ mua khi giá khớp ∈ [mức, mức + band] |
| **Rút từ mốc** *(C-11)* | `NguongRutMocBanNuaPhanTram` = 4, `NguongRutMocBanHetPhanTram` = 6 | Giảm 4% từ mốc → bán nửa; giảm 6% → bán hết. Mốc = đỉnh từ lúc mua. Chạy cho mọi vị thế, realtime trên giá khớp (C-12, C-13) |
| **Quy tắc sau chốt lời 1** *(C-20)* | `DoiDungLoVeGiaVao` (bool) | Có dời dừng lỗ về **giá điểm 1** không (không dùng bình quân) |
| **Dừng lỗ đuổi theo** | `CoDungLoDuoi` (bool), `KhoangCachDuoi` (giá) | Bật cho kịch bản theo đà; khoảng cách = k × ATR tại lúc lập kế hoạch (Q2) |
| **Thời gian** | `SoPhienDungTheoThoiGian`, `NguongLaiToiThieu` | Hết N phiên mà chưa từng lãi X% → bán hết (đóng chu kỳ C-19) |
| | `SoPhienToiDa` | Hạn giữ tối đa → bán hết (đóng chu kỳ C-19) |
| **Tổng hợp** | `TyLeLaiLo` | (chốt lời 1 − giá vào tối đa) / (giá vào tối đa − dừng lỗ), tính sẵn để hiển thị |
| | `TyLeVonDiem1 / TyLeVonDiem2` | 0.5 / 0.5 — chia vốn (C-3) |

**Lúc mua điểm 1** ghi thêm: `GiaVaoThucTe` = giá khớp lúc kích hoạt (Q6).

Thứ tự ưu tiên mức cắt khi giữ vị thế (đều định sẵn): mức cắt hiệu lực = cao nhất trong ba mức (dừng lỗ ban đầu; giá vào thực tế/bình quân nếu đã dời; đỉnh − `KhoangCachDuoi` nếu bật đuổi). **Luật rút mốc chạy song song** (C-13) — mức nào chạm trước bán trước. Cùng lượt nhiều mức → luật bán thắng luật mua (C-18).

## 2. Cách từng kịch bản đặt mức (giữ nguyên công thức hiện có)

| Kịch bản | Vùng vào | Dừng lỗ | Chốt lời 1 | Chốt lời 2 | Dừng lỗ đuổi theo |
|---|---|---|---|---|---|
| Hồi hỗ trợ | EMA20 ± 0.5% | EMA50 − 0.5×ATR | Đỉnh 20 phiên | CL1 + 0.5×(CL1 − giá vào min) | Không |
| Nổ hướng lên | [giá khớp, giá khớp + 0.3×ATR] | Đáy hộp − 0.5×ATR | Đỉnh hộp + 1.0×chiều cao hộp | Đỉnh hộp + 1.5×chiều cao hộp | Có |
| Quét thanh khoản | giá khớp ± 0.3% | Đáy quét − 0.3×ATR | Đỉnh hộp | CL1 + 0.5×(CL1 − giá vào min) | Có |

Thông số chung (cấu hình, ghi vào từng kế hoạch lúc lập): k = 2.5, ATR 14; dừng theo thời gian 5 phiên / 3%; hạn tối đa 20 phiên; bán 50% ở chốt lời 1; dời dừng lỗ về giá vào sau chốt lời 1.

**Lưu ý Nổ hướng lên / Quét thanh khoản:** vùng vào tính theo giá khớp, nên kế hoạch được lập **tại lúc cò đạt** (không có trước từ Pha 1). Vẫn đúng nguyên tắc: toàn bộ mức ra được chốt trước lệnh mua, trong cùng một lượt quét.

## 3. Vòng đời lệnh theo chu kỳ *(C-1→C-7, C-16→C-19)*

| Trạng thái / Giai đoạn | Vào khi | Ra khi |
|---|---|---|
| Đang hình thành | Pha 1 đạt bối cảnh + hình thái, **mã chưa có chu kỳ mở** (C-0) | Cò đạt + kế hoạch hợp lệ + giá trong vùng → DaKichHoat (điểm 1); hết phiên → Huỷ (`HetHanVao`) |
| Đang giữ — chưa điểm 2, chưa bán nửa | Mua điểm 1 (`GiaVaoThucTe` = giá khớp); chờ điểm 2 mỗi lượt nếu `GiaMuaDiem2 != null` (C-5: từ lúc mua điểm 1 tới khi bán bất kỳ) | Giá khớp ∈ band → mua điểm 2; chạm CL1/RutMoc4 → bán nửa; chạm RutMoc6/mức cắt/CL2/hết hạn → bán hết (đóng chu kỳ) |
| Đã mua điểm 2 | Giá khớp ∈ band điểm 2 (chưa bán nửa — C-17; chưa bị luật bán thắng — C-18); ghi `GiaMuaDiem2ThucTe`; **bỏ CL1/CL2** (C-14) | Chạm RutMoc6/mức cắt/đuổi/thời gian/hạn → bán hết → chu kỳ đóng (C-19) |
| Đã bán nửa | Chạm CL1 **hoặc** mốc giảm ≥4% (RutMoc4); C-16: mỗi chu kỳ tối đa MỘT lần | Chạm RutMoc6/mức cắt/hết hạn → bán hết → chu kỳ đóng |
| Đã thoát / Chu kỳ đóng | Bán hết theo **bất kỳ luật nào** (cắt, CL2, RutMoc6, hết thời gian, hết hạn — C-19) | Đánh giá lại mã từ **phiên hôm sau** (C-7) |
| Huỷ | Hết hạn vào (`HetHanVao`), mua lặp (`HuyMuaLap` C-10), huỷ tay | — |

Chưa đủ T+2.5 mà chạm mức ra: chỉ cảnh báo một lần, không đổi trạng thái (giữ như hiện tại).

Kiệt sức / Gãy nền (Q1): gửi **cảnh báo** kèm mức cắt hiệu lực hiện tại; không đổi trạng thái, không ghi giá thoát.

## 4. Tin mua phải in đủ *(xem `specs/009-v2-trade-plan/contracts/telegram-mua.md` để biết format chi tiết)*

Tin mua điểm 1 gồm đủ các mục:

```
🎯 <MÃ> — <kịch bản> · MUA (điểm 1)
Giá vào: <giá khớp>  (vùng <min>–<max>)
Cắt lỗ: <dừng lỗ>  (−x%)
Chốt lời 1: <CL1> (+x%) — bán 50%, sau đó dời cắt lỗ về giá vào
Chốt lời 2: <CL2> (+x%) — bán phần còn lại
Điểm mua 2: <mức> (+x%) — khớp trong [mức, mức + band]    (chỉ khi kế hoạch có điểm 2)
    · Vốn 50/50; sau điểm 2 bỏ chốt lời 1/2, chỉ thoát theo dừng lỗ / rút mốc / thời gian / hạn
Dừng lỗ đuổi theo: đỉnh − <khoảng cách>   (chỉ kịch bản theo đà)
Rút từ mốc: −4% → bán nửa; −6% → bán hết
Thời gian: sau 5 phiên chưa lãi 3% → bán; tối đa 20 phiên
Lãi/lỗ kỳ vọng: <tỷ lệ>
Chưa bán được trước T+2.5
```

Tin mua điểm 2, tin bán nửa (CL1 / RutMoc4), tin bán hết (RutMoc6 / mức cắt / CL2 / hết hạn / hết thời gian — kèm "chu kỳ đóng"): xem contract chi tiết.

## 5. Dữ liệu

- Mở rộng `KeHoachGiaoDich` (lưu JSON trong `KeHoachGiaoDichJson`): thêm các trường mục 1. JSON mở rộng không cần đổi cột.
- Cột mới trên `KetQuaKichBan`: `GiaVaoThucTe decimal(18,2)?`.
- `DungLoDuoi` (C) giữ nguyên ý nghĩa nhưng tính từ `KhoangCachDuoi` trong kế hoạch, không từ ATR hiện tại.
- Lý do thoát / huỷ mới trong `SuKienBan`: `HetHanVao`, `HuyNgoaiVung`, `KeHoachKhongHopLe`.
- Migration bổ sung cho vị thế đang mở (Q7).

## 6. Những gì giữ, sửa, bỏ so với code hiện tại

| Hiện tại | Xử lý |
|---|---|
| Phần 1 `v2-pha2-trigger-fix` (bán chạy mọi lượt) | **Giữ, ship ngay** — sửa lỗi cơ học, không phụ thuộc thiết kế này |
| Phần 2 `v2-pha2-trigger-fix` (chuẩn hoá nến hôm nay, bỏ khối lượng bắt buộc) | **Giữ**, làm cùng đợt với tài liệu này |
| Mục 2b `v2-pha2-trigger-fix` | **Gộp vào đây** (mục 1, 3) |
| B: chạm dừng lỗ / chốt lời 1 / chốt lời 2 | Giữ, đọc mức từ kế hoạch; tỷ lệ bán và quy tắc dời đọc từ kế hoạch |
| C: dừng lỗ đuổi theo | Sửa: khoảng cách cố định từ kế hoạch |
| C: dừng lỗ theo thời gian, hạn tối đa, MAE/MFE | Giữ, tham số đọc từ kế hoạch |
| A: Kiệt sức / Gãy nền bán | Đổi thành cảnh báo (Q1) |
| Pha 3 | **Làm lại (C-27)**: chỉ chấm khi chu kỳ đóng; lãi/lỗ cả chu kỳ (giá vào bình quân nếu có điểm 2, giá ra bình quân trọng số); không còn đo một lần sau 4 ngày |

## 7. Thứ tự làm đề xuất

1. Ship phần 1 `v2-pha2-trigger-fix` (đã làm xong, chờ lệnh ship).
2. Sửa nhãn DCS (Q8) — một câu `UPDATE` trên production, cần duyệt.
3. Chốt mục 0 → Spec Kit (`/speckit-specify`) cho tài liệu này + phần 2 `v2-pha2-trigger-fix`.
4. Implement theo một change set: kế hoạch đầy đủ + kiểm vùng vào + tin mua + đọc mức từ kế hoạch + migration.

## 8. Luật chu kỳ giao dịch (chốt 2026-10-19 — mã quyết định C-0→C-15 trong `specs/009-v2-trade-plan/spec.md`)

Chủ sản phẩm chốt thêm luật trọng yếu, sửa trực tiếp mô hình vào/ra ở mục 1–3 trên:

- **Một mã một chu kỳ** (C-0→C-7): Mua điểm 1 → (có thể) Mua điểm 2 → bán → đóng chu kỳ → phiên sau mới đánh giá lại mã. Tối đa 2 lần mua mỗi chu kỳ, tính chung mọi loại kịch bản. Chặn ở cả Pha 1 (không tạo kịch bản cho mã đang có chu kỳ mở) và Pha 2 (không kích hoạt).
- **Điểm mua 2 chốt sẵn lúc mua điểm 1** (C-9): Hồi hỗ trợ / Quét thanh khoản = giá vào điểm 1 + 1×ATR; Nổ hướng lên = đỉnh hộp + 0.5×chiều cao hộp. Phải nhỏ hơn chốt lời 1, không thì kế hoạch không có điểm 2. Chỉ mua khi giá khớp trong [mức điểm 2, mức điểm 2 + 0.3×ATR]. Vốn 50/50 (C-3). Chờ tới khi bán 1 hoặc cắt lỗ (C-5).
- **Sau điểm 2** (C-4, C-14): giá vào = bình quân hai lần mua; **bỏ chốt lời 1/2** — chỉ thoát theo dừng lỗ / rút từ mốc / thời gian / hạn.
- **Rút từ mốc, mọi vị thế, realtime trên giá khớp** (C-11→C-13, C-15): mốc = giá cao nhất kể từ lúc mua (chưa lên đỉnh thì = giá mua); giảm 4% từ mốc → bán nửa; giảm 6% → bán hết; 4%/6% cố định mọi mã. Chạy cùng dừng lỗ ban đầu và dừng lỗ đuổi ATR — mức nào chạm trước bán trước. Đã bán nửa theo mốc 4% thì không bán nửa lần nữa, chỉ chờ 6%.
- **Dọn vị thế mua lặp đang mở trên production** (C-10): ABB (5 dòng), DCM (3 dòng) — giữ dòng mua đầu làm điểm 1, các dòng sau huỷ `HuyMuaLap`, không đo Pha 3. UPDATE production chỉ viết sẵn, không chạy khi chưa duyệt.
- **Sửa giá vào vị thế mở mua ngoài vùng** (C-24): `GiaVaoThucTe` = giá khớp thật log TRIGGERED (thực chất NTP 2114=52.80, QNS 5765=51.00; ABB/DCM mua lặp sẽ `HuyMuaLap` nên không sửa). Cùng đợt script C-10.
- **Làm lại Pha 3 chấm theo chu kỳ** (C-27): P1 chỉ ghi `KetQuaDoLuong` khi chu kỳ đóng (`ThoiGianThoatHet != null`, C-19), đang mở = "đang giữ" (null); P2 lãi/lỗ cả chu kỳ — giá vào = điểm 1 hoặc bình quân điểm 2, giá ra = bình quân trọng số các lần bán, ngưỡng ±1% giữ nguyên; P3 chấm lại dòng đã đóng, xoá kết quả dòng đang mở. Lỗi gốc: `Pha3DoLuongRunner` đo một lần sau 4 ngày rồi khoá (PVS "Thắng" 12/10 khi còn mở, 16/10 cắt lỗ thật). Chi tiết: `specs/009-v2-trade-plan/research.md` R11.

Tham khảo: V1 VIP đã có luật "rút từ mốc 4/6%" (commit `baa7315`); V2 dùng cách tính riêng theo C-11 (mốc = đỉnh từ lúc mua, không phải anchor hồi cứu của V1) — đối chiếu chi tiết ở `specs/009-v2-trade-plan/research.md` R10.3. Các điểm giao nhau giữa luật mới và luật cũ chưa chốt (thứ tự bán/mua cùng lượt, định nghĩa đóng chu kỳ, tương tác CL1 × điểm 2 × rút mốc) liệt kê trong `specs/009-v2-trade-plan/plan.md` — mục "Câu hỏi thứ tự luật".

Mục 1 (kế hoạch), 3 (vòng đời) và 4 (tin mua) của tài liệu này sẽ còn phải viết lại chi tiết sau khi các câu hỏi trên chốt; bản hiện tại giữ nguyên như lịch sử thiết kế giai đoạn Q1–Q8.
