# Feature Specification: Gỡ phần bắn tin mua/bán + theo dõi vị thế của V1 (VIP)

**Feature Branch**: `011-remove-v1-vip-alerting`

**Created**: 2026-10-22

**Status**: Draft

**Input**: User description: "Giữ V1 phần lọc (universe Job 1, đồng bộ giá/nến, phân tích hằng ngày gate→xếp hạng→Top 5 cho màn Home 'Top cơ hội', và V2 Pha 1 cũng dùng dữ liệu này). Bỏ V1 phần bắn tin mua/bán + theo dõi vị thế: tin VIP Telegram (TopOpportunityVipAlertPublisher), Master Alert/vị thế VIP (MasterAlertPositions, PositionSellLegs, VipAlertFires), luật bán VIP V1, AI chấm tin VIP (AnthropicVipLlmJudge), 'Lợi nhuận thực' V1 nếu chỉ phục vụ VIP, quét bảng giá trong phiên chỉ phục vụ VIP. V2 (Pha 1/2/3, spec 009) đã thay phần này. Xoá tính năng là xoá sạch, không giữ shim/enum 'deprecated' nếu không có lý do bằng dữ liệu thật."

## Bối cảnh & Ý định chủ sản phẩm (ĐÃ CHỐT 2026-10-22)

Hai hệ thống chạy song song từ spec 009:

- **V1** làm hai việc khác nhau: (a) **lọc & xếp hạng** — universe (Job 1), đồng bộ giá/nến, phân tích hằng ngày theo chuỗi cổng → xếp hạng → Top 5, phục vụ màn Home "Top cơ hội"; và (b) **bắn tin mua/bán + theo dõi vị thế** — gửi cảnh báo VIP qua Telegram, mở/đóng/sửa vị thế VIP, luật bán VIP, AI chấm chất lượng tin, đo "lợi nhuận thực" của tin VIP, quét bảng giá trong phiên để ra tin.
- **V2** (Pha 1 sơ tuyển trước phiên, Pha 2 kiểm trigger trong phiên mỗi phút, Pha 3 đo lường cuối ngày → `KetQuaKichBan`) đã **thay thế trọn phần (b)** của V1: bắn tin mua/bán và theo dõi kết quả vị thế.

**Ý định đã chốt**: **GIỮ** phần (a) của V1. **BỎ sạch** phần (b) của V1 — không để lại shim/adapter/enum "deprecated" nếu chưa có lý do bằng dữ liệu thật. Khi một tính năng bị bỏ, bỏ hết đường đi của nó (job, bảng, API, màn, cấu hình, test).

** Ranh giới quan trọng**: "quét bảng giá trong phiên" của V1 nằm trong cùng một vòng chạy vừa sinh **dữ liệu dùng chung cho app** (phát hiện lô lớn, nhãn VSA, dòng tiền, bảng giá realtime đẩy qua SignalR) vừa sinh **tin VIP**. Chỉ phần phục vụ tin VIP bị bỏ; phần lô lớn/VSA/dòng tiền/realtime mà app và V2 vẫn ăn phải **GIỮ**. Chi tiết ở Phụ lục A.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Home "Top cơ hội" và dữ liệu thời gian thực không đổi sau khi gỡ VIP (Priority: P1)

Nhà đầu tư mở app (web/mobile) vẫn thấy: bảng Top cơ hội hằng ngày như cũ, bảng giá/nến cập nhật như cũ, và dòng sự kiện lô lớn / VSA / dòng tiền trong phiên vẫn chảy thời gian thực. Việc gỡ phần bắn tin V1 **không** được làm suy chuyển bất kỳ bề mặt nào ở trên.

**Why this priority**: Đây là điều kiện không được phá (regression guard). Phần lọc V1 và luồng dữ liệu thời gian thực là thứ còn sống và phục vụ cả Home lẫn V2 Pha 1; lỡ tay gỡ kèm là mất dữ liệu người dùng đang dùng hằng ngày.

**Independent Test**: Trước và sau khi gỡ VIP, chụp lại cùng một phiên trên môi trường dev: danh sách Top cơ hội, bộ đếm sự kiện lô lớn trong phiên, và trạng thái kết nối thời gian thực. Ba chỉ số phải khớp nhau (không giảm do gỡ VIP).

**Acceptance Scenarios**:

1. **Given** một phiên có Top cơ hội và có lô lớn xuất hiện, **When** gỡ toàn bộ phần bắn tin V1, **Then** màn Home "Top cơ hội" vẫn liệt kê đúng các mã Top và màn chi tiết vẫn nhận bảng giá thời gian thực.
2. **Given** người dùng đang theo dõi bảng dòng tiền/lô lớn, **When** phần VIP bị bỏ, **Then** các sự kiện lô lớn/nhãn VSA/dòng tiền vẫn được phát và đẩy tới app như trước.
3. **Given** V2 Pha 1 chạy sơ tuyển trước phiên, **When** gỡ V1 phần bắn tin, **Then** dữ liệu đầu vào mà Pha 1 đọc (universe + phân tích ngày) vẫn nguyên.

---

### User Story 2 - V2 là đường duy nhất bắn tin mua/bán và theo dõi vị thế (Priority: P2)

Nhà đầu tư chỉ còn nhận tin mua/bán và xem theo dõi vị thế qua đường V2 (kịch bản). Không còn hai luồng tin song song (VIP V1 + V2) gây nhiễu hay trùng lặp. Cấu hình/job/AI chấm thuộc đường VIP V1 không còn chạy và không còn trong hệ thống.

**Why this priority**: Đây là giá trị chính của đợt gỡ — loại bỏ chồng chéo, giảm nhiễu cho người dùng và giảm bề mặt vận hành (job trong phiên, chi phí AI chấm tin).

**Independent Test**: Kích hoạt một chu kỳ trong phiên trên dev: xác nhận chỉ có tin/kết quả từ V2, không còn tin VIP V1 nào được tạo; xác nhận không còn tiến trình nào ghi vào các bảng vị thế/kịch-bản-bắn-tin của V1.

**Acceptance Scenarios**:

1. **Given** một mã chạm điều kiện mua, **When** trong phiên chạy, **Then** chỉ đường V2 sinh tin/kết quả; đường VIP V1 không sinh tin.
2. **Given** người dùng mở mục theo dõi kết quả, **When** xem bất kỳ mã nào, **Then** mọi số liệu đến từ V2 (kịch bản đã kích hoạt), không còn nguồn "vị thế VIP V1".
3. **Given** hệ thống sau khi gỡ, **When** điểm qua danh sách công việc định kỳ trong phiên, **Then** không còn công việc dedicated cho bắn tin/luyện mô hình/chấm AI của VIP V1.

---

### User Story 3 - Nhật ký giao dịch (spec 010) chỉ còn một nguồn tín hiệu là V2 (Priority: P3)

Trong màn thêm lệnh của nhật ký, người dùng chọn tín hiệu tham chiếu **chỉ từ nguồn V2 (kịch bản)**. Nhánh chọn "tín hiệu VIP V1" biến mất. Các lệnh nhật ký **đã lưu trước đây** có gắn tham chiếu VIP không bị mất giá đã chụp; chỉ cột "kết quả tín hiệu" của tham chiếu VIP cũ không còn nguồn để cập nhật (xử lý theo quyết định ở mục Câu hỏi).

**Why this priority**: Phụ thuộc dây chuyền — spec 010 đang làm dở và đang đọc đúng các bảng V1 sắp bỏ. Phải được xử lý có ý thức, không phải tác dụng phụ âm thầm.

**Independent Test**: Mở picker tín hiệu trong nhật ký: chỉ còn tab V2. Thêm một lệnh gắn tín hiệu V2 → lưu và hiển thị bình thường. Mở một lệnh cũ từng gắn VIP → giá đã chụp vẫn còn, không lỗi.

**Acceptance Scenarios**:

1. **Given** người dùng thêm lệnh mới, **When** mở picker tín hiệu, **Then** chỉ thấy danh sách kịch bản V2, không còn tab/danh sách VIP.
2. **Given** một lệnh đã lưu có tham chiếu VIP từ trước, **When** người dùng mở lại, **Then** giá đã chụp vẫn hiển thị, màn không lỗi.
3. **Given** đánh dấu "bỏ qua tín hiệu", **When** áp dụng cho tín hiệu V2, **Then** vẫn hoạt động; với tham chiếu VIP cũ thì từ chối một cách rõ ràng (không crash âm thầm).

---

### Edge Cases

- Tham chiếu VIP đã lưu trong nhật ký: giữ nguyên giá chụp hay xoá? dữ liệu cũ (bảng VIP, lịch sử tín hiệu): xoá bảng hay archive? → xem mục Câu hỏi (chưa chốt).
- Vòng quét trong phiên sau khi gỡ VIP vẫn phải chạy (vì sinh dữ liệu lô lớn/realtime dùng chung). Nếu gỡ cả vòng quét thì mất dữ liệu app → phải tách đúng phần VIP ra khỏi vòng quét, giữ phần còn lại.
- Mã từng vừa xuất hiện ở Top V1 vừa trong phạm vi V2: bảo đảm không sinh tin trùng sau khi gỡ VIP.
- Cấu hình còn sót (khoá Telegram VIP, khoá AI chấm VIP, tham số Master Alert nội phiên): phải được gỡ kèm để không còn cổng bật lại đường đã bỏ.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Hệ thống PHẢI ngừng hoàn toàn việc gửi tin mua/bán theo đường VIP V1 (không còn tạo, không còn lịch gửi, không còn ghi bản ghi "đã bắn tin").
- **FR-002**: Hệ thống PHẢI ngừng hoàn toàn việc mở/cập nhật/đóng "vị thế VIP V1" và các nhịp bán của nó; việc theo dõi vị thế chỉ còn qua đường V2 (kịch bản).
- **FR-003**: Hệ thống PHẢI gỡ bộ chấm điểm AI dành riêng cho tin VIP V1 và mọi đường gọi/job/cấu hình của nó.
- **FR-004**: Hệ thống PHẢI gỡ phần "lợi nhuận thực" mà **chỉ** phục vụ đo tin VIP V1, **nhưng phải giữ** công thức tính lãi/lỗ dùng chung cho nhật ký giao dịch (spec 010) — xem Phụ lục A, mục "không được xoá quá đà".
- **FR-005**: Hệ thống PHẢI giữ nguyên phần lọc & xếp hạng V1 phục vụ Home "Top cơ hội" (universe Job 1, đồng bộ giá/nến, cổng → xếp hạng → Top 5) và dữ liệu mà V2 Pha 1 đọc.
- **FR-006**: Hệ thống PHẢI giữ nguyên vòng quét trong phiên ở phần sinh dữ liệu dùng chung (phát hiện lô lớn, nhãn VSA, dòng tiền, đẩy bảng giá thời gian thực tới app), dù phần VIP trong cùng vòng đó bị gỡ.
- **FR-007**: Khi một bảng dữ liệu V1 (vị thế VIP / nhịp bán / lịch sử bắn tin VIP) bị gỡ, hệ thống PHẢI có quyết định rõ cho dữ liệu đã tích luỹ: hoặc lưu trữ để tra cứu, hoặc gỡ kèm bảng (không để "bảng mồ côi" vẫn được ghi nhưng không ai đọc). → **chưa chốt**, xem Câu hỏi Q1.
- **FR-008**: Màn app (web/mobile) từng hiển thị dữ liệu bắn tin/vị thế VIP PHẢI hoặc bị gỡ, hoặc chuyển sang nguồn V2 — không để màn trống/chết vẫn tồn tại trong điều hướng. → mức xử lý **chưa chốt**, xem Câu hỏi Q2.
- **FR-009**: Picker tín hiệu V1 VIP trong nhật ký (spec 010) PHẢI bị gỡ; nguồn tín hiệu tham chiếu chỉ còn V2. Ảnh hưởng dây chuyền được liệt kê ở Phụ lục A. → cách xử lý dữ liệu tham chiếu VIP cũ **chưa chốt**, xem Câu hỏi Q3.
- **FR-010**: Việc gỡ KHÔNG được để lại shim/adapter/enum đánh dấu "deprecated" trừ khi chủ sản phẩm đưa ra lý do bằng dữ liệu thật. Mặc định là xoá sạch đường đi của tính năng.
- **FR-011**: Sau khi gỡ, số lượng công việc định kỳ trong phiên, số bảng, số endpoint và số màn phải giảm tương ứng với phạm vi đã liệt kê (không còn mã chết dùng được từ UI).

### Key Entities *(những đối tượng dữ liệu thuộc phạm vi cân nhắc gỡ)*

- **Vị thế VIP V1**: bản ghi vị thế mở/đóng theo mã do đường bắn tin V1 tạo ra (bảng `MasterAlertPositions`). Thuộc phạm vi BỎ nếu không còn màn/dịch vụ sống nào đọc ngoài picker 010 (xem Phụ lục A).
- **Nhịp bán của vị thế VIP**: các lần bán một nửa/bán hết ghi lại (bảng `PositionSellLegs`). BỎ cùng vị thế VIP.
- **Lịch sử bắn tin VIP**: mỗi tin đã gửi kèm đo lường nội phiên (bảng `VipAlertFires`). BỎ; là dữ liệu cũ cần quyết định archive hay drop (Q1).
- **Công thức lãi/lỗ dùng chung (RealizedPnlMath / phí)**: KHÔNG phải thực thể VIP-exclusive — nhật ký 010 đang dùng; GIỮ.
- **Kết quả kịch bản V2 (`KetQuaKichBan`)**: nguồn thay thế, ở ngoài phạm vi gỡ, được picker/màn V2 dùng.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% bề mặt Home "Top cơ hội" và bảng giá/sự kiện thời gian thực cho ra kết quả giống hệt trước khi gỡ (so sánh cùng phiên trên dev: 0 mã Top mất, 0 sự kiện lô lớn mất).
- **SC-002**: Sau gỡ, số tin mua/bán gửi đi trong một phiên đến từ đúng MỘT đường (V2); số tin từ đường VIP V1 = 0.
- **SC-003**: Không còn tiến trình định kỳ dedicated cho VIP V1 chạy trong ngày (đo bằng danh sách job còn lại giảm đúng các job đã liệt kê).
- **SC-004**: Picker tín hiệu trong nhật ký chỉ còn một nguồn (V2); 100% ca thêm lệnh gắn tín hiệu V2 thành công; không có lỗi âm thầm khi mở lại lệnh VIP cũ.
- **SC-005**: Không còn endpoint/màn/cấu hình "sống" nào truy xuất dữ liệu bắn tin V1 (kiểm bằng: mọi bề mặt từng đọc các bảng VIP hoặc đã gỡ hoặc đã đổi sang V2; không còn đường chết nhưng vẫn hiển thị trong điều hướng).
- **SC-006**: Toàn bộ bộ test tự động xanh sau khi gỡ; số lượng test giảm đúng các ca dedicated cho VIP V1, không giảm test của phần dùng chung.

## Assumptions

- V2 (spec 009) đã vận động ổn định và phủ đủ nghiệp vụ bắn tin + theo dõi vị thế mà V1 từng làm (chủ sản phẩm khẳng định "V2 đã thay phần này"). Nếu chưa đủ, phải bổ sung ở V2 chứ không giữ lại V1 làm cầu.
- Dữ liệu lịch sử VIP (bảng + ảnh chụp trong nhật ký) không còn giá trị phân tích bắt buộc nếu chủ sản phẩm chọn drop; quyết định cuối nằm ở Q1.
- "Lợi nhuận thực" trong spec 010 là lãi/lỗ từ **lệnh thật của người dùng**, tách biệt khỏi "lợi nhuận thực V1" đo **tin VIP**; hai thứ trùng tên nhưng khác nguồn. Đợt này chỉ nhắm bản chất đo tin VIP.
- Việc gỡ là thay đổi cấu trúc có tính gate (ảnh hưởng job/pipeline/API) → đi qua Spec Kit (tệp này) rồi mới tới plan/tasks/implement; lượt này CHỈ khảo sát + spec, chưa đụng code.

## Quyết định đã chốt *(chủ sản phẩm duyệt 2026-10-22)*

- **Q1 — Dữ liệu 3 bảng VIP**: XUẤT ra file lưu trữ trước, rồi XOÁ bảng (`MasterAlertPositions`, `PositionSellLegs`, `VipAlertFires`). Việc xuất do chủ sản phẩm tự chạy trên production; migration drop nằm ĐỢT B, ship sau khi xác nhận đã xuất.
- **Q2 — Màn app hiển thị dữ liệu VIP** (thu hẹp sau khi kiểm lại): KHÔNG có màn nào hiển thị dữ liệu VIP ngoài tab "VIP (V1)" trong picker nhật ký. Web: không trang nào. Phần VIP còn lại trên web/mobile là **mã chết → xoá**.
- **Q3 — Picker tín hiệu V1 (spec 010)**: bỏ tab/nhánh VIP (backend + mobile), giữ V2. Lệnh đã lưu `NguonTinHieu='VIP'` giữ nguyên (giá đã chụp), cột kết quả tín hiệu để trống; app hiển thị nhãn **"VIP (đã ngừng)"**. KHÔNG migrate dữ liệu lệnh cũ.
- **Phạm vi**: GIỮ V1 phần lọc tới Top (Job 1, đồng bộ giá/nến, DailyAnalysis, Top 5, Home). BỎ toàn bộ bắn tin / vị thế / ML / AI judge của VIP theo bảng khảo sát (Phụ lục A).

## Câu hỏi cần chủ sản phẩm chốt *(đã chốt ở mục trên — giữ lại để đối chiếu lý do)*

### Q1 — Dữ liệu cũ (bảng VIP + lịch sử tín hiệu): xoá bảng hay giữ để tra cứu?

**Bối cảnh**: `MasterAlertPositions`, `PositionSellLegs`, `VipAlertFires` đã tích luỹ lịch sử. Sau khi bỏ đường V1, không còn tiến trình nào ghi mới, nhưng dữ liệu cũ vẫn còn.

**Đề xuất**: **Archive rồi drop** — xuất snapshot các bảng ra nơi lưu offline (đã có tiền lệ `scripts/export-db.ps1`), rồi gỡ bảng + entity + migration drop trong đợt implement. **Lý do**: thống nhất với ý định "xoá là xoá sạch, không giữ shim"; giữ bảng mồ côi mà không có màn/endpoint đọc sẽ thành nợ kỹ thuật và gây nhầm "còn dùng được". Nếu chủ sản phẩm muốn giữ lại để tra cứu, cần chỉ rõ **ai đọc và đọc ở đâu**, vì khảo sát cho thấy không còn controller nào phục vụ các bảng này (chỉ picker 010, và picker 010 cũng đang bị gỡ ở Q3).

### Q2 — Màn app nào đang hiển thị dữ liệu VIP: bỏ hay thay bằng dữ liệu V2?

**Bối cảnh**: Khảo sát web + mobile (Phụ lục A) cho thấy **phần lớn bề mặt VIP đã là mã chết** — không màn nào trong điều hướng thật sự hiển thị tin VIP/realized V1 (các chuỗi `/alerts`, `/performance/*` ở FE là hook/API-client không có caller, và backend không còn route `/performance/*`). Bề mặt VIP **duy nhất còn sống** là tab "VIP (V1)" trong picker tín hiệu của nhật ký trên mobile.

**Đề xuất**: **Gỡ** — xoá chuỗi mã chết (web `useLiveAlerts`/`RealtimeOrderRow`/`RealtimeOrderList`/`useViewedAlerts`/`alertViewState`, mobile `models.dart` nhóm Realized*/AlertItem, `api_client.dart` nhóm `/alerts`+`/performance/*`) và bỏ tab VIP trong picker (Q3); **không** "thay bằng V2" ở những chỗ đó vì V2 đã có màn riêng (kịch bản / hiệu quả) đang chạy. **Lý do**: tránh tạo màn trùng chức năng V2 đã có; chỉ gỡ, không dựng mới. Nếu chủ sản phẩm muốn giữ lại một màn "lịch sử tin VIP" để đối chiếu, đó phải là yêu cầu mới rõ ràng, không phải mặc định.

### Q3 — Picker tín hiệu V1 trong nhật ký (spec 010): bỏ?

**Bối cảnh**: Endpoint `GET /api/v1/nhat-ky/tin-hieu/vip` và nhánh service/repo đọc `MasterAlertPositions`/`PositionSellLegs` là **kẻ đọc sống duy nhất** còn lại của bảng VIP. Bỏ V1 ⇒ picker VIP mất 100% nguồn dữ liệu (khảo sát: repo picker chỉ SELECT từ hai bảng đó, không có nguồn dự phòng).

**Đề xuất**: **Bỏ nhánh VIP, giữ nhánh V2; và định nghĩa số phận cho lệnh đã gắn VIP cũ** — cụ thể: gỡ endpoint/repo/service-method/nguồn `"VIP"` khỏi danh sách hợp lệ + bỏ tab VIP trên mobile; với lệnh nhật ký **đã lưu** từng gắn VIP thì **giữ nguyên giá đã chụp** (data-model 010 đã thiết kế "tham chiếu mềm, không FK, tín hiệu bị xoá → giữ ảnh chụp") và chấp nhận cột "kết quả tín hiệu" của các lệnh đó không còn được cập nhật. **Lý do**: nhất quán với "xoá sạch"; snapshot đã chụp vốn là điểm đúng của 010 (lãi/lỗ tính từ lệnh người dùng, không phụ thuộc tín hiệu sống). **Cần chủ sản phẩm xác nhận**: (a) có cần thông báo/ghi chú gì cho lệnh VIP cũ không, và (b) xử lý giá trị `NguonTinHieu='VIP'` đã tồn tại trong bảng nhật ký — giữ như lịch sử hay migrate/nhãn lại.

## Phụ lục A — Bảng khảo sát thành phần (file:line đã mở kiểm chứng)

> Phân loại: **GIỮ** (còn phục vụ Home/V2/app dùng chung) · **BỎ** (dedicated cho bắn tin + vị thế VIP V1) · **SỬA** (file trộn cả VIP lẫn dùng chung, gỡ phần VIP) · **CHƯA RÕ** (cần quyết).

### A.1 Backend — class/job/API/options/bảng

| Thành phần | file:line | Ai dùng | Loại |
|---|---|---|---|
| `TopOpportunityVipAlertPublisher` | `Infrastructure/Notifications/TopOpportunityVipAlertPublisher.cs:1` (DI `Application/DependencyInjection.cs:141-142`) | chỉ `OpportunityIntradayMonitorRunner` + test-service Telegram VIP | **BỎ** |
| `TopOpportunityVipAlertEvaluator` | `Infrastructure/Notifications/TopOpportunityVipAlertEvaluator.cs:74` | chỉ publisher | **BỎ** |
| `VipTelegramMessageFormatter` | `Infrastructure/Notifications/VipTelegramMessageFormatter.cs` | chỉ publisher | **BỎ** |
| `VipLlmContextBuilder` / `VipLlmJudgeParsing` | `Infrastructure/Notifications/VipLlmContextBuilder.cs`, `VipLlmJudgeParsing.cs` | `AnthropicVipLlmJudge` | **BỎ** |
| `AnthropicVipLlmJudge` + `IVipLlmJudge` | `Infrastructure/Notifications/AnthropicVipLlmJudge.cs` (DI `DependencyInjection.cs:136-140`) | publisher (veto/chấm tin) | **BỎ** |
| `VipPullbackMaCache` / `VipPositionHistoryCache` / `VipVnIndexPeakCache` | `Infrastructure/Notifications/Vip*.cs` (DI `:131-133`) | publisher | **BỎ** |
| `MasterAlertSessionTracker` | `Infrastructure/Notifications/MasterAlertSessionTracker.cs` (DI `:130`) | publisher | **BỎ** |
| `OrderFlowSnapshotTracker` | `Infrastructure/Notifications/OrderFlowSnapshotTracker.cs` | runner (detection lô lớn) | **GIỮ** |
| `OpportunityIntradayMonitorRunner` | `Infrastructure/MarketData/OpportunityIntradayMonitorRunner.cs:23,32-41,96-121,149-157` (VIP) vs `:60-94,124-146,159-163` (lô lớn/VSA/dòng tiền/SignalR) | job + SignalR + app | **SỬA** (bỏ khối `vipAlerts.*`, giữ vòng quét trade event) |
| `OpportunityIntradayMonitorJob` (job `opportunity-monitor`) | `Infrastructure/Scheduling/Jobs/OpportunityIntradayMonitorJob.cs:11` · `QuartzJobIds.cs:9` · `QuartzSchedulingExtensions.cs:39,199-212` | Quartz; runner vẫn sinh dữ liệu app | **GIỮ (cân nhắc đổi tên, không còn "VIP")** |
| `IntradayScannerJob` (radar biến động) | `Infrastructure/Scheduling/Jobs/IntradayScannerJob.cs` | app radar | **GIỮ** |
| `IntradayMonitorStatusTracker` / `IntradayMonitorStatusQueryService` | `Infrastructure/MarketData/IntradayMonitor*.cs` (DI `:127-128`) | runner + FE `/market/intraday-monitor` | **GIỮ** (dòng trạng thái scanner) |
| `VipIntradayRanker/Calibration/Threshold/Training` + bootstrap + store | `Application/Services/VipIntraday*.cs`, `Infrastructure/Ml/VipIntraday*.cs`, `FileVipIntradayCalibrationStore.cs` (DI `:74-80,250-252`) | MlController `train/vip-intraday`,`vip-intraday/status`,`recalibrate` | **BỎ** (ML nội phiên dedicated VIP) |
| `VipIntradayFeatures` | `Domain/Services/OpportunityRanking/VipIntradayFeatures.cs` | VipIntraday ranker | **BỎ** |
| `VipAlertAccuracyQueryService` / `IVipAlertAccuracyQueryService` | `Application/Services/VipAlertAccuracyQueryService.cs` (DI `:73`) | **không consumer production** (chỉ đăng ký DI) | **BỎ** (orphan) |
| `OpportunityNorthStarQueryService` / `IOpportunityNorthStarQueryService` | `Application/Services/OpportunityNorthStarQueryService.cs` (DI `:72`) | **không consumer production** | **CHƯA RÕ** (tên "opportunity" nhưng không có caller; xác nhận trước khi bỏ) |
| `RealizedPnlService` / `RealizedPnlBackfillService` | `Application/Services/RealizedPnl*.cs` (DI `:86-88`) | backfill chỉ được gọi nội bộ; **không job/controller trigger** | **BỎ** (do chỉ đo tin VIP, spec 010 KHÔNG gọi) |
| `RealizedPnlMath` / `SellLeg` / `FeeProfile` | `Domain/Services/RealizedPnlMath.cs:6,22,34` | **nhật ký 010** (`Application/Services/NhatKyService.cs:166,252`) | **GIỮ** (dùng chung) |
| Entities `MasterAlertPositionEntity`/`PositionSellLegEntity`/`VipAlertFireEntity` | `Infrastructure/Persistence/Entities/PerformanceEntities.cs:41,100,120` · `ApplicationDbContext.cs:29-31,362,387,404` | publisher (ghi) + picker 010 (đọc) | **BỎ** (kèm migration drop — Q1) |
| `EfVipAlertFireRepository` / `IMasterAlertPositionRepository` / `EfMasterAlertPositionRepository` | `Infrastructure/Persistence/Repositories/EfVipAlertFireRepository.cs`, `EfPerformanceRepositories.cs:558-876` (DI `:188-191`) | publisher + performance read | **BỎ** |
| `EfPerformanceRepositories` — `GetAlertHistoryAsync`/`GetPositionsSinceAsync` | `Infrastructure/Persistence/Repositories/EfPerformanceRepositories.cs:249,829` (`IPerformanceServices.cs:154,317`) | chỉ RealizedPnlBackfill + test; **không controller** | **BỎ** (phần đọc VIP; phần khác trong file → **SỬA**) |
| `MasterAlertKinds` | `Domain/MasterAlerts/MasterAlertKinds.cs:3` | publisher + picker 010 (`EfNhatKyTinHieuRepository.cs:83-84`) | **SỬA/BỎ có kiểm soát** (bỏ hằng số bán VIP; 010 dùng làm nhãn leg → thay literal) |
| `OutcomeBucketNames` / `RealizedStatusNames` | `Domain/MasterAlerts/OutcomeBucketNames.cs:9`, `RealizedStatusNames.cs:4` | 010 thống kê + performance | **CHƯA RÕ** (namespace `MasterAlerts` nhưng 010 đang dùng `OutcomeBucketNames`) |
| Options `MasterAlertOptions` | `Application/Options/MasterAlertOptions.cs:3` · appsettings `MasterAlerts` (`appsettings.json:290-310`, `appsettings.Production.json:2-20`) | publisher + MlController + runner cfg | **BỎ** (nhưng **SỬA**: `RealizedPnlOptions` nằm chung file `:232`, 010 dùng → phải tách ra trước) |
| `RealizedPnlOptions` | `Application/Options/MasterAlertOptions.cs:232` · appsettings `RealizedPnl` (`:356`) | **010** (`NhatKyService.cs:19,24,166,571-578`) | **GIỮ** (dịch chuyển khỏi file MasterAlertOptions) |
| Options `VipLlmJudgeOptions` | `Application/Options/VipLlmJudgeOptions.cs` (DI `:33`) · appsettings `VipLlmJudge` (`:267`, `Production:59`) | `AnthropicVipLlmJudge` | **BỎ** |
| `TelegramNotifyOptions.VipAlertsEnabled` | `Application/Options/TelegramNotifyOptions.cs` · appsettings `TelegramNotify` (`:261-263`, `Production:56`) | publisher | **SỬA** (Telegram notifier vẫn dùng cho V2 → chỉ bỏ cờ/khoá VIP) |
| `OpportunityMonitorOptions` | `Application/Options/OpportunityMonitorOptions.cs` · appsettings `OpportunityMonitor` (`:247`) | runner (batch/trade scan) | **GIỮ** (dùng cho vòng quét trade event) |
| API `GET /api/v1/alerts` | `Api/Controllers/AlertsController.cs:9-19` (`AlertService.cs:16-19,63,72`) | FE chỉ qua hook chết; bản chất VIP-only | **BỎ** |
| API `POST /api/v1/market/jobs/telegram/vip-test` | `Api/Controllers/MarketJobsController.cs:195-204` (`IVipTelegramAlertTestService`) | admin test format VIP | **BỎ** |
| API `POST /api/v1/market/jobs/opportunity-monitor` | `MarketJobsController.cs:165` (`IOpportunityIntradayMonitorService`) | manual trigger vòng quét | **GIỮ** (trigger vòng quét dùng chung) |
| API `POST /api/v1/ml/train/vip-intraday`, `GET vip-intraday/status`, `POST vip-intraday/recalibrate` | `Api/Controllers/MlController.cs:166,181,205` (deps `:18-21,26`) | admin ML VIP | **SỬA** (bỏ 3 endpoint + dep VIP, giữ endpoint ranker T+2.5 dùng chung) |
| API `GET /api/v1/nhat-ky/tin-hieu/vip` | `Api/Controllers/NhatKyController.cs:115-124` | mobile picker 010 | **BỎ** (Q3) |

**Không nằm trong phạm vi gỡ (minh hoạ để chặn xoá nhầm)**: `DailyAnalysisRunner`, `SmartMoneyOpportunitySelector`, `OpportunitiesController`, `OpportunityRankerService` — grep VIP/MasterAlert/Realized **rỗng** → đường Top/Home sạch VIP → **GIỮ**.

### A.2 Web `frontend/src` (theo khảo sát UI)

| Thành phần | file:line | Loại |
|---|---|---|
| `hooks/useLiveAlerts.ts` (duy nhất subscribe SignalR `AlertCreated`) | `hooks/useLiveAlerts.ts:83,137` | **BỎ** (hook chết, feed VIP-only) |
| `components/alerts/RealtimeOrderRow.tsx` / `RealtimeOrderList.tsx` | `components/alerts/RealtimeOrderRow.tsx:17-18,78-83` · `RealtimeOrderList.tsx` | **BỎ** (badge "Master", wrapper feed; không có importer khác) |
| `hooks/useViewedAlerts.ts` · `lib/alertViewState.ts` | `hooks/useViewedAlerts.ts:5-12` · `lib/alertViewState.ts:5-44` | **BỎ** |
| `lib/api.ts` `getAlerts` · `types/index.ts` `Alert`/`AlertFeedScope` | `lib/api.ts:290-306` · `types/index.ts:13,15,384-397` | **BỎ/SỬA** (caller duy nhất là useLiveAlerts) |
| `context/LiveMarketContext.tsx` · `lib/marketHub.ts` · `hooks/useLiveTrades.ts` · `components/trades/TradePrintList.tsx` | `context/LiveMarketContext.tsx:135-136` (chỉ Quotes/Index) · `hooks/useLiveTrades.ts:47,68-95` (`TradeEventCreated`) | **GIỮ** (SignalR realtime dùng chung, KHÔNG nghe AlertCreated) |
| `pages/HomePage.tsx` (Top cơ hội + radar live) | `pages/HomePage.tsx:66,76,240-242,355` | **GIỮ** (ăn `/opportunities` + `/radar-items/live`) |
| `pages/HieuQuaPage.tsx` ("Lợi nhuận TB (đã đo)") | `pages/HieuQuaPage.tsx:57-58,179` | **GIỮ** (V2 spec 009, KHÔNG phải RealizedPnl V1) |
| `components/alerts/IntradayMonitorStatusLine.tsx` · `hooks/useIntradayMonitorStatus.ts` | `hooks/useIntradayMonitorStatus.ts:14` (`/market/intraday-monitor`) | **CHƯA RÕ** (monitor "trong phiên" nguồn khác VIP, nhưng đang orphan) |
| `components/performance/SmartMoneyBacktestPanel.tsx` | `components/performance/SmartMoneyBacktestPanel.tsx:29,138` (`/backtest/smartmoney`) | **CHƯA RÕ** (backtest V1, không đọc bảng VIP; 0 importer) |

### A.3 Mobile `mobile/lib`

| Thành phần | file:line | Loại |
|---|---|---|
| `screens/nhat_ky_tin_hieu_picker.dart` (tab "VIP (V1)") | `:8,31,39,88` (`getTinHieuNhatKy('vip')`) | **SỬA** (bỏ tab VIP + nhánh `'vip'`, giữ V2 — Q3) |
| `screens/nhat_ky_lenh_screen.dart` | `:93,95-97,184` | **SỬA** (bỏ chữ/nhánh VIP, giữ luồng V2) |
| `core/api/api_client.dart` (picker `tin-hieu/$nguon`, bo-qua) | `:616-639` | **SỬA** (mất giá trị `'vip'`, giữ V2) |
| `core/models/models.dart` `TinHieuNhatKy` | `:2119,2134,2146` | **SỬA** (xoá comment/nhánh VIP, giữ model dùng cho V2) |
| `core/api/api_client.dart` `getUniverseAlerts`/`/alerts` | `:183-203` | **BỎ** (0 caller) |
| `core/api/api_client.dart` `getPerformance*`/`getRealizedTrades` | `:445-515` (`/performance/*`) | **BỎ** (0 screen gọi + backend không có route — code chết VIP) |
| `core/models/models.dart` `RealizedTrade`/`RealizedPnlSummary`/`AlertHistory*`/`OpportunityPerformanceSummary`/`AlertItem` | `:347-411,413-485,487-545,547-604,606-697,1088-1125` | **BỎ** (chỉ dùng trong 2 file trên) |
| `widgets/smart_money_backtest_card.dart` · `widgets/score_pill.dart` `formatAlertTime` | `:12-37` · `:155` | **CHƯA RÕ** (backtest V1, 0 importer / helper tên "alert" 0 caller) |
| `core/services/market_hub_service.dart` (Listen `QuotesUpdated`+`TradeEventCreated`) | `:65-66,121-126` | **GIỮ** (realtime dùng chung, không `AlertCreated`) |
| `screens/home_screen.dart` / `hieu_qua_screen.dart` / `stock_detail_screen.dart` / `nhat_ky_screen.dart` | `home_screen.dart:132-133` · `nhat_ky_screen.dart:47-52,270` | **GIỮ** (Top/V2/nhật ký 010, không `/alerts`) |

### A.4 Test `backend/StockRadar.Tests` (tổng 51 file .cs)

| Nhóm | file tiêu biểu | Loại |
|---|---|---|
| `VipAlerts/BullTrapBuyPathTests.cs` (evaluator mua V1) | `VipAlerts/BullTrapBuyPathTests.cs:10` | **BỎ** |
| `SellExit/` (7/8 file: fixtures, BlueSky, SellWindow, SellHalfSize, Anchor, PreSellWindow) | `SellExit/SellExitFixtures.cs:10`, `SellHalfSizeTests.cs:14`, ... | **BỎ** (⚠ `SellExitFixtures.cs:94 BuildBoxThenBreak` đang được `DarvasRegressionTests.cs:11` dùng → chuyển helper trước) |
| `RealizedPnl/RealizedPnlServiceTests.cs`, `RealizedAggregateTests.cs` | `RealizedPnl/RealizedPnlServiceTests.cs:18`, `RealizedAggregateTests.cs:16` | **BỎ** |
| `SellExit/DarvasRegressionTests.cs` (Darvas dùng chung) | `SellExit/DarvasRegressionTests.cs:6` | **GIỮ** (di chuyển ra khỏi SellExit) |
| `RealizedPnl/RealizedPnlMathTests.cs` (công thức dùng cho 010) | `RealizedPnl/RealizedPnlMathTests.cs:6` | **GIỮ** |
| `VipAlerts/VnIndexPriorPeakAnalyzerTests.cs` (Domain dùng chung + alias V1) | `VipAlerts/VnIndexPriorPeakAnalyzerTests.cs:6` | **CHƯA RÕ** (`VnIndexPriorPeakAnalyzer` dùng ở `MarketService.cs:68,513`; chỉ alias `ShouldBlockBuy` là V1) |
| `NhatKy/NhatKyTinHieuTests.cs`, `NhatKyN8GiaChupTests.cs`, `NhatKyThongKeTests.cs` | `NhatKy/NhatKyTinHieuTests.cs:75-93,264-299`, `NhatKyN8GiaChupTests.cs:72-107,323-360`, `NhatKyThongKeTests.cs:75-91,268-285` | **GIỮ-SỬA** (bỏ ca/h/seed VIP, giữ ca V2) |
| `NhatKy/NhatKyLenhTests.cs`, `NhatKyBoQuaTests.cs`, `NhatKyCachLyTests.cs` | (không seed bảng V1) | **GIỮ** (chỉ dính `RealizedPnlOptions`) |

### A.5 Ước lượng phạm vi gỡ (phục vụ lập kế hoạch, chưa phải con số cam kết)

- **Backend**: ~20 file **xoá trọn** (publisher/evaluator/formatter/LLM judge+caches+context+parsing, VipIntraday services+ML+store+features, RealizedPnl service/backfill+accuracy query, `VipAlertFireRepository`+`IVipLlmJudge`+interface VIP, 3 entity trong `PerformanceEntities`, `MasterAlertOptions` [sau khi tách `RealizedPnlOptions`], `VipLlmJudgeOptions`, 3 controller endpoint VIP). **SỬA**: `OpportunityIntradayMonitorRunner`, `MlController`, `MarketJobsController`, `AlertsController`+`AlertService` (bỏ feed Master), `ApplicationDbContext`+snapshot, `DependencyInjection` (App+Infra), `MasterAlertKinds`, Quartz (đổi tên job `opportunity-monitor`), 2 file `appsettings`. **Bảng DB**: 3 (`MasterAlertPositions`, `PositionSellLegs`, `VipAlertFires`) + 1 migration drop (Q1).
- **Web**: ~5–6 file **xoá trọn** (`useLiveAlerts`,`useViewedAlerts`,`RealtimeOrderRow`,`RealtimeOrderList`,`alertViewState`) + **SỬA** `api.ts`,`types/index.ts`. **CHƯA RÕ**: `IntradayMonitorStatus*`,`SmartMoneyBacktestPanel`.
- **Mobile**: **SỬA** 4 file (picker/lenh-screen/api_client/models) + **BỎ** nhóm model Realized*/AlertItem & block api_client `/alerts`,`/performance/*`. **CHƯA RÕ**: `smart_money_backtest_card.dart`,`formatAlertTime`.
- **Test**: ~10 file **xoá**, ~3 file **GIỮ-sửa**, còn lại giữ; spec 010 (NhatKy) bị ảnh hưởng dây chuyền như A.4.

### A.6 Ba "bẫy" khi gỡ (phải xử lý đúng thứ tự)

1. **`RealizedPnlOptions` nằm trong file `MasterAlertOptions.cs:232`** nhưng 010 đang dùng (`NhatKyService.cs:19,24,166,571-578`) → **tách ra file riêng trước khi xoá** `MasterAlertOptions`, nếu không 6 file test NhatKy vỡ.
2. **`RealizedPnlMath` / `OutcomeBucketNames` (namespace `Domain/MasterAlerts`)** là công thức dùng chung cho 010 → **không xoá trắng** thư mục/namespace `Domain/MasterAlerts`.
3. **`OrderFlowSnapshotTracker` + `sessionFlow`/`tradeDetector`/`tradeAggregator` + `publisher`/`quoteCache`/`sync`** trong `OpportunityIntradayMonitorRunner` nuôi dữ liệu lô lớn/realtime cho app → **chỉ gỡ các lời gọi `vipAlerts.*`**, giữ nguyên phần còn lại của vòng quét.
