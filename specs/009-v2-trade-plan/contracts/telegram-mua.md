# Contract: tin Telegram V2

Không có API HTTP mới. Hợp đồng ra ngoài duy nhất là nội dung tin Telegram.

**C-30 (chốt 2026-10-20):** nội dung mọi tin V2 KHÔNG chứa mã quyết định nội bộ (`(C-x)`); ý câu giữ nguyên. Mã C-x chỉ dùng ngoài khối mẫu (ghi chú, tiêu đề mục).

## Tin mua điểm 1

```
🎯 <MÃ> — <kịch bản> · MUA (điểm 1)
Giá vào: <GiaVaoThucTe>  (vùng <Min>–<Max>)
Cắt lỗ: <GiaDungLo>  (−x%)
Chốt lời 1: <CL1> (+x%) — bán <TyLeBanChotLoi1>%, sau đó dời cắt lỗ về giá vào
Chốt lời 2: <CL2> (+x%) — bán phần còn lại
Điểm mua 2: <GiaMuaDiem2> (+x%) — khớp trong [mức, mức + BandRongDiem2]     (chỉ khi kế hoạch có điểm 2)
    · Vốn 50/50; sau điểm 2 bỏ chốt lời 1/2, chỉ thoát theo dừng lỗ / rút mốc / thời gian / hạn
Dừng lỗ đuổi theo: đỉnh − <KhoangCachDuoi>        (chỉ khi CoDungLoDuoi)
Rút từ mốc: −4% → bán nửa; −6% → bán hết        (mọi vị thế)
Thời gian: sau <SoPhienDungTheoThoiGian> phiên chưa lãi <Nguong>% → bán; tối đa <SoPhienToiDa> phiên
Lãi/lỗ kỳ vọng: <TyLeLaiLo>
Chưa bán được trước T+2.5
```

- % tính từ `GiaVaoThucTe`.
- Câu "sau đó dời cắt lỗ về giá vào" chỉ in khi `DoiDungLoVeGiaVao` (C-20: về **giá điểm 1**).
- Dòng "Điểm mua 2" chỉ in khi `GiaMuaDiem2 != null` (kế hoạch có điểm 2 — C-9). Khi `GiaMuaDiem2 = null` (điểm 2 ≥ CL1): in `Điểm mua 2: không (điểm 2 ≥ chốt lời 1)`. *(đề xuất — chưa chủ sản phẩm duyệt format này.)*
- Dòng "Rút từ mốc" luôn in (4/6% cố định mọi mã — C-12).

## Tin mua điểm 2 *(C-9, C-3, C-5, C-17, C-18)*

```
🎯 <MÃ> — <kịch bản> · MUA THÊM (điểm 2)
Giá khớp: <GiaMuaDiem2ThucTe>  (vùng [<GiaMuaDiem2>, <GiaMuaDiem2> + <BandRongDiem2>])
Bình quân 2 lần: <(GiaVaoThucTe + GiaMuaDiem2ThucTe)/2>
Vốn: 50% điểm 1 + 50% điểm 2
→ Bỏ chốt lời 1/2. Chỉ thoát: dừng lỗ/đuổi, rút mốc 4/6%, thời gian, hạn.
Mốc hiện tại: <DinhTuLucMua>   · Cắt hiệu lực: <mức cao nhất>
```

- Chỉ gửi khi thực sự khớp: kế hoạch có điểm 2 (`GiaMuaDiem2 != null`), CHƯA bán nửa (C-17), CHƯA có điểm 2, giá ∈ band, C-18 (luật bán không thắng lượt đó).

## Tin bán nửa *(CL1 hoặc RutMoc4 — mỗi chu kỳ tối đa một lần, C-16)*

Dùng chung format cho cả CL1 và RutMoc4:

```
🔻 <MÃ> — BÁN NỬA (<Chốt lời 1|Rút mốc 4%>)
Giá: <gia>  · Lý do: <Chốt lời 1: chạm mục tiêu <CL1> / Rút mốc 4%: mốc <DinhTuLucMua> giảm ≥ 4%>
Cắt còn lại: <mức cắt hiệu lực sau khi dời>
Mỗi chu kỳ tối đa một lần bán nửa — sau tin này chỉ còn luật bán hết.
```

- **Bán nửa KHÔNG ghi `LyDoThoatHet`** (cột này chỉ dùng khi bán HẾT). Bán nửa chỉ ghi `GiaBanNua` + `ThoiGianBanNua`.
- C-16: mỗi chu kỳ tối đa một lần bán nửa → nguồn (CL1 hay RutMoc4) **chỉ in trong tin Telegram + log**, không lưu vào cột nào.
- Sau tin này, mọi luật "bán nửa" khác bị chặn; chỉ còn bán hết.

## Tin bán hết *(RutMoc6, dừng lỗ, chốt lời 2, hết thời gian, hết hạn — C-19: đóng chu kỳ)*

```
🔴 <MÃ> — BÁN HẾT (<lý do>)
Giá: <gia>  · Lãi/lỗ: <±x%> so bình quân <giaVao>
Lý do: <RutMoc6 / DungLo / DungLoDuoi / ChotLoi2 / HetThoiGian / HetHanTheoDoi>
Kết quả: <Thắng|Thua|Ngang> (±x% so giá vào)
Chu kỳ đóng. Phiên sau sẽ đánh giá lại mã.
```

- **C-33 (chốt 2026-10-21):** dòng "Kết quả" in khi Pha 2 bán hết — chu kỳ được chấm NGAY cùng lượt đóng bằng công thức dùng chung với Pha 3 (`ChamKetQuaChuKy`); bán nửa KHÔNG in dòng này (chu kỳ còn mở). Pha 3 16:00 chỉ chấm bù dòng đã đóng mà chưa có kết quả.

- `LyDoThoatHet` dùng **đúng hằng có sẵn** trong `SuKienBan.cs`, không tự đặt tên mới:
  - `DungLo` (`LyDoDungLo`) — chạm dừng lỗ ban đầu.
  - `DungLoDuoi` (`LyDoDungLoDuoi`) — chạm dừng lỗ đuổi theo.
  - `ChotLoi2` (`LyDoChotLoi2`) — chạm chốt lời 2 (chỉ khi **chưa** mua điểm 2; C-14 bỏ CL2 sau điểm 2).
  - `HetThoiGian` (`LyDoHetThoiGian`) — dừng theo thời gian.
  - `HetHanTheoDoi` (`LyDoHetHanTheoDoi`) — hết hạn theo dõi.
  - Hằng mới (đã chốt, thêm vào `SuKienBan`): `RutMoc6` (7, bán hết vì giảm 6% từ mốc); `RutMoc4` (7, dùng cho **log/tin bán nửa**, không ghi `LyDoThoatHet`); `HuyMuaLap` (9, huỷ dòng mua lặp).

## Tin cảnh báo Kiệt sức / Gãy nền (không đổi)

```
⚠️ <MÃ> — <Kiệt sức|Gãy nền> (cảnh báo, không phải lệnh bán)
Giá: <giá khớp>  · Mức cắt hiệu lực: <mức>
```

Gửi tối đa một lần mỗi loại mỗi vị thế (khoá trong `CanhBaoDaGui`).
