# V2 — Kế hoạch giao dịch đầy đủ trước khi mua

Trạng thái: **NHÁP — lập lại kế hoạch từ bước mua (2026-10-09). Chờ chốt mục 0. Chưa code.**

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

## 0. Câu hỏi cần chủ sản phẩm chốt

| # | Câu hỏi | Đề xuất |
|---|---|---|
| Q1 | Kiệt sức / Gãy nền (chỉ báo) làm gì? | **Chỉ cảnh báo**, không phải lệnh bán. Lệnh ra chỉ theo mức trong kế hoạch. |
| Q2 | Dừng lỗ đuổi theo: khoảng cách chốt lúc mua? | **Có.** `KhoangCachDuoi = k × ATR tại lúc mua`, lưu trong kế hoạch, không tính lại. Mức cắt = đỉnh từ lúc mua − khoảng cách đó (đỉnh thay đổi, công thức và khoảng cách cố định). |
| Q3 | Bán bao nhiêu ở chốt lời 1? | **50%.** Ghi trong kế hoạch. |
| Q4 | Lệnh chờ vào hiệu lực bao lâu? | **Chỉ trong phiên của ngày đánh giá.** Hết phiên chưa vào → huỷ, lý do `HetHanVao`. |
| Q5 | Kế hoạch không hợp lệ (chốt lời 1 ≤ giá vào tối đa, dừng lỗ ≥ giá vào tối thiểu) | **Không đưa vào trạng thái chờ vào**; ghi lý do trong bằng chứng. |
| Q6 | Giá vào dùng để tính lãi/lỗ | **Giá khớp lúc kích hoạt** (nằm trong vùng vào), lưu cột riêng `GiaVaoThucTe`. |
| Q7 | 7 vị thế đang mở (không có các trường mới) | Bổ sung các trường mới bằng giá trị mặc định lúc migration, `GiaVaoThucTe = GiaVaoLenhMin`. Ghi rõ trong tài liệu là dữ liệu bổ sung, không phải kế hoạch gốc. |
| Q8 | Nhãn huỷ của DCS | Thêm lý do `HuyNgoaiVung`, cập nhật dòng 3275 (đang ghi sai `HetHanTheoDoi`). |

## 1. Kế hoạch giao dịch đầy đủ (chốt lúc lập kế hoạch, trước khi mua)

Mỗi kịch bản mua sinh ra **một** kế hoạch gồm đủ các phần sau. Kế hoạch được lưu và **in toàn bộ trong tin mua**. Sau khi mua không trường nào được tính lại.

| Phần | Trường | Nghĩa |
|---|---|---|
| **Vào** | `GiaVaoLenhMin`, `GiaVaoLenhMax` | Vùng vào. Chỉ mua khi giá khớp nằm trong vùng. |
| | `HetHanVao` | Hạn chờ vào (Q4) |
| **Cắt lỗ** | `GiaDungLo` | Điểm chịu đựng ban đầu |
| **Chốt lời** | `GiaChotLoi1`, `TyLeBanChotLoi1` | Mục tiêu 1, bán bao nhiêu (Q3) |
| | `GiaChotLoi2` | Mục tiêu 2, bán phần còn lại |
| **Quy tắc sau chốt lời 1** | `DoiDungLoVeGiaVao` (bool) | Có dời dừng lỗ về giá vào không |
| **Dừng lỗ đuổi theo** | `CoDungLoDuoi` (bool), `KhoangCachDuoi` (giá) | Bật cho kịch bản theo đà; khoảng cách = k × ATR tại lúc lập kế hoạch (Q2) |
| **Thời gian** | `SoPhienDungTheoThoiGian`, `NguongLaiToiThieu` | Hết N phiên mà chưa từng lãi X% → bán hết |
| | `SoPhienToiDa` | Hạn giữ tối đa → bán hết |
| **Tổng hợp** | `TyLeLaiLo` | (chốt lời 1 − giá vào tối đa) / (giá vào tối đa − dừng lỗ), tính sẵn để hiển thị |

**Lúc mua** ghi thêm: `GiaVaoThucTe` = giá khớp lúc kích hoạt (Q6).

Thứ tự ưu tiên mức cắt khi giữ vị thế (đều định sẵn): mức cắt hiệu lực = cao nhất trong ba mức (dừng lỗ ban đầu; giá vào thực tế nếu đã chốt lời 1 và `DoiDungLoVeGiaVao`; đỉnh − `KhoangCachDuoi` nếu `CoDungLoDuoi`).

## 2. Cách từng kịch bản đặt mức (giữ nguyên công thức hiện có)

| Kịch bản | Vùng vào | Dừng lỗ | Chốt lời 1 | Chốt lời 2 | Dừng lỗ đuổi theo |
|---|---|---|---|---|---|
| Hồi hỗ trợ | EMA20 ± 0.5% | EMA50 − 0.5×ATR | Đỉnh 20 phiên | CL1 + 0.5×(CL1 − giá vào min) | Không |
| Nổ hướng lên | [giá khớp, giá khớp + 0.3×ATR] | Đáy hộp − 0.5×ATR | Đỉnh hộp + 1.0×chiều cao hộp | Đỉnh hộp + 1.5×chiều cao hộp | Có |
| Quét thanh khoản | giá khớp ± 0.3% | Đáy quét − 0.3×ATR | Đỉnh hộp | CL1 + 0.5×(CL1 − giá vào min) | Có |

Thông số chung (cấu hình, ghi vào từng kế hoạch lúc lập): k = 2.5, ATR 14; dừng theo thời gian 5 phiên / 3%; hạn tối đa 20 phiên; bán 50% ở chốt lời 1; dời dừng lỗ về giá vào sau chốt lời 1.

**Lưu ý Nổ hướng lên / Quét thanh khoản:** vùng vào tính theo giá khớp, nên kế hoạch được lập **tại lúc cò đạt** (không có trước từ Pha 1). Vẫn đúng nguyên tắc: toàn bộ mức ra được chốt trước lệnh mua, trong cùng một lượt quét.

## 3. Vòng đời lệnh

| Trạng thái | Vào khi | Ra khi |
|---|---|---|
| Đang hình thành | Pha 1 đạt bối cảnh + hình thái | Cò đạt và kế hoạch hợp lệ → Chờ vào / Đang giữ; hết phiên → Huỷ (`HetHanVao`) |
| Đang giữ | Giá khớp nằm trong vùng vào → mua, ghi `GiaVaoThucTe` | Chạm chốt lời 1 → Đã bán nửa; chạm mức cắt hiệu lực / hết thời gian / hết hạn → Đã thoát |
| Đã bán nửa | Chạm chốt lời 1 | Chạm chốt lời 2 / mức cắt hiệu lực / hết hạn → Đã thoát |
| Đã thoát | Một trong các mức định sẵn | — |
| Huỷ | Hết hạn vào, kế hoạch không hợp lệ, hoặc huỷ tay có lý do | — |

Chưa đủ T+2.5 mà chạm mức ra: chỉ cảnh báo một lần, không đổi trạng thái (giữ như hiện tại).

Kiệt sức / Gãy nền (Q1): gửi **cảnh báo** kèm mức cắt hiệu lực hiện tại; không đổi trạng thái, không ghi giá thoát.

## 4. Tin mua phải in đủ

```
🎯 <MÃ> — <kịch bản> · MUA
Giá vào: <giá khớp>  (vùng <min>–<max>)
Cắt lỗ: <dừng lỗ>  (−x%)
Chốt lời 1: <CL1> (+x%) — bán 50%, sau đó dời cắt lỗ về giá vào
Chốt lời 2: <CL2> (+x%) — bán phần còn lại
Dừng lỗ đuổi theo: đỉnh − <khoảng cách>   (chỉ kịch bản theo đà)
Thời gian: sau 5 phiên chưa lãi 3% → bán; tối đa 20 phiên
Lãi/lỗ kỳ vọng: <tỷ lệ>
Chưa bán được trước T+2.5
```

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
| Pha 3 | Dùng `GiaVaoThucTe` thay `GiaVaoLenhMin` khi có |

## 7. Thứ tự làm đề xuất

1. Ship phần 1 `v2-pha2-trigger-fix` (đã làm xong, chờ lệnh ship).
2. Sửa nhãn DCS (Q8) — một câu `UPDATE` trên production, cần duyệt.
3. Chốt mục 0 → Spec Kit (`/speckit-specify`) cho tài liệu này + phần 2 `v2-pha2-trigger-fix`.
4. Implement theo một change set: kế hoạch đầy đủ + kiểm vùng vào + tin mua + đọc mức từ kế hoạch + migration.
