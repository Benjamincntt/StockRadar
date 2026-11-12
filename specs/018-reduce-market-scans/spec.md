# Feature Specification: Giảm số lần quét giá và cách ghi nến trong phiên

**Feature Branch**: `018-reduce-market-scans` (làm trên `master` như spec 012–017)

**Created**: 2026-11-11

**Status**: Draft — owner chọn hướng "làm A rồi B, viết spec trước" (11/11/2026); owner chấp nhận đề xuất của AI cho Q1–Q5 ("Đồng ý hết", 11/11/2026); plan phần A owner duyệt 11/11/2026

**Input**: Owner (11/11/2026), sau khi đọc mô tả luồng chạy production: "đánh giá lại tổng thể kiến trúc và phương pháp của luồng. tôi thấy đang có quá nhiều lần quét". AI đánh giá và đưa ba hướng A (bớt lần quét), B (sửa cách ghi giá), C (chuyển lịch sử sang bảng). Owner: "Làm A rồi B, viết spec trước". C nằm ngoài spec này.

## Bối cảnh

Hiện trạng đo trên production ngày 11/11/2026, sau khi deploy đợt 3 spec 016 (commit b7a5bcb):

- **Cách lưu lịch sử giá.** Mỗi mã lưu toàn bộ nến ngày từ năm 2000 trong một ô JSON (`Stocks.HistoryJson`).
  - 1135 mã, tổng **332 MB**, trung bình **300 KB/mã**.
  - Các cột `LastClose`, `LastVolume`, `LastChangePercent` được cập nhật mỗi lần ghi.
- **Mỗi lần ghi giá đọc và viết lại cả lịch sử.** `EfMarketDataWriter.UpsertQuotesAsync` lặp từng mã:
  - gửi một truy vấn riêng cho mỗi mã (khoảng 958 truy vấn mỗi lần);
  - giải mã toàn bộ `HistoryJson`, sửa nến cuối, mã hoá lại rồi lưu.
- **Số lần quét trong một ngày:**

| Việc | Lịch hiện tại | Số mã | Ghi chú đo |
|---|---|---|---|
| KBS sync (`KbsMarketSyncJob`) | Mỗi 60 giây, **cả ngày lẫn đêm** (`MarketData:SyncIntervalSeconds = 60`, không chặn giờ) | ~958 | Khoảng **10 giây mỗi lần**, kể cả lúc 03:00 sáng (log 10–11/11) |
| Job 2 lặp trong phiên (`DailySessionSyncJob`, trigger `-interval`) | Mỗi 5 phút, chỉ trong giờ khớp lệnh | 1135 (958 đang theo dõi + 177 tạm ngưng) | 35–40 giây mỗi lần; ghi cùng giá với KBS sync |
| Lọc lại danh sách mã (`UniverseRescreenRunner`) | Cuối mỗi lần Job 2, tức khoảng 51 lần/ngày | 1135, đọc toàn bộ lịch sử | Khoảng 20 giây mỗi lần |
| Job 2 sau đóng cửa (trigger `-close`) | 15:05 | 1135 | Lần duy nhất được phép chạy ngoài giờ để ghi nến có ATC |
| V3 08:30 / 14:25 | Một lần mỗi nhịp | ~200 mã UNI đã lưu | Đã nhẹ, ngoài phạm vi |
| V3 trong phiên | Mỗi phút | Chỉ mã đang giữ | Gọi KBS riêng, ngoài phạm vi |
| V3 16:00 | Một lần | Toàn bộ | Cần nạp đầy đủ, ngoài phạm vi |

- **Tải trên server.**
  - Tiến trình API trung bình **~28% CPU** trong 2 giờ 45 phút sau khi khởi động lại, gồm cả cuối phiên 11/11. Server 4 GB RAM; tiến trình dùng khoảng 1 GB, RAM trống ~323 MB.
  - Phần CPU này **chưa** được tách theo từng job. Nhận định "vòng ghi giá là phần nặng nhất" là suy luận của AI từ thời gian chạy, chưa đo bằng công cụ phân tích hiệu năng.
- **Nến giả ban đêm** (suy từ code và log, chưa kiểm trong DB).
  - Sau 00:00, ngày hiện tại đã là phiên mới nhưng bảng giá KBS vẫn còn khối lượng của phiên trước.
  - Bộ ghi chỉ bỏ qua khi `Volume <= 0` và ngoài giờ. Vì vậy KBS sync ghi một nến ngày mới mang giá phiên cũ (log 03:00 ngày 11/11: `stocks=605`).
  - V3 không bị ảnh hưởng, vì luôn bỏ nến hôm nay qua `V3CotNen.NenHoanThanh`. Biểu đồ và chi tiết mã trước 09:00 có thể hiện nến không có thật.
- **Những chỗ đang đọc nến hôm nay từ `HistoryJson` hoặc các cột `Last*`.** B phải giữ cho các chỗ này vẫn có giá mới trong phiên:
  - Ảnh chụp giá (`MarketService.GetQuoteSnapshotAsync`): đọc bộ nhớ đệm giá trước; trống thì lấy nến cuối của lịch sử.
  - Chi tiết mã (`StockService.GetDetailAsync`): lịch sử và % thay đổi.
  - Biểu đồ khung ngày (`StockService`, interval `1D`): lấy nến trong DB nếu dài hơn dữ liệu KBS.
  - Độ rộng thị trường (`EfRepositories.BreadthSql`): chỉ đọc các cột `LastClose`, `LastVolume`, `LastChangePercent`.
  - Realtime xuống app: `MarketSyncService.ApplyAsync` đẩy `QuotesUpdated` và ghi bộ nhớ đệm, độc lập với cách lưu.
- **V3 không đọc nến hôm nay trong phiên.**
  - 08:30 và 14:25 bỏ nến hôm nay. Riêng 14:25 ghép giá khớp lấy trực tiếp từ KBS.
  - Trong phiên lấy giá KBS trực tiếp.
  - 16:00 đọc `HistoryJson` **sau** lần ghi 15:05. Đây là ràng buộc phải giữ.

## User Scenarios & Testing

### User Story 1 — Bớt lần quét trùng và quét ngoài giờ (A) (Priority: P1)

Server chỉ lấy bảng giá khi thị trường có giá mới. Không còn hai job cùng ghi một giá trong phiên. Danh sách mã chỉ được lọc lại một lần mỗi ngày.

**Why this priority**: chỉ đổi lịch chạy, rủi ro thấp, giảm tải ngay. Đồng thời hết nến giả ban đêm.

**Independent Test**: deploy lên production, đếm số lần chạy từng job trong log của một phiên trọn vẹn. So với 10–11/11:
- nến ngày trong `HistoryJson` của HPG, FPT, TCB, VCB, DRI khớp giá đóng cửa;
- nhịp V3 16:00 lưu chỉ số và danh sách UNI như thường.

**Acceptance Scenarios**:

1. **Given** ngoài khung giờ chạy (Q1), **When** tới giờ KBS sync, **Then** job bỏ qua, không gọi KBS, không ghi DB.
2. **Given** một ngày giao dịch, **When** liệt kê log từ 00:00 tới 08:59, **Then** không có dòng `KBS sync:` nào và `HistoryJson` không có nến ngày hôm nay trước giờ mở cửa.
3. **Given** trong giờ khớp lệnh, **When** chạy hết phiên, **Then** Job 2 không còn chạy mỗi 5 phút (Q2); lần 15:05 vẫn chạy một lần và vẫn cập nhật 177 mã tạm ngưng.
4. **Given** lần Job 2 15:05, **When** chạy xong phần ghi giá, **Then** lọc lại danh sách mã đúng một lần trong ngày.
5. **Given** cùng dữ liệu nến, **When** chạy nhịp V3 16:00 trước và sau thay đổi, **Then** chỉ số, danh sách UNI, ngưỡng bán và giá tham chiếu giống hệt nhau.

---

### User Story 2 — Ghi giá trong phiên không đụng tới lịch sử 25 năm (B) (Priority: P2)

Trong phiên, mỗi lần có giá mới server chỉ cập nhật phần "giá hôm nay" của từng mã. Không giải mã và viết lại `HistoryJson`. Nến hôm nay được gắn vào lịch sử **một lần** sau đóng cửa. App vẫn thấy giá nhảy, biểu đồ và chi tiết mã vẫn có nến hôm nay.

**Why this priority**: đây là gốc của chi phí. A chỉ giảm số lần; B giảm giá của mỗi lần. Làm sau A để có số đo nền sạch.

**Independent Test**:
- Trong phiên, so trước và sau: thời gian một lần KBS sync; số truy vấn SQL mỗi lần ghi giá; `HistoryJson` của một mã không đổi giữa 09:00 và 15:04.
- Sau 15:05, nến hôm nay có trong `HistoryJson` và khớp giá đóng cửa.

**Acceptance Scenarios**:

1. **Given** trong phiên, **When** KBS sync ghi giá 958 mã, **Then** mã nào cũng chỉ cập nhật phần giá hôm nay (Q3) trong một lượt truy vấn theo lô, không gửi một truy vấn riêng cho mỗi mã.
2. **Given** trong phiên, **When** mở chi tiết mã, biểu đồ `1D` và ảnh chụp giá, **Then** nến/giá hôm nay hiển thị đúng giá mới nhất như trước thay đổi.
3. **Given** trong phiên, **When** tính độ rộng thị trường, **Then** kết quả khớp số liệu trước thay đổi trên cùng giá.
4. **Given** 15:05, **When** Job 2 sau đóng cửa chạy, **Then** nến hôm nay (gồm ATC) được gắn vào `HistoryJson` đúng một lần, và nhịp V3 16:00 đọc được nến này.
5. **Given** server khởi động lại giữa phiên, **When** app mở chi tiết mã ngay sau đó, **Then** vẫn có giá hôm nay (đọc từ chỗ lưu bền theo Q3, không chỉ bộ nhớ đệm).

---

### Edge Cases

- **Lần 15:05 thất bại** (KBS lỗi, như 15:12–15:14 ngày 11/11). Hiện tại KBS sync chạy suốt nên vô tình ghi bù nến ATC. Sau A sẽ không còn ai ghi bù, nên cần lần thử lại (Q4).
- **Ngày nghỉ lễ / cuối tuần:** không job nào ghi giá, kể cả KBS sync.
- **Mã bị ngừng giao dịch trong ngày:** không có giá mới; không được tạo nến hôm nay giả.
- **KBS trả bảng giá rỗng hoặc HTTP 400 trong phiên:** giữ giá cũ, ghi cảnh báo, nhịp sau thử lại (như hiện tại).
- **Phiên đầu tiên sau deploy B, khi chưa có dữ liệu ở chỗ lưu mới:** các màn đọc lịch sử như cũ cho tới lần ghi đầu tiên.
- **V3 ATC 14:25:** không đổi gì; vẫn lấy giá trực tiếp KBS và ghép nến tạm.

## Requirements

### Functional Requirements

**Phần A — lịch chạy**

- **FR-001**: KBS sync PHẢI chỉ gọi KBS và ghi DB trong khung giờ theo Q1, ngày giao dịch. Ngoài khung thì bỏ qua và không ghi.
- **FR-002**: Job 2 PHẢI bỏ lần lặp trong phiên (Q2). Lần sau đóng cửa 15:05 giữ nguyên, kể cả phần cập nhật mã tạm ngưng.
- **FR-003**: Lọc lại danh sách mã PHẢI chỉ chạy sau lần ghi giá 15:05, một lần mỗi ngày giao dịch.
- **FR-004**: Bộ ghi giá PHẢI từ chối tạo nến ngày hôm nay ngoài khung giờ chạy, trừ lần sau đóng cửa. Lý do: chặn nến giả ban đêm ngay ở chỗ ghi, không chỉ dựa vào lịch.
- **FR-005**: Lần ghi nến sau đóng cửa PHẢI có cơ chế thử lại khi thất bại, xong trước 16:00 (Q4). Hết lần thử mà vẫn lỗi thì ghi log lỗi rõ tên mã thiếu.

**Phần B — cách ghi**

- **FR-006**: Ghi giá trong phiên PHẢI không đọc và không viết lại `HistoryJson`. Chỉ cập nhật phần "giá hôm nay" của mã (Q3), gồm giá mở, cao, thấp, đóng, khối lượng, % thay đổi, ngày.
- **FR-007**: Ghi giá PHẢI nạp và lưu theo lô (một lượt cho cả danh sách mã), không gửi một truy vấn riêng cho mỗi mã.
- **FR-008**: Lần Job 2 sau đóng cửa PHẢI gắn nến hôm nay vào `HistoryJson` đúng một lần (chạy lại thì thay nến cùng ngày, không nhân đôi).
- **FR-009**: Các chỗ đọc giá hôm nay (ảnh chụp giá, chi tiết mã, biểu đồ `1D`, độ rộng thị trường) PHẢI ghép "giá hôm nay" vào kết quả khi `HistoryJson` chưa có nến hôm nay, để app thấy như trước.
- **FR-010**: Đẩy realtime `QuotesUpdated` và bộ nhớ đệm giá PHẢI giữ nguyên hành vi và tần suất.

**Chung**

- **FR-011**: V3 (4 nhịp) PHẢI cho kết quả giống hệt trên cùng dữ liệu. Nhịp 16:00 PHẢI đọc được nến hôm nay đã gồm ATC.
- **FR-012**: Mỗi job PHẢI ghi log thời gian chạy và số mã đã ghi, để đo trước và sau.
- **FR-013**: Cập nhật `docs/domain/pipeline-jobs.md` và bản đồ `CLAUDE.md` trong cùng change set (hiến pháp, mục Quy trình Spec Kit & tài liệu).

### Key Entities

- **Giá hôm nay của mã**: giá mở, cao, thấp, đóng, khối lượng, % thay đổi, ngày phiên. Chỗ lưu theo Q3. Thay đổi mỗi phút trong phiên.
- **Lịch sử nến ngày (`Stocks.HistoryJson`)**: chỉ đổi mỗi ngày một lần sau đóng cửa (sau B), và khi Job 1 nạp lại.

## Success Criteria

Số nền là số đo 10–11/11/2026 ở mục Bối cảnh. Số đích chưa có owner chốt nên ghi dạng so sánh; số tuyệt đối sẽ đo và báo.

- **SC-001 (A)**: Số lần KBS sync mỗi ngày giảm từ ~1.440 xuống bằng số phút trong khung Q1 (ví dụ khung 09:00–11:30 + 13:00–15:10 là ~282 lần). Đếm trong log.
- **SC-002 (A)**: Số lần lọc lại danh sách mã: từ ~51 xuống **1** mỗi ngày.
- **SC-003 (A)**: Không có nến ngày hôm nay trong `HistoryJson` trước 09:00 (kiểm 5 mã mẫu, 2 phiên liên tiếp).
- **SC-004 (B)**: Mỗi lần ghi giá trong phiên: số truy vấn SQL từ ~958 xuống **không quá số lô** (một vài truy vấn). Thời gian một lần KBS sync giảm so với ~10 giây, báo số đo thật.
- **SC-005 (B)**: `HistoryJson` của 5 mã mẫu không đổi giữa 09:00 và 15:04; sau 15:05 có nến hôm nay khớp giá đóng cửa.
- **SC-006 (A+B)**: CPU trung bình tiến trình API trong một phiên trọn vẹn giảm so với nền ~28%. Báo số đo, không đặt ngưỡng khi owner chưa chốt.
- **SC-007**: Nhịp V3 16:00 hai phiên liên tiếp sau thay đổi lưu chỉ số và danh sách UNI, cập nhật vị thế không lỗi; bộ test tự động hiện có (517 pass / 1 skip) vẫn qua.

## Đã làm rõ (owner, 11/11/2026)

Owner trả lời "Đồng ý hết", tức chấp nhận đề xuất của AI cho cả năm câu dưới đây:

- **Q1**: KBS sync chạy 08:59–11:31 và 12:59–15:10, chỉ ngày giao dịch.
- **Q2**: bỏ hẳn Job 2 lặp trong phiên; chỉ giữ lần 15:05.
- **Q3**: lưu "giá hôm nay" bằng cột mới trên bảng `Stocks` (migration chỉ thêm cột).
- **Q4**: thử lại lần ghi sau đóng cửa lúc 15:15 và 15:30, chỉ cho mã còn thiếu nến hôm nay.
- **Q5**: A là một change set riêng; deploy và đo 2 phiên rồi mới làm B; bắt đầu sau khi T038 (spec 016) đánh dấu xong.

## Câu hỏi đã hỏi owner (đề xuất của AI đi kèm)

- **Q1 — Khung giờ KBS sync.**
  - Đề xuất của AI: **08:59–11:31 và 12:59–15:10**, ngày giao dịch. Lý do: bắt kịp ATO, nghỉ trưa không có giá mới, và 15:10 đủ để bảng giá có giá ATC (14:45) phục vụ màn hình.
  - Phương án khác: chỉ dùng `IsMarketOpen` (09:00–11:30, 13:00–14:45). Khi đó app sẽ không thấy giá ATC cho tới 15:05.
- **Q2 — Job 2 lặp trong phiên.**
  - Đề xuất của AI: **bỏ hẳn**. KBS sync đã ghi đủ 958 mã đang theo dõi; 177 mã tạm ngưng chỉ cần một lần/ngày ở 15:05.
  - Phương án khác: giữ nhưng giãn ra 30 phút.
- **Q3 — Chỗ lưu "giá hôm nay" cho B.**
  - Đề xuất của AI: **thêm cột trên bảng `Stocks`** (giá mở, cao, thấp, ngày phiên, cạnh `LastClose`/`LastVolume`/`LastChangePercent` đã có). Cần một migration thêm cột, không xoá gì. Lý do: bền khi khởi động lại; độ rộng thị trường đã đọc từ cột.
  - Phương án khác: chỉ giữ trong bộ nhớ đệm. Không cần migration, nhưng mất khi khởi động lại và cần thêm đường đọc dự phòng.
- **Q4 — Thử lại lần ghi sau đóng cửa.**
  - Đề xuất của AI: **thêm hai lần thử ở 15:15 và 15:30**. Mỗi lần chỉ ghi các mã còn thiếu nến hôm nay. Xong trước nhịp V3 16:00.
- **Q5 — Thứ tự giao.**
  - Đề xuất của AI: **giao A thành một change set riêng**, deploy và đo 2 phiên rồi mới làm B. Lý do: có số đo nền sạch và tách rủi ro.
  - Không làm trong lúc đang theo dõi T038 (spec 016). Bắt đầu sau khi T038 đánh dấu xong.

## Ngoài phạm vi

- **Hướng C** (chuyển lịch sử sang bảng mỗi ngày một dòng): cần spec riêng.
- **V3 trong phiên và V3 ATC** gọi KBS riêng: số mã nhỏ, giữ để giá bán realtime luôn mới nhất.
- **Job 1** đang tắt trên production (`MarketJobs:History:Enabled = false`), nên quét lại hằng tuần không chạy. Ghi nhận, không xử lý ở đây.
- **Nến phút** (spec 017).

## Assumptions

- Chỉ có một tiến trình API trên production (`stockradar`), không chạy song song nhiều bản.
- KBS cập nhật giá ATC lên bảng giá trong vài phút sau 14:45 (đã thấy bảng giá có giá ATC lúc 15:05 các ngày 09–11/11).
- Không có client nào ngoài app và web đọc trực tiếp `HistoryJson` trong phiên.
- Số đo nền lấy từ log production 09–11/11/2026 và truy vấn chỉ đọc trên DB production; chưa đo bằng công cụ phân tích hiệu năng.
