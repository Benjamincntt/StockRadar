# Phase 1 — Data Model

Không thêm bảng SQL. Thực thể dưới đây là bản ghi trong bộ nhớ + file seed.

## 1. `SuKienQuyen`

Một đợt không hưởng quyền của **một mã**. Không có trường loại.

| Trường | Kiểu | Bắt buộc | Luật |
|--------|------|----------|------|
| `ma` | string | có | Trim, so khớp không phân biệt hoa thường với `Stock.Symbol` |
| `ngayKhongHuongQuyen` | date | có | Ngày nến GDKHQ (nến này đã là thang sau quyền) |
| `tienMat` | decimal | không | Mặc định 0. Cùng thang Close. 1.000đ = **1.0**. Không âm |
| `heSoPhaLoang` | decimal | không | Mặc định 1. Phải > 0. Thưởng 5:1 = **1.2** |

**Từ chối cả dòng** (không áp dụng) nếu thiếu `ma` hoặc `ngayKhongHuongQuyen`, hoặc `heSoPhaLoang` ≤ 0, hoặc không tìm được `giaTruocQuyen` > 0 trên dãy thô.

**Quan hệ**: N mã × M sự kiện. Cùng mã + cùng ngày: **một** bản ghi (gộp tiền + pha loãng), không hai dòng cộng chồng.

## 2. `OhlcvBar` (thô vs chấm điểm)

Thực thể hiện có, không thêm field.

| Dãy | Nguồn | OHLC | Volume | Persist |
|-----|-------|------|--------|---------|
| Thô | `EntityMapper.ToDomain` / Job 2 | Khớp lệnh | Khớp lệnh | Có (`HistoryJson`) |
| Chấm điểm | `TaoDayGiaDieuChinh` | Nhân hệ số lũy kế | Nguyên | **Không** |

Nến cuối dãy chấm điểm: OHLC **trùng** nến thô.

## 3. Hệ số (tính, không lưu)

Với sự kiện i, `giaTruocQuyen` = Close **thô** của nến mới nhất có `Date < ngayKhongHuongQuyen`.

- `giaThamChieu = (giaTruocQuyen − tienMat) / heSoPhaLoang`
- `heSoNgayQuyen = giaThamChieu / giaTruocQuyen`

Nến ngày D: nhân tích `heSoNgayQuyen` của mọi sự kiện có `ngayKhongHuongQuyen > D`.

SSI 17/08/2026, `giaTruocQuyen` = 24.5: `giaThamChieu` ≈ 19.58, `heSoNgayQuyen` ≈ 0.799.

## 4. File seed

Một document JSON, git-versioned. Không state machine. Ops sửa file → ship/restart. Không CRUD runtime.

## 5. Ảnh hưởng thực thể hiện có

| Thực thể | Đổi schema? | Ghi chú |
|----------|-------------|---------|
| `Stock` / `HistoryJson` | không | Vẫn thô |
| `MarketIndex` | không | Không seed |
| `DailyOpportunities` | không | Điểm/RS đổi vì input % sạch sau land |
| `FlatBoxProfile` | không | Tính trên dãy chấm điểm khi gọi từ Evaluate/detail engine |
