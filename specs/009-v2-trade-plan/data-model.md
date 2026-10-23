# Data model: 009-v2-trade-plan

> **Cập nhật 2026-10-20**: C-0→C-27 (spec.md) — Q-a→Q-f chốt C-16→C-21; Q-g chốt C-22 (phương án A); Q-h chốt C-23 (không reset mốc); C-24 sửa giá vào vị thế mở; C-25/26/27 làm lại Pha 3. *(đề xuất)* = cách diễn đạt/cách làm của người viết tài liệu, chưa được chủ sản phẩm xác nhận.

## KeHoachGiaoDich (JSON trong `KetQuaKichBan.KeHoachGiaoDichJson`)

| Trường | Kiểu | Có sẵn? | Nguồn lúc lập |
|---|---|---|---|
| GiaVaoLenhMin, GiaVaoLenhMax | decimal | có | `TinhKeHoach` |
| GiaDungLo | decimal | có | `TinhKeHoach` |
| GiaChotLoi1, GiaChotLoi2 | decimal | có | `TinhKeHoach` |
| DieuKienHuy | string | có | giữ |
| HetHanVao | DateTime (UTC) | mới | giờ kết thúc phiên ngày đánh giá |
| TyLeBanChotLoi1 | decimal (0–1) | mới | cấu hình, 0.5 |
| DoiDungLoVeGiaVao | bool | mới | cấu hình, true |
| CoDungLoDuoi | bool | mới | loại kịch bản ∈ `KichBanDungLoDuoi` |
| KhoangCachDuoi | decimal? | mới | `HeSoAtrDungLoDuoi × ATR(ChuKyAtr)` lúc lập; null nếu không đuổi |
| SoPhienDungTheoThoiGian | int | mới | cấu hình, 5 |
| NguongLaiToiThieuPhanTram | decimal | mới | cấu hình, 3 |
| SoPhienToiDa | int | mới | cấu hình, 20 |
| LaDuLieuBoSung | bool | mới | false; true cho dòng migration bổ sung |
| TyLeLaiLo | decimal (tính) | có, đổi công thức | (CL1 − Max) / (Max − dừng lỗ) |
| *(C-9)* GiaMuaDiem2 | decimal? | mới | Mức điểm mua 2 chốt **lúc mua điểm 1**: Hồi hỗ trợ / Quét thanh khoản = `GiaVaoThucTe + 1×ATR(lúc mua điểm 1)`; Nổ hướng lên = `DinhHop + 0.5×ChieuCaoHop`. Null khi ≥ `GiaChotLoi1` — kế hoạch **không có điểm 2** (C-9) |
| *(C-9)* BandRongDiem2 | decimal | mới | `0.3 × ATR(lúc mua điểm 1)` — độ rộng vùng khớp điểm 2; lưu để không tính lại (FR-006) |
| *(C-3)* TyLeVonDiem1 / TyLeVonDiem2 | decimal (tính) | mới | 0.5 / 0.5 — chia vốn, chốt theo C-3 |
| *(C-11)* NguongRutMocBanNuaPhanTram | decimal | mới | 4 — cố định mọi mã (C-12), lưu vào kế hoạch để in tin mua |
| *(C-11)* NguongRutMocBanHetPhanTram | decimal | mới | 6 — cố định mọi mã (C-12) |

Ràng buộc hợp lệ: `GiaChotLoi1 > GiaVaoLenhMax` và `GiaDungLo < GiaVaoLenhMin`. *(C-9)* thêm ràng buộc: `GiaMuaDiem2 < GiaChotLoi1` (không đạt ⇒ `GiaMuaDiem2 = null`, không phải kế hoạch hỏng).

## KetQuaKichBan (bảng)

- Cột mới (Phase 2, đã commit): `GiaVaoThucTe decimal(18,2) NULL` — giá khớp lúc kích hoạt.

### Phương án lưu điểm mua 2 *(C-22 — ĐÃ CHỐT: phương án A)*

`IX_KetQuaKichBan_Symbol_LoaiKichBan_NgayDanhGia` (`ApplicationDbContext.cs:493`, `.IsUnique()`) chặn hai dòng cùng (Symbol, LoaiKichBan, NgayDanhGia). **C-22 chốt phương án A**: thêm 2 cột trên chính dòng điểm 1, **một dòng = một chu kỳ**, không đổi unique index, không tạo dòng riêng cho điểm 2.

- Cột mới: `GiaMuaDiem2ThucTe decimal(18,2) NULL` (giá khớp điểm 2), `ThoiGianMuaDiem2 datetime2 NULL` (thời điểm mua điểm 2). Null ⇒ chu kỳ mới chỉ có điểm 1.
- *(phương án B — đổi index + `LaDiemMua2` — ĐÃ BỊ BÁC theo C-22, chỉ giữ làm lịch sử quyết định.)*

- *(C-4)* **Giá bình quân** sau điểm 2: `(GiaVaoThucTe + GiaMuaDiem2ThucTe) / 2` — trung bình giản đơn vì vốn 50/50 (C-3). Không cột riêng; tính runtime.
- *(C-16)* Mỗi chu kỳ **tối đa một** lần bán nửa (bất kể nguồn: CL1 hay rút 4%); sau đó chỉ còn bán hết. **Bán nửa không ghi `LyDoThoatHet`** (cột này chỉ dành cho bán hết) — chỉ ghi `GiaBanNua`/`ThoiGianBanNua`; nguồn (CL1/RutMoc4) chỉ in tin + log, không lưu cột.
- *(C-11, C-23)* Mốc = giá cao nhất kể từ lúc mua: **không cột mới** — dùng `DinhTuLucMua`, cập nhật mỗi lượt. **C-23: mua điểm 2 xong KHÔNG tính lại mốc — `DinhTuLucMua` vẫn là đỉnh tính từ lúc mua điểm 1** (không reset). *(đề xuất)* `mốc = max(DinhTuLucMua, GiaVaoThucTe)` để chưa lên đỉnh thì mốc = giá mua.
- Không đổi: `DungLoDuoi`, `DinhTuLucMua`, `DaDoiDungLo`, `GiaBanNua`, `ThoiGianBanNua`, `GiaThoatHet`, `ThoiGianThoatHet`, `LyDoThoatHet` (MaxLength 16), `CanhBaoDaGui`, `KetQuaDoLuong`.

## SuKienBan (hằng chuỗi)

Thêm (Phase 2, đã commit): `HetHanVao` (9), `HuyNgoaiVung` (12). `KeHoachKhongHopLe` (17) chỉ là mã trong bằng chứng (>16).

*(C-10, C-11)* Hằng mới cho luật chu kỳ (đều ≤ 16 ký tự):

| Hằng | Số ký tự | Nghĩa |
|---|---|---|
| `HuyMuaLap` | 9 | huỷ dòng mua lặp trên production (C-10) |
| `RutMoc4` | 7 | bán nửa vì giảm 4% từ mốc (C-11) |
| `RutMoc6` | 7 | bán hết vì giảm 6% từ mốc (C-11) |

## Trạng thái kịch bản

```
DangHinhThanh --(cò đạt, kế hoạch hợp lệ, giá trong vùng, KHÔNG có chu kỳ mở — C-0)--> DaKichHoat [Đang giữ = điểm 1]
DangHinhThanh --(hết phiên ngày đánh giá)--> Huỷ (HetHanVao, KetQuaDoLuong=Huy)
DangHinhThanh --(mã đã có chu kỳ mở — C-0 Pha 2)--> không kích hoạt, giữ DangHinhThanh
Kiệt sức / Gãy nền --> chỉ cảnh báo, không đổi trạng thái
```

## Chu kỳ giao dịch *(C-1, C-5, C-16→C-19, C-22)* — một dòng = một chu kỳ

Một chu kỳ = một dòng KetQuaKichBan (điểm 1) + thông tin điểm 2 trên **chính dòng đó** (C-22: cột `GiaMuaDiem2ThucTe`/`ThoiGianMuaDiem2`). Tối đa 2 lần mua (C-1), tính chung mọi loại kịch bản (C-6). Điểm 2 được chờ **từ lúc mua điểm 1 cho tới khi bán 1 / cắt lỗ / bán nửa bất kỳ** (C-5 + C-17): đã bán nửa → **không** mua điểm 2 (C-17).

```
[Điểm 1 mua] (DaKichHoat, GiaVaoThucTe ghi)
  │
  ├─ chờ điểm 2 (C-5): mỗi lượt, nếu CHƯA bán nửa (C-17), kế hoạch có GiaMuaDiem2 (C-9),
  │     giá khớp ∈ [GiaMuaDiem2, GiaMuaDiem2 + BandRongDiem2] (C-9)
  │     → ghi GiaMuaDiem2ThucTe + ThoiGianMuaDiem2 trên cùng dòng
  │     → giá vào = bình quân 2 lần (C-4); BỎ CL1/CL2 (C-14)
  │     → chỉ còn: dừng lỗ ban đầu/đuổi, rút mốc 4/6, thời gian, hạn
  │
  ├─ chạm CL1 (chưa mua điểm 2) → bán nửa (TyLeBanChotLoi1, C-20: dời cắt lỗ về giá điểm 1)
  ├─ mốc giảm ≥4% → bán nửa (RutMoc4)
  │   (C-16: mỗi chu kỳ tối đa MỘT lần bán nửa, bất kể nguồn CL1 hay RutMoc4)
  │   (C-17: đã bán nửa → không mua điểm 2)
  ├─ mốc giảm ≥6% → bán hết (RutMoc6)
  ├─ chạm mức cắt hiệu lực / CL2 / hết thời gian / hết hạn → bán hết
  └─ (C-18: cùng lượt chạm điểm 2 + luật bán → luật bán thắng, không mua)
[DaThoat — bán hết theo mọi luật, kể cả hết hạn / dừng thời gian] → chu kỳ đóng (C-19)
                                                            → phiên sau đánh giá lại mã (C-7)
```

Mức cắt hiệu lực = max(GiaDungLo; giá vào thực tế/bình quân nếu DaDoiDungLo; DinhTuLucMua − KhoangCachDuoi nếu CoDungLoDuoi). Luật rút mốc 4/6% chạy **song song** với các mức này — mức nào chạm trước bán trước (C-13).

*(C-20)*: dời cắt lỗ sau chốt lời 1 về **giá điểm 1** (`GiaVaoThucTe`), không dùng bình quân (trường hợp có điểm 2 rồi CL1 đạt đã bị C-14 bỏ).

*(C-21)*: ABB/DCM sau dọn (C-10): có mức điểm 2 nếu lấy lại được ATR lúc mua điểm 1; không thì `GiaMuaDiem2 = null`.

## KetQuaDoLuong — Pha 3 chấm theo chu kỳ *(C-26, C-27 — ĐÃ CHỐT)*

Cột `KetQuaDoLuong` ("Thang"/"Thua"/"Ngang"/null) giữ nguyên kiểu; **chỉ đổi thời điểm và công thức tính**, không thêm cột.

| Luật | Nội dung |
|---|---|
| **P1 (C-27)** | Chỉ ghi `KetQuaDoLuong` khi **chu kỳ đóng** = `ThoiGianThoatHet != null` (C-19). Vị thế đang mở → `KetQuaDoLuong = null` ("đang giữ"). Bỏ cách đo một lần sau `SoNgayChoDo = 4` ngày (`Pha3DoLuongRunner.cs:25`). |
| **P2 — giá vào** | Không điểm 2: `giaVao = GiaVaoThucTe`. Có điểm 2: `giaVao = (GiaVaoThucTe + GiaMuaDiem2ThucTe) / 2` (C-4). |
| **P2 — giá ra** | Bình quân **có trọng số** các lần bán: `giaThoat = (GiaBanNua × 0.5 + GiaThoatHet × 0.5)` = `(GiaBanNua + GiaThoatHet)/2` khi `TyLeBanChotLoi1 = 0.5`. Bán hết một lần (không bán nửa): `giaThoat = GiaThoatHet`. Không còn dùng `nenCuoi.Close` cho vị thế chưa đóng. |
| **P2 — phân loại** | `p = (giaThoat − giaVao)/giaVao × 100`; `p ≥ 1` → Thang; `p ≤ −1` → Thua; còn lại Ngang. Ngưỡng ±1% (`NguongPhanTram = 1`) **giữ nguyên**. |
| **P3** | Chấm lại dòng đã đóng; dòng đang mở xoá `KetQuaDoLuong` cũ chờ đóng (code + script C-27 T051 bước 4). |

- `PhanTramLoiNhuan`, `TyLeLaiLoThucTe`, `GiaThoat`, `NgayThoat` tính theo `giaVao`/`giaThoat` mới; dùng ngày `ThoiGianThoatHet` cho `NgayThoat` thay vì ngày nến cuối.
- **C-26 → C-32 (2026-10-21)**: DGW 1472, PVS 2168 đã đóng — `KetQuaDoLuong` "Thang" cũ **không đáng tin**; trước khi chấm lại phải **sửa `GiaVaoThucTe` bằng UPDATE tay** trong script (DGW = 45.80, PVS = 33.60 — giá khớp thật trong log TRIGGERED, mua ngoài vùng), rồi bước 4 của script xóa kết quả cũ để runner C-27 chấm lại. Kỳ vọng lượt 16:00: DGW "Ngang" (45.60 so 45.80 = −0.4%), PVS "Thua" (31.50 so 33.60 = −6.25%).
- **C-24 + C-32** (cùng đợt script C-10, bước 2 = 4 dòng): sửa `GiaVaoThucTe` = giá khớp thật cho NTP 2114=52.80, QNS 5765=51.00 (đang mở) và DGW 1472=45.80, PVS 2168=33.60 (đã đóng).
