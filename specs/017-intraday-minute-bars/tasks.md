# Tasks: Lưu nến phút (spec 017)

**Input**: [spec.md](./spec.md) · [plan.md](./plan.md)
**Ngày lập**: 2026-11-13 · **Trạng thái**: chờ owner duyệt plan
**Baseline**: 537 test pass / 0 fail (12/11/2026)

## Luật bắt buộc cho mọi task

- **Không** dùng `FetchAsync` cho job hay lệnh nạp: hàm đó cache 45 giây và cắt còn 300 nến (`KbsHistoryClient.cs:43,116`).
- **Không** đổi giờ sang UTC/epoch: chỉ `bar.Time.DateTime` (`ParseTime` dùng `AssumeLocal`, `KbsHistoryClient.cs:156`).
- **Không** coi danh sách rỗng là "KBS không có nến": HTTP lỗi, thiếu key và exception đều trả `[]` (`dòng 96-123`).
- **Không** gán cứng giờ nến ATC. Nến ATC đã thấy ở **14:40** hoặc **14:45** tuỳ phiên; nhận theo vị trí ("nến cuối phiên, sau khoảng trống từ 14:30"). Test phải có **cả hai** ca.
- **Không** gọi KBS bằng curl/Python/trình duyệt — mọi phép đo đi qua `KbsHistoryClient` trên API chạy local.
- Mỗi nến **không** một dòng; mỗi mã mỗi phiên **một dòng** (Q2).
- Không đưa FireAnt vào phương án nào (Q3).
- Comment code mới: chỉ nhãn `DO-NOT-CHANGE:`, `SECURITY:`, `LIMIT:`, `BUSINESS-RULE:` (tối đa 2 dòng).

---

## Phase 1 — Nền dùng chung (chặn mọi user story)

- [ ] T001 Thêm 2 entity `NenPhutTheoPhienEntity` (Symbol, NgayPhien, SoNen, Nguon, DuLieuNen `varbinary(max)`, ThoiDiemGhi) và `SoChatLuongNenPhutEntity` (Symbol, NgayPhien, SoNenNhanDuoc, TrangThai, GioNenAtc, SoLanThu, ThongDiepLoi, LanGhi, ThoiDiemGhi) trong `backend/StockRadar.Domain/Entities/NenPhutEntities.cs`; thêm 2 `DbSet` + cấu hình (unique `(Symbol, NgayPhien)`, index `(NgayPhien)`, độ dài chuỗi) trong `backend/StockRadar.Infrastructure/Persistence/ApplicationDbContext.cs` (mẫu `ChiSoNgayV3` dòng 198-209).
- [ ] T002 [P] `NenPhutCodec` trong `backend/StockRadar.Infrastructure/MarketData/NenPhutCodec.cs`: nén/giải nén JSON mảng lồng `[p,o,h,l,c,v]` với `p` = phút kể từ 00:00 giờ VN; `TimNenAtc(bars)` nhận nến cuối phiên khi `p ≥ 879` và nến liền trước `p ≤ 869`; `PhutTrongNgay(DateTimeOffset)` chỉ đọc `.DateTime`.
- [ ] T003 [P] Test `backend/StockRadar.Tests/MarketData/NenPhutCodecTests.cs`: ① nén → giải nén ra đúng 226 nến (O/H/L/C/KL khớp); ② `TimNenAtc` nhận **cả** nến cuối 14:40 **và** 14:45, không gộp, không bỏ; ③ phiên không có ATC (nến cuối 14:29) → `null`; ④ nến 14:29 lưu ra đúng 14:29 (không 07:29, không 21:29) khi máy chạy UTC.
- [ ] T004 Thêm `KbsKetQuaTai` (Bars + TrangThai `Ok/Rong/HttpError/ThieuKey/Loi` + Loi) và `FetchMinuteRangeAsync`, tách `FetchRangeCoreAsync` **giữ nguyên hành vi** `FetchAsync`/`FetchRangeAsync` (vẫn `TakeLast`, vẫn trả `[]` khi lỗi) trong `backend/StockRadar.Infrastructure/MarketData/KbsHistoryClient.cs`; không cache, cảnh báo khi chạm `MaxNenMoiLanGoi`.
- [ ] T005 [P] Test `backend/StockRadar.Tests/MarketData/NenPhutKbsClientTests.cs` (HttpMessageHandler giả, mẫu `Tests/Scheduling/DailySessionSyncRetryTests.cs:33`): ① 500 nến trả về **đủ 500**, không mất nến; ② HTTP 500 → `HttpError`, JSON rỗng → `Rong` (hai trạng thái khác nhau); ③ thiếu key `data_1P` → `ThieuKey`; ④ `FetchAsync` cũ **vẫn** cắt còn 300 nến (không hồi quy biểu đồ app).
- [ ] T006 `INenPhutRepository` (trong `backend/StockRadar.Application/Abstractions/IRepositories.cs`) + `EfNenPhutRepository` (`backend/StockRadar.Infrastructure/Persistence/Repositories/EfNenPhutRepository.cs`): upsert bảng nến phút + sổ chất lượng, đọc dòng theo mã + phiên, đếm/đọc trạng thái.
- [ ] T007 [P] Test `backend/StockRadar.Tests/MarketData/NenPhutRepositoryTests.cs` (EF InMemory): ① ghi hai lần cùng mã + phiên → **1 dòng**, khối nến là bản mới; ② 226 nến → `TrangThai = Du`; ③ 120 nến → `Thieu` (ngưỡng 200); ④ KBS rỗng → `Rong` không có khối nến; ⑤ lỗi HTTP → `Loi` + `ThongDiepLoi`.
- [ ] T008 `NenPhutJobOptions` (Enabled, Hour=17, Minute=0, DelayBetweenSymbolsMs=200, SoLanThuLai=2, ChoGiuaLanThuMs=2000, NguongThieuNen=200, MaxNenMoiLanGoi=5000, SoNgayMoiLan=7, TuNgayMacDinh) gắn vào `MarketJobsOptions`; đăng ký DI (state, runner, repo); thêm `QuartzJobIds.NenPhutSync = "nen-phut-sync"` và entry `JobCatalog` (TriggerEndpoint `nen-phut`).

**Checkpoint**: build + test sạch, chưa có job nào chạy theo lịch.

---

## Phase 2 — US2 (P1): job 17:00 mỗi ngày giao dịch

- [ ] T009 `NenPhutSyncDecision.Decide(homNay, laNgayGiaoDich)` trong `backend/StockRadar.Infrastructure/MarketData/NenPhutSyncDecision.cs` (hàm thuần, mẫu `DailySessionSyncDecision`); test `backend/StockRadar.Tests/Scheduling/NenPhutSyncDecisionTests.cs`: ngày lễ (31/08/2026 — có trong `TradingSessionMath.VietnamHolidays`) → không chạy; thứ Bảy/Chủ nhật → không chạy; thứ Năm thường → chạy với `NgayPhien = hôm nay`.
- [ ] T010 `NenPhutSyncRunner` + `INenPhutSyncService` trong `backend/StockRadar.Infrastructure/MarketData/` (và `Application/Abstractions/INenPhutService.cs`): lấy `GetActiveSymbolsAsync`, bỏ `VNINDEX`; bỏ qua mã đã `Du` (trừ `taiLai`); gọi `FetchMinuteRangeAsync` cho đúng phiên; thử lại tối đa `SoLanThuLai` **chỉ** khi `HttpError`/`Loi`; `Task.Delay(DelayBetweenSymbolsMs)`; log tổng cuối job (số mã, số nến, số mã thiếu, số mã lỗi, thời gian chạy).
- [ ] T011 Test `backend/StockRadar.Tests/MarketData/NenPhutSyncRunnerTests.cs` (InMemory + handler giả): 3 mã — một đủ 226 nến, một lỗi HTTP, một chỉ 74 nến → chạy **tiếp** sau mã lỗi, sổ chất lượng có `Du`/`Loi`/`Thieu`; chạy lại lần hai không thêm dòng và không gọi KBS cho mã đã `Du`.
- [ ] T012 `NenPhutSyncJob` (`backend/StockRadar.Infrastructure/Scheduling/Jobs/NenPhutSyncJob.cs`) + `ConfigureNenPhutJob` trong `QuartzSchedulingExtensions.cs` dùng `BuildWeekdayCron(cfg.Hour, cfg.Minute)` + `VietnamTimeZone` (mặc định **17:00** T2–T6, sau nhịp V3 16:00); thêm `POST api/v1/market/jobs/nen-phut?ngayPhien=&taiLai=` trong `MarketJobsController.cs` qua `jobStatus.TrackAsync` và `IsAuthorized`.

**Checkpoint**: US2 chạy được độc lập; SC-002/SC-003 đo ở Phase 5.

---

## Phase 3 — US1 (P1): nạp một lần phần KBS còn giữ

- [ ] T013 `NenPhutNapTrangThai` (singleton, lock — mẫu `HistoryBackfillState`) + `NenPhutNapMotLanRunner`: chia khoảng `SoNgayMoiLan` ngày trên các phiên giao dịch, kẹp `denNgay ≤ PreviousTradingDay(hôm nay)` (**không tải ngày hôm nay**), lọc `chiSoMa`, `taiLai=false` thì bỏ mã + phiên đã `Du`; `LanGhi = NapMotLan`/`ChayBu`.
- [ ] T014 Endpoint `POST api/v1/market/jobs/nen-phut-nap` (chạy nền, `202` + trạng thái, `409` nếu đang chạy) và `GET api/v1/market/jobs/nen-phut-nap/status` trong `MarketJobsController.cs`; DTO trạng thái ở `backend/StockRadar.Application/DTOs/NenPhutDtos.cs`. Chạy bù một phiên **dùng chung** endpoint với `tuNgay=denNgay=<ngày>&taiLai=true`.
- [ ] T015 **[OWNER]** Đo trước deploy trên **dev** qua API local (không gọi KBS ngoài app): ① giới hạn khoảng ngày và số nến mỗi lần gọi `data_1P` (thử 7/30/90 ngày); ② kích thước nén thật của ~20 mã (cả thanh khoản cao và thấp) → ngoại suy dung lượng/năm so với ổ trống **đo lại**; ③ thời gian cho 963 lần gọi. Ghi kết quả vào mục "Số đo" bên dưới rồi chốt `SoNgayMoiLan`.
- [ ] T016 Migration `AddNenPhut` trong `backend/StockRadar.Infrastructure/Migrations/` (thư mục đang có `ApplicationDbContextModelSnapshot.cs`): AI sinh bằng `dotnet ef migrations add`, **đọc lại toàn bộ body** trước khi đưa; `Down` drop 2 bảng. **[OWNER]** áp lên production.
- [ ] T017 **[OWNER]** Chạy nạp một lần trên production **ngoài giờ giao dịch** (`POST market/jobs/nen-phut-nap`, theo dõi `status`), rồi báo cáo ngày sớm nhất theo từng mã (SC-001) và `COUNT(*)` / dung lượng bảng thực tế.

**Checkpoint**: US1 xong; dữ liệu cũ đã vào bảng, chạy lại không tạo trùng.

---

## Phase 4 — US3 (P2): xuất nến phút cho sổ nghiên cứu

- [ ] T018 `docs/features/v3-scenario-engine/research/scripts/export_nen_phut.sh`: chỉ đọc, `sqlcmd` xuất `Symbol|NgayPhien|hex(DuLieuNen)` rồi `gzip` (khuôn `export_prod.sh`, đọc chuỗi kết nối từ `appsettings.Production.json`, **không in mật khẩu**).
- [ ] T019 `docs/features/v3-scenario-engine/research/scripts/load_nen_phut.py`: hex → `gzip.decompress` → `json.loads` → DataFrame; in số dòng theo mã/phiên để đối chiếu `COUNT(*)`; chạy thử trên dữ liệu dev (SC-004).

---

## Phase 5 — Tài liệu và theo dõi

- [ ] T020 `docs/domain/pipeline-jobs.md`: thêm dòng job **17:00** `nen-phut-sync` vào bảng "Luật as-is", mục "Nến phút" (nguồn KBS, một dòng/mã/phiên, sổ chất lượng, 2 endpoint), cập nhật bảng nguồn đối chiếu (FR-004, FR-010).
- [ ] T021 `docs/entity_model.md`: thêm `NEN_PHUT_THEO_PHIEN`, `SO_CHAT_LUONG_NEN_PHUT` (cột + quan hệ mềm theo `Symbol`) (FR-010).
- [ ] T022 `CLAUDE.md` đoạn **Pipeline**: thêm một dòng 17:00 nến phút — nêu rõ **chỉ thu thập, V3 không đọc** (FR-009, FR-010).
- [ ] T023 `docs/features/v3-scenario-engine/research/README.md` (mục "Dữ liệu nến phút": cách xuất, cách nạp) + bổ sung Q7 trong `05-quyet-dinh-va-cau-hoi-mo.md` (FR-008).
- [ ] T024 **[OWNER]** Theo dõi 10 phiên sau deploy: SC-002 (≥ 95% mã active có `TrangThai = Du`) đọc từ `SoChatLuongNenPhut`; SC-003 so log nhịp V3 16:00 với 5 phiên trước deploy; kiểm `MarketJobs:NenPhut:Enabled = false` vẫn dừng job (đường lùi lại).

---

## Số đo (điền ở T015, trước khi áp migration)

| Phép đo | Kết quả | Ngày | Ghi chú |
|---|---|---|---|
| Khoảng ngày tối đa mỗi lần gọi `data_1P` | *chưa đo* | | thử 7 / 30 / 90 ngày |
| Giới hạn số nến mỗi lần trả | *chưa đo* | | xác nhận `MaxNenMoiLanGoi` |
| Kích thước nén / dòng (20 mã) | *chưa đo* | | ước lượng tay 4–6 KB, chưa đo |
| Ngoại suy dung lượng / năm | *chưa đo* | | so với ổ trống đo lại (06/11: còn 14 GB) |
| Thời gian 963 lần gọi tuần tự | *chưa đo* | | chốt `DelayBetweenSymbolsMs` |

## Đối chiếu FR/SC ⇔ task

| Yêu cầu | Task |
|---|---|
| FR-001 bảng nến phút, một dòng/mã/phiên, nén gzip, khoá duy nhất | T001, T002, T006, T007 |
| FR-002 một quy ước giờ (VN, không múi giờ) | T002, T003, T005 |
| FR-003 lệnh nạp một lần, chạy lại không trùng | T013, T014, T017 |
| FR-004 job Quartz sau đóng cửa, bật/tắt + giờ bằng cấu hình | T008, T009, T010, T012 |
| FR-005 dùng lại `KbsHistoryClient`, hàm theo khoảng ngày, không cache | T004, T005 |
| FR-006 log tổng + sổ chất lượng theo mã + phiên, cờ thiếu (Q5) | T001, T006, T007, T010 |
| FR-007 không làm chậm nhịp V3 (17:00, tuần tự, giãn cách) | T008, T010, T012 |
| FR-008 script xuất chỉ đọc + cập nhật README/Q7 | T018, T019, T023 |
| FR-009 không đổi luật V3, V3 không đọc bảng này | T010 (không chạm code V3), T022, T024 |
| FR-010 tài liệu cùng change set | T020, T021, T022 |
| SC-001 có nến từ ngày sớm nhất KBS còn giữ, báo theo mã | T017 |
| SC-002 ≥ 95% mã đủ nến trong 10 phiên | T007 (cờ thiếu), T024 |
| SC-003 nhịp V3 16:00 không đổi | T012 (17:00 tách nhịp), T024 |
| SC-004 sổ nghiên cứu nạp được và chạy lại được một phép kiểm | T018, T019 |

## Ghi chú

- Task **[OWNER]** cần owner quyết hoặc chạy: T015 (đo trên dev), T016 (áp migration production), T017 (nạp một lần production), T024 (theo dõi 10 phiên).
- Test viết trước phần cài đặt tương ứng và phải đỏ trước khi xanh.
- Không task nào chạm `mobile/`, `frontend/`, `scripts/` ops, hay đổi luật mua/bán V3.
