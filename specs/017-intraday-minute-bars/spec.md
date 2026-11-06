# Feature Specification: Lưu nến phút cho backtest và huấn luyện

**Feature Branch**: `017-intraday-minute-bars` (làm trên `master` như spec 012–016)

**Created**: 2026-11-06

**Status**: Draft — owner chốt 06/11/2026 (mục "Đã làm rõ"); chờ lập plan

**Input**: Owner (06/11/2026): "có lẽ dữ liệu nến phút nên được cập nhật từ 2023 đến nay và về sau để dễ cho việc traning cũng như backtest". Sau khi AI đo KBS và FireAnt (không nguồn miễn phí nào có nến phút từ 2023), AI đưa các hướng; owner chọn: "Lưu nến phút từ nay về sau, cộng phần KBS còn giữ".

## Bối cảnh

- DB chỉ lưu nến ngày (`Stocks.HistoryJson`). Nến trong phiên trên app gọi thẳng KBS mỗi lần mở (`KbsHistoryClient`, cache 45 giây), không lưu.
- Vì chỉ có nến ngày, backtest không biết trong phiên giá chạm mức nào trước:
  - PH-24 và PH-32 (cắt lỗ) phải tính 3 cách (trung tính / xấu / tốt) và kết luận có độ bất định.
  - PH-24: riêng cách xử lý một phiên đã làm lệch tới 46 điểm lãi.
  - Luật bán V3 chạy realtime trên giá khớp (`V3LuatBanThucTe`), nhưng hiện không đo được chính xác.
- Đo độ sâu nguồn (06/11/2026, ghi ở Q7 `docs/features/v3-scenario-engine/research/05-quyet-dinh-va-cau-hoi-mo.md`):
  - **KBS** (`data_1P/5P/15P/60P`):
    - Không mã nào có nến trong phiên trước 05/2026.
    - HPG có từ tuần 04/05/2026. FPT, VCB, SSI, MWG, DGC có từ khoảng 09/2026.
    - Có mã thiếu nến: DGC chỉ 85 nến phút/tuần, đủ phải ~1.130.
  - **FireAnt** (SignalR `tradestation.fireant.vn/quote-lite`, `GetBars`, token khách):
    - FPT 1 phút có từ ≤ 01/2026; 09/2025 trở về trước rỗng.
    - Mới đo 2 mã.
    - Nguồn này dùng token khách lấy từ trang web, nên **không** dùng cho tải hàng loạt lâu dài (xem Q3).
- Hai nguồn có thể đang xoá dần dữ liệu cũ (chưa kiểm). Tải phần còn lại càng sớm càng ít mất.

## User Scenarios & Testing

### User Story 1 — Tải một lần phần nến phút KBS còn giữ (Priority: P1)

Lần đầu bật, hệ thống tải hết nến 1 phút KBS còn giữ cho các mã thuộc phạm vi lưu (Q1) và ghi vào bảng nến phút. Lần chạy lại không tạo dòng trùng.

**Why this priority**: dữ liệu cũ có thể mất dần; đây là phần duy nhất lấy lại được.

**Independent Test**: chạy lệnh tải một lần trên dev cho 3 mã. Số nến mỗi phiên khớp với gọi KBS trực tiếp. Chạy lại lần hai không đổi số dòng.

**Acceptance Scenarios**:

1. **Given** mã có nến KBS từ 04/05/2026, **When** chạy tải một lần, **Then** bảng có nến của mọi phiên từ 04/05/2026 tới phiên gần nhất. Phiên nào KBS rỗng thì được ghi vào sổ chất lượng (FR-006), không bỏ im lặng.
2. **Given** đã tải xong, **When** chạy lại, **Then** không có dòng trùng (khoá mã + ngày phiên).

### User Story 2 — Lưu nến phút mỗi ngày từ nay về sau (Priority: P1)

Sau mỗi phiên, một job tải nến 1 phút của phiên vừa xong cho các mã thuộc phạm vi lưu và ghi vào bảng. Job không ảnh hưởng các nhịp V3.

**Why this priority**: từ nay về sau đây là nguồn chính; mỗi ngày bỏ lỡ là mất hẳn nếu nguồn chỉ giữ ngắn hạn.

**Independent Test**: 3 phiên liên tiếp trên production:
- Mỗi mã có khoảng 226 nến/phiên.
- 10 mã ngẫu nhiên có nến 14:45 khớp giá trên bảng giá.
- Nhịp V3 16:00 chạy và log thời gian như trước.

**Acceptance Scenarios**:

1. **Given** phiên giao dịch kết thúc, **When** tới 17:00, **Then** bảng có một dòng cho mỗi mã active của phiên đó, và log ghi số mã, số nến, số mã thiếu, thời gian chạy.
2. **Given** ngày nghỉ hoặc lễ, **When** tới giờ job, **Then** job bỏ qua và không ghi gì.
3. **Given** KBS lỗi hoặc trả rỗng cho một mã, **When** job chạy, **Then** job thử lại theo giới hạn, ghi mã thiếu vào sổ chất lượng, chạy tiếp các mã khác. Lần chạy sau (hoặc lệnh chạy bù) tải bù được.

### User Story 3 — Xuất nến phút cho sổ nghiên cứu (Priority: P2)

Có script chỉ đọc (theo kiểu `research/scripts/export_prod.sh`) để xuất nến phút của một khoảng ngày ra file nén, dùng cho backtest bằng Python.

**Independent Test**: xuất 1 tuần cho 5 mã. File nạp được bằng Python, số dòng khớp `COUNT(*)` trên DB.

### Edge Cases

- **Phiên chưa xong:** nến cuối có thể chưa chốt. Job chỉ tải phiên đã đóng cửa. Không tải ngày hôm nay trong lúc tải một lần.
- **Giờ:** KBS trả giờ VN, FireAnt trả UTC (`02:15Z` = 09:15 VN). Bảng lưu **một** quy ước (FR-002), ghi rõ.
- **Đơn vị giá:** lưu cùng đơn vị với `HistoryJson` (nghìn đồng, như `KbsHistoryClient` đang đổi). Lưu giá **thô**, chưa điều chỉnh theo quyền. Điều chỉnh làm lúc dùng, theo spec 015 (`BoDieuChinhGiaTheoQuyen`).
- **Mã đổi sàn, huỷ niêm yết, mã mới vào universe:** chỉ lưu từ ngày mã thuộc phạm vi lưu; dữ liệu đã lưu không xoá.
- **Phiên có ATC:** nến 14:45 là phiên ATC (một lần khớp). Ghi như nguồn trả, không gộp.
- **Không dồn tải lên KBS:** gọi tuần tự, có giãn cách, giới hạn số lần thử lại. Không chạy trùng giờ các nhịp V3 hoặc `KbsMarketSyncJob`.

## Requirements

### Functional Requirements

- **FR-001**: Hệ thống MUST có bảng nến phút riêng, không đụng `Stocks.HistoryJson`. **Mỗi dòng là một mã trong một phiên**: mã, ngày phiên, số nến, nguồn (`KBS`), và cột nhị phân chứa toàn bộ nến 1 phút của phiên (thời điểm, O/H/L/C, khối lượng) **nén gzip**. Khoá duy nhất là mã + ngày phiên; ghi lại cùng phiên thì thay dòng cũ.
- **FR-002**: Thời điểm MUST lưu theo một quy ước thống nhất cho mọi nguồn. Đề xuất: giờ VN, kiểu không múi giờ, đúng như bảng giá hiển thị.
- **FR-003**: Hệ thống MUST có lệnh tải một lần (endpoint `market/jobs/...` như `v3-nap-chi-so`) để tải hết nến 1 phút KBS còn giữ cho phạm vi lưu. Chạy lại không tạo trùng.
- **FR-004**: Hệ thống MUST có job Quartz chạy mỗi ngày giao dịch sau đóng cửa (giờ theo Q4). Job tải nến 1 phút của phiên vừa xong cho phạm vi lưu. Job bật/tắt và đặt giờ được bằng cấu hình.
- **FR-005**: Job MUST dùng lại `KbsHistoryClient` (thêm hàm lấy theo khoảng ngày cho khung phút, không cache). Không thêm nguồn mới trong spec này.
- **FR-006**: Mỗi lần chạy MUST ghi log tổng (số mã, số nến, số mã thiếu, thời gian chạy) và sổ chất lượng theo mã + phiên: số nến nhận được, cờ thiếu khi số nến dưới ngưỡng (Q5).
- **FR-007**: Job MUST không làm chậm nhịp V3. Chạy lúc 17:00, sau nhịp 16:00 (Q4); gọi tuần tự có giãn cách.
- **FR-008**: Có script xuất chỉ đọc cho sổ nghiên cứu (US3). Cập nhật `docs/features/v3-scenario-engine/research/README.md` (cách lấy dữ liệu) và Q7.
- **FR-009**: Spec này không đổi luật mua/bán, cổng hay điểm của V3, và không cho V3 đọc bảng nến phút lúc chạy. Dùng nến phút vào luật là spec riêng, đo trước rồi quyết.
- **FR-010**: Tài liệu cập nhật cùng change set: `docs/domain/pipeline-jobs.md` (job mới), `docs/entity_model.md` (bảng mới), `CLAUDE.md` (đoạn Pipeline).

### Key Entities

- **Nến phút** (bảng mới, ví dụ `NenPhutTheoPhien`): mã, ngày phiên, số nến, nguồn, khối nến nén. Khoảng 963 dòng/ngày, ~240 nghìn dòng/năm.
- **Sổ chất lượng nến phút** (bảng mới hoặc gộp vào log, theo plan): mã, phiên, số nến, cờ thiếu, lần tải.

## Success Criteria

- **SC-001**: Sau tải một lần, mỗi mã trong phạm vi lưu có nến từ ngày sớm nhất KBS còn giữ cho mã đó. Báo cáo ghi ngày sớm nhất theo từng mã.
- **SC-002**: 10 phiên liên tiếp sau deploy: ≥ 95% số mã trong phạm vi lưu có đủ nến (≥ ngưỡng Q5); mã thiếu nằm trong sổ chất lượng.
- **SC-003**: Thời gian chạy và lỗi của nhịp V3 16:00 không đổi so với 5 phiên trước deploy (so log).
- **SC-004**: Sổ nghiên cứu nạp được nến phút bằng script xuất và chạy lại được một phép kiểm PH-32 trên đoạn có nến phút.

## Đã làm rõ (owner, 06/11/2026)

Owner hỏi lưu 15/30 phút có được không, rồi nói "máy chục triệu dòng 1 năm thì hơi quá đáng". AI đưa cách A (15 phút, mỗi nến một dòng) và cách B (1 phút, mỗi mã mỗi phiên một dòng). Owner hỏi "việc đọc ghi nhiều có làm chết app không"; AI đo production (chỉ đọc): 963 mã active, `HistoryJson` trung bình 299 KB, DB ~1,3 GB, ổ đĩa còn 14 GB. Owner: "oke xong việc đi" → theo đề xuất của AI:

- **Q1**: lưu **toàn bộ mã active** (963 mã lúc đo).
- **Q2**: **1 phút**, mỗi mã mỗi phiên **một dòng, nén gzip** (FR-001), giữ vĩnh viễn. Ước lượng tay chưa đo: 4–6 KB/dòng, ~1–1,5 GB/năm. Plan MUST đo kích thước nén thật trên dev và kiểm ổ đĩa trước khi deploy.
- **Q3**: không dùng FireAnt.
- **Q4**: chạy **17:00** ngày giao dịch (sau nhịp V3 16:00). Ước lượng 5–15 phút cho 963 lần gọi tuần tự; log thời gian chạy. Tải một lần ban đầu chạy ngoài giờ giao dịch.
- **Q5**: dưới **200** nến/phiên thì đánh dấu thiếu, chỉ để theo dõi.
- Ghi nhận ngoài phạm vi: `KbsMarketSyncJob` mỗi 60 giây ghi lại toàn bộ `HistoryJson` của từng mã (`EfMarketDataWriter.UpsertQuotesAsync`). Đây là tải nặng sẵn có, không thuộc spec này.

## Câu hỏi đã hỏi owner (đề xuất của AI đi kèm)

- **Q1 — Lưu mã nào?**
  - Đề xuất: **toàn bộ mã đang active trong universe** (Job 1 lọc giá > 8.000đ). Lý do: danh sách `UNI` của V3 (~200 mã) đổi hằng ngày, mà backtest cần cả mã nằm ngoài `UNI` của hôm nay.
  - Phương án nhẹ: chỉ `UNI` của V3 cộng mã đang giữ.
  - Chưa đếm số mã active trên production; số này quyết định dung lượng ở Q2.
- **Q2 — Khung nào, giữ bao lâu?**
  - Đề xuất: **chỉ lưu 1 phút** (gộp ra 5, 15, 60 phút được), **giữ vĩnh viễn**.
  - Ước lượng: ~226 nến/mã/phiên. Với 400 mã thì ~90 nghìn dòng/ngày, ~23 triệu dòng/năm. Dung lượng cỡ vài GB/năm tuỳ cách lưu và chỉ mục; chưa đo, plan sẽ đo trên dev.
  - Nếu lo dung lượng: lưu 5 phút thì giảm 5 lần, nhưng mất độ chính xác khi đo chạm mức.
- **Q3 — Có tải thêm phần FireAnt còn giữ không?**
  - FireAnt sâu hơn KBS (FPT có từ ≤ 01/2026 so với 09/2026). Nhưng nguồn này dùng token khách lấy từ trang web, tải hàng loạt có thể trái điều khoản.
  - Đề xuất: **không** đưa vào spec này. Nếu owner muốn phần sâu hơn thì mua gói API chính thức của FireAnt rồi làm spec riêng.
- **Q4 — Giờ chạy job?**
  - Đề xuất: **15:30** ngày giao dịch, sau trigger Job 2 sau đóng cửa (15:05, spec 016 D1) và trước nhịp V3 16:00. Nếu đo thấy job chạy quá 25 phút thì dời sang **17:00**, sau V3.
- **Q5 — Ngưỡng "thiếu nến"?**
  - Đề xuất: dưới **200** nến phút/phiên (đủ ~226) thì đánh dấu thiếu.
  - Mã ít thanh khoản có phút không khớp nên ít nến là bình thường. Vì vậy sổ chất lượng chỉ để biết, không tải lại liên tục.

## Assumptions

- KBS tiếp tục trả `data_1P` theo khoảng ngày như hiện nay (`KbsHistoryClient` đang dùng cho biểu đồ app).
- Lưu nến phút là việc thu thập dữ liệu, không đổi hành vi người dùng thấy. Không có màn hình mới.
- Spec 016 D1 (trigger Job 2 sau đóng cửa) làm trước hoặc độc lập. Spec này không phụ thuộc phần gỡ V1.
