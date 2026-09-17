# Feature Specification: Điều chỉnh giá theo sự kiện quyền

**Feature Branch**: `005-ohlcv-corporate-adjust`

**Created**: 2026-08-21

**Status**: Implemented

**Input**: User description: "Chọn phương án A: điều chỉnh giá theo quyền trước khi tính % / RS / FOMO / sóng ngành / Buy Score. Không chọn B (bỏ phiên GDKHQ) hay C (trung vị RS). Nguồn v1 = file seed nhập tay. Một công thức hai tham số (tiền + pha loãng), không enum từng loại. Case neo SSI GDKHQ 17/08/2026."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - % và RS không bị “rớt giả” ngày không hưởng quyền (Priority: P1)

Nhà đầu tư / hệ thống chấm điểm nhìn mã vừa chia thưởng cổ phiếu hoặc cổ tức tiền. Trên sàn, nến giảm mạnh (SSI 24.5 → 19.8 ngày 17/08) nhưng đó là **khoảng trống cơ học vì quyền**, cổ đông không mất phần kinh tế tương ứng. Hiện tại mọi phép đo % N phiên (RS 5 phiên, FOMO so đỉnh hộp, cổng % phiên) dùng Close **thô** → SSI bị khoảng −15% / RS −17.6, kéo **trung bình RS ngành** xuống âm, nhãn sóng ngành còn **Chớm** dù phiên đó rổ CK 16 tăng / 0 giảm.

Sau thay đổi, phép đo **lợi suất / sức mạnh tương đối** phải dùng dãy giá **đã điều chỉnh quyền**, để khoảng trống cơ học không còn bị hiểu là bán tháo.

**Why this priority**: Root cause đã quan sát trên production 21/08/2026 — lệch cảm quan (trần tím cả ngành) vs nhãn Chớm. Vá RS bằng trung vị hoặc bỏ 1 phiên không chữa FOMO / hộp / MA.

**Independent Test**: Với SSI và sự kiện GDKHQ 17/08/2026 (`tienMat` 1.0 + `heSoPhaLoang` 1.2), tính lợi suất 5 phiên kết thúc 21/08 trên dãy đã điều chỉnh; kết quả **không** còn khoảng −15% chỉ do gap quyền. RS ngành Chứng khoán ngày 21/08 **không** fail cửa RS **chỉ vì** gap SSI đó.

**Acceptance Scenarios**:

1. **Given** SSI có Close thô 14/08 = 24.5 và Close 17/08 ≈ 19.8 (GDKHQ), **When** hệ thống tính lợi suất 1 phiên qua ngày quyền trên dãy điều chỉnh, **Then** mức thay đổi **không** còn ≈ −19% như Close thô (19.8 / 24.5 − 1). So với **giá tham chiếu** `(24.5 − 1.0) / 1.2 ≈ 19.58`, Close ≈ 19.8 tương đương lợi suất khoảng **+1%**; sai số cho phép **±1 điểm phần trăm**.
2. **Given** cùng cửa sổ 5 phiên tới 21/08, **When** tính RS SSI vs VNINDEX trên dãy điều chỉnh, **Then** RS **không** còn ≈ −17.6 chỉ vì sự kiện 17/08.
3. **Given** ngành Chứng khoán ngày 21/08 có độ rộng 16 tăng / 0 giảm, **When** cửa RS ngành dùng RS đã điều chỉnh, **Then** cửa RS **không** bị đánh fail **duy nhất** bởi gap quyền SSI. Nhãn Strong vs Emerging **được đo lại** — không cam kết trước là Strong nếu các mã khác vẫn kéo RS ≤ 0.

---

### User Story 2 - Mọi cổng dùng % giá nhìn cùng một dãy đã điều chỉnh (Priority: P1)

Người vận hành không muốn “chỉ vá RS ngành”. FOMO so đỉnh hộp, % phiên kích hoạt, so sánh MA trên lịch sử, lợi suất T+2.5 — nếu vẫn dùng Close thô thì mã vừa chia thưởng sẽ trông như dump rồi bật, hộp Darvas gãy, FOMO lệch.

**Why this priority**: Cùng root cause; vá một cổng sẽ tái phát ở cổng khác.

**Independent Test**: Chọn một mã có sự kiện quyền trong cửa sổ hộp / FOMO; xác nhận các phép **% thay đổi giá** (không gồm khối lượng) dùng dãy điều chỉnh, trong khi **giá khớp lệnh hiện tại** trên thẻ mã vẫn là giá sàn. Nến lưu kho **không** bị ghi đè.

**Acceptance Scenarios**:

1. **Given** một mã có thưởng/tách trong 45 phiên hộp, **When** engine đo % so đỉnh hộp / biên hộp, **Then** không dùng Close thô hai phía của GDKHQ như thể cùng một thang giá.
2. **Given** nhà đầu tư mở chi tiết mã, **When** xem giá last / nến phiên hiện tại, **Then** số vẫn khớp giá giao dịch trên sàn (không nhân hệ số lên giá live).
3. **Given** chưa ghi nhận sự kiện quyền cho mã, **When** tính %, **Then** hành vi bằng dãy thô hiện tại (không bịa hệ số).

---

### User Story 3 - Sự kiện quyền được ghi nhận và kiểm chứng được (Priority: P2)

Người vận hành cần biết hệ thống **đã biết** GDKHQ nào, `tienMat` và `heSoPhaLoang` bao nhiêu — để SSI 17/08 và các đợt sau không phụ thuộc “nhớ trong chat”. **v1: danh sách sự kiện versioned (file seed trong repo), nhập tay khi HOSE công bố.** Không bắt buộc bảng CSDL hay crawler trước khi land P1. Crawl tự động = v2 khi có nguồn quyền tin cậy.

**Why this priority**: Không có sự kiện đã lưu thì không điều chỉnh được; P1 không chạy nổi.

**Independent Test**: Tra sự kiện SSI 17/08 trong seed: `tienMat` = 1.0 (1.000đ trên thang Close) và `heSoPhaLoang` = 1.2 (thưởng 5:1), ngày không hưởng quyền 17/08/2026. Thiếu sự kiện → dãy thô, có cảnh báo vận hành (không im lặng sai).

**Acceptance Scenarios**:

1. **Given** SSI 17/08/2026, **When** tra sự kiện quyền của mã, **Then** có bản ghi `tienMat` = 1.0 và `heSoPhaLoang` = 1.2, ngày không hưởng quyền 17/08/2026.
2. **Given** mã không có sự kiện trong cửa sổ, **When** tính RS, **Then** không áp hệ số bịa.
3. **Given** sự kiện thiếu ngày hoặc thiếu cả hai tham số có nghĩa, **When** nạp, **Then** bị từ chối / không áp dụng im lặng.

---

### Edge Cases

- Cùng ngày vừa cổ tức tiền vừa thưởng cổ phiếu (case SSI): **một** sự kiện, đủ cả `tienMat` và `heSoPhaLoang` — không hai nhánh riêng rồi cộng chồng.
- Chỉ tiền: `heSoPhaLoang` = 1. Chỉ thưởng / tách / gộp: `tienMat` = 0. Reverse split: `heSoPhaLoang` &lt; 1.
- Sự kiện trùng phiên cuối cửa sổ N ngày hoặc trùng nến đang tính FOMO.
- VNINDEX / mã không chia: không bị hệ số của cổ phiếu khác.
- Khối lượng: **không** điều chỉnh như giá ở v1. Vol× có thể nhảy sau thưởng — chấp nhận.
- Lịch sử không có trong seed: mã → dãy thô; backfill sự kiện 3 năm là vận hành, không chặn P1 nếu SSI đã seed.
- Không cam kết nhãn **Sóng mạnh** ngày 21/08 sau khi sửa — chỉ cam kết **không fail RS vì gap quyền SSI**.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Hệ thống PHẢI đọc danh sách sự kiện quyền **versioned** (seed trong repo, nhập tay). Mỗi `SuKienQuyen`: mã, `ngayKhongHuongQuyen`, `tienMat` (cùng thang Close; thiếu = 0), `heSoPhaLoang` (thiếu = 1). Một ngày được phép vừa tiền vừa pha loãng (SSI). **Không** bắt buộc bảng SQL hay crawler để land P1. **Không** enum/nhánh riêng theo loại sự kiện.
- **FR-002**: Mọi phép đo **lợi suất giá** hoặc **% thay đổi Close (hoặc High/Low/Open cùng thang)** trên N phiên — gồm RS 5 phiên, RS ngành (trung bình các RS mã), FOMO so đỉnh hộp, % phiên dùng cho cổng điểm vào — PHẢI dùng dãy đã điều chỉnh qua các sự kiện trong khoảng so sánh.
- **FR-003**: Giá last / OHLC **phiên đang khớp** hiển thị cho người dùng PHẢI là giá sàn, không nhân hệ số điều chỉnh lên giá live.
- **FR-004**: Nến OHLCV **đã lưu** (Job 1 / Job 2) PHẢI giữ giá thô. Điều chỉnh chỉ xảy ra lúc **tính % / chỉ báo**, không ghi đè lịch sử khớp lệnh.
- **FR-005**: Case neo SSI GDKHQ 17/08/2026 PHẢI có trong seed: `tienMat` = 1.0, `heSoPhaLoang` = 1.2, ngày 17/08/2026.
- **FR-006**: Khi không có sự kiện quyền cho mã trong cửa sổ, hành vi % PHẢI trùng dãy thô hiện tại.
- **FR-007**: Cửa sóng ngành **không đổi ngưỡng** (vẫn 4 trục, RS ngành > 0, trung bình cộng các mã). Slice này chỉ **làm sạch đầu vào RS**, không đổi luật Strong/Emerging, không đổi sang trung vị, không bỏ phiên GDKHQ khỏi cửa sổ như giải pháp gốc.
- **FR-008**: Sự kiện thiếu `ngayKhongHuongQuyen` hoặc không đọc được tham số PHẢI không được áp dụng (fail rõ, không hệ số 0 giả).
- **FR-009**: Kiểm chứng hồi quy: ít nhất case SSI 17/08 và một sự kiện quyền khác đã biết — lợi suất qua ngày quyền trên dãy điều chỉnh không còn bằng gap thô.
- **FR-010**: Mọi sự kiện (tiền, thưởng, tách, gộp, hoặc kết hợp) PHẢI dùng **một** cặp công thức dưới đây. ESOP / phát hành thêm giá khác mệnh **ngoài v1** (không quy về hai tham số này).

### Công thức (hợp đồng sản phẩm)

Hai đại lượng — **không** được gọi lẫn:

| Tên | Công thức | SSI 17/08 (`giaTruocQuyen` = 24.5) |
|-----|-----------|-------------------------------------|
| `giaThamChieu` | `(giaTruocQuyen − tienMat) / heSoPhaLoang` | `(24.5 − 1.0) / 1.2 ≈ 19.58` — giá Close “cùng phần kinh tế” sau quyền |
| `heSoNgayQuyen` | `giaThamChieu / giaTruocQuyen` = `(giaTruocQuyen − tienMat) / (giaTruocQuyen × heSoPhaLoang)` | `19.58 / 24.5 ≈ 0.799` — nhân vào nến **trước** ngày quyền để về thang sau quyền |

Lợi suất 1 phiên qua ngày quyền trên dãy điều chỉnh: `Close_GDKHQ / giaThamChieu − 1` (SSI ≈ 19.8 / 19.58 − 1 ≈ **+1%**), **không** phải `Close_GDKHQ / giaTruocQuyen − 1` (≈ **−19%**).

Đơn vị: `tienMat` **cùng thang Close**. 1.000đ = **1.0**. Ghi `1000` khi Close = 24.5 là sai đơn vị.

### Key Entities *(include if feature involves data)*

- **SuKienQuyen**: một đợt không hưởng quyền của một mã — `ngayKhongHuongQuyen`, `tienMat`, `heSoPhaLoang`. Không có trường “loại”.
- **DayGiaTho**: OHLC đã khớp / đã lưu — nguồn sự thật giá giao dịch.
- **DayGiaDieuChinh**: OHLC quy về thang sau quyền (nến trước quyền nhân `heSoNgayQuyen` lũy kế) — chỉ dùng để so sánh % / chỉ báo phụ thuộc % giá.
- **RS mã / RS ngành**: RS mã = % N phiên (điều chỉnh) − % chỉ số cùng N; RS ngành = trung bình cộng RS các mã đủ điều kiện trong ngành (không đổi công thức, đổi đầu vào).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Trên SSI, lợi suất 1 phiên qua 17/08/2026 dùng cho engine **không** còn khoảng −19% (Close thô 24.5→19.8). So với `giaThamChieu` ≈ 19.58, lợi suất điều chỉnh nằm trong **+1% ± 1 điểm phần trăm**.
- **SC-002**: RS 5 phiên SSI kết thúc 21/08/2026 dùng cho sóng ngành **không** còn ≈ −17.6 chỉ do sự kiện 17/08.
- **SC-003**: Cửa RS ngành Chứng khoán ngày 21/08 **không** fail **duy nhất** vì gap quyền SSI; nhãn Strong/Emerging được tính lại từ RS sạch — kết quả Strong **không** phải tiêu chí bắt buộc của spec này.
- **SC-004**: Giá last SSI/HCM trên thẻ chi tiết vẫn khớp giá sàn cùng thời điểm (thay đổi 0.00 so với nguồn quote).
- **SC-005**: Ít nhất 1 sự kiện quyền khác (không phải SSI 17/08) trong dữ liệu lịch sử thỏa “gap thô ≠ lợi suất điều chỉnh” cùng kiểu SC-001.

## Assumptions

- Đơn vị giá hệ thống: nghìn đồng (25 = 25.000đ) → cổ tức 1.000đ = **`tienMat` = 1.0**.
- Thưởng 5:1 = **`heSoPhaLoang` = 1.2** (5 cũ + 1 mới). Tách a:b quy về cùng một số pha loãng.
- Nến 17/08 ≈ 19.8 vs `giaThamChieu` ≈ 19.58: lệch nhỏ do biên độ phiên, không phải lỗi công thức.
- **Không** làm B (bỏ phiên GDKHQ) hay C (median RS) trong feature này.
- Khối lượng / Vol× **không** điều chỉnh ở v1.
- VNINDEX không áp sự kiện quyền của cổ phiếu.
- **Đã chốt 2026-08-21:** v1 = **nhập tay qua file seed** (không crawler, không bắt buộc bảng quyền). Crawler HOSE/vendor = v2. Ops: khi có TB GDKHQ, sửa seed rồi ship/restart — mỗi lần tính % đọc seed, không cần sửa engine.
- Job 1 / Job 2 **không** đổi: vẫn append OHLCV thô như hiện tại.
- ESOP / phát hành thêm giá khác mệnh: **ngoài v1**.
- Backtest “toàn bộ cổng 3 năm” là việc vận hành sau khi P1–P2 có dữ liệu sự kiện; spec không bắt buộc replay Top mọi ngày 3 năm trước khi land P1.
- Luật sóng ngành (4 trục, average RS) giữ nguyên — chỉ hết “RS âm giả”.
