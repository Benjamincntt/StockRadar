# Implementation Plan: V3 chấm điểm và đặt ngưỡng bán trên giá đã điều chỉnh theo quyền

**Branch**: `015-v3-rights-adjusted-prices` (làm trên `master`) | **Date**: 2026-11-05 | **Spec**: [spec.md](./spec.md)

**Input**: [spec.md](./spec.md) — owner chấp nhận Q1–Q5 ngày 05/11/2026 ("Đồng ý cả 5").

## Summary

Đưa mọi danh sách `Stock` mà V3 nạp (nhịp 16:00, 08:30, ATC, nạp chỉ số) qua **một** bước điều chỉnh theo quyền ngay sau khi nạp, dùng lại công thức 005/006 (`BoDieuChinhGiaTheoQuyen`). Từ đó chỉ báo, chỉ số thị trường, độ rộng và ngưỡng bán tự tính trên dãy đã điều chỉnh mà không phải sửa từng phép tính.

Sự kiện quyền lấy tự động từ FireAnt ở nhịp 16:00, lưu vào bảng mới, gộp với file tay (file tay thắng) thành một nguồn chung cho cả V1 và V3. Thêm ba phần nhỏ: chặn mã nhảy giá không rõ sự kiện (Q2), quy đổi vị thế đang giữ qua ngày quyền (Q3), tính lại 50 phiên chuỗi chỉ số khi có sự kiện mới (Q4).

## Technical Context

**Language/Version**: C# / .NET 10 (backend). Không đổi mobile, web.

**Primary Dependencies**: EF Core (SQL Server); `HttpClient` + `IMemoryCache` đang dùng cho FireAnt. Không thêm thư viện.

**Storage**: SQL Server `StockRadarDb` — bảng mới `SuKienQuyenTuDong`; thêm 2 cột vào `ViTheV3`. Migration mới.

**Testing**: xUnit trong `StockRadar.Tests` (hiện 527 test qua).

**Target Platform**: API Linux trên server production.

**Performance Goals**: nhịp 16:00 tăng ≤ 5 giây (1 lần gọi FireAnt + điều chỉnh ~1.100 mã; điều chỉnh chỉ chạy cho mã có sự kiện). Nhịp 08:30 / 14:25 tăng ≤ 1 giây (chỉ ~200 mã, đọc sự kiện từ bộ nhớ).

**Constraints**:
- Bộ điều chỉnh hiện có **chỉ nhân giá** (Open/High/Low/Close), giữ nguyên Volume (`BoDieuChinhGiaTheoQuyen.cs:81-89`). V3 tính TV20 = close × volume (`V3ChiSoToanThiTruong.TinhTv20`) nên phải chia Volume cho cùng hệ số trong bước của V3 để giữ giá trị giao dịch (FR-002). Không sửa bộ điều chỉnh dùng chung (V1 giữ nguyên hành vi).
- Giá live trong phiên là giá thô sau quyền. Dãy điều chỉnh lùi về quá khứ nên phiên gần nhất giữ nguyên → ngưỡng bán và giá live cùng thang (Assumption của spec).
- Cấu hình server khác bản repo: mọi khoá cấu hình mới phải có mặc định an toàn trong code.

**Scale/Scope**: ~1.100 mã, vài chục sự kiện quyền mỗi tháng, ≤ 5 vị thế mở.

## Constitution Check

*Source: `.specify/memory/constitution.md`*

- [x] **I. Code là nguồn sự thật**: đã đọc `V3Engine` (`LayCacMaAsync`, `LayMaNhipNheAsync`, `LayChiSoAsync`, `TinhNguongBan`), `V3SauPhienRunner`, `V3ChiSoToanThiTruong`, `BoDieuChinhGiaTheoQuyen`, `FileNguonSuKienQuyen`, `FireAntLichChotQuyenClient`.
- [x] **II. Spec trước**: spec 015 đã làm rõ Q1–Q5. Đổi dữ liệu đầu vào cổng chấm điểm + thêm bước vào nhịp 16:00 → đúng Spec Kit.
- [x] **III. Tối thiểu**: không đổi luật kịch bản, ngưỡng, xếp hạng, tỉ trọng; không đổi API, mobile, web. Một điểm điều chỉnh duy nhất trong `V3Engine`.
- [x] **IV. Cổng domain**: cập nhật `docs/domain/pipeline-jobs.md`, `specs/012` Phụ lục D, `CLAUDE.md` (dòng V3) cùng change set.
- [x] **V. Đơn giản**: dùng lại `BoDieuChinhGiaTheoQuyen` và `V3ChiSoToanThiTruong.TinhChuoi`; hàm mới đều thuần ở Domain.

## Nghiên cứu trước khi code (Phase 0)

### R1 — Dữ liệu sự kiện của FireAnt (Q1) — đã khảo sát 05/11/2026

Owner cho phép chạy script khảo sát (phương án a). Gọi `events/search` cho TCB, GMD, HDB, TPB, KDH, TCX, SSI, cửa sổ 07–11/2026, token khách lấy như `FireAntLichChotQuyenClient`. Script: scratchpad `fireant_probe.py` (không lưu vào repo).

- **Trường trả về chỉ có**: `eventID`, `symbol`, `name`, `title`, `recordDate`, `registrationDate`, `executionDate`, `type`. Không có trường tỷ lệ dạng số → tỷ lệ phải đọc từ `title`.
- **`recordDate` = ngày GDKHQ**, không phải ngày đăng ký cuối cùng (`registrationDate` mới là ngày đăng ký, sau GDKHQ một phiên). Bằng chứng: TCB `recordDate` 29/10, `registrationDate` 30/10, CafeF ghi GDKHQ 29/10; SSI `recordDate` 17/08 trùng GDKHQ ca neo của spec 005. → dùng thẳng `recordDate` làm `NgayKhongHuongQuyen`, **không** lùi một phiên.
- **Mẫu `title` gặp**:
  - Cổ phiếu (type 2): "Cổ tức năm 2025 bằng cổ phiếu, tỷ lệ 100:50" / "… tỷ lệ 2:1" / "… tỷ lệ 5:1". Dạng `a:b` = a cổ cũ nhận b cổ mới → hệ số pha loãng = 1 + b/a. Thưởng cổ phiếu (CafeF gọi "thưởng") cũng đến dưới type 2 với cùng mẫu.
  - Tiền (type 1): "Cổ tức đợt 1/2026 bằng tiền, tỷ lệ 500đ/CP" / "… tỷ lệ 1.000đ/CP" → tiền mặt = số đồng / 1.000 (thang nghìn đ của giá).
  - Quyền mua (type 3): **không gặp** trong mẫu khảo sát → bộ đọc chưa biết mẫu; gặp thì không ghi, log cảnh báo kèm `title` (xem P1).
- Nhiều sự kiện cùng mã + ngày là các dòng riêng (HDB 100:25 và 100:5; TPB 100:15 và 500đ/CP) → gộp theo D2.

### R2 — Đối chiếu với tin CafeF — khớp 6/6 (+ SSI)

| Mã | FireAnt `recordDate` | Đọc từ title | CafeF |
|---|---|---|---|
| TCB | 29/10 | 100:50 → ×1,5 | GDKHQ 29/10, thưởng 50% |
| GMD | 07/10 | 2:1 → ×1,5 | GDKHQ 07/10, thưởng 2:1 |
| HDB | 09/10 | 100:25 + 100:5 → ×1,30 | GDKHQ 09/10, 25% + 5% |
| TPB | 02/10 | 100:15 → ×1,15; 500đ → 0,5 | GDKHQ 02/10, 15% + 500 đ |
| KDH | 07/10 | 100:10 → ×1,10 | GDKHQ 07/10, 100:10 |
| TCX | 17/09 | 5:1 → ×1,20 | GDKHQ 17/09, 5:1 |
| SSI | 17/08 | 5:1 → ×1,2; 1.000đ → 1,0 | ca neo spec 005: ×1,2 + 1,0 |

Hệ số trên là AI đọc tay từ title; bộ đọc `V3DocSuKienFireAnt` phải cho đúng các số này (Kiểm thử ca 6–7, SC-003).

## Thiết kế

### D1 — Một điểm điều chỉnh trong `V3Engine`

Hàm mới `V3Engine.DieuChinhTheoQuyen(IReadOnlyList<Stock>)`: với mỗi mã, gọi `BoDieuChinhGiaTheoQuyen.TaoDayGiaDieuChinh`, rồi chia Volume của nến bị nhân cho cùng hệ số (làm tròn, giữ close × volume). Mã không có sự kiện trả nguyên object (không cấp phát).

Gọi ở **đầu ra** của `LayCacMaAsync` và `LayMaNhipNheAsync`. Mọi đường dùng sau đó đều nhận dãy đã điều chỉnh:

| Đường | Dùng dãy đã điều chỉnh qua |
|---|---|
| Chỉ báo mã + chỉ số + Breadth50 + UNI (16:00, 08:30, 14:25) | `LayChiSoAsync` → `V3ChiSoToanThiTruong.Tinh` |
| Ngưỡng bán 16:00 | `V3SauPhienRunner` → `closes` → `TinhNguongBan` |
| Ngưỡng bán khi khớp ATC | `V3AtcRunner` → `TinhNguongBan` |
| Nạp chuỗi chỉ số | `V3NapChiSoBackfillRunner` → `TinhChuoi` |
| Giá tham chiếu `ViTheV3.GiaThamChieu` | `closes[^1]` của dãy đã điều chỉnh = đóng cửa thô hôm nay khi không có sự kiện; khi có sự kiện ngày mai thì = giá tham chiếu sau quyền (đúng giá sàn dùng tính trần/sàn) |

Nhịp mỗi phút (`V3TrongPhienRunner`) không nạp nến → không đổi.

### D2 — Nguồn sự kiện tự động + gộp với file tay

- **Bảng `SuKienQuyenTuDong`**: `Id`, `Ma`, `NgayKhongHuongQuyen` (date), `TienMat`, `HeSoPhaLoang`, `SoCoCu`, `SoCoMoi`, `GiaPhatHanh`, `MoTaNguon` (title gốc), `GhiNhanLuc` (UTC). Khoá duy nhất (`Ma`, `NgayKhongHuongQuyen`). Nhiều sự kiện FireAnt cùng mã + ngày (cổ tức tiền + cổ phiếu + thưởng) gộp thành **một** dòng: cộng tiền mặt, nhân hệ số pha loãng.
- **Bước nạp** `V3NapSuKienQuyen` (Infrastructure): gọi FireAnt `events/search` kiểu 1/2/3 cho cửa sổ [hôm nay − 60 ngày, hôm nay + 15 ngày]; bộ đọc thuần `V3DocSuKienFireAnt` (Domain) đổi một sự kiện thành bộ số 006 hoặc trả "không đọc được": `NgayKhongHuongQuyen` = `recordDate` (R1); type 2 đọc "tỷ lệ a:b" → `HeSoPhaLoang` = 1 + b/a; type 1 đọc "tỷ lệ x đ/CP" (dấu chấm là phân cách nghìn) → `TienMat` = x / 1.000; type 3 và title không khớp mẫu → không ghi, log cảnh báo kèm title. Upsert theo khoá; không xoá dòng cũ. Lỗi mạng / định dạng → giữ dữ liệu đã lưu, log lỗi (FR-010). Tách hàm lấy token ra dùng chung với `FireAntLichChotQuyenClient` (không sao chép).
- **Nguồn gộp** `NguonSuKienQuyenTongHop : INguonSuKienQuyen` (singleton, thay đăng ký hiện tại): file tay + danh sách tự động trong bộ nhớ; trùng mã + ngày thì file tay thắng (FR-009). Có `LamMoiAsync()` đọc lại bảng; gọi ở đầu mỗi nhịp V3 và đầu `DailyAnalysisRunner` (V1 dùng chung — Q5). `StocksController` (form nhập tay của 006) vẫn ghi vào file như cũ.

### D3 — Chặn mã nhảy giá không rõ sự kiện (Q2, FR-011)

Hàm thuần `V3BuocNhayKhongRo.Tim(Stock, nguon, soPhien = 20)`: trên **nến thô**, tìm phiên trong 20 phiên gần nhất có |mở cửa / đóng cửa phiên trước − 1| > biên độ sàn + 1 điểm % (HOSE 7%, HNX 10%, UPCOM 15%) mà nguồn không có sự kiện đúng ngày đó. Có → mã bị loại khỏi danh sách ứng viên trước khi `V3BoKichBan.Cham` xếp hạng (cả 08:30 và 14:25), log cảnh báo một dòng mỗi mã: mã, ngày, % nhảy.

Lỗi đơn vị giá (VPG, DDG… 1,00 ↔ 900+) sẽ rơi vào nhánh này và bị loại luôn — chấp nhận, log ghi rõ để phân biệt.

### D4 — Quy đổi vị thế qua ngày quyền (Q3, FR-007)

Ở nhịp 16:00 phiên D, với mỗi vị thế DangGiu có sự kiện có `NgayKhongHuongQuyen` = phiên kế tiếp (D+1) và chưa quy đổi:
- `GiaVao` = giá tham chiếu theo công thức 005/006 tính trên `GiaVao` cũ (`BoDieuChinhGiaTheoQuyen.TinhGiaThamChieu`).
- `KhoiLuong` × `HeSoPhaLoang` (làm tròn xuống cổ phiếu nguyên). Quyền mua trả tiền **không** cộng khối lượng (owner chưa chắc thực hiện quyền) — xem P1.
- `TienCoTuc` += khối lượng cũ × `TienMat` (cột mới, nghìn đ).
- `GiaTriLenh` giữ nguyên (tiền đã bỏ ra không đổi).
- `QuyDoiQuyenDen` = ngày sự kiện (cột mới) → không quy đổi hai lần.

Làm trước vòng tính ngưỡng bán để ngưỡng phiên D+1 tính với giá vào mới. Lãi % khi đóng (`V3ChamKetQua.Cham`, màn Hiệu quả): (giá thoát × khối lượng + `TienCoTuc`) / `GiaTriLenh` − 1 − phí. Tiền mặt danh mục cộng thêm `TienCoTuc` khi vị thế đóng.

Spec 014 (đối chiếu nhật ký) ghi đè khối lượng/giá vào theo lệnh thật ở **phiên khớp**; quy đổi D4 chạy sau đó nên không xung đột.

### D5 — Tính lại chuỗi chỉ số khi có sự kiện mới (Q4, FR-012)

Bước nạp D2 trả danh sách mã có sự kiện **mới** (dòng mới hoặc đổi hệ số). Nếu có mã thuộc UNI: nhịp 16:00 gọi `V3ChiSoToanThiTruong.TinhChuoi` với `batDau` = 50 phiên trước, trên dãy đã điều chỉnh, rồi upsert `ChiSoNgayV3` (`ChiSo`, `ChiSoMa50`, độ lệch, `Breadth50`). Giữ `DanhSachUni` đã lưu. Không đụng `TinHieuV3`, `ViTheV3`.

Khi deploy: chạy một lần `v3-nap-chi-so` (đã có) để tính lại toàn chuỗi từ `V3:TuNgayNapChiSo` trên dãy đã điều chỉnh — **sau** khi bước nạp D2 đã chạy một lần với cửa sổ đủ dài (tham số cửa sổ cho lần đầu: từ 01/2023).

### D6 — Nghiên cứu (US5, FR-013)

- Xuất bảng `SuKienQuyenTuDong` + file tay ra CSV khi xuất dữ liệu nghiên cứu.
- `load.py` nhận thêm đường dẫn CSV sự kiện, nhân giá (và chia khối lượng) như D1 trước khi lưu `bars.pkl`; `BAD` vẫn giữ để bắt lỗi dữ liệu khác.
- Chạy lại danh mục luật production 2023–2026 trên hai bộ giá, ghi PH mới (số tín hiệu đổi, chênh lãi/sụt).

## Thay đổi theo file

| File | Thay đổi |
|---|---|
| `backend/StockRadar.Infrastructure/MarketData/V3Engine.cs` | `DieuChinhTheoQuyen` (D1), gọi ở `LayCacMaAsync` + `LayMaNhipNheAsync`; quy đổi vị thế D4 |
| `backend/StockRadar.Domain/Services/V3DocSuKienFireAnt.cs` (mới) | Bộ đọc thuần title/trường FireAnt → bộ số 006 (D2) |
| `backend/StockRadar.Domain/Services/V3BuocNhayKhongRo.cs` (mới) | Hàm thuần D3 |
| `backend/StockRadar.Domain/Services/V3QuyDoiViTheQuyen.cs` (mới) | Hàm thuần D4 |
| `backend/StockRadar.Domain/Entities/V3Entities.cs` | `SuKienQuyenTuDongEntity`; `ViTheV3Entity` thêm `TienCoTuc`, `QuyDoiQuyenDen` |
| `backend/StockRadar.Infrastructure/Persistence/ApplicationDbContext.cs` | DbSet + khoá duy nhất |
| `backend/StockRadar.Infrastructure/Migrations/<ngày>_AddSuKienQuyenTuDong.cs` (mới) | Bảng mới + 2 cột. **Đọc lại file sinh ra trước khi apply** — `dotnet ef migrations add` trong repo này hay sinh body sai |
| `backend/StockRadar.Infrastructure/MarketData/V3NapSuKienQuyen.cs` (mới) | Gọi FireAnt, upsert, trả mã có sự kiện mới (D2) |
| `backend/StockRadar.Infrastructure/MarketData/FireAntLichChotQuyenClient.cs` | Tách hàm lấy token dùng chung (không đổi hành vi cổng V1) |
| `backend/StockRadar.Infrastructure/MarketData/NguonSuKienQuyenTongHop.cs` (mới) | Gộp file + bảng (D2) |
| `backend/StockRadar.Infrastructure/DependencyInjection.cs` | Đăng ký nguồn gộp thay `FileNguonSuKienQuyen` |
| `backend/StockRadar.Infrastructure/MarketData/V3SauPhienRunner.cs` | Thứ tự: nạp sự kiện (D2) → làm mới nguồn → nạp mã → chỉ số → đối chiếu nhật ký (014) → quy đổi D4 → ngưỡng bán → tính lại chuỗi D5 |
| `V3TruocPhienRunner.cs`, `V3AtcRunner.cs` | Làm mới nguồn ở đầu nhịp; lọc mã D3 trước `V3BoKichBan.Cham` |
| `backend/StockRadar.Infrastructure/MarketData/DailyAnalysisRunner.cs` | Làm mới nguồn ở đầu nhịp (Q5) |
| `backend/StockRadar.Domain/Services/V3ChamKetQua.cs`, `backend/StockRadar.Infrastructure/MarketData/V3HieuQuaService.cs`, `V3Engine.LayTrangThaiDanhMucAsync` | Cộng `TienCoTuc` vào lãi % và tiền mặt (D4) |
| `backend/StockRadar.Tests/V3/*` (mới) | Test mục Kiểm thử |
| `docs/domain/pipeline-jobs.md`, `CLAUDE.md`, `specs/012-v3-scenario-engine/spec.md` Phụ lục D | Nhịp 16:00 thêm bước; V3 dùng giá đã điều chỉnh |
| `docs/features/v3-scenario-engine/research/scripts/load.py` + PH mới | D6 |

Không đổi: luật kịch bản, ngưỡng, xếp hạng, tỉ trọng, tin Telegram / đẩy app, API, mobile, web, bộ điều chỉnh dùng chung `BoDieuChinhGiaTheoQuyen`.

## Kiểm thử

**Unit** (dữ liệu bịa ghi rõ là giả, trừ ca TCB dùng giá đóng cửa thật đã công khai):
1. `DieuChinhTheoQuyen` mã không có sự kiện → trả nguyên object, chỉ báo trùng trước thay đổi (SC-004).
2. TCB nến thật 06/2026–02/11/2026 + sự kiện 29/10 ×1,5 → RSI14 39,7 ± 0,5, ret20 −1,2% ± 0,5, floor20 = 0 (SC-001, US1).
3. Cùng dữ liệu → TV20 trước và sau điều chỉnh bằng nhau (± 0,1%).
4. Ngưỡng bán TCB tính ngày 04/11 ≈ 22,1 ± 0,1 (SC-002).
5. Chỉ số: lợi nhuận ngày TCB phiên 29/10 đóng góp ≈ +0,1% (US3).
6. `V3DocSuKienFireAnt`: các title thật ở R1 ("tỷ lệ 100:50", "tỷ lệ 2:1", "tỷ lệ 5:1", "tỷ lệ 500đ/CP", "tỷ lệ 1.000đ/CP") → đúng bộ số bảng R2; `NgayKhongHuongQuyen` = `recordDate`; type 3 và title lạ → "không đọc được".
7. Gộp nhiều sự kiện cùng ngày (HDB 25% + 5%) → hệ số 1,30; TPB 15% + 500 đ → 1,15 và 0,5.
8. Nguồn gộp: trùng mã + ngày → file tay thắng.
9. `V3BuocNhayKhongRo`: nhảy −32% không sự kiện → bị loại 20 phiên; có sự kiện → không loại; nhảy −6% trên HOSE → không loại.
10. `V3QuyDoiViTheQuyen`: 100 cp × 33,0, thưởng 50% → 150 cp × 22,0, `GiaTriLenh` giữ nguyên; cổ tức tiền 1,0 → `TienCoTuc` = 100; chạy lần hai → không đổi.
11. Lãi % có `TienCoTuc`.

**Tích hợp (DB local)**: chèn sự kiện TCB vào `SuKienQuyenTuDong`, gọi `POST /api/v1/market/jobs/v3-sau-phien`, kiểm `ChiSoNgayV3` 50 phiên được tính lại và `NguongChamMa` của vị thế thử. Dọn dữ liệu thử sau khi kiểm.

**Chạy thật sau ship**: nhịp 16:00 đầu tiên — log "nạp sự kiện quyền: N dòng, M mới", "tính lại chỉ số 50 phiên", và các dòng cảnh báo mã nhảy giá không rõ sự kiện. So 6 sự kiện R2 trong bảng.

## Câu hỏi cho owner (plan)

- **P1 — Vị thế giữ qua ngày quyền mua trả tiền**: (a) không cộng khối lượng quyền mua, chỉ quy đổi giá vào theo công thức 006 (giá trị vị thế trên sổ giảm đúng bằng phần quyền chưa thực hiện); (b) log cảnh báo, không quy đổi, owner sửa tay. *Đề xuất của AI*: (b) — quyền mua hiếm, mà (a) làm sổ lệch nếu owner có thực hiện quyền.
- ~~R1~~: owner chọn (a) "Cho chạy script", đã khảo sát 05/11/2026.

## Rủi ro

| Rủi ro | Cách chặn |
|---|---|
| FireAnt đổi API / token → không có sự kiện mới | Giữ sự kiện cũ, log lỗi; D3 vẫn chặn mã nhảy giá → không ra tín hiệu giả; file tay vẫn dùng được |
| Bộ đọc title sai hệ số → điều chỉnh sai | Title không khớp mẫu thì bỏ, không đoán; R2 đối chiếu 6 sự kiện trước khi bật; file tay sửa đè được |
| Đổi dãy giá làm đổi tín hiệu V3 so với trước | Đó là mục tiêu; đo trước bằng D6 (PH mới) |
| Tính lại chuỗi chỉ số đổi số lịch sử trên màn Hiệu quả | Chỉ đổi `ChiSoNgayV3`; tín hiệu, vị thế đã ghi giữ nguyên |
| Cấu hình server khác repo | Không thêm khoá bắt buộc; FireAnt dùng cấu hình `FireAnt` đã có trên server |
| Migration sinh body sai | Đọc lại file migration trước khi apply |

## Complexity Tracking

Không có vi phạm hiến pháp cần biện minh.
