# Feature Specification: Thông báo đẩy V3 lên app mobile (song song Telegram)

**Feature Branch**: `013-mobile-push-notify` (làm trên `master` như spec 012)

**Created**: 2026-10-29

**Status**: Draft — owner đã chốt 3 câu hỏi (29/10/2026), chờ lập plan

**Input**: Owner (29/10/2026): "gửi tất cả thông tin dưới dạng noti trực tiếp từ app sang điện thoại đang cài app như noti của các app khác"; chọn **chạy song song** — giữ Telegram làm dự phòng.

## Bối cảnh

- Tin tối ưu mô hình (HPO) cũng chỉ đi Telegram: `HyperparameterTuningRunner` gửi 4 loại — thiếu Python, quá giờ, lỗi thoát, kết quả xong.
- Hiện mọi tin V3 chỉ đi Telegram, qua `ITelegramNotifier.SendAsync` ở 7 chỗ trong 4 runner: `V3TruocPhienRunner` (08:30), `V3TrongPhienRunner` (mỗi phút), `V3AtcRunner` (~14:25), `V3SauPhienRunner` (16:00). Nội dung dựng bởi `V3TelegramFormatter` (`FormatMua`, `FormatBan`, `FormatHuyMua`, `FormatAtoN3`).
- App mobile chưa có phần nhận thông báo. Kết nối SignalR (`MarketHubService`) chỉ sống khi app mở, không dùng được cho thông báo lúc app tắt.
- Máy owner: vivo, Android 16, có Google Play Services → nhận được thông báo qua Firebase Cloud Messaging (FCM).
- Log production đang ghi lộ token bot Telegram (HttpClient ghi đủ URL `.../bot<token>/sendMessage` ở mức Information). Vì Telegram còn chạy song song nên spec này gồm cả việc chặn lộ.

## User Scenarios & Testing

### User Story 1 — Nhận tin V3 trên thanh thông báo điện thoại (Priority: P1)

Owner đã đăng nhập app một lần trên điện thoại. Khi V3 phát lệnh mua, bán, huỷ mua hoặc ATO N3, điện thoại hiện thông báo như các app khác, kể cả khi app đang tắt hoặc màn hình khoá. Telegram vẫn nhận tin như cũ.

**Why this priority**: là mục tiêu chính của owner; Telegram giữ làm dự phòng nên rủi ro thấp.

**Independent Test**: trên DB local, chèn dữ liệu làm runner phát 1 lệnh mua (hoặc gọi endpoint gửi thử ở US3), tắt app, kiểm thông báo hiện trên máy thật và Telegram vẫn nhận.

**Acceptance Scenarios**:

1. **Given** app đã đăng nhập và cấp quyền thông báo, app đang tắt, **When** nhịp ATC phát lệnh mua N1 mã HPG, **Then** điện thoại hiện thông báo có mã, kịch bản, giá; Telegram nhận tin cùng nội dung.
2. **Given** FCM lỗi (mạng, khoá sai), **When** runner gửi tin, **Then** Telegram vẫn gửi, runner vẫn chạy hết nhịp, log ghi lỗi FCM một dòng.
3. **Given** Telegram lỗi, **When** runner gửi tin, **Then** thông báo đẩy vẫn gửi.
4. **Given** owner từ chối quyền thông báo, **When** có tin, **Then** không có gì hiện trên máy, app không sập; trong app có chỗ nhắc bật lại quyền.

---

### User Story 2 — Chạm thông báo mở đúng màn (Priority: P2)

Chạm vào thông báo thì app mở thẳng màn chi tiết mã (`/stocks/:symbol`), cuộn tới thẻ kịch bản V3.

**Why this priority**: tiện, nhưng không có cũng vẫn nhận được tin.

**Independent Test**: gửi thử 1 thông báo cho HPG, chạm vào, kiểm app mở Chi tiết HPG — cả khi app đang tắt hẳn lẫn đang chạy nền.

**Acceptance Scenarios**:

1. **Given** app tắt hẳn, **When** chạm thông báo HPG, **Then** app mở, qua màn đăng nhập nếu phiên hết hạn, rồi tới Chi tiết HPG.
2. **Given** app đang mở ở màn khác, **When** chạm thông báo, **Then** chuyển sang Chi tiết mã đó, nút Quay lại về màn trước.

---

### User Story 3 — Quản lý thiết bị nhận tin (Priority: P3)

Đăng nhập là máy tự đăng ký nhận tin; đăng xuất là gỡ. Trong app có nút "Gửi thông báo thử" để kiểm đường đi đầu–cuối mà không cần chờ tín hiệu thật.

**Why this priority**: cần để test và để máy cũ không nhận tin mãi; nhưng US1 chạy được với một máy đăng ký tay.

**Acceptance Scenarios**:

1. **Given** máy chưa đăng ký, **When** đăng nhập, **Then** server lưu mã thiết bị gắn với tài khoản.
2. **Given** máy đã đăng ký, **When** đăng xuất, **Then** server xoá mã thiết bị đó; tin sau không tới máy.
3. **Given** FCM báo mã thiết bị không còn hợp lệ (gỡ app, đổi máy), **When** gửi tin, **Then** server xoá mã đó, không gửi lại lần sau.
4. **Given** đã đăng nhập, **When** bấm "Gửi thông báo thử", **Then** trong vòng vài giây máy nhận một thông báo ghi rõ là tin thử.

### Edge Cases

- Nhịp trong phiên phát nhiều lệnh bán cùng phút: mỗi lệnh một thông báo, không gộp (giống Telegram).
- Cùng một tài khoản đăng nhập trên 2 máy: cả 2 máy nhận.
- Mã thiết bị FCM đổi (Firebase tự làm mới): app gửi mã mới lên, server thay mã cũ.
- Server khởi động lại giữa nhịp: không gửi trùng tin đã gửi (dựa cờ đã có, ví dụ `DaBanTinThoat`).
- Chế độ tiết kiệm pin của vivo làm tin tới trễ: gửi mức ưu tiên cao; app hướng dẫn tắt tối ưu pin.
- Khoá Firebase chưa cấu hình trên server: bỏ qua kênh đẩy, log cảnh báo một lần, ở lần gửi đầu tiên, Telegram chạy bình thường.

## Requirements

### Functional Requirements

**Gửi tin**

- **FR-001**: Mọi tin V3 hiện gửi Telegram (mua ATO, mua ATC, bán realtime, ATO N3, huỷ mua) MUST được gửi thêm qua thông báo đẩy, cùng thời điểm, cùng thông tin chính (mã, kịch bản, giá, lý do).
- **FR-001a**: Tin HPO (thiếu Python, quá giờ, lỗi thoát, kết quả xong) MUST cũng được đẩy, vào kênh Android riêng "Hệ thống" (mức ưu tiên thường), tiêu đề dạng "HPO · xong" / "HPO · lỗi". Chạm vào chỉ mở app, không điều hướng. Owner chốt: đang phát triển app nên cần nhận cả tin hệ thống.
- **FR-002**: Gửi Telegram và gửi thông báo đẩy MUST độc lập: kênh này lỗi không chặn kênh kia, không làm hỏng hay dừng nhịp runner.
- **FR-003**: Nội dung thông báo đẩy MUST là chữ thường (không thẻ HTML như tin Telegram); tiêu đề ngắn dạng "MUA · HPG · N1" / "BÁN · HPG" / "HUỶ MUA · HPG", thân là giá + lý do.
- **FR-004**: Thông báo MUST gửi ở mức ưu tiên cao của FCM, vào kênh Android riêng "Tín hiệu V3" (có âm thanh).
- **FR-005**: Thông báo MUST mang theo mã CK để app mở đúng màn khi chạm (US2).

**Thiết bị**

- **FR-006**: App MUST xin quyền thông báo (Android 13+) sau khi đăng nhập lần đầu, giải thích ngắn vì sao cần.
- **FR-007**: Đăng nhập thành công → app MUST gửi mã thiết bị FCM lên server; mã đổi → gửi lại. Đăng xuất → app MUST gọi server gỡ mã.
- **FR-007a**: Tin đẩy MUST gửi tới mọi mã thiết bị đang đăng ký, của mọi tài khoản.
- **FR-008**: Server MUST lưu mã thiết bị gắn tài khoản; một mã chỉ thuộc một bản ghi (đăng ký lại thì cập nhật, không nhân bản).
- **FR-009**: FCM báo mã không hợp lệ → server MUST xoá mã đó.
- **FR-010**: Endpoint mới (dưới `api/v1`, cần đăng nhập): đăng ký mã, gỡ mã, gửi thông báo thử cho chính tài khoản đang đăng nhập. Hợp đồng chi tiết để ở `plan.md` / `contracts/`.

**Bảo mật cấu hình**

- **FR-011**: Khoá tài khoản dịch vụ Firebase MUST chỉ nằm trong cấu hình trên server, không vào repo, không ghi ra log.
- **FR-012**: Kênh gửi Telegram và kênh gửi FCM MUST không ghi URL/headers ra log (tắt log của HttpClient cho hai kênh này). Hệ quả: token Telegram thôi lộ trong log.
- **FR-013**: Thiếu cấu hình Firebase → kênh đẩy tự tắt (không lỗi khởi động), log cảnh báo một lần, ở lần gửi đầu tiên.

### Key Entities

- **ThietBiNhanThongBao**: một máy nhận tin của một tài khoản — tài khoản (Users), mã thiết bị FCM, nền tảng (android), thời điểm đăng ký, lần cập nhật gần nhất. Cần migration mới (bảng mới, không sửa bảng cũ).

## Đã làm rõ (owner chốt 29/10/2026)

1. **Ai nhận tin** → mọi tài khoản đã đăng nhập app (theo đề xuất của AI).
2. **Tin tổng kết khi 0 tín hiệu** → không làm trong spec này (theo đề xuất của AI).
3. **Tin HPO / ML** → **có đẩy lên app** (owner: "đang phát triển app") — xem FR-001a.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Trên máy owner, 100% tin V3 trong 5 phiên đầu sau khi ship hiện trên thanh thông báo (đối chiếu từng tin với Telegram).
- **SC-002**: Thông báo đẩy tới máy không trễ hơn tin Telegram quá 60 giây ở ≥ 95% số tin (khi máy có mạng, đã tắt tối ưu pin). *Ngưỡng 60 giây là đề xuất của AI, owner chốt.*
- **SC-003**: Sau khi ship, `journalctl -u stockradar` không còn dòng nào chứa `api.telegram.org/bot`.
- **SC-004**: Thời gian chạy mỗi nhịp V3 (log "nhịp chạy hết … ms") không tăng quá 1 giây so với trước khi có kênh đẩy. *Đề xuất của AI.*
- **SC-005**: Tắt hẳn kênh FCM (bỏ cấu hình) → Telegram và runner chạy y như trước, 0 lỗi.

## Assumptions

- Owner tự tạo dự án Firebase (miễn phí), tải `google-services.json` cho app và khoá tài khoản dịch vụ cho server; AI không đăng nhập Firebase, không xử lý khoá.
- Chỉ Android. iOS, web push, chọn lọc theo từng kịch bản, giờ im lặng: ngoài phạm vi.
- Giữ nguyên toàn bộ luật V3 (spec 012), thời điểm các nhịp, nội dung tin Telegram.
- Sau khi chặn log (FR-012), owner đổi token bot Telegram qua BotFather vì token cũ đã nằm trong log.
- Cần build và cài lại APK; bản app cũ không đăng ký thiết bị nên không nhận tin đẩy (Telegram vẫn nhận).
