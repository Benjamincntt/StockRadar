# Implementation Plan: Danh mục V3 chạy theo lệnh thật trong Nhật ký giao dịch

**Branch**: `014-v3-real-portfolio` (làm trên `master`) | **Date**: 2026-11-02 | **Spec**: [spec.md](./spec.md)

**Input**: [spec.md](./spec.md) — owner chốt hướng và C1–C3 ngày 02/11/2026.

## Summary

Thêm một bước **đối chiếu nhật ký** vào nhịp 16:00 (`V3SauPhienRunner`). Bước này đọc lệnh Nhật ký của tài khoản danh mục (`V3:TaiKhoanDanhMuc`) gắn tín hiệu V3, rồi **ghi đè** khối lượng, giá vào, giá thoát lên chính bản ghi `ViTheV3`. Mọi phép tính hiện có (tiền mặt, giá trị danh mục, cỡ lệnh sau, màn Hiệu quả) đều đọc từ `ViTheV3`, nên tự đúng theo số thật mà không phải sửa từng chỗ.

Hạn nhập (C2) được đảm bảo bằng cách **mỗi vị thế chỉ đối chiếu đúng một lần cho mỗi chiều**: phần mua ở nhịp 16:00 của phiên khớp mua, phần bán ở nhịp 16:00 của phiên khớp bán. Lệnh nhập muộn hơn thì không còn nhịp nào đọc tới nữa.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), Dart / Flutter (mobile), TypeScript / React + Vite (web)

**Primary Dependencies**: EF Core (SQL Server), không thêm thư viện mới

**Storage**: SQL Server `StockRadarDb` — thêm 3 cột vào `ViTheV3` (migration mới). `NhatKyGiaoDich`, `NhatKyTinHieuBoQua` chỉ đọc.

**Testing**: xUnit trong `StockRadar.Tests` (hiện 511 test qua); `flutter analyze`; `npm run build`

**Target Platform**: API Linux trên server production; app Android; web

**Project Type**: web-service + mobile-app + web-app (monorepo)

**Performance Goals**: nhịp 16:00 tăng ≤ 1 giây (SC-004). Danh mục tối đa 5 vị thế mở; nhật ký một tài khoản — vài trăm dòng, đọc một lần mỗi nhịp.

**Constraints**:
- Giá trong Nhật ký nhập theo **nghìn đ/cp** (`mobile/lib/screens/nhat_ky_lenh_screen.dart:226`, nhãn "Giá khớp (nghìn đ/cp)") — cùng đơn vị với `ViTheV3.GiaVao`. Không quy đổi. `Phi` trong nhật ký tính bằng đồng → **không dùng** cho V3; lãi % V3 vẫn tính theo phí cấu hình như hiện nay (`V3ChamKetQua.Cham`).
- Ngày khớp lấy từ `NhatKyGiaoDich.ThoiDiemKhop` (UTC) quy về ngày giờ Việt Nam.

**Scale/Scope**: 1 tài khoản danh mục, ≤ 5 vị thế mở, vài lệnh nhật ký mỗi ngày.

## Constitution Check

*Source: `.specify/memory/constitution.md` v1.0.1*

- [x] **I. Code là nguồn sự thật**: plan dẫn file entry cụ thể (bảng "Thay đổi theo file" bên dưới), đã đọc `V3SauPhienRunner`, `V3Engine`, `V3HieuQuaService`, `INhatKyRepository`.
- [x] **II. Spec trước**: có `spec.md` đã làm rõ C1–C3. Thêm một bước vào runner 16:00 (thứ tự job đổi) → đi đúng Spec Kit.
- [x] **III. Tối thiểu**: không đổi luật chọn mã/bán/tỉ trọng, không đổi bảng nhật ký, không thêm route mới; chỉ thêm 1 trường vào DTO vị thế đang có.
- [x] **IV. Cổng domain**: không đổi Buy Score / Top / MA·pha / flatBox. Cập nhật `docs/domain/pipeline-jobs.md` (nhịp 16:00 thêm bước) cùng change set.
- [x] **V. Đơn giản**: một hàm thuần ở Domain + một bước trong runner. Không thêm abstraction ngoài phần đó.
- [x] **Stack**: luật đối chiếu ở Domain (test được không cần DB); đọc nhật ký + ghi `ViTheV3` ở Infrastructure; DTO ở Application; nhãn ở `mobile/` + `frontend/`. Sau deploy: `backend/restart-api.ps1` (local), owner tự ship.

## Thiết kế

### D1 — Ghi đè trực tiếp `ViTheV3`, không tạo bảng danh mục mới

Spec FR-001 chốt "chỉ 1 danh mục". `V3Engine.LayTrangThaiDanhMucAsync` (cỡ lệnh) và `V3HieuQuaService.TinhDanhMucAsync` (màn Hiệu quả) đều tính tiền mặt = vốn − Σ `GiaTriLenh` (DangGiu + DaThoat) + Σ `GiaThoat × KhoiLuong` (DaThoat). Ghi đè `KhoiLuong`, `GiaVao`, `GiaTriLenh`, `GiaThoat` là đủ để mọi số đi theo thật.

### D2 — Đối chiếu một lần mỗi chiều, đúng nhịp 16:00 của phiên khớp (C2)

Ở nhịp 16:00 phiên D, với tài khoản danh mục:

| Việc | Áp cho vị thế | Nguồn nhật ký |
|---|---|---|
| **Mua** | `NgayVao == D`, trạng thái DangGiu hoặc DaThoat | lệnh `Chieu = "Mua"`, `NguonTinHieu = "V3"`, `MaTinHieu = TinHieuV3Id` |
| **Bỏ qua** | `NgayVao == D` | `NhatKyTinHieuBoQua` (`Nguon = "V3"`, `MaTinHieu = TinHieuV3Id`) |
| **Bán** | DangGiu (owner bán trước tín hiệu) hoặc `NgayThoat == D` | lệnh `Chieu = "Ban"` gắn cùng tín hiệu, `ThoiDiemKhop` (giờ VN) rơi vào ngày D |

- Không có dòng nhật ký phù hợp → **không đổi gì** (FR-006 "lấy lệnh cũ").
- Phiên sau không bao giờ đọc lại phần đã qua hạn → lệnh nhập/sửa/xoá muộn tự động không có tác dụng (FR-010). Không cần cột khoá.
- Thứ tự trong runner: chạy **trước** bước (2) tính ngưỡng bán, để ngưỡng tính theo giá vào thật.

### D3 — Luật tính (hàm thuần mới ở Domain: `V3DoiChieuNhatKy`)

Đầu vào: số đang lưu của vị thế + danh sách lệnh nhật ký khớp tín hiệu + cờ bỏ qua. Đầu ra: số mới hoặc "không đổi".

- **Mua** (FR-004): `KhoiLuong = Σ KL mua`; `GiaVao = Σ(Gia × KL) / Σ KL`; `GiaTriLenh = KhoiLuong × GiaVao`.
- **Ngưỡng bán** (FR-007): lệnh thường → `NguongChamMa` được bước (2) của runner tính lại theo `GiaVao` mới như mọi ngày; **lệnh hoảng loạn sâu** → bước đối chiếu tự đặt `NguongChamMa = GiaVao mới × HoangLoanSauChotLoi` (vì bước (2) không ghi đè lệnh này — DO-NOT-CHANGE đang có).
- **Bỏ qua** (FR-008): `TrangThai = BiHuy`, tín hiệu `BiHuy`. Tiền mặt tự trả lại vì công thức chỉ đếm DangGiu + DaThoat.
- **Bán, vị thế đã DaThoat** (FR-005): `GiaThoat = Σ(Gia × KL bán) / Σ KL bán`; tính lại `PhanTramLoiNhuan`, `KetQua` bằng `V3ChamKetQua.Cham`; chép sang `TinHieuV3` như `KhopBanAsync`. Giữ `LyDoThoat` của hệ thống.
- **Bán, vị thế còn DangGiu** (owner bán trước tín hiệu): đóng vị thế bằng đường `KhopBanAsync` với `LyDoThoat = "BanTheoNhatKy"`, ngày thoát = ngày khớp. Đặt `DaBanTinThoat = true` để nhịp mỗi phút không bắn tin bán nữa.
- **Bán một phần** (Σ KL bán < `KhoiLuong`): xem câu hỏi **P2** — bản đầu chỉ áp khi bán hết.
- Lệnh nhật ký gắn tín hiệu không có vị thế, hoặc không gắn tín hiệu V3 → bỏ qua (FR-009, C3).

### D4 — Tài khoản danh mục (C1)

Thêm `V3Options.TaiKhoanDanhMuc` (chuỗi, **mã tài khoản Guid** — owner chọn P3 = b). Runner đọc thẳng Guid, không tra bảng Users. Trống, sai dạng Guid → bỏ qua bước đối chiếu, log cảnh báo một dòng mỗi nhịp, phần còn lại của nhịp chạy bình thường.

### D5 — Đánh dấu nguồn số (FR-012, US4)

Thêm vào `ViTheV3`:

| Cột | Kiểu | Ý nghĩa |
|---|---|---|
| `DoiChieuMua` | bit, mặc định 0 | phần mua đã lấy theo nhật ký |
| `DoiChieuBan` | bit, mặc định 0 | phần bán đã lấy theo nhật ký |
| `DoiChieuLuc` | datetime2, null | lần đối chiếu gần nhất (UTC) |

`ViTheV3Dto` thêm `DoiChieuMua`, `DoiChieuBan` (thêm cuối, có giá trị mặc định → client cũ không hỏng). Mobile + web hiện nhãn "theo nhật ký" khi một trong hai cờ bật, ngược lại "theo hệ thống".

## Thay đổi theo file

| File | Thay đổi |
|---|---|
| `backend/StockRadar.Domain/Services/V3DoiChieuNhatKy.cs` (mới) | Hàm thuần D3 |
| `backend/StockRadar.Domain/Entities/V3Entities.cs` | 3 cột D5 trên `ViTheV3Entity` |
| `backend/StockRadar.Infrastructure/Migrations/<ngày>_AddViTheV3DoiChieu.cs` (mới) | Thêm 3 cột. **Đọc lại file sinh ra trước khi apply** — `dotnet ef migrations add` trong repo này hay sinh body sai |
| `backend/StockRadar.Application/Options/V3Options.cs` | `TaiKhoanDanhMuc` |
| `backend/StockRadar.Infrastructure/MarketData/V3Engine.cs` | Hàm ghi kết quả đối chiếu lên vị thế + tín hiệu (dùng lại `V3ChamKetQua`, `KhopBanAsync`) |
| `backend/StockRadar.Infrastructure/MarketData/V3SauPhienRunner.cs` | Bước đối chiếu trước bước (2); đọc `INhatKyRepository.LayToanBoLenhAsync` + `LayBoQuaTheoNguonAsync(userId, "V3")`; log số vị thế đã đối chiếu |
| `backend/StockRadar.Application/DTOs/V3Dtos.cs`, `V3HieuQuaService.ToDto` | 2 cờ D5 |
| `mobile/lib/screens/hieu_qua_screen.dart` | Parse 2 cờ, nhãn nguồn trên thẻ vị thế |
| `mobile/lib/core/utils/v3_parsers.dart` | Nhãn lý do thoát `BanTheoNhatKy` → "Bán theo nhật ký" |
| `frontend/src/types/index.ts`, `frontend/src/pages/HieuQuaPage.tsx` | 2 cờ + nhãn nguồn, nhãn lý do thoát mới |
| `backend/StockRadar.Tests/V3/V3DoiChieuNhatKyTests.cs` (mới) | Test hàm thuần |
| `docs/domain/pipeline-jobs.md`, `CLAUDE.md` (dòng V3 16:00) | Nhịp 16:00 thêm bước đối chiếu |
| `specs/012-v3-scenario-engine/spec.md` Phụ lục D | Ghi bước mới của nhịp 16:00, trỏ sang spec 014 |

Không đổi: luật chọn mã, luật bán realtime, tỉ trọng, tin Telegram/đẩy app, bảng và API Nhật ký.

## Kiểm thử

**Unit (hàm thuần `V3DoiChieuNhatKy`)** — dữ liệu bịa có ghi rõ:
1. Không có nhật ký → không đổi.
2. Một lệnh mua 100 cp × 53,4 thay 200 cp × 53,2 → KL 100, giá 53,4, giá trị 5.340.
3. Hai lệnh mua 60 × 53,0 + 40 × 54,0 → KL 100, giá 53,4.
4. Lệnh hoảng loạn sâu → ngưỡng = giá vào mới × 1,15.
5. Bỏ qua → huỷ.
6. Bán hết khi vị thế DaThoat → giá thoát, lãi %, kết quả tính lại.
7. Bán hết khi vị thế DangGiu → đóng với `BanTheoNhatKy`.
8. Lệnh khớp ngày khác ngày đang đối chiếu → không tính (kiểm hạn C2).
9. Lệnh gắn tín hiệu khác / không gắn tín hiệu → không tính.

**Tích hợp (DB local)**: chèn 1 vị thế + 1 lệnh nhật ký cho tài khoản cấu hình, gọi `POST /api/v1/market/jobs/v3-sau-phien` (`MarketJobsController`), kiểm `ViTheV3` và `GET /v3/hieu-qua/tom-tat` (tiền mặt khớp SC-001). Dọn dữ liệu thử sau khi kiểm.

**Chạy thật**: sau ship, nhịp 16:00 đầu tiên — đọc log "đối chiếu nhật ký: N vị thế", so tiền mặt trên app với tay tính.

## Đã chốt (owner, 02/11/2026)

- **P1 — Vị thế FPT hiện tại**: owner: "bạn tự sửa dữ liệu fpt db server luôn" → **không** làm lần bù trong code; AI sửa tay dòng FPT trên DB production (một lần, owner duyệt câu SQL trước khi chạy).
- **P2 — Bán một phần**: phương án (a) — bản đầu **chỉ áp khi bán hết**; bán một phần thì log cảnh báo, giữ số hệ thống.
- **P3 — Tài khoản danh mục**: phương án (b) — ghi bằng **mã tài khoản (Guid)** trong `V3:TaiKhoanDanhMuc`.

## Rủi ro

| Rủi ro | Cách chặn |
|---|---|
| Nhập sai mã tín hiệu trong nhật ký → ghi đè nhầm vị thế | Chỉ áp khi mã CK của lệnh trùng `ViTheV3.Symbol`; lệch thì bỏ qua và log |
| Chưa cấu hình `V3:TaiKhoanDanhMuc` trên server (cấu hình server khác bản repo) | Bỏ qua bước, log cảnh báo; owner đặt trong `appsettings.Production.json` **trên server** |
| Owner bán thật trước tín hiệu, nhịp mỗi phút cùng ngày vẫn bắn tin bán (đối chiếu chạy 16:00) | Chấp nhận: tin bán chỉ là chỉ dẫn; 16:00 đóng vị thế theo nhật ký |
| Migration sinh body sai | Đọc lại file migration trước khi apply (ghi chú trong bảng file) |

## Complexity Tracking

Không có vi phạm hiến pháp cần biện minh.
