# Implementation Plan: Lưu nến phút (spec 017)

**Spec**: [spec.md](./spec.md) · **Created**: 2026-11-13 · **Status**: Plan đã lập 13/11/2026, chờ owner duyệt → [tasks.md](./tasks.md)

## Summary

Thu nến 1 phút của mỗi phiên vào **một dòng/mã/phiên** (`NenPhutTheoPhien`), khối nến nén gzip trong cột `varbinary(max)`, khoá duy nhất mã + ngày phiên. Hai đường ghi dùng chung một hàm KBS mới (`KbsHistoryClient.FetchMinuteRangeAsync`): job Quartz **17:00** T2–T6 (US2) và lệnh nạp một lần có trạng thái (US1). Sổ chất lượng riêng (`SoChatLuongNenPhut`) phân biệt rỗng / lỗi / ít nến và ghi lại giờ nến ATC từng phiên. Không đổi luật V3, V3 không đọc bảng này (FR-009).

## Kiểm lại trước khi lập plan (đọc code 13/11/2026)

| Điểm cần kiểm | Kết quả kiểm (file:dòng) | Hệ quả |
|---|---|---|
| Nến bị cắt khi lấy | `KbsHistoryClient.FetchRangeAsync` `backend/StockRadar.Infrastructure/MarketData/KbsHistoryClient.cs:80-124`, `.TakeLast(maxBars)` **dòng 116**; `FetchAsync` **dòng 43** với `"1m"` truyền `300` (`DefaultBarCount` **dòng 32**, `GetDateRange` → 7 ngày **dòng 199-214**). Đo 10/11 và 13/11: phiên cách 2 ngày chỉ còn 74 nến từ 13:17. | Hàm mới **không** `TakeLast`; cảnh báo khi số nến chạm ngưỡng cấu hình. Job/lệnh nạp **không** gọi `FetchAsync`. |
| Lỗi biến thành danh sách rỗng | HTTP lỗi **dòng 96-100**, thiếu key `data_1P` **dòng 105-109**, exception **dòng 119-123** → đều `return []` + `LogWarning`. | Kiểu trả mới mang trạng thái (`Ok`/`HttpError`/`ThieuKey`/`Loi`/`Rong`) để sổ chất lượng ghi đúng lý do. |
| Giờ nến | `ParseTime` **dòng 147-166**, `DateTimeStyles.AssumeLocal` **dòng 156**; server production chạy `Etc/UTC`. Giờ treo tường VN mang nhãn `+00:00`. | Chỉ đọc `.DateTime` để lấy giờ VN. Cấm `UtcDateTime`, `ToUnixTimeMilliseconds`, so `DateTimeOffset.UtcNow`. |
| Giờ nến ATC | 4 phiên 09–12/11: nến cuối mang **14:40** (10/11, 12/11) hoặc **14:45** (09/11, 11/11); không có nến 14:30–14:39. Spec **dòng 52** và **dòng 73** ghi cứng 14:45. | Thiết kế nhận ATC **theo vị trí** (nến cuối phiên sau khoảng trống ≥ 10 phút tính từ sau 14:29), lưu giờ thật vào sổ chất lượng. Không sửa spec — xem "Câu hỏi cho owner". |
| Hình dạng phiên | Phiên đủ **226 nến** (10/11, 12/11); nến đầu 09:15; nghỉ trưa: nến cuối sáng 11:29, đầu chiều 13:00. | Ngưỡng thiếu Q5 = 200 khớp thực tế; test dùng 226 nến. |
| Giờ chạy không trùng | Job 2 **15:05/15:15/15:30** (`Scheduling/QuartzSchedulingExtensions.cs:104-123`), V3 sau phiên **16:00** (**dòng 179-184**), KBS sync 60 s nhưng tự chặn ngoài 08:59–11:31 · 12:59–15:10 (`MarketData/VietnamMarketCalendar.cs:71-81`). | Job mới **17:00** T2–T6, cron riêng, không đụng ba mốc trên. |
| Lịch giao dịch | `VietnamMarketCalendar` `IsTradingDay` **dòng 20-21**, `PreviousTradingDay` **dòng 31-37**, `TodayVietnam` **dòng 17-18**; lễ ở `Domain/Services/TradingSessionMath.cs:11`. | Job và lệnh nạp bỏ qua ngày nghỉ; lệnh nạp chặn `denNgay ≥ hôm nay`. |
| Quyền trigger | `JobTriggerAuthorization.IsAllowed` (`backend/StockRadar.Api/Security/JobTriggerAuthorization.cs:18-29`) — admin hoặc `X-Sync-Key`. Mẫu endpoint: `MarketJobsController.ChayV3NapChiSo` (`Controllers/MarketJobsController.cs:176-191`). | Hai endpoint mới dùng lại `IsAuthorized` của controller (`dòng 193-200`), không thêm `[Authorize]` cấp controller. |
| HttpClient KBS | Typed client đăng ký `Infrastructure/DependencyInjection.cs:48-55` (timeout 45 s, có User-Agent/Accept); class `internal` (+ `InternalsVisibleTo("StockRadar.Tests")` ở `StockRadar.Infrastructure.csproj:20`). | Hàm mới nằm trong `KbsHistoryClient`; không tạo `HttpClient` mới, không tự đặt header. |
| Mẫu job dài có trạng thái | `HistoryBackfillState` (singleton, lock) + `IHistoryBackfillService` + `GET market/jobs/history/status`; `IJobStatusService.TrackAsync` dùng ở `MarketJobsController.cs:50-55`. | Lệnh nạp một lần chạy nền theo đúng mẫu này (tránh HTTP timeout khi tải hàng giờ). |
| Mẫu job/ghi DB | `DailySessionSyncRunner` (tuần tự, log tiến độ, bỏ ngày nghỉ); `HistoryBackfillRunner` giãn cách `Task.Delay` (`MarketData/HistoryBackfillRunner.cs:43-44,163`). | Job nến phút chạy tuần tự từng mã, `DelayBetweenSymbolsMs`, log tổng cuối (FR-006). |
| Đơn vị giá | `ScalePrice` (`KbsHistoryClient.cs:168-169`): giá ≥ 1000 chia 1000 → **nghìn đồng**, cùng `OhlcvBar` của `HistoryJson` (`Domain/Entities/MarketEntities.cs:3-9`). | Khối nén lưu giá thô nghìn đồng, chưa điều chỉnh quyền (FR-001 + spec Edge Cases). |
| Schema hiện tại | 13 bảng; `ApplicationDbContext` (`Persistence/ApplicationDbContext.cs:9-25`, cấu hình V3 **dòng 156-209**); entity V3 nằm ở `Domain/Entities/V3Entities.cs`. | Thêm 2 entity (kiểu V3, tên tiếng Việt không dấu) + 2 cấu hình; migration mới nhất `20261112064612_RemoveV1Tables.cs` trong `Infrastructure/Migrations/` (thư mục có snapshot đang dùng). |
| Danh mục job | `JobCatalog.All` chỉ 3 job (`Application/Jobs/JobCatalog.cs:26-37`); `QuartzJobIds` 7 id (`Scheduling/QuartzJobIds.cs:5-13`). | Thêm 1 id `nen-phut-sync` vào cả hai để màn Jobs thấy và trigger được. |
| Test sẵn có | EF InMemory (`StockRadar.Tests/StockRadar.Tests.csproj`) dùng ở `Tests/MarketData/EfMarketDataWriterChotNenTests.cs:35`; handler HTTP giả ở `Tests/Scheduling/DailySessionSyncRetryTests.cs:33`. Baseline **537 pass**. | Test không cần SQL Server, không cần KBS thật. |

## Thiết kế

### D1 — Bảng nến phút `NenPhutTheoPhien` (FR-001)

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `Id` | `bigint` identity PK | ~240 nghìn dòng/năm; `bigint` để khỏi lo trần |
| `Symbol` | `nvarchar(16)` | mã, chữ hoa |
| `NgayPhien` | `date` | ngày giao dịch theo giờ VN |
| `SoNen` | `int` | số nến trong khối — đếm được không cần giải nén |
| `Nguon` | `nvarchar(8)` | `KBS` (Q3: chỉ KBS) |
| `DuLieuNen` | `varbinary(max)` | khối nến nén gzip (D2) |
| `ThoiDiemGhi` | `datetime2` | UTC, lần ghi gần nhất |

- Khoá duy nhất: index unique `(Symbol, NgayPhien)`. Ghi lại cùng phiên = **cập nhật dòng cũ** (không xoá-rồi-thêm để không đổi `Id`).
- Index phụ `(NgayPhien)` cho truy vấn theo phiên khi xuất dữ liệu.
- Không đụng `Stocks.HistoryJson`.

### D2 — Định dạng trong khối nén: **JSON mảng lồng**, gzip `CompressionLevel.Optimal`

```text
[[555, 30.10, 30.40, 30.00, 30.25, 12400], [556, ...], ...]
  p     o      h      l      c      v
```

- `p` = **số phút kể từ 00:00 giờ treo tường VN** (555 = 09:15, 885 = 14:45 nếu nến mang giờ đó). Không mang múi giờ, không đi qua UTC — đúng bẫy (c). Lấy từ `bar.Time.DateTime`, chỉ đọc `Hour`/`Minute`.
- Giá: `decimal` nghìn đồng thô (D1 của spec), không điều chỉnh quyền.
- Nến ATC ghi **đúng `p` nguồn trả** (14:40 hay 14:45 tuỳ phiên), không đổi, không gộp.
- **Lý do chọn JSON thay vì nhị phân cố định**: đọc được bằng `json` + `gzip` thuần, không cần thư viện/struct; soi được khi lỗi; gzip đã đưa về mức nhỏ (đo ở D9). Nhị phân cố định (int32 phút + 4 giá + int64 KL = 28 byte/nến) bị loại vì khó gỡ lỗi và tiết kiệm sau gzip chưa được chứng minh.
- Python:

```python
import gzip, json
rows = json.loads(gzip.decompress(blob))          # [[p,o,h,l,c,v], ...]
# phiên 2026-11-12, p=555 → 09:15 giờ VN (không cộng/trừ múi giờ)
```

### D3 — Sổ chất lượng `SoChatLuongNenPhut` (FR-006, Q5)

Tách bảng riêng, **không** gộp vào bảng nến phút. Lý do: lần gọi lỗi hoàn toàn vẫn phải có dòng để biết "đã thử và lỗi"; nếu gộp thì dòng nến phút sẽ có khối `NULL`, phá định nghĩa "mỗi dòng là một phiên có nến" của FR-001 và mọi truy vấn đọc nến phải lọc thêm.

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `Id` | `bigint` identity PK | |
| `Symbol`, `NgayPhien` | như D1 | unique `(Symbol, NgayPhien)` — chạy lại thì cập nhật |
| `SoNenNhanDuoc` | `int` | |
| `TrangThai` | `nvarchar(16)` | `Du` / `Thieu` / `Rong` / `Loi` |
| `GioNenAtc` | `time(0)` null | giờ nến ATC **thật của phiên** — để tìm quy luật 14:40/14:45 về sau mà không phải giải nén |
| `SoLanThu` | `int` | số lần gọi KBS cho mã + phiên đó |
| `ThongDiepLoi` | `nvarchar(256)` null | mã HTTP / exception rút gọn |
| `LanGhi` | `nvarchar(16)` | `Job` / `NapMotLan` / `ChayBu` |
| `ThoiDiemGhi` | `datetime2` | UTC |

- `TrangThai` phân biệt đúng ba thứ FR-006 yêu cầu: `Rong` (KBS trả 0 dòng, không lỗi), `Loi` (HTTP/parse/exception), `Thieu` (có nến nhưng `< NguongThieuNen` = 200 → Q5).
- Nhận nến ATC **theo vị trí** (`NenPhutCodec.TimNenAtc`): nến cuối phiên nếu `p ≥ 879` (14:39) và nến liền trước `p ≤ 869` (14:29) — tức có khoảng trống ≥ 10 phút sau 14:29. Không so với một giờ cố định.

### D4 — Hàm KBS mới: `KbsHistoryClient.FetchMinuteRangeAsync` (FR-005)

- Chữ ký: `Task<KbsKetQuaTai> FetchMinuteRangeAsync(string symbol, DateOnly start, DateOnly end, CancellationToken ct)`.
- `KbsKetQuaTai` (record): `IReadOnlyList<KbsChartBar> Bars`, `KbsTrangThaiTai TrangThai`, `string? Loi` — `TrangThai` ∈ `Ok / Rong / HttpError / ThieuKey / Loi`.
- **Không cache** (không chạm `IMemoryCache`), không `TakeLast`. Ngưỡng cảnh báo `MaxNenMoiLanGoi` (mặc định 5000): chạm ngưỡng ⇒ `LogWarning` "nghi KBS cắt" + vẫn trả hết số đã nhận.
- Tách phần lõi dùng chung, **giữ nguyên hành vi cũ**:
  - `FetchRangeCoreAsync(...)` trả `KbsKetQuaTai` (parse + phân loại lỗi, đã có `LogWarning` như hiện tại).
  - `FetchRangeAsync` (dòng 80-124) thành lớp mỏng: `core` → `TrangThai == Ok ? bars.TakeLast(maxBars) : []` ⇒ `FetchAsync` / `FetchDailyHistoryAsync` hành vi y nguyên (biểu đồ app + backfill nến ngày không đổi).
  - `FetchMinuteRangeAsync` = `core` + không cắt + cảnh báo ngưỡng.
- Giờ: chỉ `bar.Time.DateTime` như D2.

### D5 — Job 17:00 (FR-004, FR-006, FR-007)

- `MarketJobsOptions` thêm `NenPhut` (`NenPhutJobOptions`):

| Khoá | Mặc định | Ý nghĩa |
|---|---|---|
| `Enabled` | `true` | tắt job (cách lùi lại) |
| `Hour` / `Minute` | `17` / `0` | giờ VN, sau nhịp V3 16:00 (Q4) |
| `DelayBetweenSymbolsMs` | `200` | giãn cách giữa hai mã |
| `SoLanThuLai` | `2` | chỉ thử lại khi `HttpError`/`Loi`, không thử lại khi `Rong` |
| `ChoGiuaLanThuMs` | `2000` | chờ giữa hai lần thử |
| `NguongThieuNen` | `200` | Q5 |
| `MaxNenMoiLanGoi` | `5000` | ngưỡng cảnh báo bị cắt |
| `SoNgayMoiLan` | `7` | chia khoảng ngày khi nạp một lần (đo lại ở D9) |
| `TuNgayMacDinh` | `2026-05-04` | ngày sớm nhất KBS còn giữ, quan sát trên HPG |

- `NenPhutSyncDecision.Decide(DateOnly homNay, bool laNgayGiaoDich)` → `(bool Chay, DateOnly? NgayPhien, string LyDo)`: không phải ngày giao dịch ⇒ `Chay=false, LyDo="nghi"`. Hàm thuần, test không cần Quartz (theo mẫu `DailySessionSyncDecision`).
- `NenPhutSyncRunner`: lấy `IJobStockRepository.GetActiveSymbolsAsync` (Q1 — **chỉ active**, bỏ `VNINDEX`); với mỗi mã: bỏ qua nếu sổ chất lượng đã ghi `TrangThai = Du` cho phiên đó (trừ khi `taiLai = true`) → gọi KBS khoảng `[ngayPhien, ngayPhien]` → nén → upsert 2 bảng → `Task.Delay(DelayBetweenSymbolsMs)`.
- Log cuối (FR-006): `số mã`, `số nến`, `số mã thiếu`, `thời gian chạy`, số mã lỗi.
- Quartz: `QuartzJobIds.NenPhutSync = "nen-phut-sync"`, `ConfigureNenPhutJob` dùng `BuildWeekdayCron(cfg.Hour, cfg.Minute)` + `VietnamTimeZone`; thêm `JobCatalog` entry (FrequencyRank 3, `TriggerEndpoint = "nen-phut"`).
- Trigger thủ công: `POST api/v1/market/jobs/nen-phut?ngayPhien=yyyy-MM-dd&taiLai=true|false` qua `jobStatus.TrackAsync(JobCatalog.NenPhutSync, "manual", ...)`.

### D6 — Lệnh nạp một lần (FR-003) + chạy bù

- `POST api/v1/market/jobs/nen-phut-nap?tuNgay=yyyy-MM-dd&denNgay=yyyy-MM-dd&chiSoMa=HPG&taiLai=false` — chạy **nền**, trả `202` kèm trạng thái; `409` nếu đang chạy (mẫu `HistoryBackfillState`).
- `GET api/v1/market/jobs/nen-phut-nap/status` → `NenPhutNapTrangThaiDto(IsRunning, Processed, Total, CurrentSymbol, StartedAt, FinishedAt, Loi?)`.
- Mặc định `tuNgay = NenPhut:TuNgayMacDinh`; `denNgay` **kẹp** về `VietnamMarketCalendar.PreviousTradingDay(hôm nay)` — không tải ngày hôm nay (Edge Case "phiên chưa xong").
- Với mỗi mã: lặp **từng cụm `SoNgayMoiLan` ngày** (mặc định 7) trên các phiên giao dịch, gọi `FetchMinuteRangeAsync` một lần/cụm, ghi từng phiên có nến; phiên không có nến ghi `Rong`/`Loi` vào sổ chất lượng. Số lần gọi ≈ `số mã × số cụm` (963 × ~19 cụm cho 5 tháng) — **ước lượng, chưa đo**.
- Chạy lại: unique index + bỏ qua dòng đã `Du` ⇒ không tạo trùng (FR-003, US1 #2). `taiLai=true` ép tải lại.
- **Chạy bù một phiên: dùng chung endpoint** với `tuNgay=denNgay=<ngày>` + `taiLai=true`; không làm endpoint riêng.
- `chiSoMa` chỉ để đo trên dev (D9) — một mã, không đụng production.

### D7 — Script xuất cho sổ nghiên cứu (US3, FR-008)

- `docs/features/v3-scenario-engine/research/scripts/export_nen_phut.sh`: chỉ đọc, `sqlcmd` xuất `Symbol|NgayPhien|hex(DuLieuNen)`, `gzip` file, giữ mẫu của `export_prod.sh` (đọc chuỗi kết nối từ `appsettings.Production.json`, không in mật khẩu).
- `docs/features/v3-scenario-engine/research/scripts/load_nen_phut.py`: hex → `gzip.decompress` → `json.loads` → DataFrame; in số dòng theo mã/phiên để đối chiếu `COUNT(*)`.
- Không thêm dữ liệu vào repo (`scripts/.gitignore` đã chặn `*.txt`/`*.gz`).

### D8 — Tài liệu (FR-008, FR-010)

- `docs/domain/pipeline-jobs.md`: thêm dòng job **17:00** vào bảng "Luật as-is", thêm mục "Nến phút" (nguồn KBS, một dòng/mã/phiên, sổ chất lượng, hai endpoint), cập nhật bảng nguồn đối chiếu (entry `NenPhutSyncRunner`).
- `docs/entity_model.md`: thêm 2 mục `NEN_PHUT_THEO_PHIEN`, `SO_CHAT_LUONG_NEN_PHUT` + 2 quan hệ mềm theo `Symbol`.
- `CLAUDE.md` đoạn **Pipeline**: một dòng "17:00 nến phút 1 phút/mã/phiên (chỉ thu thập, V3 không đọc)".
- `docs/features/v3-scenario-engine/research/README.md`: mục "Dữ liệu nến phút" (cách xuất, cách nạp).
- `05-quyet-dinh-va-cau-hoi-mo.md` Q7: bổ sung quyết định 06/11 + trạng thái thực hiện.

### D9 — Đo trước deploy (bắt buộc theo Q2) và cách lùi lại

Chỉ đo **trên API chạy local**, đi qua `KbsHistoryClient` (KBS chặn gọi ngoài app):

1. Giới hạn khoảng ngày mỗi lần gọi `data_1P` (thử 7/30/90 ngày) và giới hạn số nến mỗi lần trả.
2. Kích thước khối nén thật của ~20 mã (cả thanh khoản cao và thấp) → ngoại suy dung lượng/năm, so với ổ trống đo lại lúc đó (số 06/11: còn 14 GB; DB đã nhỏ đi sau khi xoá 26 bảng V1 — **chưa đo lại**).
3. Thời gian cho 963 lần gọi tuần tự → chốt `DelayBetweenSymbolsMs` và số cụm ngày.
4. Kết quả ghi vào `specs/017-intraday-minute-bars/tasks.md` (mục "Số đo") và cập nhật `SoNgayMoiLan`.

Lùi lại: `MarketJobs:NenPhut:Enabled = false` (job ngừng, dữ liệu giữ nguyên), hoặc migration `Down` xoá 2 bảng.

## Project Structure

```text
backend/
├── StockRadar.Domain/Entities/NenPhutEntities.cs               # 2 entity (kiểu V3Entities)
├── StockRadar.Application/
│   ├── Options/MarketJobsOptions.cs                            # + NenPhutJobOptions
│   ├── Abstractions/INenPhutService.cs                         # job + nạp một lần
│   ├── Abstractions/IRepositories.cs                           # + INenPhutRepository
│   ├── DTOs/NenPhutDtos.cs                                     # kết quả job, trạng thái nạp
│   └── Jobs/JobCatalog.cs                                      # + "nen-phut-sync"
├── StockRadar.Infrastructure/
│   ├── MarketData/KbsHistoryClient.cs                          # + FetchMinuteRangeAsync
│   ├── MarketData/NenPhutCodec.cs                              # nén/giải nén + nhận ATC theo vị trí
│   ├── MarketData/NenPhutSyncRunner.cs + NenPhutSyncDecision.cs
│   ├── MarketData/NenPhutNapMotLanRunner.cs + NenPhutNapTrangThai.cs
│   ├── Persistence/Repositories/EfNenPhutRepository.cs
│   ├── Persistence/ApplicationDbContext.cs                     # + 2 DbSet + cấu hình
│   ├── Scheduling/QuartzJobIds.cs + QuartzSchedulingExtensions.cs
│   ├── Scheduling/Jobs/NenPhutSyncJob.cs
│   ├── Migrations/xxxx_AddNenPhut.cs                           # thư mục có snapshot đang dùng
│   └── DependencyInjection.cs                                  # đăng ký runner/repo/state
├── StockRadar.Api/Controllers/MarketJobsController.cs           # + nen-phut, nen-phut-nap(+status)
└── StockRadar.Tests/MarketData/NenPhut*.cs · Scheduling/NenPhut*Decision*.cs

docs/features/v3-scenario-engine/research/scripts/export_nen_phut.sh · load_nen_phut.py
docs/domain/pipeline-jobs.md · docs/entity_model.md · CLAUDE.md ·
docs/features/v3-scenario-engine/research/README.md · 05-quyet-dinh-va-cau-hoi-mo.md
```

**Structure Decision**: chỉ backend + `docs/` + `scripts/` nghiên cứu. Không chạm `mobile/`, `frontend/` (spec: không có màn hình mới), không chạm `scripts/` ops.

## Thứ tự thực hiện

| Đợt | Nội dung | Ai làm | Điều kiện sang đợt sau |
|---|---|---|---|
| 1 | Hàm KBS mới + codec + entity/repo + test (không job) | AI | `dotnet build`, `dotnet test` sạch; 8 test bắt buộc xanh |
| 2 | **Đo D9 trên dev** (giới hạn ngày/nến, kích thước nén 20 mã, thời gian 963 gọi) | AI chạy, **owner duyệt số** | Có số thật, chốt `SoNgayMoiLan`, ổ trống đủ |
| 3 | Migration + job 17:00 + lệnh nạp một lần + endpoint | AI soạn; **owner** áp migration production | Migration body đọc lại, chạy thử dev |
| 4 | **Nạp một lần trên production** ngoài giờ giao dịch | AI chuẩn lệnh, **owner** chạy | `GET nen-phut-nap/status` về đích; báo cáo ngày sớm nhất theo mã (SC-001) |
| 5 | Script xuất + tài liệu (D7, D8) | AI | Link tài liệu đúng; script chạy được trên dữ liệu dev |
| 6 | Theo dõi 10 phiên: SC-002 (≥ 95% mã đủ nến) và SC-003 (nhịp V3 16:00 không đổi) | **owner** đọc log | Đạt cả hai ⇒ đóng spec |

Mỗi đợt một commit khi owner nói "commit đi".

## Kiểm chứng

- Mỗi đợt: `dotnet build StockRadar.slnx`, `dotnet test backend/StockRadar.Tests/StockRadar.Tests.csproj` (baseline 537 pass, không được giảm).
- Đợt 1: test thuần (không DB, không mạng) cho codec + ATC + cắt nến + giờ VN; test handler giả cho trạng thái lỗi.
- Đợt 3: test runner với EF InMemory + handler giả (3 mã: đủ, lỗi, thiếu).
- Đợt 4: `SELECT COUNT(*), MIN(NgayPhien), MAX(NgayPhien) FROM NenPhutTheoPhien` (chỉ đọc) đối chiếu báo cáo lệnh nạp.
- Đợt 6: đối chiếu log `V3 sau phiên` 5 phiên trước/sau deploy (SC-003) và bảng `SoChatLuongNenPhut` (SC-002).

## Rủi ro

| Rủi ro | Giảm thiểu |
|---|---|
| Job/lệnh nạp lỡ dùng `FetchAsync` (cache 45 s, 300 nến) ⇒ mất nến | Chỉ job gọi `FetchMinuteRangeAsync`; test "trên 300 nến không mất" + grep `FetchAsync` trong `NenPhut*` |
| Lệch 7 tiếng do đi qua UTC | Chỉ `.DateTime`; test cố định "máy chạy UTC, lưu 14:29" |
| Ghi sai lý do khi KBS lỗi | Kiểu trả có trạng thái; test `Rong` ≠ `Loi` |
| Coi nến ATC là 14:45 cố định | Nhận theo vị trí + lưu `GioNenAtc`; test cả hai giờ |
| Dung lượng vượt ổ trống | D9 đo thật trước khi áp migration; `Enabled=false` để dừng |
| KBS chặn vì gọi quá dày | Tuần tự, `DelayBetweenSymbolsMs`, `SoLanThuLai` giới hạn, không chạy trùng giờ Job 2/V3 |
| Migration sinh sai (repo này từng bị) | Sinh bằng `dotnet ef`, đọc lại toàn bộ body trước khi owner áp; `Down` drop 2 bảng |

## Ngoài phạm vi

- Đưa nến phút vào luật V3, backtest hay màn hình nào (FR-009 — spec riêng).
- Nguồn FireAnt (Q3), khung 5/15/60 phút (Q2: chỉ 1 phút, gộp sau).
- Dọn `KbsMarketSyncJob` ghi lại `HistoryJson` mỗi 60 giây (spec 018, đã ghi nhận ở spec).
- Sửa spec 017 (chỉ đổi dòng `Status`); các chỗ sai ghi ở mục dưới.

## Câu hỏi cho owner

1. **Spec ghi cứng "nến 14:45" (dòng 52, dòng 73) trong khi nến ATC thực tế là 14:40 hoặc 14:45 tuỳ phiên** (quan sát 4 phiên 09–12/11, chưa đủ suy ra luật). Đề xuất của AI — thay câu, không đổi nghĩa:
   - Dòng 52 → "10 mã ngẫu nhiên có nến ATC (nến cuối phiên, sau khoảng trống từ 14:30; giờ nến là 14:40 hoặc 14:45 tuỳ phiên) khớp giá trên bảng giá."
   - Dòng 73 → "**Phiên có ATC:** nến ATC là nến cuối phiên, sau khoảng trống ≥ 10 phút tính từ sau 14:29; giờ nến thay đổi theo phiên (đã thấy 14:40 và 14:45). Ghi như nguồn trả, không gộp."
   - Owner duyệt 13/11/2026 ("Sửa đi"); spec đã sửa hai dòng này, dòng 73 giữ thêm cụm "(một lần khớp)" của bản cũ.
2. **Mã inactive có lưu nến phút không?** Q1 chốt "toàn bộ mã active (963)". Đề xuất của AI: **chỉ active** (đúng Q1, giảm ~10–15% số lần gọi). Nếu owner muốn warm inactive như Job 2 làm với nến ngày thì thêm `WarmInactive` (mặc định tắt).
