# Feature Specification: V3 — Bộ kịch bản P1++ (bán realtime), thay V2

**Feature Branch**: `012-v3-scenario-engine`

**Created**: 2026-10-26

**Status**: v2 đã duyệt và đã chạy production từ 27/10/2026. **Bổ sung v3 (2026-10-27)**: luật bán riêng cho lệnh mua lúc hoảng loạn sâu — FR-015a, FR-016a, FR-024 (cờ), Phụ lục A (ngoại lệ), Phụ lục E (dòng tin), SC-008. Owner yêu cầu đưa vào V3 ngày 2026-10-27 (nguồn: research PH-21).

**Input**: Chủ sản phẩm: "Làm V3 = bộ kịch bản P1++ (bán realtime), gỡ sạch phần V2 không dùng."

**Nguồn luật chuẩn**: [`docs/features/v3-scenario-engine/research/03-ket-qua.md`](../../docs/features/v3-scenario-engine/research/03-ket-qua.md) (bảng "Luật từng kịch bản"), [`01-phuong-phap.md`](../../docs/features/v3-scenario-engine/research/01-phuong-phap.md) (định nghĩa chỉ báo), [`02-so-phat-hien.md`](../../docs/features/v3-scenario-engine/research/02-so-phat-hien.md) (lý do từng luật). Ba file này là **chỉ dẫn (normative)**; khi tài liệu nghiên cứu lệch nhau → hỏi owner, không tự chọn.

---

## Bối cảnh & vì sao

Backtest trên dữ liệu production 01/2023 – 22/10/2026 (xem research 03):

| Bộ | Kết quả |
|---|---|
| V2 mô phỏng (luật thật, đã đối chiếu 13/22 lệnh thật trùng đúng ngày) | Thắng 27–41%, lãi TB âm. Danh mục 2023 −1%, 2024 −26%, 2025 −34%, 2026 −39% |
| Lệnh V2 thật trên production (30/09–22/10/2026) | 11 lệnh đã đóng, thắng 3, lãi TB −1,79% |
| **P1++ bản realtime (V3)** | 2023 +10%, 2024 +28%, 2025 +75%, 2026 +84% (tới 22/10); sụt giảm tối đa −15,4% |

Ba kịch bản mua V2 thuộc nhóm "mua theo đà / breakout" — nhóm đã chứng minh thua lỗ (PH-01, PH-14). Bộ P1++ gồm 5 kịch bản bắt đáy / hiệu ứng mùa vụ, mua ATC – bán realtime (PH-06, PH-16).

**Phạm vi owner đã chốt:**

- V3 = bộ P1++, bản **bán realtime trên giá khớp trong phiên**. Không dùng luật "xét giá đóng cửa rồi bán phiên sau". Lệnh theo thời điểm cố định (ATO/ATC) được phép.
- **Gỡ toàn bộ V2 không dùng**: xoá hẳn, không giữ enum, không dán nhãn "deprecated", không để lớp chuyển tiếp.
- V3 **bắn Telegram thay V2**, chạy thật ngay sau khi xong, **không chạy song song**.
- V1 (Home Top 5, Job 1/Job 2, Daily analysis) **giữ nguyên**, không thuộc việc này.
- Các số backtest gồm giả định tiền nhàn rỗi gửi lãi 4,5%/năm; **app không mô phỏng phần này** (xem FR-021).

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Nhận tín hiệu V3 realtime qua Telegram (Priority: P1)

Nhà đầu tư mở app/biểu đồ trong phiên. Khi một trong 5 kịch bản V3 báo và vượt qua xếp hạng + ràng buộc danh mục, app bắn Telegram: tin MUA và tin BÁN cho vị thế đang giữ (giá khớp đã chạm điều kiện thoát). Hai nhịp thời điểm mua, không được hiểu thành mua trễ thêm một phiên:

- **KB1/KB2 (mua mở cửa)**: điều kiện báo chấm trên **giá đóng cửa phiên T** → tin MUA bắn lúc **08:30 sáng phiên T+1** → mua **ATO/mở cửa ngay đầu phiên T+1**.
- **KB4/N1/N3 (mua ATC)**: điều kiện báo chấm lúc **~14:25 phiên tín hiệu T** bằng giá khớp hiện tại thay giá đóng cửa → tin MUA bắn ~14:25 → mua **ATC cuối chính phiên T đó**.

**Why this priority**: Không có tín hiệu đúng luật thì không có gì để giao dịch; đây là sản phẩm chính.

**Independent Test**: Chạy bộ job V3 thủ công trên DB local với dữ liệu bịa có kiểm soát (phiên hoảng loạn, ngành giảm sâu, cuối tháng) → tín hiệu bắn ra đúng luật từng kịch bản, đúng xếp hạng top 5, đúng luật bán realtime.

**Acceptance Scenarios**:

1. **Given** dữ liệu phiên T thỏa KB1 (`BREADTH50` < 0,20 và `TV20` > 30) trên **giá đóng cửa phiên T**, **When** job trước phiên chạy 08:30 sáng phiên T+1, **Then** tối đa 5 mã có `RS126` thấp nhất được báo MUA và **khớp ATO đầu phiên T+1** (không phải phiên T+2).
2. **Given** một vị thế KB4 đang giữ, **When** giá khớp trong phiên ≥ max(chạm MA10, giá vào × 1,004) và đã qua T+2.5, **Then** bắn tin BÁN trong phút đó, không chờ giá đóng cửa.
3. **Given** vị thế chưa đủ T+2.5, **When** giá chạm điều kiện thoát, **Then** KHÔNG bán, không bắn tin bán.
4. **Given** tín hiệu N3 vào ngày `LEFT` = 5, **When** chỉ số đang cách MA50 trong ±1,5%, **Then** loại toàn bộ N3 của ngày đó.

---

### User Story 2 — Danh mục 5 vị thế với ưu tiên và tỉ trọng (Priority: P1)

Hệ thống tự quản lý ràng buộc danh mục: tối đa 5 vị thế, ưu tiên KB1 > KB2 > KB4 > N1 > N3, không mua trùng mã đang giữ, mỗi lệnh = giá trị danh mục / 5 (KB1/KB2/N1 nhân 1,5), không vượt tiền mặt còn lại.

**Why this priority**: Tín hiệu riêng lẻ chưa đủ — danh mục là nơi backtest chứng minh +352%. Nếu bỏ ràng buộc này thì số liệu hiển thị không phản ánh chiến lược đã kiểm.

**Independent Test**: Dựng dữ liệu bịa có 7 tín hiệu cùng ngày thuộc cả 5 loại, tiền mặt hữu hạn → kiểm thứ tự được chọn, tỉ trọng ×1,5, và lệnh bị cắt vì hết tiền mặt.

**Acceptance Scenarios**:

1. **Given** 3 vị thế còn trống và 6 tín hiệu đạt, **When** chọn theo ưu tiên, **Then** chỉ 3 lệnh được mở, theo đúng thứ tự KB1 > KB2 > KB4 > N1 > N3.
2. **Given** giá trị danh mục 1,0 tỷ, **When** mở một lệnh KB2 (hệ số 1,5), **Then** giá trị lệnh = (1 tỷ / 5) × 1,5 = 200 triệu × 1,5 = **300 triệu (30% danh mục)** (nếu đủ tiền mặt).
3. **Given** tiền mặt còn 80 triệu, **When** lệnh KB2 cần 300 triệu, **Then** giá trị lệnh bị cắt còn 80 triệu (không vượt tiền mặt).
4. **Given** đã giữ HPG, **When** HPG lại vào top một kịch bản khác, **Then** bị loại (không trùng mã).

---

### User Story 3 — Xem hiệu quả V3 trên web và mobile (Priority: P2)

Nhà đầu tư mở màn "Hiệu quả": tóm tắt theo từng kịch bản V3 (số lệnh, thắng, lãi TB) và danh sách lệnh đã đóng/đang giữ; màn chi tiết mã hiện tín hiệu V3 gần nhất của mã đó.

**Why this priority**: Cần theo dõi nhưng không chặn việc bắn tín hiệu.

**Independent Test**: Sau khi job V3 tạo tín hiệu/vị thế trên DB local, gọi API mới và mở màn hình → số liệu khớp đúng bảng trong DB.

**Acceptance Scenarios**:

1. **Given** DB local có vị thế V3 đã đóng, **When** mở màn Hiệu quả, **Then** kịch bản và kết quả (Thắng/Thua/Hoà vốn) hiển thị đúng.
2. **Given** mã chưa từng có tín hiệu V3, **When** mở màn chi tiết mã, **Then** hiện trạng thái rỗng, không lỗi.

---

### Edge Cases

- **Nến giữ chỗ lúc 08:30 (BẮT BUỘC xử lý)**: `Stocks.HistoryJson` đã chứa nến hôm nay giữ chỗ (giá tham chiếu, khối lượng 0); trong phiên chứa nến dở dang. Không loại bỏ/xử lý đúng → tín hiệu lệch một ngày. Bằng chứng: Pha 1 V2 chỉ khớp FORMING 44/49 khi tính đến nến giữ chỗ (PH-14).
- Mã trong `UNI` nhưng thiếu lịch sử đủ 130 phiên → không tính được `RS126`/MA50 → loại khỏi xếp hạng kịch bản cần chỉ số đó, không crash.
- Không lấy được giá realtime trong phiên → bỏ qua tick đó, không bắn tin, không bán (fail-open, giữ invariant B3 kiểu V2 nhưng cho V3).
- Ngày `LEFT` = 5 rơi vào phiên cuối trước kỳ nghỉ dài → vẫn tính theo lịch giao dịch thật, không tự "đoán" tháng.
- Bán rơi đúng phiên sàn trắng bên mua (H = L = giá sàn, đóng ở sàn) → không bán được, vị thế giữ sang phiên sau, đồng hồ "giữ tối đa" vẫn tính.
- Hết "giữ tối đa" mà giá chưa chạm điều kiện thoát → bán ATC phiên hôm đó (~14:25).
- Giá mua khớp đúng giá trần → bỏ lệnh mua.
- Tín hiệu MUA bắn lúc ~14:25 nhưng đến 14:45 phiên bị suspend/thanh khoản bằng 0 → lệnh không khớp; hệ thống chỉ ghi kế hoạch, không giả định đã khớp.
- DB local chỉ 526 mã → số tín hiệu ít hơn production; không dùng số local để đối chiếu backtest.

## Requirements *(mandatory)*

### Functional Requirements

**Chỉ báo dùng chung (tính một lần, mọi kịch bản dùng chung):**

- **FR-001**: Hệ thống PHẢI dùng đơn vị giá nghìn đồng và các định nghĩa sau (bản research 01), KHÔNG dùng `IndicatorMath` hiện có cho các chỉ báo này:
  - `TV20` = TB 20 phiên của close × volume / 10⁶, đơn vị tỷ đồng.
  - Vũ trụ `UNI`: `TV20` ≥ 5 **và** close ≥ 5.
  - Chỉ số bình quân đều `MKT`: lợi nhuận ngày = TB lợi nhuận các mã `UNI` của **phiên trước**, mỗi mã kẹp ±16%; cộng dồn thành chỉ số. MA20/MA50 tính trên chỉ số này. Đây **không phải VNINDEX**.
  - `BREADTH50` = tỉ lệ mã `UNI` có close > MA50 của chính nó.
  - `RS63`/`RS126` = hạng phần trăm trong `UNI` của close/close[−63] (hoặc [−126]).
  - `RSI14` = làm mượt Wilder (alpha 1/14) — **hàm mới**, khác `IndicatorMath.Rsi` (cộng dồn 14 phiên).
  - MA = trung bình đơn giá đóng cửa; `RET20` = close/close[−20] − 1.
  - `SECR20` = trung vị `RET20` của các mã `UNI` cùng `Stocks.Sector`.
  - Phiên sàn: lợi nhuận ngày ≤ −(biên độ − 0,5%), biên độ HOSE 7% / HNX 10% / UPCOM 15%. `FLOOR20` = số phiên sàn trong 20 phiên.
  - `LEFT` = số phiên còn lại tới cuối tháng (0 = phiên cuối); `TDM` = thứ tự phiên trong tháng (0 = phiên đầu).
  - "Chạm MAn" realtime = giá khớp ≥ TB (n−1) giá đóng cửa trước.
- **FR-002**: Hệ thống PHẢI loại nến giữ chỗ / nến dở dang của ngày hôm nay khỏi mọi tính báo trước phiên, và PHẢI dùng đúng tập nến khi tính lúc 08:30 so với trong phiên (xem Edge Cases).
- **FR-003**: Hệ thống PHẢI tính `SECR20` từ `Stocks.Sector`; mã không có ngành hợp lệ → không đủ điều kiện KB4/N1.

**Luật 5 kịch bản (đúng như research 03 — xem Phụ lục A):**

- **FR-010**: Hệ thống PHẢI báo và xếp hạng 5 kịch bản KB1, KB2, KB4, N1, N3 theo đúng điều kiện báo, đúng thước đo xếp hạng (top 5/ngày), đúng thời điểm mua và đúng luật bán trong Phụ lục A. Không thêm/bớt ngưỡng nào so với tài liệu nghiên cứu.
- **FR-011**: Tham số hoá toàn bộ ngưỡng (`TV20`, −7%/−5% MA50, −10%/−12% `SECR20`, 0,35/0,20 `BREADTH50`, `LEFT`, ±1,5%, ×1,004, số phiên giữ, top 5, 5 vị thế, tỉ trọng 1,5) trong appsettings; không hardcode trong logic.

**Mua:**

- **FR-012**: KB1, KB2 mua **ATO/mở cửa phiên T+1** — điều kiện báo chấm trên **giá đóng cửa phiên T** (job sau phiên 16:00 phiên T), tin MUA bắn lúc **08:30 sáng phiên T+1**, và lệnh khớp **đầu chính phiên T+1**. Không được hiểu là mua phiên T+2.
- **FR-013**: KB4, N1, N3 mua **ATC phiên tín hiệu** — tín hiệu tính lúc khoảng 14:25 bằng **giá khớp hiện tại thay giá đóng cửa**.
- **FR-014**: Hệ thống PHẢI bỏ lệnh mua nếu giá mua rơi đúng giá trần.

**Bán (realtime — bắt buộc):**

- **FR-015**: Luật bán của KB1/KB2/KB4 = giá khớp ≥ max(chạm MA10, giá vào × 1,004); N1 = giá khớp ≥ max(chạm MA20, giá vào × 1,004); N3 = lệnh ATO phiên `TDM` = 2 của tháng sau. PHẢI đánh giá trên giá khớp trong phiên mỗi phút, **không** chờ giá đóng cửa.
- **FR-015a** *(bổ sung v3)*: **Lệnh hoảng loạn sâu** = vị thế KB1/KB2/KB4/N1 có `BREADTH50` **của phiên tín hiệu** < 0,15 (nhỏ hơn hẳn; đúng 0,15 thì không phải). Với lệnh này, luật bán FR-015 được **thay** bằng: bán realtime khi giá khớp ≥ **giá vào × 1,15**; không dùng ngưỡng chạm MA. N3 không bao giờ là lệnh hoảng loạn sâu. Cờ được chốt **một lần** lúc tạo vị thế và không đổi về sau:
  - KB1/KB2: lấy `BREADTH50` của phiên tín hiệu T (đóng cửa), đã có lúc nhịp 08:30 tạo lệnh chờ mua.
  - KB4/N1: lấy `BREADTH50` mà nhịp ATC ~14:25 vừa tính (giá khớp lúc đó). Backtest dùng độ rộng lúc đóng cửa — chênh lệch này ghi ở Assumptions.
  - Ngưỡng bán = giá vào khớp × 1,15, ghi vào vị thế lúc khớp mua; nhịp 16:00 **không được** ghi đè bằng ngưỡng MA.
- **FR-016**: "Giữ tối đa" tính **từ phiên tín hiệu**; hết hạn thì bán ATC (~14:25 phiên đó).
- **FR-016a** *(bổ sung v3)*: Lệnh hoảng loạn sâu có "giữ tối đa" = **40 phiên** (thay 15/10/15 của KB1/KB2/KB4/N1); hết hạn bán ATC như FR-016. FR-017 (T+2.5) và FR-018 (sàn trắng) áp như mọi lệnh.
- Ba ngưỡng của FR-015a/FR-016a (0,15 · 1,15 · 40) nằm trong cấu hình V3 (FR-011), không hardcode.
- **FR-017**: T+2.5 — sớm nhất được bán buổi chiều phiên T+2, với T là phiên mua.
- **FR-018**: Phiên sàn trắng bên mua (H = L = giá sàn, đóng ở sàn) → không được phép bán trong phiên đó.

**Danh mục:**

- **FR-019**: Tối đa 5 vị thế; ưu tiên KB1 > KB2 > KB4 > N1 > N3; không mua trùng mã đang giữ. **Một chỗ danh mục/tiền mặt chỉ được giải phóng sau khi lệnh bán KHỚP** (không giải phóng ngay khi ra lệnh). Lưu ý: backtest giải phóng ngay trong ngày → kết quả thật có thể thấp hơn backtest (ghi ở Assumptions).
- **FR-020**: Mỗi lệnh = giá trị danh mục / 5 (≈ 20% nền); riêng KB1, KB2, N1 nhân 1,5 (≈ 30% danh mục); không vượt tiền mặt còn lại. Giá trị danh mục và tiền mặt xác định như Phụ lục C (công thức chuẩn, phải nêu trong spec và trong code).
- **FR-021**: Hệ thống KHÔNG mô phỏng lãi tiền nhàn rỗi — tiền mặt để 0%. Số backtest +352% có giả định gửi 4,5%/năm; app chỉ so được với bản "Lãi 0%" (+307%, sụt giảm −16,6%). Có luật FR-015a/FR-016a: +391% (gửi 4,5%) / **+344%** (lãi 0%), sụt giảm giữ −15,4% / −16,6%.

**Tín hiệu & phân phối:**

- **FR-022**: V3 PHẢI bắn Telegram qua luồng gửi tin hiện có (`ITelegramNotifier`), **thay thế** tin V2; mẫu tin ở Phụ lục E.
- **FR-023**: V3 chạy thật ngay sau khi implement; KHÔNG chạy song song với V2 (V2 đã bị gỡ trong cùng change set).
- **FR-024**: Mỗi tín hiệu/vị thế PHẢI lưu snapshot đủ để giải thích: loại kịch bản, ngày tín hiệu, giá trị các chỉ báo quyết định (`TV20`, `RS126`/`RSI14`/`RET20`/`SECR20`/`RS63`, `BREADTH50`, độ lệch chỉ số so MA50, `FLOOR20`, `LEFT`), giá vào, giá trị lệnh, giá thoát và lý do thoát. *(bổ sung v3)* Vị thế lưu thêm cờ lệnh hoảng loạn sâu (FR-015a); lý do thoát của lệnh này khi chạm ngưỡng là "chốt +15% hoảng loạn sâu", tách khỏi "chạm MA".

**Gỡ V2:**

- **FR-030**: Toàn bộ mã V2 liệt kê trong Phụ lục B PHẢI bị xoá khỏi code, config, DB, UI, docs. Không giữ enum, không nhãn "deprecated", không lớp chuyển tiếp.
- **FR-031**: Sau khi gỡ, grep các khoá `KetQuaKichBan|LoaiKichBan|Pha1TruocPhien|Pha2TrongPhien|Pha3DoLuong|SoTuyen|XepHangCoHoi|V2Telegram|KichBanNoHuongLen|KichBanHoiHoTro|KichBanQuetThanhKhoan` PHẢI trả 0 kết quả ngoài `Migrations/`.
- **FR-032**: Tên mới của V3 PHẢI né các khoá ở FR-031 (quy ước `V3…`, xem Phụ lục F) để lệnh kiểm không report dương tính giả.
- **FR-033**: KHÔNG chép logic V2 sang V3: điểm mua 2, chốt lời 1/2, rút mốc 4/6%, dừng theo thời gian 5 phiên, dừng lỗ đuổi ATR, Kiệt sức/Gãy nền, sơ tuyển 10 tỷ.

**Nhật ký (spec 010):**

- **FR-040**: Phần lệnh tự nhập của nhật ký giữ nguyên. Phần "gắn lệnh với tín hiệu" **trỏ sang V3** (phương án (a), owner chốt 2026-10-26): **endpoint nhật ký đổi hẳn thành `GET /nhat-ky/tin-hieu/v3`** (KHÔNG giữ đường dẫn `tin-hieu/v2` trỏ sang V3), `NhatKyTinHieuBoQua` và `EfNhatKyTinHieuRepository` trỏ sang `TinHieuV3`, giá chụp theo chiều lệnh V3 (mua → giá vào; bán → giá thoát). **Không giữ bất kỳ đường đọc nào từ `KetQuaKichBan`.** Mobile `nhat_ky_tin_hieu_picker.dart` (`_nguon = 'v3'`) và web cũng gọi `/tin-hieu/v3`. Phần lệnh tự nhập không đổi. Xử lý dữ liệu gắn-tín hiệu-V2 cũ trong nhật ký được quyết **cùng lúc với migration** và **DỪNG chờ owner duyệt** (xem Phụ lục G).
- **FR-041**: Dữ liệu nhật ký không được làm thay đổi số liệu tín hiệu V3 (kế thừa FR-007 spec 010).

### Kế hoạch job (Phụ lục D — tóm tắt ở đây)

- **FR-050**: Lịch chạy V3 (giờ VN, T2–T6): **08:30** trước phiên; **mỗi 1 phút 09:00–14:45** trong phiên; **~14:25** cho tín hiệu ATC/ATC hết hạn; **16:00** sau phiên (đóng ngày, đo kết quả, chuẩn bị tin mua mở cửa).

### Key Entities

- **Tín hiệu V3 (`TinHieuV3`)**: một lần một kịch bản báo trên một mã tại một phiên — mã, loại kịch bản (KB1/KB2/KB4/N1/N3), ngày tín hiệu, phiên mua dự kiến (mở cửa phiên sau / ATC phiên tín hiệu), hạng và giá trị thước xếp hạng, snapshot chỉ báo, trạng thái (đã báo / bị cắt vì danh mục / đã vào lệnh / huỷ).
- **Vị thế V3 (`ViTheV3`)**: lệnh đang giữ hoặc đã đóng — mã, loại kịch bản, giá vào, khối lượng, giá trị lệnh, phí, ngày vào, ngày tín hiệu gốc, điều kiện thoát đang áp (MAn hay ATO), giá thoát/ lý do thoát (chạm MAn, hết hạn ATC, ATO theo lịch), kết quả (Thắng/Thua/Hoà vốn, lãi %).
- **Trạng thái danh mục (`DanhMucV3`)**: tiền mặt, giá trị danh mục theo giá tham chiếu hiện hành, số vị thế đang giữ — dùng cho FR-020.
- **Chỉ báo thị trường ngày (`ChiSoNgayV3`)**: `MKT`, MA20/MA50 của chỉ số, `BREADTH50`, `FLOOR20`, `LEFT`, `TDM`, đếm mã `UNI` — tính một lần/ngày, mọi kịch bản dùng chung.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% tín hiệu V3 phát ra được đánh giá trên **giá khớp trong phiên**; không còn đường nào bán theo "giá đóng cửa rồi phiên sau bán" (kiểm bằng test + đọc code).
- **SC-002**: Mỗi luật trong Phụ lục A có ít nhất một test xUnit chứng minh (điều kiện báo, xếp hạng, luật bán realtime kể cả điều kiện ≥ giá vào × 1,004, T+2.5, sàn trắng, ưu tiên + tỉ trọng ×1,5, bẫy nến giữ chỗ) — tất cả pass.
- **SC-003**: `dotnet build backend/StockRadar.slnx` 0 lỗi; `dotnet test` bằng/sau khi gỡ V2 đều pass (mốc 26/10/2026: 392/392), cộng thêm toàn bộ test V3 mới; `flutter analyze` và `npm run build` sạch lỗi.
- **SC-004**: Grep sau gỡ (FR-031) = 0 kết quả ngoài `Migrations/`.
- **SC-005**: Chạy API local + gọi job V3 thủ công trên DB local liệt kê được tín hiệu; số tín hiệu ít hơn production được ghi rõ khi báo (DB local 526 mã).
- **SC-006**: Đối chiếu kỳ vọng ⇔ thực tế theo từng kịch bản (bảng Phụ lục A/G): số tín hiệu mỗi ngày ≤ 5 mỗi kịch bản, tỉ lệ xuất hiện từng kịch bản trong 4 năm backtest khớp bậc độ lớn với research 03 (KB4 256 lệnh, KB2 139, N3 142, KB1 114, N1 97 trên ~1.000 phiên).
- **SC-008** *(bổ sung v3)*: Có test xUnit cho FR-015a/FR-016a: biên `BREADTH50` 0,149 → hoảng loạn sâu, 0,15 → không; ngưỡng bán = giá vào × 1,15 và không bị nhịp 16:00 ghi đè; giữ tối đa 40; N3 không bao giờ bật cờ; lệnh thường vẫn ra đúng ngưỡng MA như cũ. Tất cả test V3 cũ vẫn pass.
- **SC-007**: Sau 1 tháng chạy thật, màn Hiệu quả V3 có đủ số lệnh đã đóng để tính tỉ lệ thắng từng kịch bản (không yêu cầu giá trị thắng bằng backtest — backtest chưa được kiểm chứng live).

## Assumptions

- Backtest của AI phiên trước (research 03) **chưa từng chạy thật**; số +352% là kỳ vọng, không phải cam kết. App phải hiển thị số liệu thật, không trích số backtest vào UI như kết quả hiện tại.
- Chỉ có nến ngày + giá khớp realtime (bảng giá KBS), không có nến giờ ổn định trước 28/04/2026 → không kiểm được khớp lệnh intra-bar chính xác; luật bán dùng so sánh giá khớp với ngưỡng, khớp một lần mỗi phút.
- Chưa tính trượt giá (backtest khớp đúng giá mở cửa/đóng cửa/mức chạm); phí khứ hồi 0,4% ghi trong cấu hình.
- V1 (`DailyAnalysisRunner`, Home Top 5, Job 1/Job 2) và `docs/features/v2-*` ngoài phần bị gỡ không đổi.
- DB production đang có lịch sử V2 trong bảng `KetQuaKichBan`; migration xoá bảng này **không áp dụng** trước khi owner duyệt (stop point).
- `appsettings.Production.json` trên server không sửa trong việc này; section config mới phải có giá trị mặc định an toàn trong `appsettings.json`.
- Web/mobile chỉ làm phần V3 cần (màn Hiệu quả + khối tín hiệu trên màn chi tiết mã); không redesign.
- **Lệnh bán chỉ giải phóng chỗ danh mục/tiền mặt sau khi khớp** (FR-019). Backtest P1++ giải phóng **ngay trong ngày** ra lệnh → số backtest +352% là cận trên; kết quả chạy thật có thể **thấp hơn** do tiền/chỗ bị chiếm thêm tới khi bán khớp.
- *(bổ sung v3)* Luật FR-015a/FR-016a dựa trên ~6 đợt hoảng loạn độc lập (10/2023, 04/2024, 08/2024, 04/2025, 03/2026, 07/2026) — ít mẫu; tốt lên ở cả trong mẫu (+60% → +69%) và ngoài mẫu (+182% → +191%) và ở tham số lân cận (giữ 30/60, chốt +10%), nhưng chốt +20% lại kém hơn. Với KB4/N1, cờ dùng độ rộng lúc ~14:25 chứ không phải lúc đóng cửa như backtest → ngày có độ rộng sát 0,15 có thể xếp khác backtest. Lệnh giữ tới 40 phiên chiếm chỗ lâu hơn → trong đợt hoảng loạn có thể bỏ lỡ thêm tín hiệu (đã tính trong backtest danh mục).
- N3 bán bằng **lệnh ATO** phiên `TDM`=2 tháng sau → app **bắn thêm tin nhắc ATO lúc 08:30 sáng đúng phiên `TDM`=2** cho mọi vị thế N3 đang giữ (ngoài dòng "giữ tối đa 12 phiên"), để owner không lỡ nhịp ATO.

---

## Phụ lục A — Luật 5 kịch bản V3 (chuẩn, sao chép từ research 03)

| Mã | Điều kiện báo | Xếp hạng (top 5/ngày) | Mua | Bán (realtime) | Giữ tối đa |
|---|---|---|---|---|---|
| **KB1 Hoảng loạn** | (`BREADTH50` < 0,20 **hoặc** chỉ số dưới MA50 hơn 7%) **và** `TV20` > 30 | `RS126` **thấp nhất** | Mở cửa phiên sau | giá khớp ≥ max(chạm MA10, giá vào × 1,004) | 15 phiên |
| **KB2 Chỉ số thủng MA50** | chỉ số dưới MA50 hơn 5% **và** `TV20` > 100 | `RSI14` **thấp nhất** | Mở cửa phiên sau | như KB1 | 15 phiên |
| **KB4 Ngành bán quá mức** | `SECR20` ≤ −10% **và** `RET20` < `SECR20` **và** `TV20` > 20 **và** `BREADTH50` < 0,35 | `RET20` **thấp nhất** | ATC phiên tín hiệu | giá khớp ≥ max(chạm MA10, giá vào × 1,004) | 10 phiên |
| **N1 Giải chấp cạn kiệt** | `FLOOR20` ≥ 1 **và** hôm nay không sàn **và** `SECR20` ≤ −12% **và** `TV20` > 10 **và** `LEFT` ≤ 6 | `SECR20` **thấp nhất** | ATC phiên tín hiệu | giá khớp ≥ max(chạm MA20, giá vào × 1,004) | 15 phiên |
| **N3 Đón dòng tiền đầu tháng** | `LEFT` = 5 **và** `TV20` > 30, **bỏ nếu** chỉ số cách MA50 trong ±1,5% | `RS63` **cao nhất** | ATC phiên tín hiệu | lệnh ATO phiên `TDM` = 2 của tháng sau | 12 phiên |

**Luật chung**: hết "giữ tối đa" → bán ATC; T+2.5 (sớm nhất bán buổi chiều phiên T+2, T = phiên mua); sàn trắng bên mua không bán được; bỏ lệnh mua nếu giá mua rơi đúng giá trần; danh mục 5 vị thế, ưu tiên KB1 > KB2 > KB4 > N1 > N3, không trùng mã; mỗi lệnh = giá trị danh mục / 5, riêng KB1/KB2/N1 × 1,5, không vượt tiền mặt.

**Ngoại lệ hoảng loạn sâu** *(bổ sung v3, FR-015a/FR-016a, research PH-21)*: KB1/KB2/KB4/N1 có `BREADTH50` phiên tín hiệu < 0,15 → bán realtime khi giá khớp ≥ giá vào × 1,15, giữ tối đa 40 phiên, hết hạn bán ATC. Điều kiện báo, xếp hạng, nhịp mua, tỉ trọng **không đổi**. Backtest lệnh loại này: thắng 81% → 86%, lãi TB +4,2% → +9,8%, giữ TB 6,5 → 25 phiên.

Ghi chú tương đương: "chạm MA10" = TB 9 giá đóng cửa trước (research 03 ghi "TB 9 đóng cửa trước"); "chạm MA20" = TB 19 giá đóng cửa trước — nhất quán với định nghĩa FR-001.

Kết quả backtest tham chiếu (research 03, từng kịch bản trước bộ lọc danh mục):

| Mã | Lệnh | Thắng trong/ngoài mẫu | Lãi TB | Lỗ TB | Tổng lãi/tổng lỗ | Tệ nhất | Giữ TB |
|---|---|---|---|---|---|---|---|
| KB1 | 114 | 75,7% / 81,8% | +2,59% | −8,64% | 4,80 | −13,1% | 5,6 |
| KB2 | 139 | 72,6% / 85,5% | +2,84% | −7,73% | 5,65 | −18,8% | 6,3 |
| KB4 | 256 | 67,6% / 71,1% | +1,55% | −9,76% | 2,16 | −31,2% | 6,0 |
| N1 | 97 | 78,0% / 84,2% | +3,52% | −7,71% | 5,43 | −11,2% | 9,5 |
| N3 | 142 | 57,0% / 69,0% | +3,21% | −5,51% | 2,51 | −21,0% | 9,0 |

## Phụ lục B — Danh sách gỡ V2 (đã grep lại trên HEAD 86a54da; phải grep lại trước khi xoá)

**Domain**
- `StockRadar.Domain/Entities/KetQuaKichBanEntity.cs`
- `StockRadar.Domain/Enums/LoaiKichBan.cs`, `Enums/TrangThaiKichBan.cs`
- `StockRadar.Domain/Services/ChamKetQuaChuKy.cs`, `Services/KetQuaKichBan.cs` (+ test tương ứng)

**Application**
- `Services/KichBanNoHuongLen.cs`, `KichBanHoiHoTro.cs`, `KichBanQuetThanhKhoan.cs`, `KichBanKietSuc.cs`, `KichBanGayNen.cs`
- `Services/MayNhanKichBanService.cs`, `XepHangCoHoiService.cs`, `SoTuyenService.cs`
- `Abstractions/`: `IKichBanDanhGia.cs`, `IMayNhanKichBan.cs`, `IXepHangCoHoi.cs`, `ISoTuyenService.cs`, `IPha1TruocPhienService.cs`, `IPha2TrongPhienService.cs`, `IPha3DoLuongService.cs`, `IHieuQuaKichBanService.cs`, `INguonDuLieuXepHang.cs` (riêng `INhatKyTinHieuRepository` **không xoá** — giữ và trỏ sang V3 theo FR-040)
- `Options/KichBanOptions.cs`, `Options/Pha2Options.cs`, `Options/SoTuyenOptions.cs`
- `DTOs/KichBanDtos.cs`, `DTOs/HieuQuaDtos.cs`, `DTOs/ChiTietKichBanStockDto.cs`

**Infrastructure**
- `MarketData/Pha1TruocPhienRunner.cs`, `Pha2TrongPhienRunner.cs`, `Pha3DoLuongRunner.cs`, **`Pha2TrailingStopRuntime.cs`** (file V2, nằm ngoài danh sách grep gốc — đã xác nhận bằng grep HEAD)
- `MarketData/ChuKyMua.cs`, `HieuQuaKichBanService.cs`, `NguonDuLieuXepHang.cs`
- `Notifications/V2TelegramFormatter.cs`
- `Scheduling/Jobs/Pha1TruocPhienJob.cs`, `Pha2TrongPhienJob.cs`, `Pha3DoLuongJob.cs`
- `Persistence/ApplicationDbContext.cs` (DbSet `KetQuaKichBan` dòng ~46 + cấu hình dòng ~525), `Persistence/Repositories/EfNhatKyTinHieuRepository.cs` (giữ file, **trỏ truy vấn sang `TinHieuV3`** theo FR-040 — không còn đọc `KetQuaKichBan`)
- Mục `Pha1TruocPhien`/`Pha2TrongPhien`/`Pha3DoLuong` trong `Scheduling/QuartzJobIds.cs` và `ConfigurePha1/2/3*` trong `Scheduling/QuartzSchedulingExtensions.cs` (dòng 40–42, 219–260)
- Hai file `DependencyInjection.cs` (Application dòng 35–38, 80–93; Infrastructure dòng 135–152)

**Api**
- `Controllers/KichBanController.cs`, `Controllers/HieuQuaController.cs`
- `Controllers/StocksController.cs`: action `GetKichBan` (`GET /stocks/{symbol}/kich-ban`, dòng ~54–62)
- `Controllers/MarketJobsController.cs`: tham số `IPha1/2/3…` (dòng 24–26) + 3 action `pha1-truoc-phien` / `pha2-trong-phien` / `pha3-do-luong` (dòng ~196–240)
- `appsettings.json`: section `SoTuyen` (dòng 23), `KichBan` (28), `XepHang` (55), `Pha2` (64) kể cả `KichBanDungLoDuoi` (76)

**Tests**
- `StockRadar.Tests/KichBan/` — 12 file: `HieuQuaServiceTests`, `KichBanGayNenTests`, `KichBanHoiHoTroTests`, `KichBanKietSucTests`, `KichBanNoHuongLenTests`, `KichBanQuetThanhKhoanTests`, `Pha1TruocPhienTests`, `Pha2TrongPhienTests`, `Pha3DoLuongTests`, `V2TelegramFormatterHtmlTests`, `V2TelegramFormatterTests`, `XepHangCoHoiTests`
- `StockRadar.Tests/NhatKy/NhatKyTinHieuTests.cs` — chỉnh seed/assert từ V2 (`SeedV2`, `PickerV2_*`, `KetQuaKichBan`) sang V3 `TinHieuV3` theo FR-040

**Mobile**
- `screens/hieu_qua_screen.dart`, `widgets/scenario_widgets.dart`
- `screens/nhat_ky_tin_hieu_picker.dart` (`_nguon = 'v2'` → `'v3'`), `screens/nhat_ky_lenh_screen.dart` (bỏ nhánh `loaiKichBan == 'KietSuc'/'GayNen'`, đổi nhãn "chọn từ V2" → V3) — theo FR-040
- Phần V2 trong `screens/stock_detail_screen.dart` (`_loadKichBan`, `ScenarioStatusBadges`, `TradePlanCard`, `ScenarioEvidenceCard`, `ScenarioEmptyState`), `services/api_client.dart`, `models/models.dart`, `app_router.dart` (route Hiệu quả)

**Web**
- `pages/HieuQuaPage.tsx`
- Phần V2 trong `pages/StockDetailPage.tsx` (`getKichBanTheoSymbol`, `BangChungKichBan`, `ChiTietKichBan`, `KichBanTheoSymbol`), `lib/api.ts` (`getKichBanTheoSymbol`, `getHieuQuaTomTat`, `getHieuQuaLichSu`), `types/index.ts`, `App.tsx` (route `/hieu-qua`)

**Docs**
- `docs/features/v2-scenario-engine/`, `docs/features/v2-sell-fix/`, `docs/features/v2-sell-plan-tracking/`, `docs/features/v2-sell-trailing-stop/` (và mọi mục `v2-*` khác còn mô tả luồng đang bị gỡ)
- `CLAUDE.md`: mục Pipeline + bảng "Luật sản phẩm" → viết lại cho V3
- `docs/domain/pipeline-jobs.md`: mục "Luồng V2 Scenario Engine — sự thật chuẩn duy nhất" → thay bằng canon V3

**Không gỡ (ngoài phạm vi):** V1 (`DailyAnalysisRunner`, `BuyDecisionEngine`, Home Top, Job 1/Job 2, `OpportunityIntradayMonitor*`), `docs/features/*` không thuộc V2, `specs/003-*`, file chưa theo dõi của owner.

## Phụ lục C — Cách tính giá trị danh mục và tiền mặt (chuẩn hoá cho FR-020)

Đơn vị: nghìn đồng (giá) × khối lượng (cổ phiếu) = giá trị nghìn đồng; UI hiển thị tỷ đồng.

```
Giá trị vị thế i   = khối lượng_i × GiáThamChiếu hiện hành(position_i)
Giá trị danh mục V = TiềnMặt + Σ giá trị vị thế đang giữ
TiềnMặt            = V0 − Σ giá trị lệnh đã khớp (gồm phí mua) + Σ (giá trị bán thu được − phí bán − thuế bán)
Giá trị lệnh mong muốn = (V / 5) × HệSốKịch bản      (KB1/KB2/N1 = 1,5; KB4/N3 = 1,0)
Giá trị lệnh thực = min(Giá trị lệnh mong muốn, TiềnMặt còn lại)
Khối lượng = floor(Giá trị lệnh thực / giá khớp theo lô quy định của sàn: HOSE 100, HNX/UPCoM 100 hoặc theo cấu hình hiện có)
```

`GiáThamChiếu hiện hành`: lúc 08:30/16:00 = giá đóng cửa phiên gần nhất; trong phiên = giá khớp mới nhất lấy được. Khi không lấy được giá trong phiên → dùng giá đóng cửa gần nhất và **không** mở/bán lệnh mới cho mã đó trong tick đó (fail-open).

Phí: 0,4% khứ hồi, trừ một lần khi bán (khớp backtest). Tiền nhàn rỗi: 0% (FR-021). Vốn ban đầu `V0`: tham số cấu hình/backtest, chỉ dùng để hiển thị tỉ lệ % lãi; tín hiệu và tỉ trọng chỉ phụ thuộc **V**, không phụ thuộc `V0`.

## Phụ lục D — Lịch job V3 (Quartz, giờ VN, T2–T6)

| Job id | Thời điểm | Việc |
|---|---|---|
| `v3-truoc-phien` | **08:30** cron `0 30 8 ? * MON-FRI` | Tính chỉ báo chung + `UNI` (loại nến giữ chỗ hôm nay); chấm điều kiện báo KB1/KB2 trên **giá đóng cửa phiên hôm qua (T)** → bắn tin MUA để mua **ATO/mở cửa ngay phiên T+1 này**; **nếu hôm nay là phiên `TDM`=2 của tháng, bắn tin ATO nhắc bán N3**; cập nhật ngưỡng thoát MA10/MA20 cho vị thế đang giữ; ghi `ChiSoNgayV3` |
| `v3-trong-phien` | **mỗi 1 phút, 09:00–14:45** cron `0 0/1 9-14 ? * MON-FRI` (job tự skip ngoài khoảng, như V2 từng làm) | Lấy giá khớp realtime; (a) khớp lệnh mua mở cửa KB1/KB2 với giá mở cửa + bỏ nếu giá trần; (b) đánh giá luật bán realtime cho mọi vị thế (kèm T+2.5, sàn trắng); (c) bắn tin MUA/BÁN ngay khi chạm |
| `v3-atc` | **~14:25** cron `0 25 14 ? * MON-FRI` | Dùng giá khớp hiện tại thay giá đóng cửa: chấm KB4/N1/N3 → top 5, áp ràng buộc danh mục → bắn tin mua ATC; xử lý vị thế hết hạn giữ → tin bán ATC |
| `v3-sau-phien` | **16:00** cron `0 0 16 ? * MON-FRI` | Đóng ngày: cập nhật vị thế (MAE/MFE, số phiên giữ), đo kết quả lệnh đã đóng (Thắng/Thua/Hoà vốn theo ngưỡng 0,05%), ghi lịch sử, cập nhật `TDM`/`LEFT` tháng sau; chuẩn bị ngữ cảnh để 08:30 hôm sau bắn tin mua KB1/KB2 và tin ATO N3 |

Job thủ công để test: `POST /api/v1/v3/jobs/truoc-phien|trong-phien|atc|sau-phien` (bảo vệ như các job hiện có trong `MarketJobsController`).

## Phụ lục E — Mẫu tin Telegram V3

**Tin MUA** (KB1/KB2 — mua ATO/mở cửa phiên T+1, bắn lúc 08:30 sáng T+1):

```
🟢 <b>KB1 · HPG — Hoảng loạn</b>
Mua <b>ATO mở cửa phiên hôm nay (T+1)</b> — tín hiệu từ đóng cửa phiên qua
Giá tín hiệu: <code>27,3</code>  ·  TV20: <code>38 tỷ</code>
RS126: <code>4%</code> (thấp nhất top 5)
Độ rộng: <code>BREADTH50 0,16</code> · Chỉ số dưới MA50 <code>−8,1%</code>
Quy mô: <code>30%</code> DANH MỤC (300 triệu — V/5 × 1,5)
Bán realtime: giá khớp ≥ max(chạm MA10, giá vào +0,4%) · Giữ tối đa 15 phiên
⚠️Backtest P1++ chưa kiểm chứng live
```

**Tin MUA** (KB4/N1/N3 — ATC phiên tín hiệu, bắn ~14:25): thay dòng "Mua" bằng `Mua <b>ATC cuối phiên nay</b>` và thêm `Giá hiện tại: <code>...</code> (giá đóng cửa chưa chốt)`. Với N3, dòng bán: `Bán ATO phiên TDM=2 tháng sau (02/12) · Giữ tối đa 12 phiên`.

**Tin nhắc ATO bán N3** (bắn 08:30 sáng phiên `TDM`=2 của tháng sau, cho mọi vị thế N3 đang giữ):

```
🔴 <b>ATO · HPG — N3</b>
Bán ATO mở cửa <b>ngay phiên hôm nay</b> (đón dòng tiền đầu tháng, đã hết chu kỳ)
Giá vào: <code>27,3</code>  ·  Phiên giữ: 12/12
```

**Tin BÁN** (realtime, trong phiên):

```
🔴 <b>BÁN · HPG — KB1</b>
Giá khớp: <code>28,6</code>  ·  Giá vào: <code>27,3</code>  ·  +4,8%
Lý do: chạm MA10 (ngưỡng 28,55) · đã T+2.5
Phiên giữ: 6/15
```

**Lệnh hoảng loạn sâu** *(bổ sung v3)*: tin MUA thay dòng "Bán realtime…" bằng `⚡ Hoảng loạn sâu (BREADTH50 <code>0,12</code> &lt; 0,15) · Bán realtime: giá khớp ≥ giá vào +15% · Giữ tối đa 40 phiên`. Tin BÁN dòng "Lý do" = `chốt +15% hoảng loạn sâu (ngưỡng 31,40) · đã T+2.5`; phiên giữ hiện `x/40`. Với KB1/KB2 (chờ mua 08:30) giá vào chưa có → dòng bán ghi "giá vào +15%" như trên, không in con số.

**Tin bán hết hạn / ATO**: cùng khối, dòng "Lý do" = `Hết hạn giữ 15 phiên → bán ATC phiên nay` hoặc `Lệnh ATO phiên TDM=2 tháng sau`.

Quy ước: nhãn tiếng Việt có dấu, giữ thuật ngữ trading quen thuộc (MA10, RS126, ATC, ATO, T+2.5); đơn vị giá nghìn đồng hiển thị 2 chữ số; không in bí mật/token.

## Phụ lục F — Quy ước đặt tên code V3 (né khoá grep FR-031)

| Khái niệm | Tên code | Ghi chú |
|---|---|---|
| Enum kịch bản | `LoaiKichBanV3` = `KB1, KB2, KB4, N1, N3` | Không dùng `LoaiKichBan` |
| Trạng thái vị thế | `TrangThaiViTheV3` = `DanhDau, ChoMua, DangGiu, DaThoat, BiHuy` | Không dùng `TrangThaiKichBan` |
| Entity | `TinHieuV3Entity`, `ViTheV3Entity`, `ChiSoNgayV3Entity` | Bảng `TinHieuV3`, `ViTheV3`, `ChiSoNgayV3` |
| Chỉ báo chung | `V3ChiSoToanThiTruong` (Domain service), `V3UniverseEntry` | Không dùng `SoTuyen*` |
| Chấm kịch bản | `V3KichBanEvaluator` + `V3KichBanEvaluatorBase`, 5 lớp `V3Kb1Evaluator` … `V3N3Evaluator` | Không dùng `KichBan*` |
| Xếp hạng + danh mục | `V3DanhMucXepHang` (Domain) | Không dùng `XepHangCoHoi*` |
| Luật bán realtime | `V3LuatBanThucTe` | |
| Runners / jobs | `V3TruocPhienRunner`, `V3TrongPhienRunner`, `V3AtcRunner`, `V3SauPhienRunner` + `V3*Job` | Không dùng `Pha1/2/3*` |
| Telegram | `V3TelegramFormatter` | |
| Hiệu quả | `V3HieuQuaService`, `V3HieuQuaDtos` | Route `/api/v1/v3/hieu-qua/*` |
| API | `V3KichBanController` (`/api/v1/v3/tin-hieu`, `/api/v1/v3/vi-the`, `/api/v1/v3/chi-so`), `GET /stocks/{symbol}/v3` | Thay `kich-ban` / `hieu-qua` cũ |

## Phụ lục G — Phương án DB

- **Bảng mới** cho V3: `TinHieuV3` (tín hiệu + snapshot chỉ báo, JSON), `ViTheV3` (vị thế, giá vào/thoát, kết quả), `ChiSoNgayV3` (chỉ báo thị trường theo ngày — tránh tính lại mỗi phút).
- **Migration xoá** `KetQuaKichBan` (toàn bộ lịch sử V2). Production **đang có** lịch sử V2 (11 lệnh đã đóng 30/09–22/10/2026) → chỉ áp migration sau khi owner duyệt; nếu owner muốn giữ dữ liệu lịch sử thì export DB trước khi xoá (có sẵn `scripts/export-db.ps1`).
- **Bẫy bắt buộc**: `dotnet ef migrations add` trong repo này sinh `Up()` sai (dựng lại toàn bộ ~35 bảng thay vì diff). Thủ tục: chạy `migrations add` → **mở file đọc lại** → viết tay `Up()`/`Down()` chỉ gồm thao tác tăng dần thật sự → giữ nguyên snapshot và `.Designer.cs` do tool sinh. Mẫu đúng: `20260811013021_AddRealizedPnl.cs`.
- Nhật ký (FR-040, owner chốt phương án (a)): giữ nguyên cấu trúc bảng `NhatKyLenh` và `NhatKyTinHieuBoQua`; `EfNhatKyTinHieuRepository` và endpoint **đổi hẳn thành `GET /nhat-ky/tin-hieu/v3`** (KHÔNG giữ `tin-hieu/v2` trỏ V3), đọc từ `TinHieuV3` (không còn đọc `KetQuaKichBan`). Mobile `nhat_ky_tin_hieu_picker.dart` (`_nguon = 'v2'` → `'v3'`) và web cũng gọi `/tin-hieu/v3`. Các dòng nhật ký cũ đang gắn tín hiệu V2 (`NguonTinHieu = "V2"`, `MaTinHieu` trỏ `KetQuaKichBan.Id`): **hướng xử lý (xóa / vô hiệu / để lại ảnh chụp giá cũ) được chốt cùng lúc với migration** và **DỪNG chờ owner duyệt** — vì production đang có lịch sử V2.

## Phụ lục H — Phương án màn "Hiệu quả" và nhật ký

**Màn Hiệu quả (web `pages/…`, mobile `screens/…`) — làm lại trên nền dữ liệu V3, không giữ layout V2:**

- Khối 1 — tóm tắt theo kịch bản: 5 dòng KB1/KB2/KB4/N1/N3, mỗi dòng: số lệnh đã đóng, tỉ lệ thắng, lãi TB, lỗ TB, tổng lãi/tổng lỗ, lệnh tệ nhất. Định nghĩa thắng: lãi ròng > 0,05%; từ −0,05% đến +0,05% là **hoà vốn** (không tính thắng) — khớp research.
- Khối 2 — đường giá trị danh mục: `V` theo ngày đóng cửa, sụt giảm tối đa, so với chỉ số bình quân đều `MKT` cùng kỳ (không so VNINDEX — DB không đủ dữ liệu trước 25/06/2026).
- Khối 3 — danh sách lệnh: mã, kịch bản, ngày vào, giá vào, ngày thoát, giá thoát, lý do thoát, lãi %, trạng thái đang giữ / đã đóng.
- API: `GET /api/v1/v3/hieu-qua/tom-tat?from&to`, `GET /api/v1/v3/hieu-qua/lich-su?page&size`, `GET /api/v1/v3/vi-the?status`.

**Màn chi tiết mã**: khối tín hiệu V3 thay khối kịch bản V2 — hiện kịch bản, ngày tín hiệu, snapshot chỉ báo quyết định, kế hoạch mua/bán theo Phụ lục A. Không còn SL/TP1/TP2 (V2).

**Nhật ký (spec 010)**: phần lệnh tự nhập giữ nguyên 100%. Phần gắn tín hiệu theo FR-040(a): picker hiển thị tín hiệu V3 đã kích hoạt với giá chụp theo chiều lệnh (mua → giá vào; bán → giá thoát), bỏ các nhánh `KietSuc`/`GayNen` của V2. Dữ liệu gắn-tín hiệu-V2 cũ xử lý cùng migration (Phụ lục G, DỪNG chờ duyệt).

## Phụ lục I — Invariants (bất biến V3)

- **V3-B1**: Luật bán luôn đánh giá trên giá khớp trong phiên; không có đường thoát nào chờ giá đóng cửa (trừ lệnh ATO/ATC theo thời điểm đã định).
- **V3-B2**: Không có composite score. Mỗi kịch bản là tập điều kiệnboolean + một thước xếp hạng duy nhất. Không cộng trung bình nhiều chỉ báo thành một điểm.
- **V3-B3**: Fail-open — thiếu dữ liệu/thiếu giá realtime → bỏ qua tick, không bắn tin sai, không crash.
- **V3-B4**: Mỗi lệnh chỉ thuộc đúng một kịch bản (kịch bản thắng vòng loại ưu tiên), không có "nhiều kịch bản đồng thời" như V2.
- **V3-B5**: Không dùng `IndicatorMath` hiện có cho `RSI14` Wilder / MA của chỉ số; hàm mới đặt trong `V3ChiSoToanThiTruong`, có test riêng.
- **V3-B6**: Dữ liệu test phải gắn nhãn rõ là giả (bịa) và dọn sạch sau khi kiểm; không ghi DB production.
