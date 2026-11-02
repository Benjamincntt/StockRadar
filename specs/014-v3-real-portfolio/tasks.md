# Tasks: Danh mục V3 chạy theo lệnh thật trong Nhật ký giao dịch

**Input**: [spec.md](./spec.md), [plan.md](./plan.md)

**Đã chốt P1–P3** (owner, 02/11/2026 — xem plan mục "Đã chốt"): P1 sửa tay FPT trên DB production, không làm lần bù trong code; P2 = chỉ áp khi bán hết; P3 = Guid.

`[P]` = làm song song được (khác file, không phụ thuộc nhau).

## Phase 1 — Nền (chặn mọi story)

- [x] T001 Thêm `DoiChieuMua`, `DoiChieuBan`, `DoiChieuLuc` vào `ViTheV3Entity` (`backend/StockRadar.Domain/Entities/V3Entities.cs`) và cấu hình EF (`ApplicationDbContext`).
- [x] T002 Tạo migration `AddViTheV3DoiChieu`. **Đọc lại toàn bộ file sinh ra**, chỉ giữ 3 lệnh `AddColumn` (mặc định `false` cho 2 cột bit); sai thì sửa tay trước khi apply. Apply trên DB local.
- [x] T003 [P3] Thêm `TaiKhoanDanhMuc` (mã tài khoản Guid) vào `V3Options` (`backend/StockRadar.Application/Options/V3Options.cs`) và vào `appsettings.json` với giá trị trống.

## Phase 2 — US1 + US2 + US3: đối chiếu ở nhịp 16:00 (P1 của spec)

- [x] T004 [P] Viết hàm thuần `V3DoiChieuNhatKy` (`backend/StockRadar.Domain/Services/V3DoiChieuNhatKy.cs`) theo plan D3: mua, bỏ qua, bán hết khi DaThoat, bán hết khi DangGiu, lọc theo ngày khớp và mã tín hiệu + mã CK.
- [x] T005 [P2] Bán một phần: hàm trả "không áp + lý do" để runner log cảnh báo.
- [x] T006 [P] Test `backend/StockRadar.Tests/V3/V3DoiChieuNhatKyTests.cs`: 9 ca trong plan mục "Kiểm thử" + ca bán một phần + ca mã CK lệch.
- [x] T007 Thêm vào `V3Engine` hàm ghi kết quả đối chiếu lên `ViTheV3` + `TinHieuV3` (mua: KL/giá/giá trị lệnh + ngưỡng hoảng loạn; bỏ qua: `BiHuy`; bán: dùng `V3ChamKetQua.Cham`, đóng vị thế qua `KhopBanAsync` với `BanTheoNhatKy`). Bật cờ D5, ghi `DoiChieuLuc`.
- [x] T008 Thêm bước đối chiếu vào `V3SauPhienRunner.ChayBenTrongAsync`, **sau** huỷ chờ mua và **trước** vòng tính ngưỡng bán: đọc `UserId` (Guid) từ `V3:TaiKhoanDanhMuc`; đọc `INhatKyRepository.LayToanBoLenhAsync` + `LayBoQuaTheoNguonAsync(userId, "V3")`; lấy thêm vị thế `DaThoat` có `NgayThoat == homNay`; log "đối chiếu nhật ký: N vị thế". Thiếu cấu hình → log cảnh báo, bỏ bước.
- [x] T009 [P1] ~~Lần bù đầu tiên trong code~~ — bỏ: owner chọn sửa tay dòng FPT trên DB production (làm riêng, ngoài code).
- [x] T010 Test tích hợp trên DB local: chèn vị thế + lệnh nhật ký thử (gắn nhãn dữ liệu thử), gọi `POST /api/v1/market/jobs/v3-sau-phien`, kiểm `ViTheV3` và `GET /api/v1/v3/hieu-qua/tom-tat`; **dọn dữ liệu thử**, đếm lại = 0.

## Phase 3 — US4: nhãn nguồn số

- [x] T011 `ViTheV3Dto` thêm `DoiChieuMua`, `DoiChieuBan` (cuối record, mặc định `false`); `V3HieuQuaService.ToDto` truyền vào.
- [x] T012 [P] Mobile `hieu_qua_screen.dart`: parse 2 cờ, nhãn "theo nhật ký" / "theo hệ thống" trên thẻ vị thế; `v3_parsers.dart` thêm nhãn `BanTheoNhatKy`. `flutter analyze` sạch.
- [x] T013 [P] Web `types/index.ts` + `HieuQuaPage.tsx`: 2 cờ, nhãn nguồn, nhãn lý do thoát mới. `npm run build` qua.

## Phase 4 — Tài liệu và kiểm cuối

- [x] T014 Cập nhật `docs/domain/pipeline-jobs.md`, dòng V3 trong `CLAUDE.md`, Phụ lục D spec 012: nhịp 16:00 thêm bước đối chiếu nhật ký (trỏ spec 014).
- [x] T015 Chạy toàn bộ `dotnet test` (đang 511 qua; số mới = 511 + test thêm), báo số pass.
- [ ] T016 Ghi vào báo cáo cho owner: việc owner phải làm trên server — đặt `V3:TaiKhoanDanhMuc` (Guid tài khoản) trong `appsettings.Production.json` **trên server**.

## Phụ thuộc

T001 → T002 → T007 → T008 → T009 → T010. T004/T006 chạy song song với T001–T003. T011 → T012, T013. T014–T016 sau cùng.

Không commit, không push, không ship — owner tự chạy sau khi xem báo cáo.
