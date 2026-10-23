# Tasks: V2 — Kế hoạch giao dịch đầy đủ trước khi mua + chuẩn hoá cò kích hoạt

**Input**: `specs/009-v2-trade-plan/` (spec, plan, research, data-model, contracts, quickstart)

**Tests**: có — spec và quickstart yêu cầu test cho từng story. Test luồng Pha 2 phải gọi qua `Pha2TrongPhienRunner.RunAsync`, không chỉ hàm con.

Đường dẫn tính từ repo root. Mỗi task kết thúc bằng build pass; test chạy cuối mỗi phase.

## Phase 1: Setup (bước 0 — kiểm, không sửa code)

- [x] T001 Kiểm nến hôm nay có được ghi vào `Stock.HistoryJson` trong 09:00–14:45 không (đọc `backend/StockRadar.Infrastructure/MarketData/EfMarketDataWriter.cs` và các runner gọi nó, đối chiếu log production); ghi kết quả vào `specs/009-v2-trade-plan/research.md` mục R8
- [x] T002 Kiểm đơn vị khối lượng lịch sử vs KBS `SessionVolume`; ghi vào `specs/009-v2-trade-plan/research.md` mục R8. **Khác đơn vị → dừng và hỏi chủ sản phẩm**
- [x] T003 Chạy toàn bộ `dotnet test backend/StockRadar.Tests` lấy mốc số test pass trước khi sửa

## Phase 2: Foundational (chặn mọi story)

- [x] T004 Thêm trường mới vào record trong `backend/StockRadar.Domain/ValueObjects/KeHoachGiaoDich.cs` (HetHanVao, TyLeBanChotLoi1, DoiDungLoVeGiaVao, CoDungLoDuoi, KhoangCachDuoi, SoPhienDungTheoThoiGian, NguongLaiToiThieuPhanTram, SoPhienToiDa, LaDuLieuBoSung) theo `data-model.md`; đổi `TyLeLaiLo` sang dùng `GiaVaoLenhMax`
- [x] T005 [P] Thêm hằng `HetHanVao`, `HuyNgoaiVung` vào `backend/StockRadar.Domain/Constants/SuKienBan.cs`
- [x] T006 [P] Thêm `GiaVaoThucTe decimal?` vào `backend/StockRadar.Domain/Entities/KetQuaKichBanEntity.cs` và cấu hình precision (18,2) trong `backend/StockRadar.Infrastructure/Persistence/ApplicationDbContext.cs`
- [x] T007 Tạo migration thêm cột `GiaVaoThucTe` trong `backend/StockRadar.Infrastructure/Migrations/` (sau `20261009031049_AddSellTrailingStopFields`, theo thư mục migration thật trên disk — không phải Persistence/Migrations); **đọc lại body sinh ra** trước khi dùng (repo hay sinh sai)
- [x] T008 Trong cùng migration T007: SQL bổ sung cho vị thế đang mở (đã kích hoạt, `ThoiGianThoatHet` null): `GiaVaoThucTe = GiaVaoLenhMin` từ JSON; chèn trường mặc định vào `KeHoachGiaoDichJson` với `LaDuLieuBoSung = true`; `KhoangCachDuoi = DinhTuLucMua − DungLoDuoi` nếu có, ngược lại null (research R7). Không đụng dòng DCS 3275
- [x] T009 Kiểm `TyLeLaiLo` đổi công thức có ai đọc ngoài hiển thị (`HieuQuaKichBanService`, `XepHangCoHoiService`); ghi kết quả, sửa test hiện có nếu kỳ vọng đổi trong `backend/StockRadar.Tests/KichBan/`

**Checkpoint**: build pass, toàn bộ test cũ pass (trừ test `TyLeLaiLo` đã ghi rõ lý do).

## Phase 3: US1 — Chỉ mua khi giá nằm trong vùng vào (P1) 🎯 MVP

**Goal**: không còn mua ngoài vùng / với kế hoạch không có đường ra; huỷ cuối phiên.

**Independent Test**: test DCS (vùng 607.66–613.77, CL1 700, giá 700) không kích hoạt; về 610 lượt sau thì kích hoạt.

- [x] T010 [P] [US1] Test trong `backend/StockRadar.Tests/KichBan/Pha2TrongPhienTests.cs`: DCS ngoài vùng → không kích hoạt, vẫn đang hình thành, không gửi tin; lượt sau trong vùng → kích hoạt; giá bằng biên → kích hoạt
- [x] T011 [P] [US1] Test: kế hoạch CL1 ≤ Max hoặc dừng lỗ ≥ Min → không kích hoạt, bằng chứng có mã `KeHoachKhongHopLe`, lượt sau kế hoạch hợp lệ → kích hoạt được
- [x] T012 [P] [US1] Test: Nổ hướng lên với kế hoạch hợp lệ vẫn kích hoạt (không bị kiểm vùng chặn)
- [x] T013 [P] [US1] Test trong `backend/StockRadar.Tests/KichBan/Pha3DoLuongTests.cs`: dòng đang hình thành của hôm nay → Pha 3 đánh dấu `LyDoThoatHet = HetHanVao`, `KetQuaDoLuong = Huy`; dòng ngày cũ không động
- [x] T014 [US1] Trong `backend/StockRadar.Application/Services/MayNhanKichBanService.cs` (`KiemTraTriggerTrongPhienAsync`, sau `TinhKeHoach` ~dòng 130): kiểm kế hoạch hợp lệ rồi kiểm giá trong vùng; không đạt → không kích hoạt, ghi bằng chứng / log Debug; comment `BUSINESS-RULE` một dòng (sự cố DCS 09/10)
- [x] T015 [US1] Trong `backend/StockRadar.Infrastructure/MarketData/Pha3DoLuongRunner.cs`: huỷ dòng đang hình thành của ngày đánh giá với `HetHanVao` (research R4)

**Checkpoint**: T010–T013 pass + hồi quy.

## Phase 4: US2 — Kế hoạch đầy đủ chốt lúc mua, in trong tin mua (P1)

**Independent Test**: kích hoạt kịch bản theo đà, đọc JSON kế hoạch và nội dung tin; đổi ATR phiên sau → kế hoạch không đổi.

- [x] T016 [P] [US2] Test trong `Pha2TrongPhienTests.cs`: sau kích hoạt, JSON có đủ trường; `GiaVaoThucTe` = giá khớp; `KhoangCachDuoi = 2.5 × ATR14` lúc kích hoạt chỉ với Nổ hướng lên / Quét thanh khoản; Hồi hỗ trợ → `CoDungLoDuoi = false`
- [x] T017 [P] [US2] Test formatter (file test mới `backend/StockRadar.Tests/KichBan/V2TelegramFormatterTests.cs`): tin mua đủ mục theo `contracts/telegram-mua.md`, % tính từ `GiaVaoThucTe`, dòng đuổi chỉ khi `CoDungLoDuoi`
- [x] T018 [US2] Điền trường quy tắc của kế hoạch từ `Pha2Options` + ATR lúc đó ngay sau `TinhKeHoach` trong `MayNhanKichBanService.cs` (một chỗ, research R2)
- [x] T019 [US2] Lưu `GiaVaoThucTe` = giá khớp khi chuyển `DaKichHoat` trong `backend/StockRadar.Infrastructure/MarketData/Pha2TrongPhienRunner.cs` (`CapNhatDbAsync` ~dòng 237-241)
- [x] T020 [US2] Sửa `FormatMua` trong `backend/StockRadar.Infrastructure/Notifications/V2TelegramFormatter.cs` theo contract

## Phase 5: US3 — Lệnh ra chỉ theo mức trong kế hoạch (P1)

**Đánh giá lại (2026-10-20)**: code + test trên disk (chưa commit) xây trên giả định cũ (một lần mua, CL1/CL2 luôn còn). C-1/C-9/C-14/C-16/C-17/C-18 thay đổi căn bản → đánh giá từng task cũ:

| Task cũ | Đánh giá | Lý do |
|---|---|---|
| T021 | **SỬA** | Test `KhoangCachDuoi` không đổi theo ATR mới vẫn đúng, nhưng cần thêm điều kiện "chưa mua điểm 2". Disk test `T021_...` OK, chỉ cần xét lại sau Phase 7. |
| T022 | **GIỮ** | CL1 → bán nửa + cắt = `GiaVaoThucTe`: vẫn đúng cho trường hợp chưa có điểm 2 (C-16: mỗi chu kỳ 1 lần; C-20: dời về giá điểm 1). Test hiện trên disk pass logic này. |
| T023 | **GIỮ** | Kiệt sức/Gãy nền → cảnh báo, không đổi trạng thái. Không liên quan chu kỳ. |
| T024 | **SỬA** | Lỗi compile (`soPhienDungTheoThoiGian` ở `Pha2TrongPhienTests.cs:1317`); logic vẫn đúng cho chưa có điểm 2. Phải sửa signature `TaoSellRunner` hoặc đổi cách gọi. Test "cùng lượt CL1 + mức cắt → cắt thắng" giữ; thêm: "cùng lượt CL1 + RutMoc4" → bán nửa **1 lần** (C-16), không 2 lần. |
| T025 | **GIỮ** | Code trên disk OK: `giaVao = GiaVaoThucTe ?? GiaVaoLenhMin`, hạn/thời gian đọc từ kế hoạch, dời dừng lỗ theo `DoiDungLoVeGiaVao`. |
| T026 | **GIỮ** | `DinhTuLucMua − KhoangCachDuoi` thay ATR runtime, bỏ `GetOrComputeAtr`. Disk OK, đã build pass. |
| T027 | **GIỮ** | Kiệt sức/Gãy nền → `FormatCanhBaoKichBanThoat` + `CanhBaoDaGui`. Disk OK. |
| T028 | **GIỮ** | Pha 3 / HieuQua dung `GiaVaoThucTe ?? GiaVaoLenhMin`. Disk OK. |

**Kết luận Phase 5**: T021–T028 trên disk phần lớn vẫn đúng **cho phần chưa có điểm 2**; cần (a) sửa lỗi compile T024, (b) bổ sung điều kiện "chưa mua điểm 2" khi xét CL1/CL2 (C-14), (c) test "đã có điểm 2 → bỏ CL1/CL2" (thuộc Phase 8 mới). T021–T028 **không phải làm lại từ đầu**, chỉ vá.

- [x] T021 [P] [US3] *(SỬA)* Test `Pha2TrongPhienTests.cs`: `T021_KhoangCachDuoi_ChotLucMua_KhongDoiTheoAtrMoi` — giữ; thêm assert "nếu đã mua điểm 2 → vẫn dùng KhoangCachDuoi, không reset" (chặn khi Phase 8 xong). Đường dẫn: `backend/StockRadar.Tests/KichBan/Pha2TrongPhienTests.cs` *(ghi chú 2026-10-20: test disk đã pass; assert điểm-2 chờ Phase 7 vì chưa có cột — C-23 đã mô tả trong T040.)*
- [x] T022 [P] [US3] *(GIỮ)* Test: CL1 → bán nửa, `DaDoiDungLo` chỉ khi `DoiDungLoVeGiaVao`; mức cắt sau đó = `GiaVaoThucTe`. C-16: đã bán nửa → không bán nửa lần nữa. Đường dẫn: `backend/StockRadar.Tests/KichBan/Pha2TrongPhienTests.cs`
- [x] T023 [P] [US3] *(GIỮ)* Test: Kiệt sức / Gãy nền → tin cảnh báo một lần, không ghi `GiaBanNua` / `GiaThoatHet`, trạng thái không đổi. Đường dẫn: `backend/StockRadar.Tests/KichBan/Pha2TrongPhienTests.cs`
- [x] T024 [P] [US3] *(SỬA — xong 2026-10-20)* **SỬA LỖI COMPILE** `Pha2TrongPhienTests.cs:1317` — TC_C7 bỏ tham số `soPhienDungTheoThoiGian`/`nguongMfe` (knob đọc từ kế hoạch theo thiết kế US3), seed `MakeKeHoachJsonDayDu`. Thêm: `T024_C16_CungLuotCL1VaRutMoc4_BanNuaMotLan` (C-16 — bản hiện có dùng CL2 làm nguồn thứ hai; nhánh RutMoc4 thuộc Phase 8, nối lại ở T043) và `T024_ViTheBoSung_KhongDuoiVanCatDungLoBanDau` (LaDuLieuBoSung=true, KhoangCachDuoi=null). Đường dẫn: `backend/StockRadar.Tests/KichBan/Pha2TrongPhienTests.cs`
- [x] T025 [US3] *(GIỮ disk)* `Pha2TrongPhienRunner.KiemTraSellAsync`: giá vào = `GiaVaoThucTe ?? GiaVaoLenhMin`; hạn/thời gian đọc từ kế hoạch; dời dừng lỗ theo `DoiDungLoVeGiaVao` (C-20: dùng `GiaVaoThucTe` điểm 1, không bình quân). Đường dẫn: `backend/StockRadar.Infrastructure/MarketData/Pha2TrongPhienRunner.cs`
- [x] T026 [US3] *(GIỮ disk)* Dừng lỗ đuổi = `DinhTuLucMua − KhoangCachDuoi`; bỏ `GetOrComputeAtr`; comment DO-NOT-CHANGE. Đường dẫn: `backend/StockRadar.Infrastructure/MarketData/Pha2TrongPhienRunner.cs`, `Pha2TrailingStopRuntime.cs`
- [x] T027 [US3] *(GIỮ disk)* Kiệt sức / Gãy nền → cảnh báo, không bán; chống lặp `CanhBaoDaGui`. Đường dẫn: `backend/StockRadar.Infrastructure/MarketData/Pha2TrongPhienRunner.cs`, `V2TelegramFormatter.cs`
- [x] T028 [US3] *(GIỮ disk)* Pha 3 / hiệu quả dùng `GiaVaoThucTe ?? GiaVaoLenhMin`. Đường dẫn: `Pha3DoLuongRunner.cs`, `HieuQuaKichBanService.cs`

**Checkpoint Phase 5**: ✅ XONG 2026-10-20 — sửa compile T024; `dotnet test` toàn bộ **297/297 pass** (mốc 281 + 16 test Phase 5 disk + 2 test mới T024); build Api + Tests 0 lỗi.

## Phase 6: Chặn mua lặp — C-0, C-6, C-7 *(phase mới 2026-10-20)*

**Goal**: mỗi mã chỉ một chu kỳ; không tạo kịch bản/cò kích hoạt cho mã đang có chu kỳ mở.

- [x] T029 [P] [US-C0] Test trong `Pha2TrongPhienTests.cs`: 6 ca chặn mua lặp qua `ChayAsync` — (a) mã đang giữ (dòng mở) không kích hoạt/không gửi tin; (b) `TrangThai=HuyLenh` nhưng `ThoiGianThoatHet==null` vẫn coi mở, vẫn chặn; (c) đóng hôm nay chặn Pha 2 hôm nay; (d) đóng hôm trước cho kích hoạt; (e) đang giữ Hồi hỗ trợ chặn Nổ hướng lên (C-6 tính chung); (f) hai kịch bản cùng mã cùng đạt → chỉ một kích hoạt (giữ theo `DiemXepHang`→`MucHoanThien`→loại). Đường dẫn: `backend/StockRadar.Tests/KichBan/Pha2TrongPhienTests.cs`
- [x] T030 [P] [US-C0] Test Pha 1 (`Pha1TruocPhienTests.cs`, tạo mới): mã có chu kỳ mở → `Pha1TruocPhienRunner` không tạo dòng `DangHinhThanh` hôm nay; chu kỳ đóng hôm trước → tạo được dòng mới. Đường dẫn: `backend/StockRadar.Tests/KichBan/Pha1TruocPhienTests.cs`
- [x] T031 [US-C0] Hàm chung `ChuKyMua` (`backend/StockRadar.Infrastructure/MarketData/ChuKyMua.cs`): `LayMaDangMoAsync` = mở thuần (`ThoiGianKichHoat != null && ThoiGianThoatHet == null`, kịch bản mua, KHÔNG lọc `TrangThai` — DO-NOT-CHANGE), `LayMaBiChanMuaTrongPhienAsync` = mở HOẶC đóng hôm nay. Pha 2 chặn ngay tại `Pha2TrongPhienRunner.KiemTraMuaAsync` (Application service không có db) + dedup mỗi mã một kịch bản. Pha 1 gọi từ `Pha1TruocPhienRunner.LuuKetQuaAsync`.
- [x] T032 [US-C0] C-7: `LayMaBiChanMuaTrongPhienAsync` chặn mã có dòng mua `ThoiGianThoatHet` thuộc NGÀY HÔM NAY (đóng trong phiên → chưa mua lại cùng hôm nay); hôm trước đã đóng → cho. Pha 1 dùng `LayMaDangMoAsync` (mở thuần). Đường dẫn: `backend/StockRadar.Infrastructure/MarketData/Pha2TrongPhienRunner.cs`, `backend/StockRadar.Infrastructure/MarketData/Pha1TruocPhienRunner.cs`

**Checkpoint Phase 6**: T029+T030 pass (8/8); hồi quy toàn bộ 305/305.

## Phase 7: Điểm mua 2 — C-2→C-5, C-9, C-14, C-17, C-18, C-22, C-23 *(phase mới 2026-10-20)*

> **C-22 chốt (2026-10-20)**: phương án A — thêm 2 cột `GiaMuaDiem2ThucTe`/`ThoiGianMuaDiem2` trên dòng điểm 1; một dòng = một chu kỳ; không đổi unique index. **C-23**: mua điểm 2 xong không reset `DinhTuLucMua`.

- [x] T033 [P] [US-D2] Test `Pha2TrongPhienTests.cs`: mua điểm 2 khi giá khớp ∈ [GiaMuaDiem2, GiaMuaDiem2 + BandRongDiem2]; `GiaMuaDiem2ThucTe` ghi đúng; `TyLeLaiLo` không tính lại; tin "MUA THÊM (điểm 2)" gửi theo contract. Đường dẫn: `backend/StockRadar.Tests/KichBan/Pha2TrongPhienTests.cs` *(xong 2026-10-20: `T033_TinhDiemMua2_...` + `T033_MuaDiem2_GiaKhopTrongBand_...` + `T033_GiaDuoiBand_...`)*
- [x] T034-D2 [P] [US-D2] Test: đã bán nửa → không mua điểm 2 (C-17). Đường dẫn: `backend/StockRadar.Tests/KichBan/Pha2TrongPhienTests.cs` *(xong: `T034_D2_DaBanNua_KhongMuaDiem2`)*
- [x] T035-D2 [P] [US-D2] Test: cùng lượt điểm 2 + một luật bán → luật bán thắng (C-18); không ghi `GiaMuaDiem2ThucTe`. Đường dẫn: `backend/StockRadar.Tests/KichBan/Pha2TrongPhienTests.cs` *(xong 2026-10-20. SỬA: bản cũ bắt cặp với RutMoc4 (luật rút 4%) thuộc Phase 8, chưa có → kiểm C-18 bằng luật bán ĐANG có: cùng lượt giá chạm band điểm 2 VÀ chạm dừng lỗ đuổi → bán, không mua. Ghi chú: Phase 8 đã nối thêm ca rút 4% — `T045_C18_CungLuotBandDiem2VaRut4_BanNuaKhongMuaDiem2` (cùng lượt chạm band điểm 2 và −4% từ mốc → bán nửa thắng, không mua điểm 2).)*
- [x] T036-D2 [P] [US-D2] Test: đã mua điểm 2 → CL1/CL2 không còn kích hoạt (C-14); chỉ mức cắt/đuổi/thời gian/hạn thoát. Thêm (C-23): sau mua điểm 2, `DinhTuLucMua` không đổi. Đường dẫn: `backend/StockRadar.Tests/KichBan/Pha2TrongPhienTests.cs` *(xong: `T036_D2_DaMuaDiem2_BoCL1CL2_DinhTuLucMuaKhongReset`)*
- [x] T037 [US-D2] Migration *(C-22 phương án A)*: thêm cột `GiaMuaDiem2ThucTe decimal(18,2) NULL`, `ThoiGianMuaDiem2 datetime2 NULL` vào `KetQuaKichBan`; EF migration; **không đổi index**. Đọc lại body trước khi apply (trap repo). Đường dẫn: `backend/StockRadar.Domain/Entities/KetQuaKichBanEntity.cs`, `backend/StockRadar.Infrastructure/Persistence/ApplicationDbContext.cs`, `backend/StockRadar.Infrastructure/Migrations/` *(xong 2026-10-20: `20261020084135_AddMuaDiem2Columns`, body đúng 2 AddColumn/2 DropColumn; apply→rollback→re-apply kiểm trên DEV SQL Server 2022 (localhost, StockRadarDb); KHÔNG đụng production)*
- [x] T038 [US-D2] Implement khớp điểm 2 trong `Pha2TrongPhienRunner`: mỗi lượt trong `KiemTraSellAsync` (khúc sau khi xét luật bán): kiểm `GiaMuaDiem2 != null`, chưa bán nửa (C-17), chưa có điểm 2, giá ∈ band, không bị C-18 chặn (`suKien is null`) → ghi cột, gửi tin `FormatMuaDiem2`. Đường dẫn: `backend/StockRadar.Infrastructure/MarketData/Pha2TrongPhienRunner.cs`, `V2TelegramFormatter.cs`
- [x] T039 [US-D2] Bỏ CL1/CL2 khi đã mua điểm 2 (C-14): trong `KiemTraSellAsync`, nếu `GiaMuaDiem2ThucTe != null` → skip nhánh CL1/CL2. Đường dẫn: `backend/StockRadar.Infrastructure/MarketData/Pha2TrongPhienRunner.cs`
- [x] T040 [US-D2] Bình quân giá vào (C-4, C-20): khi tính lãi/lỗ, MAE/MFE, mức cắt → nếu có điểm 2, `giaVao = (GiaVaoThucTe + GiaMuaDiem2ThucTe) / 2`; CL1 dời cắt lỗ dùng `GiaVaoThucTe` (C-20, chỉ xảy ra khi chưa có điểm 2). **C-23**: `DinhTuLucMua` không reset. *(Pha 3 bình quân theo chu kỳ thuộc Phase 9b — T066/T068.)* Đường dẫn: `backend/StockRadar.Infrastructure/MarketData/Pha2TrongPhienRunner.cs`
- [x] T041 [US-D2] Formatter: thêm `FormatMuaDiem2(symbol, kịch bản, giá1, giá2, bình quân, mốc, mức cắt)` theo contract. Đường dẫn: `backend/StockRadar.Infrastructure/Notifications/V2TelegramFormatter.cs`

### Bổ sung Phase 7 (chủ sản phẩm 2026-10-20)

- [x] T041-B1 [US-D2] Tính `GiaMuaDiem2` + `BandRongDiem2` khi lập kế hoạch lúc kích hoạt điểm 1 — ngay trong `DienTruongQuyTuc` (cùng chỗ điền các trường quy tắc). Nổ hướng lên lấy `2×CL1 − CL2` (= đỉnh hộp + 0.5×cao hộp, đúng công thức TP đã chốt) → KHÔNG sửa `TinhKeHoach`/`IKichBanDanhGia`. Hồi hỗ trợ/Quét thanh khoản: `giá khớp + ATR`, band `0.3×ATR`. Sửa `FormatMua` in thêm dòng "Điểm mua 2" và "Rút từ mốc" theo `contracts/telegram-mua.md`; không có điểm 2 → in `Điểm mua 2: không` (giữ ngắn). Đường dẫn: `backend/StockRadar.Application/Services/MayNhanKichBanService.cs`, `V2TelegramFormatter.cs`, test `V2TelegramFormatterTests.cs`

**Checkpoint Phase 7**: ✅ XONG 2026-10-20 — T033–T041 + T041-B1 pass; `dotnet test` toàn bộ **314/314** (mốc 305 + 9 test Phase 7); build 0 lỗi; migration apply/rollback/re-apply kiểm trên DEV, không đổi index.

## Phase 8: Luật rút từ mốc 4%/6% — C-11→C-16 *(phase mới 2026-10-20)*

- [x] T042 [P] [US-RM] Test `Pha2TrongPhienTests.cs`: mốc giảm ≥4% → bán nửa `RutMoc4`; mốc giảm ≥6% → bán hết `RutMoc6`. Mốc = `max(DinhTuLucMua, GiaVaoThucTe)` (chưa lên đỉnh → mốc = giá mua, đúng C-11). Đường dẫn: `backend/StockRadar.Tests/KichBan/Pha2TrongPhienTests.cs` *(xong 2026-10-20: 6 test `T042_*` — chưa lên đỉnh, lên đỉnh rồi giảm, −6% cùng lượt với cắt lỗ, chống nhiễu N lượt, kế hoạch cũ không có trường ngưỡng, chưa đủ T+2.5.)*
- [x] T043 [P] [US-RM] Test: đã bán nửa ở CL1, sau đó mốc giảm 4% → **không** bán nửa lần nữa (C-16); chỉ chờ 6%. Đường dẫn: `backend/StockRadar.Tests/KichBan/Pha2TrongPhienTests.cs` *(xong 2026-10-20: `T043_C16_DaBanNuaCL1_Giam4KhongBanThem_Giam6BanHet`.)*
- [x] T044 [P] [US-RM] Test: mốc giảm 4% khi đã có điểm 2 → bán nửa (C-16 vẫn áp dụng; điều kiện: `GiaBanNua == null`). Đường dẫn: `backend/StockRadar.Tests/KichBan/Pha2TrongPhienTests.cs` *(xong 2026-10-20: `T044_DaMuaDiem2_Giam4TuMocDiem1_BanNua` — mốc vẫn từ giá điểm 1, không dùng bình quân (C-23); thêm `T045_C18_CungLuotBandDiem2VaRut4_BanNuaKhongMuaDiem2` cho ca C-18 còn thiếu của Phase 7.)*
- [x] T045 [US-RM] Implement nhánh rút mốc trong `KiemTraSellAsync`: `moc = max(DinhTuLucMua, giaVao)`; `giaKhop <= moc × 0.96` && `GiaBanNua == null` → bán nửa (ghi `GiaBanNua`/`ThoiGianBanNua`; **không ghi `LyDoThoatHet`** — nguồn RutMoc4 chỉ in tin + log); `giaKhop <= moc × 0.94` → bán hết + `LyDoThoatHet = RutMoc6`. Đường dẫn: `backend/StockRadar.Infrastructure/MarketData/Pha2TrongPhienRunner.cs` *(xong 2026-10-20: ngưỡng đọc từ kế hoạch — thêm `NguongRutMocBanNuaPhanTram`=4 / `NguongRutMocBanHetPhanTram`=6 vào `KeHoachGiaoDich` (mặc định record nên kế hoạch cũ thiếu trường vẫn dùng 4/6, JSON kế hoạch mới tự mang hai trường); rút mốc đi qua chuỗi xác nhận N lượt DÙNG CHUNG với dừng lỗ; thứ tự ưu tiên cùng lượt: dừng lỗ (kế hoạch/đuổi/dời về giá vào) → chốt lời 1/2 → thời gian → rút mốc 6% → rút mốc 4%.)*
- [x] T046 [US-RM] Thêm hằng `HuyMuaLap` (9 ký tự), `RutMoc4` (7), `RutMoc6` (7) vào `SuKienBan.cs`; lý do bán hết dùng đúng hằng có sẵn (`LyDoDungLo`/`LyDoDungLoDuoi`/`LyDoChotLoi2`/`LyDoHetThoiGian`/`LyDoHetHanTheoDoi`) — không tự đặt tên mới. Đường dẫn: `backend/StockRadar.Domain/Constants/SuKienBan.cs` *(xong 2026-10-20: `RutMoc4`/`RutMoc6` (tên sự kiện) + `LyDoRutMoc6` (lý do bán hết) + `LyDoHuyMuaLap` cho script C-10 Phase 9.)*
- [x] T047 [US-RM] Formatter: `FormatBanNuaRutMoc(symbol, gia, moc)` + `FormatBanHetRutMoc(symbol, gia, moc, lyDo)` theo contract. Đường dẫn: `backend/StockRadar.Infrastructure/Notifications/V2TelegramFormatter.cs` *(xong 2026-10-20: signature rộng hơn một chút để đủ nội dung contract — `FormatBanNuaRutMoc(symbol, gia, moc, mucCatHieuLuc, nguongPhanTram)` in "Cắt còn lại", `FormatBanHetRutMoc(symbol, gia, giaVao, moc)` in lãi/lỗ so bình quân + `LyDoThoatHet` = hằng `RutMoc6`.)*
- [x] T048 [US-RM] Tin mua `FormatMua` bổ sung dòng "Điểm mua 2: <mức>" và "Rút từ mốc: −4%/−6%" theo contract mới. *(SỬA T020 disk.)* Đường dẫn: `backend/StockRadar.Infrastructure/Notifications/V2TelegramFormatter.cs` *(kiểm lại 2026-10-20: đúng contract; dòng rút mốc nay đọc ngưỡng từ kế hoạch thay vì hard-code.)*

**Checkpoint Phase 8**: ✅ XONG 2026-10-20 — T042–T048 pass; `dotnet test` toàn bộ **327/327** (mốc 314 + 13 test Phase 8); build 0 lỗi, không migration. Ba test Phase 5 đổi kỳ vọng vì luật mới: `T022_KeHoachKhongDoi_SauCL1...` (giá 97 sau CL1 giờ là −11.8% từ đỉnh → bán hết RutMoc6), `SL_KietSucKhongDoiDungLo` (9.6 so đỉnh 10.5 → RutMoc6), `T024_ViTheBoSung...` (96 đúng ngưỡng −4% → nâng giá lên 97 để test chỉ đo dừng lỗ).

## Phase 9: Đóng chu kỳ + dọn production — C-10, C-19, C-21 *(phase mới 2026-10-20)*

- [x] T049 [P] [US-CK] Test: chu kỳ đóng khi `DaThoat` (mọi lý do — C-19) → phiên sau Pha 1 tạo lại kịch bản cho Symbol đó. Đường dẫn: `backend/StockRadar.Tests/KichBan/Pha2TrongPhienTests.cs` *(2026-10-20: `T049_DongHomTruoc_MoiLyDoThoatHet_DeuDuocMuaLai` — theory 6 lý do ChotLoi2/RutMoc6/DungLo/HetThoiGian/HetHanVao/HuyMuaLap; cơ chế đóng→mua lại đã có từ Phase 6 (`T029_DongHomTruoc...`, `T030_DongHomTruoc...`) vẫn pass.)*
- [x] T050 [US-CK] Đảm bảo logic C-19: trạng thái `DaThoat` (bất kể `LyDoThoatHet` = CL2/RutMoc6/Cắt/HetThoiGian/HetHan) → `CoChuKyMoAsync` không coi đó là mở (vì `ThoiGianThoatHet != null`). *(thường đã đúng từ Phase 6; verify.)* *(đã verify 2026-10-20: `ChuKyMua` xét mở/đóng thuần theo `ThoiGianThoatHet`, không đọc `LyDoThoatHet`/`TrangThai`.)*
- [x] T051 [US-CK] *(chỉ viết, KHÔNG chạy)* **MỘT script production gộp**, chạy theo đúng thứ tự, mỗi bước kiểm số dòng affected và **tự ROLLBACK nếu lệch kỳ vọng** (BEGIN TRAN + IF @@ROWCOUNT <> expected ROLLBACK ELSE COMMIT). Viết vào `specs/009-v2-trade-plan/quickstart.md`; chủ sản phẩm duyệt rồi chạy trên server. Thứ tự:
  1. **C-10** `HuyMuaLap`: ABB 4106/5444/5891, DCM 4619/5957 → `LyDoThoatHet = HuyMuaLap`, `KetQuaDoLuong = NULL` (không đo Pha 3); giữ ABB 3650, DCM 1916. (kỳ vọng 5 dòng)
  2. **C-24** sửa `GiaVaoThucTe` = giá khớp thật (log TRIGGERED) cho NTP 2114 = 52.80, QNS 5765 = 51.00. (kỳ vọng 2 dòng; ABB/DCM đã bị huỷ ở bước 1 nên không sửa)
  3. **C-21** tính `GiaMuaDiem2` cho ABB 3650 / DCM 1916 nếu lấy lại được ATR lúc mua điểm 1 (không thì NULL). (kỳ vọng 0–2 dòng)
  4. **C-27 P3** xoá `KetQuaDoLuong` của dòng **đang mở** (`ThoiGianThoatHet IS NULL`) để chờ đóng; các dòng đã đóng giữ nguyên (sẽ được chấm lại bởi code Pha 3 mới). *(Đề xuất phạm vi script: chỉ xoá, chưa tính lại trong SQL — để runner mới chấm.)*
  - **Không** đưa DCS 3275 (C-25) vào script này — chủ sản phẩm tự chạy. **Không** đụng DGW 1472 / PVS 2168 đã đóng (C-26 — giữ dữ liệu, chấm lại bằng code Pha 3). *(C-32 2026-10-21 overriding: DGW/PVS giờ được sửa `GiaVaoThucTe` ngay trong bước 2 — 45.80/33.60 — trước khi bước 4 xóa kết quả cũ.)* Đường dẫn: `specs/009-v2-trade-plan/quickstart.md`
  *(VIẾT 2026-10-20 tại `specs/009-v2-trade-plan/scripts/009-production-fix.sql` — CHƯA chạy prod. Chốt chủ sản phẩm overriding chữ ở trên: bước 1 = 6 dòng (thêm ABB 4556) và `KetQuaDoLuong = 'Huy'` thay vì NULL; bước 3 = đủ 5 dòng ABB/DCM/NTP/QNS/FRT (số liệu TinhDiemMua2 offline, bảng chú thích trong script); bước 4 = xóa KetQuaDoLuong của MỌI dòng V2-activated (`TrangThai >= 2`) trừ 'Huy', đếm trước rồi so affected. Dry-run DB dev với 14 dòng GIẢ (Id thật): cả 4 bước khớp → COMMIT đúng; kiểm cả đường ROLLBACK (1 dòng đã đóng trước → toàn bộ hoàn tác, không dòng nào ghi); đã xoá dữ liệu giả sau khi kiểm.)*
  *(SỬA 2026-10-21 theo C-31/C-32: bước 2 = 4 dòng — NTP/QNS (mở) + DGW 1472 = 45.80/PVS 2168 = 33.60 (đã đóng, kiểm `ThoiGianThoatHet IS NOT NULL`); bước 3 thay bảng điểm 2 bằng ATR14 từ lịch sử production (14 phiên trước ngày mua): ABB 17.7471/0.0771, DCM 34.0764/0.2839, NTP 54.1786/0.4136, QNS 52.3571/0.4071, FRT 147.4186/1.5236; đầu script ghi kỳ vọng sau lượt 16:00 — DGW "Ngang", PVS "Thua"; bước 4 vẫn chạy sau bước 2. Dry-run dev mới với 16 dòng GIẢ (thêm DGW/PVS đóng): COMMIT đủ 4 bước (6/4/5/3 dòng), ROLLBACK kiểm cả nhánh DGW/PVS mở trái kỳ vọng; đã xóa sạch dữ liệu giả.)*

**Checkpoint Phase 9**: ✅ XONG 2026-10-20 — T049 pass (theory 6 lý do); script gộp `scripts/009-production-fix.sql` sẵn sàng chờ chủ sản phẩm duyệt (chưa chạy prod; đã dry-run dev + chứng minh ROLLBACK).

## Phase 9b: Pha 3 chấm theo chu kỳ — C-26, C-27 *(phase mới 2026-10-20)*

**Goal**: kết quả Pha 3 phản ánh lãi/lỗ thật của **cả chu kỳ**, chỉ chấm khi **chu kỳ đóng** (C-19). Bỏ hẳn cách đo một lần sau `SoNgayChoDo = 4` ngày rồi giữ nguyên (`Pha3DoLuongRunner.cs:25`, bộ lọc `KetQuaDoLuong == null` dòng 59) — nguồn gốc lỗi PVS (chấm "Thắng" 12/10 theo giá đóng cửa khi còn mở, 16/10 cắt lỗ thật 31.50 mua 33.60).

**Test** (file `backend/StockRadar.Tests/` — tạo `KichBan/Pha3DoLuongTests.cs` nếu chưa đủ; gọi qua `Pha3DoLuongRunner.RunAsync`):
- [x] T064 [P] [US-P3] Test **P1**: vị thế đang mở (`ThoiGianThoatHet == null`, đã quá 4 ngày) → `RunAsync` **không** đặt `KetQuaDoLuong` (vẫn null = "đang giữ"). Đây là regression của bug PVS. Đường dẫn: `backend/StockRadar.Tests/KichBan/Pha3DoLuongTests.cs` *(2026-10-20: `T064_ChuKyMo_HonBonNgay_VanKhongCham`.)*
- [x] T065 [P] [US-P3] Test **P2 giá ra bình quân trọng số**: đóng bằng bán nửa (50%) + bán hết (50%) → `giaThoat = (GiaBanNua + GiaThoatHet)/2`; đối chiếu `PhanTramLoiNhuan`. Đường dẫn: `backend/StockRadar.Tests/KichBan/Pha3DoLuongTests.cs`
- [x] T066 [P] [US-P3] Test **P2 giá vào bình quân khi có điểm 2**: `giaVao = (GiaVaoThucTe + GiaMuaDiem2ThucTe)/2` (C-4/C-22). Phụ thuộc cột điểm 2 (Phase 7); nếu Phase 7 chưa xong thì blocker test này lại, ghi rõ. Đường dẫn: `backend/StockRadar.Tests/KichBan/Pha3DoLuongTests.cs` *(Phase 7 đã xong — không blocker; RR thực tế 1.5 kiểm bằng giá cụ thể.)*
- [x] T067 [P] [US-P3] Test **PVS-style**: mở > 4 ngày, đóng lỗ (`GiaThoatHet` dưới giá vào) → `KetQuaDoLuong = "Thua"`, `TrangThai = HuyLenh`, ngưỡng ±1% giữ nguyên. Đường dẫn: `backend/StockRadar.Tests/KichBan/Pha3DoLuongTests.cs`

**Code**:
- [x] T068 [US-P3] Sửa `Pha3DoLuongRunner.RunAsync`: (a) **P1** — bỏ bộ lọc "đo sau 4 ngày"; chỉ chấm dòng có `ThoiGianThoatHet != null` (chu kỳ đóng); (b) **P2** — `giaVao` = bình quân hai lần mua khi có điểm 2, `giaThoat` = bình quân trọng số các lần bán; xoá nhánh `else` dùng `nenCuoi.Close` cho vị thế còn mở (dòng 130–151); (c) giữ nguyên ngưỡng `NguongPhanTram = 1` và cách phân loại Thắng/Thua/Ngang. Comment BUSINESS-RULE cho "chỉ chấm khi đóng". Đường dẫn: `backend/StockRadar.Infrastructure/MarketData/Pha3DoLuongRunner.cs` *(VIẾT LẠI toàn bộ runner 2026-10-20; `NgayThoat` = ngày giờ VN của `ThoiGianThoatHet` UTC; dòng 'Huy' không bao giờ chấm/ghi đè.)*
- [x] T069 [US-P3] **P3 (code)**: với dòng **đang mở** mà còn `KetQuaDoLuong` cũ (do cách đo cũ để lại) → set `KetQuaDoLuong = NULL` (chờ đóng) trong `RunAsync`, log số dòng đã xoá. Kiểm `HieuQuaKichBanService.cs:63` đã tính `choDo = KetQuaDoLuong null` → "Chờ đo" (không đổi API). Đọc `docs/features/v2-trade-plan/spec.md` mục hiển thị. Đường dẫn: `backend/StockRadar.Infrastructure/MarketData/Pha3DoLuongRunner.cs`, `HieuQuaKichBanService.cs` *(pass P3 trong RunAsync: code chỉ xóa kết quả cũ trên dòng CÒN MỞ; dòng đã đóng mang kết quả cũ do script prod bước 4 xóa để runner chấm lại. Hành vi TrangThai theo kết quả đo giữ nguyên: Thang→ChotLoi, Thua→HuyLenh, Ngang→giữ.)*
- [x] T070 [US-P3] *(chỉ đọc, ghi tài liệu — KHÔNG đổi API/DTO)* Ghi nhận web/mobile đã xử lý null = "Chờ đo" (`frontend/src/pages/HieuQuaPage.tsx:32`, `mobile/lib/screens/hieu_qua_screen.dart:639`). NẾU muốn đổi nhãn "Chờ đo" → "Đang giữ" cho dòng chu kỳ mở → **dừng hỏi chủ sản phẩm** (thay đổi hiển thị, không thuộc phạm vi tự quyết). Đường dẫn: `frontend/src/pages/HieuQuaPage.tsx`, `mobile/lib/screens/hieu_qua_screen.dart` *(ĐÃ ĐỌC 2026-10-20: web `useKetQuaStyle` default → "Chờ đo" (amber), mobile filter khớp 'chờ'/'cho' → amber; cả hai chỉ hiển thị chuỗi từ API `ketQuaDoLuong` — null vẫn ra "Chờ đo" (server map). KHÔNG cần đổi gì, không đổi nhãn.)*

**Checkpoint Phase 9b**: ✅ XONG 2026-10-20 — T064–T067 pass (Pha3DoLuongTests viết lại, 16 test); runner chấm theo chu kỳ đóng; `dotnet test` toàn bộ **333/333** (mốc 327 + theory T049×6); build 0 lỗi, không migration, không đổi API/DTO.

## Phase 9c: Chấm kết quả NGAY khi đóng lệnh — C-33 *(phase mới 2026-10-21)*

**Goal**: Pha 2 bán hết chu kỳ ở lượt nào thì chấm `KetQuaDoLuong` ngay lượt đó (cùng lần SaveChanges với `ThoiGianThoatHet/GiaThoatHet/LyDoThoatHet`), dùng **chung một hàm** với Pha 3; Pha 3 16:00 giữ vai trò chấm bù. Tin bán hết in thêm dòng kết quả. Bán nửa và dòng 'Huy' không chấm.

- [x] T071 [P] [US-P3] Tách công thức chấm C-27 về một chỗ không phụ thuộc DbContext: `ChamKetQuaChuKy.Tinh` (giá vào bình quân điểm 2 / giá ra bình quân bán nửa / ±1% / R:R / TrangThai) + `GhiVaoDong`. Đường dẫn: `backend/StockRadar.Domain/Services/ChamKetQuaChuKy.cs`
- [x] T072 [US-P3] `Pha3DoLuongRunner` gọi `ChamKetQuaChuKy` (xóa công thức inline — không còn bản sao); vai trò P1 thành chấm bù: dòng đã có kết quả không chấm lại, 'Huy' giữ nguyên. Đường dẫn: `backend/StockRadar.Infrastructure/MarketData/Pha3DoLuongRunner.cs`
- [x] T073 [US-P3] `Pha2TrongPhienRunner`: cả hai điểm bán hết (nhánh sự kiện DungLo/DungLoDuoi/ChotLoi2/RutMoc6/HetThoiGian + HetHanTheoDoi) ghi giá thoát rồi chấm ngay bằng helper `ChamDongChuKy`, cùng lần `SaveChangesAsync`; chỉ chấm dòng chưa có kết quả. Đường dẫn: `backend/StockRadar.Infrastructure/MarketData/Pha2TrongPhienRunner.cs`
- [x] T074 [US-P3] Tin bán hết in thêm `Kết quả: Thắng/Thua/Ngang (±x% so giá vào)` qua `V2TelegramFormatter.FormatDongKetQua` — không mã C-x; bán nửa không có dòng này. Đường dẫn: `backend/StockRadar.Infrastructure/Notifications/V2TelegramFormatter.cs`
- [x] T075 [P] Test C-33 (8 ca trong `Pha2TrongPhienTests.cs`, tiền tố `C33_`): bán hết DungLo chấm ngay; bán nửa→bán hết giá ra bình quân; có điểm 2 giá vào bình quân (RR null khi vào==SL); tin bán hết có dòng kết quả + không chứa "(C-"; bán nửa vẫn null; HetHanTheoDoi chấm ngay; Pha 3 không chấm lại + chấm bù dòng thiếu; **tương đương Pha 2 vs Pha 3 trên cùng dữ liệu** (khẳng định dùng chung hàm). *(`KiemTraSellAsync` thay vì `ChayAsync` vì `ChayAsync` chặn theo giờ thật — cùng luồng bán, pattern mọi test sell sẵn có.)*

**Checkpoint Phase 9c**: ✅ XONG 2026-10-21 — `dotnet test` **341/341** (mốc 333 + 8 test C33); build 0 lỗi; không migration, không đổi API/DTO, không đụng Pha 1/luật mua-bán; chưa chạy production, chưa commit.

## Phase 9d: Không xét mua/bán từ giá chưa khớp — C-34 *(phase mới 2026-10-21)*

**Goal**: Pha 2 KHÔNG xét MUA (điểm 1 và điểm 2) khi mã chưa có khớp trong phiên (khối lượng phiên = 0 trên bảng giá KBS) **hoặc** giờ VN trước `Pha2:GioBatDauMua` (09:15, cấu hình — không hard-code); KHÔNG xét BÁN khi volume = 0 nhưng **không** chặn bán theo giờ (luật bán vẫn realtime — mở cửa sập bán ở giá khớp thật đầu tiên). MAE/MFE/ĐỉnhTưLựcMua không cập nhật từ giá chưa khớp. Mã bị bỏ qua: không đổi trạng thái, không gửi tin, log Debug, lượt sau xét lại bình thường. Bằng chứng: 8/20 lệnh mua V2 từ 05/10 ghi 09:00–09:02 với "volume 0" (giá tham chiếu chưa khớp).

- [x] T076 [P] [US-P2] `Pha2Options.GioBatDauMua` (string "HH:mm", mặc định "09:15") + mục `Pha2:GioBatDauMua` trong `appsettings.json`. Đường dẫn: `backend/StockRadar.Application/Options/Pha2Options.cs`, `backend/StockRadar.Api/appsettings.json`
- [x] T077 [US-P2] `Pha2TrongPhienRunner.KiemTraMuaAsync`: cổng giờ (trước `GioBatDauMua` → return sớm, log Debug) đặt trước mọi truy vấn; cổng volume (lọc `formingEntities` theo `volumeHienTai` sau khi lấy bảng giá — mã volume 0: giữ nguyên trạng thái, không trigger, log Debug). Đường dẫn: `backend/StockRadar.Infrastructure/MarketData/Pha2TrongPhienRunner.cs`
- [x] T078 [US-P2] `KiemTraSellAsync`: `continue` đầu vòng theo mã khi volume phiên = 0 (đặt SAU kiểm tra "đã thoát hết", TRƯỚC khối MAE/MFE → giá tham chiếu không pollute MAE/MFE/Đỉnh); không chặn bán theo giờ. Điểm 2 (bên trong bước bán) thêm điều kiện `nowVn.TimeOfDay >= gioBatDauMua`. Đường dẫn: `backend/StockRadar.Infrastructure/MarketData/Pha2TrongPhienRunner.cs`
- [x] T079 [US-P2] Đồng hồ kiểm thử được tất định: `KiemTraMuaAsync` thành `internal` nhận `DateTime nowVn`; `KiemTraSellAsync` thêm tham số cuối tùy chọn `DateTime? nowVnGiaLap = null` (call-site cũ không đổi); `ChayAsync` truyền một lần đọc `nowVn` duy nhất cho cả hai bước. Đường dẫn: `backend/StockRadar.Infrastructure/MarketData/Pha2TrongPhienRunner.cs`
- [x] T080 [P] Test C-34 (8 ca trong `Pha2TrongPhienTests.cs`, tiền tố `C34_`): mua chặn volume 0; mua chặn 09:10 dù volume > 0; mua được 09:16; điểm 2 + MAE chặn volume 0 (null toàn bộ, không tin); điểm 2 chặn trước 09:15 rồi chốt sau 09:15 trên cùng db; **bán chặn volume 0 qua `ChayAsync`**; bán KHÔNG chặn lúc 09:05 khi volume > 0 (chốt "Thua" ngay — nối C-33); assert mặc định `GioBatDauMua = "09:15"`. Helper `TaoSellRunner`/`TaoChayAsyncRunner` thêm `gioBatDauMua` (mặc định "00:00" để trung hòa cổng giờ cho test cũ).
- [x] T081 [P] Cập nhật `docs/domain/pipeline-jobs.md`: hàng Pha 2 (bảng Luật as-is + hàng canon "2 — Trong phiên") mô tả cổng volume/giờ C-34.

**Checkpoint Phase 9d**: ✅ XONG 2026-10-21 — `dotnet test` **349/349** (mốc 341 + 8 test C34, không sửa kỳ vọng test cũ); build 0 lỗi; không migration, không đổi API/DTO, không đụng V1/Pha 1/công thức kế hoạch; chưa chạy production, chưa commit.

## Phase 10: US4 — Cò kích hoạt ổn định (P2) *(không đổi so với tasks cũ)*

**Independent Test**: test 5–12 trong `docs/features/v2-pha2-trigger-fix/spec.md` mục 2.

- [ ] T052 [P] [US4] Test 5–10 của `v2-pha2-trigger-fix` trong `backend/StockRadar.Tests/KichBan/KichBanNoHuongLenTests.cs` và `KichBanQuetThanhKhoanTests.cs`
- [ ] T053 [US4] Chuẩn hoá lịch sử (lịch sử trước hôm nay + nến hôm nay từ bảng giá) trong `MayNhanKichBanService.KiemTraTriggerTrongPhienAsync`; truyền giá mở cửa / cao / thấp từ `Pha2TrongPhienRunner.cs`
- [ ] T054 [P] [US4] `backend/StockRadar.Application/Services/KichBanNoHuongLen.cs`: đỉnh hộp và trung bình khối lượng trên lịch sử trước hôm nay; bỏ khối lượng khỏi `Dat`, giữ trong bằng chứng ("tham khảo, không bắt buộc"); comment `BUSINESS-RULE`
- [ ] T055 [P] [US4] `backend/StockRadar.Application/Services/KichBanQuetThanhKhoan.cs`: như T054
- [ ] T056 [US4] Test 11 (Hồi hỗ trợ): chạy `KichBanHoiHoTroTests.cs`, test nào đổi kỳ vọng thì ghi rõ lý do

## Phase 11: US5 — Dữ liệu cũ bổ sung minh bạch (P3) *(không đổi, đánh số lại)*

- [x] T057 [US5] *(= T034 cũ)* Kiểm migration T007–T008 trên DB dev có dữ liệu giống production (vị thế đang mở): đủ trường, `LaDuLieuBoSung = true`, luồng bán vẫn xét bình thường. *(đã kiểm 19/10/2026)*
- [ ] T058 [US5] Tách nhóm `LaDuLieuBoSung` trong `HieuQuaKichBanService.cs` (SC-006) — chỉ nếu không phải đổi API; đổi API thì dừng hỏi
- [ ] T059 [US5] Viết script UPDATE DCS 3275 (`LyDoThoatHet = HuyNgoaiVung`) vào `specs/009-v2-trade-plan/quickstart.md`. **Không chạy**; chờ chủ sản phẩm duyệt lúc chạy

## Phase 12: Polish *(đánh số lại)*

- [ ] T060 [P] Cập nhật `docs/domain/pipeline-jobs.md` (Pha 1: chặn mã có chu kỳ mở; Pha 2: kiểm vùng vào + kế hoạch đầy đủ + điểm 2 + rút mốc + cảnh báo; Pha 3: huỷ `HetHanVao` + `HuyMuaLap`)
- [ ] T061 [P] Cập nhật trạng thái `docs/features/v2-trade-plan/spec.md` và `docs/features/v2-pha2-trigger-fix/spec.md` (mục 8 + các mục 1/3/4 đã viết lại 2026-10-20)
- [ ] T062 Chạy toàn bộ `dotnet test backend/StockRadar.Tests`, so với mốc T003 (281); chạy `backend/restart-api.ps1` và smoke dev
- [ ] T063 Đưa bản nháp commit message (một dòng, có prefix) và lệnh `ship-all.ps1` cho chủ sản phẩm tự chạy

## Dependencies

- Phase 1 → Phase 2 → Phase 3 (US1) → Phase 4 (US2) → Phase 5 (US3, vá disk).
- Phase 6 (C-0 chặn mua lặp) cần Phase 3 xong (kiểm vùng); phải xong **trước** Phase 7.
- Phase 7 (điểm 2) cần Phase 5 (code sell disk OK) + Phase 6 (chặn); C-22 đã chốt phương án A.
- Phase 8 (rút mốc) cần Phase 5 (nhánh sell) + Phase 7 (điểm 2 → C-14 bỏ CL1).
- Phase 9 (đóng chu kỳ + production) cần Phase 6 + Phase 7.
- Phase 9b (Pha 3 theo chu kỳ — C-27) cần Phase 5 ( ThoátHet/GiaBanNua ghi đúng) + Phase 9 (script xoá kết quả dòng đang mở); T066 (bình quân điểm 2) cần cột điểm 2 của Phase 7.
- Phase 9c (chấm ngay khi đóng — C-33) cần Phase 9b (hàm chấm chung tách từ runner 9b).
- Phase 9d (không xét giá chưa khớp — C-34) cần Phase 9c (ca bán 09:05 khẳng định cả chấm ngay C-33).
- Phase 10 (US4) độc lập logic nhưng cùng file `MayNhanKichBanService.cs` → làm sau Phase 6.
- Phase 11 (US5) cần Phase 2.
- Phase 12 (Polish) làm cuối.

## Parallel

- Phase 5: T021–T028 phần lớn đã trên disk, chỉ vá T024 (compile) + test T021 thêm điều kiện.
- Phase 6: T029, T030 song song trước T031.
- Phase 7: T033–T036 (test) viết song song; T037 (migration) trước T038.
- Phase 8: T042–T044 (test) song song trước T045–T048 (code).
- Phase 10: T054, T055 song song sau T053.
- Phase 12: T060, T061 song song.

## Implementation strategy

1. MVP = Phase 1–5 (chặn mua ngoài vùng + kế hoạch + lệnh ra đọc kế hoạch). Code Phase 5 trên disk đã ~80% xong → chỉ vá T024 compile + chạy test. Ship Phase 5 rồi tiếp.
2. Phase 6 (chặn C-0): thay đổi căn bản — phải xong trước điểm 2 (tránh kích hoạt trùng khi đang code Phase 7).
3. Phase 7 + 8 (điểm 2 + rút mốc): C-22/C-23 đã chốt — làm theo phương án A.
4. Phase 9 (production UPDATE ABB/DCM): script chỉ viết, không chạy.
5. Phase 10–12: như cũ.

## Test cases tối thiểu (bao phủ C-0→C-21)

Gọi qua `Pha2TrongPhienRunner.RunAsync`. Mỗi case một `[Fact]`:

1. **T029** — Mua lặp bị chặn: Symbol có `DaKichHoat` chưa thoát → cò đạt, giá trong vùng → không kích hoạt (C-0).
2. **T030** — Pha 1 không tạo kịch bản cho Symbol có chu kỳ mở (C-0).
3. **T033** — Điểm 2 mua đúng vùng: giá ∈ [GiaMuaDiem2, GiaMuaDiem2 + BandRongDiem2] → ghi cột, bắn tin (C-9).
4. **T034-D2** — Đã bán nửa → không mua điểm 2 (C-17).
5. **T035-D2** — Cùng lượt điểm 2 + RutMoc4 → bán trước, không mua (C-18).
6. **T036-D2** — Đã mua điểm 2 → CL1 không kích hoạt (C-14).
7. **T042** — Mốc giảm ≥4% → bán nửa `RutMoc4`; giảm ≥6% → bán hết `RutMoc6` (C-11).
8. **T043** — CL1 đã bán nửa rồi, mốc giảm 4% → không bán nửa nữa (C-16).
9. **T044** — Đã có điểm 2, mốc giảm 4% → bán nửa (C-16 áp cả sau điểm 2).
10. **T049** — Chu kỳ đóng (`DaThoat`), phiên sau mã được đánh giá lại (C-19 + C-7).
11. **T064** — Vị thế đang mở (`ThoiGianThoatHet == null`) không bị Pha 3 chấm (C-27 P1; regression PVS).
12. **T065** — Đóng sau bán nửa + bán hết → giá ra bình quân trọng số (C-27 P2).
13. **T066** — Có điểm 2 → giá vào bình quân hai lần mua (C-27 P2 + C-4/C-22).
14. **T067** — PVS-style: mở > 4 ngày rồi đóng lỗ → "Thua" (C-27; thay cách đo một lần sau 4 ngày).
