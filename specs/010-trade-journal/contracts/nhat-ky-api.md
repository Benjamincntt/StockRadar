# API Contract: `/api/v1/nhat-ky` (Nhật ký giao dịch thật)

**Branch**: `010-trade-journal` | **Date**: 2026-10-21 | **Trạng thái**: ĐỀ XUẤT (chờ duyệt Q1–Q7 trong plan.md)

Controller mới `NhatKyController` (`[Route("api/v1/nhat-ky")]`, `[Authorize]` — bắt buộc đăng nhập, khác pattern guest của `WatchlistsController`; xem research D1). JSON camelCase theo mặc định toàn app. **Toàn bộ endpoint là mới; không endpoint/DTO hiện có nào đổi.**

## Quy ước chung

- **Cách ly user (N6/FR-009)**: `UserId` luôn lấy từ JWT (`ICurrentUserService`), không bao giờ từ body/query. mọi query lọc theo user hiện tại. Truy vấn/xoá/sửa nguồn không thuộc user → **404** (không lộ sự tồn tại).
- **Timezone**: mọi `DateTime` ISO-8601 UTC (convention repo); mobile hiển thị giờ VN (plan Q6).
- **Tiền**: `decimal`, VND; giá tính theo **1 cổ phiếu** (scale như bảng `Stocks`: decimal(18,2)).
- **Lỗi chuẩn**: 400 validation (`{"detail": "..."}`), 401 chưa đăng nhập, 404 không tồn tại/không thuộc user, 409 trùng khoá tự nhiên, 422 nghiệp vụ (bán quá vị thế).

## Tài nguyên

### 1. Lệnh

| Method | Path | Mô tả |
|---|---|---|
| `GET` | `/api/v1/nhat-ky/lenh` | Danh sách lệnh của user, phân trang, lọc. |
| `POST` | `/api/v1/nhat-ky/lenh` | Thêm lệnh. |
| `GET` | `/api/v1/nhat-ky/lenh/{id}` | Một lệnh (kèm kết quả tín hiệu song song nếu gắn tín hiệu). |
| `PUT` | `/api/v1/nhat-ky/lenh/{id}` | Sửa lệnh (toàn bộ trường; recompute tự nhiên vì projection on-the-fly). |
| `DELETE` | `/api/v1/nhat-ky/lenh/{id}` | Xoá lệnh. |

**`GET /lenh` query**: `symbol` (tuỳ chọn), `tu` / `den` (DateOnly VN), `loaiLenh` (`Mua|Ban|DieuChinh`), `page=1`, `size=50` (max 200). Trả mới nhất trước (`ThoiDiemKhop desc`).

`200 OK`:
```json
{
  "items": [
    {
      "id": 12,
      "symbol": "HPG",
      "loaiLenh": "Mua",
      "thoiDiemKhop": "2026-10-21T02:31:00Z",
      "giaKhop": 27.5,
      "khoiLuong": 1000,
      "phi": 41250,
      "nguonTinHieu": "V2",
      "maTinHieu": "341",
      "giaTinHieuChup": 27.3,
      "lechSoTinHieu": 0.2,
      "lechSoTinHieuPhanTram": 0.73,
      "lyDo": "Breakout",
      "ghiChu": null,
      "kLieuChinh": null,
      "vonDieuChinh": null,
      "createdAt": "2026-10-21T02:33:10Z"
    }
  ],
  "totalCount": 57, "page": 1, "pageSize": 50
}
```
(`lechSoTinHieu*` server tính khi trả về = `giaKhop − giaTinHieuChup`; null với lệnh tự do hoặc tín hiệu chưa có giá ở bậc tương ứng (N8). Nhiều lệnh cùng tín hiệu: BQ chỉ gộp lệnh **cùng chiều và cùng mức giá chụp** (N8). Khung phân trang giống `HieuQuaController.LichSuResponse`.)

**`POST /lenh` / `PUT /lenh/{id}` body** (`TaoLenhRequest` — không có `userId`, không có `giaTinHieuChup`):
```json
{
  "symbol": "HPG",
  "loaiLenh": "Mua",
  "thoiDiemKhop": "2026-10-21T02:31:00Z",
  "giaKhop": 27.5,
  "khoiLuong": 1000,
  "phi": 41250,
  "nguonTinHieu": "V2",
  "maTinHieu": "341",
  "lyDo": "Breakout",
  "ghiChu": "mua thêm nửa position",
  "kLieuChinh": null,
  "vonDieuChinh": null
}
```
Quy tắc theo `loaiLenh`:
- `Mua`/`Ban`: `giaKhop > 0`, `khoiLuong > 0`; `phi` = tiền phí+thuế thực, null → dùng % config.
- `DieuChinh`: `giaKhop`/`khoiLuong`/`phi` phải null; `ghiChu` bắt buộc; `kLieuChinh` (±) và/hoặc `vonDieuChinh` (± VND/cp) khác 0.
- Có `nguonTinHieu` + `maTinHieu`: server kiểm tín hiệu tồn tại **và** tự chụp `giaTinHieuChup` theo **N8 (chiều + thứ tự lệnh trong chu kỳ)**: V2 mua1 = `GiaVaoThucTe ?? KeHoach.GiaVaoLenhMin`, mua2 = `GiaMuaDiem2ThucTe`; bán đầu = `GiaBanNua` (không có thì `GiaThoatHet`), bán sau = `GiaThoatHet`. VIP mua1 = `EntryPrice` (mua2: không có cột giá → null), bán = `SellPrice` của `PositionSellLegs` (`BanNua` rồi `BanHet`). Bậc chưa có giá → `giaTinHieuChup = null`, lệch trống. Thêm/sửa/xoá làm đổi thứ tự → chụp lại lệnh bị dịch bậc; tín hiệu bị xoá → giữ ảnh chụp. Client không được gửi giá chụp (data-model validation #4).

`201 Created` (body = lệnh đã lưu, Location `/api/v1/nhat-ky/lenh/{id}`); `200 OK` cho PUT.
Lỗi: `400` thiếu `giaKhop`/`khoiLuong` (AC2 US1), `404` tín hiệu không tồn tại, `422` bán quá vị thế / bán khi chưa có mua / điều chỉnh làm tồn kho âm — body `{"detail": "Đang giữ 500 HPG, không thể bán 800.", "khoLuongHienCo": 500}` (AC4 US1; đề xuất cứng — plan Q7). `DELETE` → `204`.

### 2. Đánh dấu tín hiệu bỏ qua / không khớp (FR-003)

| Method | Path | Mô tả |
|---|---|---|
| `PUT` | `/api/v1/nhat-ky/tin-hieu/{nguon}/{maTinHieu}/bo-qua` | Đánh dấu bỏ qua (idempotent upsert). Body: `{"lyDoBoQua": "nhảy quá xa"}` (optional). |
| `DELETE` | `/api/v1/nhat-ky/tin-hieu/{nguon}/{maTinHieu}/bo-qua` | Bỏ đánh dấu (khi user đổi ý ghi lệnh thật). `204` kể cả khi chưa đánh dấu. |

`{nguon}` ∈ `v2|vip`. POST lệnh gắn cùng tín hiệu **tự xoá** đánh dấu bỏ qua (data-model note). `404` nếu tín hiệu không tồn tại trong bảng nguồn.

### 3. Picker tín hiệu (N1 — chỉ đọc)

| Method | Path | Mô tả |
|---|---|---|
| `GET` | `/api/v1/nhat-ky/tin-hieu/v2?den=&limit=30` | Kịch bản V2 `TrangThai = DaKichHoat`, mới nhất trước. |
| `GET` | `/api/v1/nhat-ky/tin-hieu/vip?den=&limit=30` | Vị thế VIP từ `MasterAlertPositions` (mới nhất trước) — **đề xuất theo plan Q2** vì API V1 cũ đã bị xoá ở `183adb5`. |

Item V2:
```json
{
  "nguon": "V2", "maTinHieu": "341", "symbol": "HPG",
  "loaiKichBan": "NoHuongLen", "ngayDanhGia": "2026-10-21",
  "giaTinHieu": 27.3,
  "keHoach": { "giaVaoMin": 27.1, "giaVaoMax": 27.6, "dungLo": 25.9, "chotLoi1": 30.0, "chotLoi2": 32.5, "giaMuaDiem2": 26.5 },
  "ketQuaTinHieu": "Thắng",
  "daCoLenh": true, "daBoQua": false
}
```
(`giaTinHieu` = `GiaVaoThucTe ?? KeHoach.GiaVaoLenhMin` — cách chọn theo `LichSuLenhDto.GiaVao`, HieuQuaDtos.cs:44; `ketQuaTinHieu` = `KetQuaDoLuong` (V2) / `RealizedReturnOnDeployedPercent` (VIP, field `ketQuaTinHieuPhanTram`); `keHoach` trích từ `KeHoachGiaoDichJson` — form điền sẵn theo N1.)

### 4. Vị thế theo mã (projection — FR-004, US3 mở rộng)

`GET /api/v1/nhat-ky/vi-the` → `200 OK`:
```json
[
  {
    "symbol": "HPG",
    "khoiLuongMo": 1000,
    "giaVonBinhQuan": 27.5413,
    "tienVonDangKe": 27541300,
    "ngayMo": "2026-10-21",
    "soLenhDaBan": 0,
    "nguonTinHieuGan": "V2",
    "maTinHieuGan": "341",
    "daDong": false,
    "ngayDong": null,
    "returnOnDeployedPercent": null,
    "loiNhuanRongVND": null,
    "nhipBan": []
  }
]
```
US3: gồm cả vị thế **đã đóng** (`daDong = true`, `khoiLuongMo = 0`) — trừ nhóm vừa đóng hết vừa không từng bán (không có gì để hiển thị). `giaVonBinhQuan` đã bao gồm phí mua hiệu dụng (data-model §Bảng 3); khi đã đóng thì BQ/tiền vốn = 0. `loiNhuanRongVND` + `returnOnDeployedPercent` = lãi/lỗ **đã thực hiện** trên các nhịp bán (kể cả vị thế còn mở một phần), tính bằng `RealizedPnlMath.NetLegReturnPercent` trên giá vốn BQ tại nhịp — phí mua đã nằm trong giá vốn nên chỉ trừ phí+thuế bán. `nhipBan[]`: `{ngay, giaBan, khoiLuong, giaVonTaiNhip, laiRongPhanTram}`. Phí bán: `phi` theo lệnh nếu người dùng nhập, ngược lại cấu hình `RealizedPnl` (SellFeePercent + SellTaxPercent).

### 5. Tổng hợp tài khoản thật (FR-006)

`GET /api/v1/nhat-ky/thong-ke?ky=month` (`ky` ∈ `week|month|quarter|all`, mặc định `month` — theo convention `HieuQuaController.cs:20-24`, mốc tương đối −7/−30/−90 ngày) → `200 OK`:
```json
{
  "viThe": { "tang": 12, "thua": 4, "ngang": 1, "choDo": 3, "tyLeThang": 70.6 },
  "soLenh": 57,
  "tongLoiNhuanRongVND": 18300000,
  "tongDoLosuDungVND": 259000000,
  "nguongThangPhanTram": 0,
  "tyLeKhopTinHieu": 62.5,
  "tinHieuCoLenh": 15, "tinHieuBoQua": 9,
  "lechTBSoTinHieuPhanTram": 0.41,
  "lechTBMuaPhanTram": 0.73,
  "lechTBBanPhanTram": -0.35,
  "theoKy": [
    { "kyBatDau": "2026-10-01", "thang": 5, "thua": 2, "tongLoiNhuanRongVND": 9200000 }
  ]
}
```
Định nghĩa từng số: data-model §Thang đo. **Thắng = lãi ròng `%` > `nguongThangPhanTram`** (cấu hình `RealizedPnl.WinThresholdPercent`, mặc định 0 — nêu rõ trong response); phân loại qua `RealizedPnlMath.Classify`. Tỷ lệ thắng chỉ đếm **vị thế đã đóng** (loại `choDo`), mẫu số = tang+thua+ngang. "Vị thế đã đóng trong kỳ" theo **ngày nhịp bán cuối** — khác lựa chọn group theo EntryDate của V1 (G-RPNL-2 docs realized-pnl) vì tài khoản thật quan tâm lúc chốt lời; ghi thành note, có thể đổi khi tasks. `tongLoiNhuanRongVND` / `tongDoLosuDungVND` tính theo **nhịp bán có ngày trong kỳ** (kể cả nhịp đã thực hiện của vị thế còn mở). `tyLeKhopTinHieu` = tín hiệu có lệnh / (có lệnh + bỏ qua) trong kỳ, null nếu mẫu số 0. `lechTB*` = trung bình `lechSoTinHieuPhanTram` từng lệnh (mua và bán tách riêng, loại DieuChinh). `theoKy` bucket theo tháng của nhịp bán cuối.

`GET /lenh` (mục 1): từ US3, mỗi lệnh gắn tín hiệu đã đóng trả kèm `ketQuaTinHieu` (V2: `KetQuaDoLuong` "Thang/Thua/Ngang"; VIP: null) và `ketQuaTinHieuPhanTram` (V2: `PhanTramLoiNhuan`; VIP: `RealizedReturnOnDeployedPercent`) — để so cạnh lệch/kết quả thật ngay trên dòng lệnh.

## Mã lỗi tổng hợp (endpoint này)

| HTTP | Khi nào | Ví dụ payload |
|---|---|---|
| 400 | Validation cơ bản fail (thiếu giá/KL, sai `loaiLenh`, thời điểm tương lai) | `{"detail": "Thiếu giá khớp."}` |
| 401 | Không có/không hợp lệ JWT (controller `[Authorize]`) | — |
| 404 | id không tồn tại **hoặc** thuộc user khác **hoặc** tín hiệu nguồn không tồn tại | — |
| 409 | Vi phạm unique (vd. hai đánh dấu bỏ qua cùng tín hiệu — race) | `{"detail": "Tín hiệu đã được đánh dấu bỏ qua."}` |
| 422 | Nghiệp vụ: bán quá/ khi chưa có vị thế; điều chỉnh làm tồn kho âm | `{"detail": "...", "khoLuongHienCo": 500}` |

## Ràng buộc tương thích (FR-007/N4)

- Không sửa 17 controller/DTO hiện có; client cũ không ảnh hưởng.
- Web sau này dùng đúng bộ endpoint này (API đủ — N4); mobile là khách hàng đầu tiên.
- Endpoint V2/VIP picker chỉ `SELECT` từ `KetQuaKichBan`/`MasterAlertPositions`/`PositionSellLegs` qua repository **mới**; không đụng `IHieuQuaKichBanService`, `RealizedPnlService`, runners.
