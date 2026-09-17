# Phase 1 — Data Model

Không thêm bảng SQL. Mở rộng bản ghi `SuKienQuyen` trong file seed.

## 1. `SuKienQuyen`

| Trường | Kiểu | Bắt buộc | Luật |
|--------|------|----------|------|
| `ma` | string | có | Trim, không phân biệt hoa thường |
| `ngayKhongHuongQuyen` | date | có | GDKHQ (không ĐKCC / thanh toán) |
| `tienMat` | decimal | không | Mặc định 0. 400đ = **0.4**. Không âm. ≥ 100 từ chối khi thêm qua dịch vụ |
| `heSoPhaLoang` | decimal | không | Mặc định 1. Phải > 0 |
| `soCoCu` | int | không | Mặc định 0. Số cổ cũ trong tỷ lệ n:m |
| `soCoMoi` | int | không | Mặc định 0. Số cổ mới được mua |
| `giaPhatHanh` | decimal | không | Mặc định 0. 10.000đ = **10.0**. Không âm |

**Tính**: `tyLeQuyenMua` = `soCoMoi / soCoCu` khi cả hai > 0, ngược lại 0.

**Từ chối dòng** nếu thiếu `ma`/`ngayKhongHuongQuyen`, `heSoPhaLoang` ≤ 0, `soCoMoi` > 0 nhưng `soCoCu` ≤ 0, hoặc `giaPhatHanh` < 0.

**Cùng mã + cùng ngày**: một bản ghi (tiền + thưởng + quyền mua), replace khi thêm lại.

## 2. Hệ số (tính, không lưu)

`giaTruocQuyen` = Close thô nến mới nhất `Date < ngayKhongHuongQuyen`.

- `giaThamChieu = (giaTruocQuyen − tienMat + tyLeQuyenMua × giaPhatHanh) / (heSoPhaLoang + tyLeQuyenMua)`
- `heSoNgayQuyen = giaThamChieu / giaTruocQuyen`

HCM 16/07, P = 26.95: `giaThamChieu` = 23.24, `heSoNgayQuyen` ≈ 0.862.

SSI 17/08, `tyLe` = 0: không đổi 005.

## 3. Ảnh hưởng thực thể hiện có

| Thực thể | Đổi schema? | Ghi chú |
|----------|-------------|---------|
| `OhlcvBar` / `HistoryJson` | không | Vẫn thô |
| `DailyOpportunities` | không | RS/FOMO đổi vì input sạch |
| DTO `rights-events` | additive | Field mới optional; client cũ bỏ qua |
