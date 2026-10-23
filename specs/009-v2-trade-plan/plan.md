# Implementation Plan: V2 — Kế hoạch giao dịch đầy đủ trước khi mua + chuẩn hoá cò kích hoạt

**Branch**: `009-v2-trade-plan` | **Date**: 2026-10-14 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/009-v2-trade-plan/spec.md`

## Summary

Mở rộng `KeHoachGiaoDich` thành kế hoạch đầy đủ (vào, cắt, chốt, dời, đuổi với khoảng cách cố định, thời gian, hạn), điền một lần lúc kích hoạt trong `MayNhanKichBanService`. Chỉ kích hoạt khi giá khớp nằm trong vùng vào và kế hoạch hợp lệ. Lưu `GiaVaoThucTe`. Luồng bán Pha 2, Pha 3 và hiệu quả đọc mức / tham số từ kế hoạch; Kiệt sức / Gãy nền chỉ cảnh báo. Huỷ kịch bản đang hình thành cuối phiên (`HetHanVao`). Chuẩn hoá dữ liệu cò (phần 2 `v2-pha2-trigger-fix`). Chi tiết quyết định: [research.md](./research.md).

## Technical Context

**Language/Version**: C# / .NET (backend hiện có)

**Primary Dependencies**: EF Core, System.Text.Json, Telegram (sẵn có). Không thêm thư viện.

**Storage**: SQL Server; 1 cột mới + migration dữ liệu JSON cho vị thế đang mở

**Testing**: xUnit, `backend/StockRadar.Tests/KichBan/` (9 file)

**Target Platform**: Linux server (`stockradar.service`)

**Project Type**: web-service + background jobs

**Performance Goals**: Pha 2 chạy mỗi phút; không thêm truy vấn mỗi vị thế (bỏ được tính ATR mỗi phiên)

**Constraints**: Luật bán realtime trên giá khớp; `LyDoThoatHet` tối đa 16 ký tự

**Scale/Scope**: ~70 kịch bản / ngày, 7 vị thế đang mở

## Constitution Check

*Source: `.specify/memory/constitution.md` v1.0.0*

- [x] **I. Code as truth**: entry file và dòng cụ thể nêu ở research.md R1–R9 (khảo sát 2026-10-14)
- [x] **II. Spec-first**: spec.md có, mục 0 đã chốt
- [x] **III. Minimal surface**: giữ công thức mức giá, giữ thứ tự kiểm cắt / CL1, giữ khoá `Pha2Options`; không đụng V1, Pha 1 bối cảnh / hình thái
- [x] **IV. Domain gates**: đổi pipeline V2 → cập nhật `docs/domain/pipeline-jobs.md` + `docs/features/v2-trade-plan/spec.md` cùng change set
- [x] **V. Simplicity**: không thêm abstraction; mở rộng record sẵn có
- [x] **Stack**: Domain (record, hằng), Application (service, options, kịch bản), Infrastructure (runner, formatter, EF, migration). Không đổi API, mobile, web. Sau code: `backend/restart-api.ps1`; ship do chủ sản phẩm chạy

Re-check sau Phase 1: không phát sinh vi phạm.

## Project Structure

### Documentation (this feature)

```text
specs/009-v2-trade-plan/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/telegram-mua.md
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code

```text
backend/
├── StockRadar.Domain/ValueObjects/KeHoachGiaoDich.cs        # trường mới, TyLeLaiLo
├── StockRadar.Domain/Constants/SuKienBan.cs                 # HetHanVao, HuyNgoaiVung
├── StockRadar.Domain/Entities/KetQuaKichBanEntity.cs        # GiaVaoThucTe
├── StockRadar.Application/Services/MayNhanKichBanService.cs # điền kế hoạch, kiểm vùng, chuẩn hoá lịch sử
├── StockRadar.Application/Services/KichBanNoHuongLen.cs     # phần 2
├── StockRadar.Application/Services/KichBanQuetThanhKhoan.cs # phần 2
├── StockRadar.Infrastructure/MarketData/Pha2TrongPhienRunner.cs  # lưu GiaVaoThucTe, bán đọc kế hoạch, cảnh báo
├── StockRadar.Infrastructure/MarketData/Pha3DoLuongRunner.cs     # giá vào thực tế, huỷ HetHanVao
├── StockRadar.Application/Services/HieuQuaKichBanService.cs      # giá vào thực tế, tách nhóm bổ sung
├── StockRadar.Infrastructure/Notifications/V2TelegramFormatter.cs
├── StockRadar.Infrastructure/Persistence/ (ApplicationDbContext, Migrations/)
└── StockRadar.Tests/KichBan/
docs/domain/pipeline-jobs.md
```

**Structure Decision**: chỉ backend + docs.

## Điểm đã chốt khi duyệt plan

Chủ sản phẩm trả lời "đồng ý cả 4 điểm" ngày 2026-10-19: duyệt cả 4 đề xuất dưới đây.

1. **R3:** kế hoạch không hợp lệ ở một lượt → không kích hoạt lượt đó nhưng vẫn xét lại lượt sau (kế hoạch trước mua còn tính lại theo lịch sử); hết phiên mới huỷ. Hay huỷ ngay ở lượt đầu?
2. **R4:** huỷ `HetHanVao` chạy ở Pha 3 16:00.
3. **R5:** chạm CL1 và mức cắt cùng lượt → mức cắt thắng (giữ như hiện tại).
4. **R7:** `KeHoachKhongHopLe` không lưu vào `LyDoThoatHet` (quá 16 ký tự), chỉ trong bằng chứng.

## Câu hỏi thứ tự luật — ĐÃ CHỐT (C-16→C-23, 2026-10-20)

Chủ sản phẩm duyệt cả 8 đề xuất Q-a→Q-h, ghi vào spec.md thành C-16→C-23.

| Câu | Quyết định | Mã |
|---|---|---|
| Q-a | Mỗi chu kỳ tối đa một lần bán nửa (bất kể nguồn CL1 hay rút 4%) | C-16 |
| Q-b | Đã bán nửa → không mua điểm 2 | C-17 |
| Q-c | Cùng lượt điểm 2 + luật bán → luật bán thắng | C-18 |
| Q-d | Chu kỳ đóng khi bán hết theo mọi luật, kể cả hết hạn/thời gian | C-19 |
| Q-e | Dời cắt lỗ sau CL1 về giá điểm 1 (không dùng bình quân) | C-20 |
| Q-f | ABB/DCM: có điểm 2 nếu lấy lại được ATR lúc mua điểm 1, không thì null | C-21 |
| Q-g | Điểm 2 lưu theo **phương án A**: thêm 2 cột `GiaMuaDiem2ThucTe`/`ThoiGianMuaDiem2` trên dòng điểm 1; một dòng = một chu kỳ; không đổi unique index | C-22 |
| Q-h | Mua điểm 2 xong **không tính lại mốc**: `DinhTuLucMua` vẫn là đỉnh tính từ lúc mua điểm 1 | C-23 |

## Câu hỏi còn lại cần chủ sản phẩm chốt

- **Q-i → C-28 (ĐÃ CHỐT 2026-10-20) — Nhãn hiển thị cho vị thế đang mở.** Giữ nguyên nhãn **"Chờ đo"** cho `KetQuaDoLuong == null`; KHÔNG đổi web/mobile, KHÔNG đổi API/DTO. C-27 chỉ cần "dòng mở không chấm kết quả" → `KetQuaDoLuong` null là đủ, nhãn không đổi.

(Phần dưới giữ lại phân tích phương án A/B và dinh-mốc để đối chiếu lịch sử quyết định.)

## Luật chu kỳ + Pha 3 đã chốt thêm (C-24→C-27, 2026-10-20)

| Mã | Quyết định |
|---|---|
| C-24 | Sửa `GiaVaoThucTe` vị thế mở mua ngoài vùng = giá khớp thật log TRIGGERED (thực chất NTP 2114=52.80, QNS 5765=51.00); cùng đợt script C-10 |
| C-25 | DCS 3275 → `HuyNgoaiVung`; chủ sản phẩm **tự chạy**, không thuộc task |
| C-26 | DGW 1472, PVS 2168 đã đóng: ~~giữ dữ liệu~~ → **C-32 (2026-10-21)** sửa `GiaVaoThucTe` = 45.80/33.60 trong script bước 2 trước khi chấm lại theo C-27 |
| C-27 | **Làm lại Pha 3**: P1 chỉ chấm khi chu kỳ đóng (C-19); P2 lãi/lỗ cả chu kỳ (giá vào = điểm 1/bình quân điểm 2, giá ra = bình quân trọng số, ±1% giữ nguyên); P3 chấm lại dòng đóng, xoá kết quả dòng mở |

## Phân tích phương án đã cân nhắc (giữ làm lịch sử)

- **Q-g — Điểm 2 lưu thế nào (xung đột unique index)? → ĐÃ CHỐT C-22 = A.**
  `IX_KetQuaKichBan_Symbol_LoaiKichBan_NgayDanhGia` (`.IsUnique()`, `ApplicationDbContext.cs:493`) chặn hai dòng cùng (Symbol, LoaiKichBan, NgayDanhGia). Hai phương án đã cân nhắc:
  - **(A) — CHỌN (C-22)**: thêm cột `GiaMuaDiem2ThucTe`/`ThoiGianMuaDiem2` trên dòng điểm 1; một dòng = một chu kỳ. **Ưu**: không phá index, không đổi query, migration đơn giản (thêm 2 cột null). **Nhược**: cột phình; khó mở rộng >2 lần mua (C-1 chốt tối đa 2).
  - **(B) — BÁC**: thêm `LaDiemMua2`; đổi index → `UNIQUE (Symbol, LoaiKichBan, NgayDanhGia, LaDiemMua2)`. **Nhược**: migration lớn (drop/rebuild index SQL Server); mọi query hiện tại phải lọc `LaDiemMua2==false`.

- **Q-h — `DinhTuLucMua` cập nhật từ khi nào sau điểm 2? → ĐÃ CHỐT C-23 = không reset.** `DinhTuLucMua` duy trì là "đỉnh cao nhất kể từ lúc **mua điểm 1**", mua điểm 2 xong **không tính lại mốc**; lý do: C-11 ghi "kể từ lúc mua" — hiểu là đầu chu kỳ; reset sẽ khiến mốc rút 4%/6% thấp hơn, nới rủi ro.

## Complexity Tracking

Không có vi phạm.
