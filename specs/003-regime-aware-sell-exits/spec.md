# Feature Specification: Điểm bán 1/2 theo bối cảnh giá (nền trên vs vượt đỉnh)

**Feature Branch**: `003-regime-aware-sell-exits`

**Created**: 2026-08-10

**Status**: Draft

**Input**: User description: "Điểm cắt lỗ/chốt lãi cần động theo bối cảnh. (1) Nếu phía trên còn nền giá thì lấy cạnh dưới của nền trên làm điểm ra — ví dụ nền 1/7–20/7 dao động 10–12, gãy 2–3 phiên rồi bật lên, mua nửa ở 8.5, mua hết ở 9, thì điểm chốt lãi là 10. Nền xác định theo biên độ giá (dùng lại bộ dò hộp Darvas hiện có), với độ dài từ 20 phiên trở lên. (2) Nếu mua trên đà tăng vượt đỉnh (không còn đỉnh cao hơn, hoặc đỉnh cao hơn cách cả năm) thì điểm cắt lỗ 1 là khi giá giảm ≥4% so với giá cao nhất 20 phiên vừa qua (dùng giá cao nhất trong phiên, không phải giá chốt phiên), cắt lỗ 2 là giảm ≥6%; phủ nhận hoàn toàn cây tăng vượt đỉnh thì bán hết. Hệ số pha thị trường đảo chiều: chợ xấu bán sớm, chợ tốt giữ lâu (Favorable 1.25 / Neutral 1.0 / Unfavorable 0.75)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Bảo vệ vốn theo đỉnh 20 phiên khi mua vượt đỉnh (Priority: P1)

Nhà đầu tư mua một mã đang phá đỉnh, phía trên không còn vùng cản nào gần. Vì không có mốc kháng cự để chốt, hệ thống phải bảo vệ bằng khoảng cách so với **giá cao nhất 20 phiên gần nhất** (tính theo giá cao nhất trong phiên, gồm cả diễn biến của phiên đang chạy). Khi giá rơi khỏi neo này đủ sâu, nhà đầu tư nhận cảnh báo bán 1 nửa, rồi bán hết. Nếu giá phủ nhận hoàn toàn cây nến vượt đỉnh, nhà đầu tư nhận lệnh bán hết ngay, không chờ đủ mức giảm.

**Why this priority**: Hệ thống hiện tại chỉ kích hoạt thoát lệnh sau khi vị thế đã từng lãi tối thiểu; vị thế mua xong rơi thẳng sẽ **không nhận bất kỳ tín hiệu bán nào**. Đây là lỗ hổng mất vốn lớn nhất và phải được bịt trước tiên. Slice này chạy độc lập, không cần bộ phát hiện nền trên.

**Independent Test**: Mở một vị thế mô phỏng trên mã vừa phá đỉnh, cho giá giảm dần từ đỉnh 20 phiên và kiểm tra mốc phát cảnh báo bán 1 nửa và bán hết đúng ngưỡng theo pha thị trường; lặp lại với kịch bản thủng đáy cây nến vượt đỉnh để xác nhận bán hết tức thì.

**Acceptance Scenarios**:

1. **Given** vị thế đã qua cửa sổ bán và pha thị trường Neutral, đỉnh cao nhất 20 phiên là 100, **When** giá giao dịch chạm 96, **Then** hệ thống phát cảnh báo bán 1 nửa và ghi nhận vị thế còn 50%.
2. **Given** cùng vị thế đã bán 1 nửa, **When** giá tiếp tục xuống 94, **Then** hệ thống phát cảnh báo bán hết và đóng vị thế.
3. **Given** pha thị trường Unfavorable, đỉnh 20 phiên là 100, **When** giá chạm 97, **Then** hệ thống đã phát cảnh báo bán 1 nửa (ngưỡng siết còn 3%).
4. **Given** pha thị trường Favorable, đỉnh 20 phiên là 100, **When** giá chạm 96, **Then** hệ thống **chưa** phát cảnh báo (ngưỡng nới thành 5%).
5. **Given** vị thế mở từ cây nến vượt đỉnh có giá thấp nhất phiên là 88, **When** giá xuống dưới 88, **Then** hệ thống phát cảnh báo bán hết ngay cả khi mức giảm so với đỉnh 20 phiên chưa đạt ngưỡng bán hết.
6. **Given** vị thế đang lỗ và chưa từng có lãi, **When** giá rơi khỏi ngưỡng bán 1 nửa so với mốc tham chiếu, **Then** hệ thống vẫn phát cảnh báo (không còn điều kiện "phải từng lãi tối thiểu").
7. **Given** vị thế mua ở 8.5 trong khi giá cao nhất 20 phiên trước ngày mua là 12, **When** cửa sổ bán mở ra và giá vẫn quanh 8.5, **Then** hệ thống **không** phát cảnh báo bán, vì mốc tham chiếu chỉ tính từ ngày mua trở đi.

---

### User Story 2 - Chốt lãi tại cạnh dưới nền giá bên trên (Priority: P2)

Nhà đầu tư mua một mã vừa gãy khỏi nền tích lũy dài rồi bật lên (mua 1 nửa ở vùng thấp, mua nốt khi xác nhận). Phía trên vị thế vẫn còn **nền giá cũ kéo dài từ 20 phiên trở lên** đóng vai trò vùng cản. Hệ thống lấy **cạnh dưới của nền trên gần nhất** làm mục tiêu chốt lãi: khi giá tiến tới mốc đó, nhà đầu tư nhận cảnh báo bán 1 nửa. Phần còn lại chỉ bán hết khi giá bị đẩy ngược khỏi vùng cản; nếu giá vượt hẳn nền trên kèm thanh khoản, phần còn lại chuyển sang bảo vệ theo User Story 1 thay vì bán.

**Why this priority**: Đây là luật giao dịch chính của người dùng cho nhóm lệnh mua hồi, nhưng cần bộ phát hiện vùng cản mới nên phụ thuộc hạ tầng nhiều hơn slice P1.

**Independent Test**: Nạp lịch sử giá của một mã có nền ≥20 phiên rồi gãy nền, mở vị thế mô phỏng dưới nền, cho giá hồi lên chạm cạnh dưới nền và kiểm tra cảnh báo bán 1 nửa cùng mục tiêu hiển thị đúng bằng cạnh dưới nền.

**Acceptance Scenarios**:

1. **Given** mã có nền 20 phiên dao động 10–12 rồi gãy xuống, vị thế mở ở 8.5 và 9, **When** hệ thống phân loại vị thế, **Then** vị thế được gắn mục tiêu chốt lãi bằng 10 (cạnh dưới nền trên).
2. **Given** vị thế trên đã qua cửa sổ bán, **When** giá hồi lên chạm vùng ngay dưới 10, **Then** hệ thống phát cảnh báo bán 1 nửa kèm nêu rõ mục tiêu là cạnh dưới nền 10–12.
3. **Given** vị thế đã bán 1 nửa tại cạnh dưới nền, **When** giá đóng cửa trở lại dưới cạnh dưới nền sau khi đã chạm, **Then** hệ thống phát cảnh báo bán hết.
4. **Given** vị thế đã bán 1 nửa tại cạnh dưới nền, **When** giá vượt lên trên nền trên kèm thanh khoản xác nhận, **Then** hệ thống **không** bán nốt mà chuyển phần còn lại sang luật bảo vệ theo đỉnh (User Story 1).
5. **Given** phía trên vị thế chỉ có đỉnh đơn lẻ kéo dài dưới 20 phiên, **When** hệ thống phân loại, **Then** vị thế **không** được coi là có nền trên và đi theo User Story 1.
6. **Given** đỉnh cao hơn gần nhất nằm cách hơn khoảng một năm giao dịch, **When** hệ thống phân loại, **Then** coi như không còn cản phía trên và đi theo User Story 1.

---

### User Story 3 - Cảnh báo trong cửa sổ khóa T+ và ghi nhận để đối chứng (Priority: P3)

Trong giai đoạn chưa được phép bán theo quy định thanh toán, nhà đầu tư vẫn cần biết vị thế đã chạm mục tiêu hoặc đã thủng ngưỡng bảo vệ, để chuẩn bị hành động ngay khi cửa sổ bán mở. Đồng thời mỗi cảnh báo bán phải ghi lại bối cảnh (chế độ nào, mốc nào, pha nào) để đối chứng hiệu quả về sau.

**Why this priority**: Không đổi quyết định mua/bán, nhưng quyết định chất lượng trải nghiệm và khả năng đo lường luật mới.

**Independent Test**: Mở vị thế mô phỏng và cho chạm ngưỡng ở phiên chưa đủ điều kiện bán; kiểm tra nội dung cảnh báo nêu đúng mốc đã chạm và thời điểm dự kiến được bán, đồng thời bản ghi vị thế lưu đủ bối cảnh.

**Acceptance Scenarios**:

1. **Given** vị thế mới mua chưa đủ số phiên tối thiểu để bán, **When** giá chạm mục tiêu chốt lãi hoặc thủng ngưỡng bảo vệ, **Then** hệ thống phát cảnh báo rủi ro nêu rõ mốc đã chạm và ghi rõ chưa bán được, **không** dùng chữ "Bán".
2. **Given** một cảnh báo bán vừa phát, **When** kiểm tra bản ghi vị thế, **Then** thấy chế độ áp dụng, mốc tham chiếu, ngưỡng đã dùng và pha thị trường tại thời điểm phát.

---

### Edge Cases

- **Mua hồi sâu nhưng không có nền trên hợp lệ**: vị thế rơi vào chế độ Vượt đỉnh dù giá vào cách xa đỉnh 20 phiên trước đó. Mốc tham chiếu không lùi xa hơn ngày mua nên không bắn cảnh báo bán ngay tại phiên mở cửa sổ bán (FR-012, FR-016).
- **Nền trên rộng hơn giới hạn biên độ**: vùng cản có thật nhưng khung đóng cửa vượt 15% → không được nhận là nền, vị thế đi theo chế độ Vượt đỉnh và mất mục tiêu chốt lãi tại cản.
- **Nhiễu trong phiên**: biên độ phiên rộng khiến giá quét qua ngưỡng rồi hồi ngay; cần xác nhận trước khi phát (FR-013).
- **Nền trên rất rộng**: nền dài nhưng biên độ lớn thì cạnh dưới vẫn là mục tiêu; không loại nền vì lý do biên độ (FR-007).
- **Nhiều nền phía trên**: chọn nền gần nhất theo giá, không phải nền lớn nhất (FR-008).
- **Thiếu dữ liệu lịch sử**: mã chưa đủ số phiên để dựng neo hoặc dò nền → không được im lặng bỏ qua bảo vệ (FR-017).
- **Pha thị trường đổi giữa kỳ nắm giữ**: mua lúc thuận lợi, thị trường xấu đi → ngưỡng phải siết ngay, không dùng pha lúc mua (FR-011).
- **Giá trần / mất thanh khoản**: giá thủng ngưỡng nhưng không khớp được lệnh; cảnh báo vẫn phát, hệ thống không giả định đã bán được.
- **Vị thế đã bán 1 nửa từ luật cũ** khi luật mới bật: phải tiếp tục được quản lý, không mồ côi.

## Requirements *(mandatory)*

### Functional Requirements

**Phân loại chế độ**

- **FR-001**: Hệ thống MUST phân loại mỗi vị thế vào một trong hai chế độ thoát lệnh: **Có nền trên** (còn vùng cản phía trên giá vào) hoặc **Vượt đỉnh** (không còn cản gần phía trên).
- **FR-002**: Hệ thống MUST chốt chế độ tại thời điểm mở vị thế và lưu kèm vị thế, để ngưỡng thoát không đổi nghĩa giữa các chu kỳ quét trong ngày.
- **FR-003**: Hệ thống MUST cho phép một vị thế chuyển từ chế độ **Có nền trên** sang **Vượt đỉnh** khi giá vượt hẳn nền trên kèm thanh khoản xác nhận; chiều ngược lại KHÔNG được phép.

**Nhận diện nền giá bên trên**

- **FR-004**: Hệ thống MUST nhận diện nền giá theo **biên độ giá**, dùng lại bộ nhận diện hộp tích lũy hiện hành của sản phẩm (khung dao động giới hạn, râu nến không vượt biên quá mức cho phép, chạm đủ số lần ở cả cạnh trên và cạnh dưới) thay vì xây bộ dò riêng.
- **FR-005**: Hệ thống MUST yêu cầu nền có **độ dài tối thiểu 20 phiên giao dịch**; nền dài hơn vẫn hợp lệ.
- **FR-005a**: Giới hạn biên độ áp cho nền-làm-vùng-cản MUST là **15%**, cấu hình độc lập với giới hạn biên độ dùng cho nhận diện phá vỡ (giữ nguyên ~10%) để không đổi hành vi chọn Top hiện tại.
- **FR-006**: Hệ thống MUST chỉ xét các nền nằm **phía trên** giá vào của vị thế.
- **FR-007**: Hệ thống MUST lấy **cạnh dưới của nền** làm mục tiêu chốt lãi của chế độ Có nền trên.
- **FR-008**: Khi có nhiều nền phía trên, hệ thống MUST chọn nền **gần giá hiện tại nhất**.
- **FR-009**: Hệ thống MUST coi vùng cản nằm quá xa về thời gian (mốc mặc định: hơn một năm giao dịch) là **không còn hiệu lực**, và xếp vị thế vào chế độ Vượt đỉnh.
- **FR-010**: Hệ thống MUST phân biệt nền đi ngang với một đoạn xu hướng cùng độ dài, để không lấy đáy của một nhịp giảm làm mục tiêu chốt lãi; điều kiện chạm đủ số lần ở cả hai cạnh của bộ nhận diện hộp hiện hành đóng vai trò này.

**Ngưỡng thoát và pha thị trường**

- **FR-011**: Hệ thống MUST điều chỉnh mọi ngưỡng thoát theo **pha thị trường tại thời điểm đánh giá** (không phải pha lúc mua), với hệ số: Favorable **1.25**, Neutral **1.0**, Unfavorable **0.75** — chợ xấu thoát sớm hơn, chợ tốt giữ lâu hơn.
- **FR-012**: Ở chế độ Vượt đỉnh, mốc tham chiếu MUST là **giá cao nhất trong phiên đạt được trong cửa sổ 20 phiên giao dịch gần nhất, nhưng không lùi xa hơn ngày mở vị thế**, và bao gồm cả phiên đang diễn ra; MUST NOT dùng giá đóng cửa làm mốc. Ngày mới mua, mốc là giá cao nhất của chính phiên mua; vị thế giữ trên 20 phiên thì mốc trượt theo cửa sổ 20 phiên.
- **FR-013**: Ở chế độ Vượt đỉnh, hệ thống MUST phát **bán 1 nửa** khi giá thấp hơn mốc tham chiếu **4%** (nhân hệ số pha) và **bán hết** khi thấp hơn **6%** (nhân hệ số pha); phần trăm tính trên chính mốc tham chiếu, không tính trên giá vốn.
- **FR-014**: Hệ thống MUST phát **bán hết ngay** khi giá phủ nhận hoàn toàn cây nến vượt đỉnh của vị thế, không cần chờ đủ mức giảm ở FR-013.
- **FR-015**: Hệ thống MUST NOT yêu cầu vị thế phải đạt một mức lãi tối thiểu trước khi kích hoạt các ngưỡng thoát — luật thoát đồng thời đóng vai trò cắt lỗ.
- **FR-016**: Khi không tìm được nền trên hợp lệ, hệ thống MUST xếp vị thế vào chế độ Vượt đỉnh; hệ thống MUST NOT có luật bảo vệ thứ ba. Nhờ mốc tham chiếu ở FR-012 không lùi xa hơn ngày mua, chế độ Vượt đỉnh cũng an toàn với lệnh mua hồi sâu.
- **FR-017**: Khi thiếu dữ liệu lịch sử để dò nền, hệ thống MUST xếp vị thế vào chế độ Vượt đỉnh và ghi nhận sự kiện, MUST NOT bỏ vị thế không được bảo vệ; mốc tham chiếu luôn dựng được vì chỉ cần dữ liệu từ ngày mua trở đi.

**Hành vi chế độ Có nền trên**

- **FR-018**: Hệ thống MUST phát **bán 1 nửa** khi giá tiến tới cạnh dưới nền trên, chốt trước mốc một khoảng đệm nhỏ thay vì đợi chạm đúng mốc.
- **FR-019**: Sau khi đã bán 1 nửa tại cạnh dưới nền, hệ thống MUST phát **bán hết** khi giá bị đẩy ngược khỏi vùng cản (đóng cửa trở lại dưới cạnh dưới nền sau khi đã chạm) hoặc khi thủng đáy của nhịp hồi.
- **FR-020**: Sau khi chuyển sang chế độ Vượt đỉnh theo FR-003, phần vị thế còn lại MUST dùng đúng định nghĩa mốc tham chiếu ở FR-012; hệ thống MUST NOT định nghĩa mốc riêng cho tình huống chuyển chế độ.

**Chống nhiễu, cửa sổ bán và giao tiếp**

- **FR-021**: Hệ thống MUST yêu cầu xác nhận giá giữ qua ngưỡng trong nhiều chu kỳ quét liên tiếp trước khi phát cảnh báo bán, để giảm cảnh báo do nhiễu trong phiên.
- **FR-022**: Hệ thống MUST giữ nguyên ràng buộc chỉ phát tín hiệu bán từ phiên thứ ba kể từ ngày mua; trước mốc đó MUST phát cảnh báo rủi ro nêu rõ mốc đã chạm và MUST NOT dùng ngôn ngữ ra lệnh bán.
- **FR-023**: Mỗi cảnh báo bán MUST nêu chế độ đang áp dụng, mốc tham chiếu (cạnh dưới nền hoặc đỉnh 20 phiên), ngưỡng đã dùng và pha thị trường.
- **FR-024**: Hệ thống MUST giữ nguyên nhánh phát cảnh báo khi phát hiện dấu hiệu phân phối và cơ chế hạn chế tần suất gửi lại cùng một loại cảnh báo.
- **FR-025**: Hệ thống MUST ghi lại chế độ, mốc tham chiếu, ngưỡng và pha của mỗi cảnh báo bán để phục vụ đối chứng hiệu quả.
- **FR-026**: Toàn bộ ngưỡng, hệ số pha, độ dài nền tối thiểu, cửa sổ neo và số chu kỳ xác nhận MUST cấu hình được, không cố định trong mã.
- **FR-027**: Vị thế đang mở theo luật cũ khi luật mới có hiệu lực MUST được phân loại lại và tiếp tục quản lý theo luật mới.

### Key Entities

- **Vị thế cảnh báo**: một lần vào lệnh đang theo dõi — mã, ngày mua, giá vốn, tỷ trọng còn lại, các loại cảnh báo đã phát; **bổ sung**: chế độ thoát lệnh, mục tiêu chốt lãi (nếu có nền trên), mốc phủ nhận cây vượt đỉnh, mốc tham chiếu đang dùng.
- **Nền giá bên trên**: hộp dao động theo biên độ giá, dài từ 20 phiên trở lên, nằm phía trên giá vào — cạnh dưới, cạnh trên, phiên bắt đầu, phiên kết thúc, khoảng cách thời gian tới hiện tại.
- **Mốc tham chiếu bảo vệ**: giá cao nhất của cửa sổ 20 phiên gần nhất, cập nhật theo diễn biến trong phiên.
- **Cấu hình ngưỡng thoát**: ngưỡng bán 1 nửa / bán hết, hệ số theo pha thị trường, độ dài nền tối thiểu, khoảng đệm chốt trước cản, số chu kỳ xác nhận.
- **Bản ghi cảnh báo bán**: mã, loại cảnh báo, chế độ, mốc tham chiếu, ngưỡng, pha, giá tại thời điểm phát.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% vị thế đang mở luôn có ít nhất một ngưỡng thoát xác định; không còn vị thế nào lỗ sâu mà hệ thống chưa từng phát tín hiệu bán hoặc cảnh báo.
- **SC-002**: Trên dữ liệu đối chứng 12 tháng, tỷ lệ lệnh đóng với mức lỗ vượt 8% giảm ít nhất 50% so với luật thoát hiện hành.
- **SC-003**: Trên cùng dữ liệu đối chứng, mức lãi trung bình mỗi lệnh thắng không giảm quá 10% so với luật hiện hành (siết cắt lỗ nhưng không cắt cụt lệnh thắng).
- **SC-004**: Ở chế độ Có nền trên, ít nhất 70% cảnh báo bán 1 nửa được phát khi giá nằm trong phạm vi 1% quanh cạnh dưới nền trên.
- **SC-005**: Khi pha thị trường chuyển sang Unfavorable, ngưỡng thoát siết lại chậm nhất trong một chu kỳ quét kế tiếp.
- **SC-006**: Số cảnh báo bán trung bình trên mỗi vị thế không vượt quá 3 trong suốt vòng đời vị thế (kiểm chứng hiệu quả chống nhiễu).
- **SC-007**: Mọi cảnh báo bán đều đọc được mốc tham chiếu và lý do; kiểm tra ngẫu nhiên 20 cảnh báo, 100% nêu đủ chế độ, mốc và pha.

## Assumptions

- "Phủ nhận hoàn toàn cây tăng vượt đỉnh" được hiểu là giá xuống dưới **giá thấp nhất của cây nến vượt đỉnh** đó.
- Giá dùng cho mọi so sánh trong phiên là giá khớp gần nhất; mốc tham chiếu dùng giá cao nhất đã đạt, không dùng giá đóng cửa.
- Ngưỡng gốc 4% / 6% ứng với pha Neutral; hai pha còn lại suy ra từ hệ số 1.25 và 0.75 (Favorable 5% / 7.5%, Unfavorable 3% / 4.5%). Vùng 0.75–0.85 cho Unfavorable sẽ được dò lại bằng đối chứng dữ liệu.
- Khoảng đệm chốt trước cạnh dưới nền cũng chịu ảnh hưởng của pha: chợ xấu lùi xa cản hơn, chợ tốt cho phép chạm sát cản.
- Cửa sổ neo 20 phiên và độ dài nền tối thiểu 20 phiên là hai tham số độc lập, dù cùng giá trị mặc định.
- Chỉ có một định nghĩa mốc tham chiếu dùng chung cho mọi vị thế ở chế độ Vượt đỉnh, kể cả vị thế vừa chuyển từ chế độ Có nền trên; không có luật bảo vệ thứ ba.
- Giới hạn biên độ 15% đo trên khung giá đóng cửa; râu nến vẫn được phép vượt biên trong dung sai của bộ dò hộp hiện hành, nên một vùng dao động dạng 10–12 thường vẫn đạt chuẩn.
- Nền được nhận diện bằng bộ dò hộp tích lũy sẵn có của sản phẩm, chỉ nới tham số (độ dài tối thiểu, giới hạn biên độ) và bổ sung khả năng liệt kê các hộp nằm phía trên một mức giá; KHÔNG xây bộ dò song song và KHÔNG đổi tham số đang dùng cho nhận diện phá vỡ.
- Cạnh dưới của nền được đo theo giá đóng cửa thấp nhất trong nền, thống nhất với cách bộ dò hộp hiện hành xác định biên.
- Tính năng chỉ đổi **luật thoát lệnh và cảnh báo bán**; KHÔNG đổi Buy Score, cổng chọn Top, luật vào lệnh hay hệ chấm điểm sóng hồi.
- Phạm vi áp dụng là các vị thế do hệ cảnh báo VIP mở; không mở rộng sang danh mục người dùng tự nhập.
- Ràng buộc thanh toán T+ giữ nguyên, không nằm trong phạm vi thay đổi.
- Việc đảo chiều hệ số pha chỉ ảnh hưởng luồng bán; không có nơi nào khác trong hệ thống tiêu thụ bộ hệ số này.
- Thay đổi này chạm hợp đồng sản phẩm về cảnh báo bán nên phải cập nhật tài liệu domain về quyết định mua/bán trong cùng change set.
