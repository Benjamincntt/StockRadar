# StockRadar (JUICE) — bản đồ agent ngắn

**Governance:** [`.specify/memory/constitution.md`](./.specify/memory/constitution.md) (Spec Kit).  
**Canon tài liệu:** [`docs/README.md`](./docs/README.md) → [`docs/domain/`](./docs/domain/).

Đổi cổng / điểm / kịch bản V3 / luật bán / pipeline **trọng yếu** → Spec Kit (`/speckit-specify`…) **và** cập nhật `docs/domain/*` **cùng change set**. Bản đồ này **không** thay constitution hay code.

Khi docs lệch code → **tin code trên disk**.

## Giao thức mặc định — LUẬT BẮT BUỘC

**1. Hỏi trước, đừng sửa ngay.**  
Khi user hỏi, mô tả lỗi, yêu cầu phân tích, hoặc trao đổi hướng xử lý mà chưa ra lệnh rõ ràng:

- Chỉ đưa: nguyên nhân, 1–3 phương án, ưu/nhược, rủi ro, cách test.
- **Không sửa code, không tạo file, không chạy lệnh đổi codebase trong lượt đó.**
- Đọc code để phân tích thì được, nhưng không dùng kết quả để implement ngay.

Chỉ bắt đầu sửa khi user xác nhận rõ: **"làm đi"**, **"fix đi"**, **"implement"**, **"apply phương án X"**. Không chắc → hỏi lại.

Xem chi tiết tại `.specify/memory/bug-fix-constitution.md` và `.specify/memory/constitution.md` (Nguyên tắc III).

**2. Thay đổi tối thiểu xâm lấn.**  
Chỉ sửa bề mặt tối thiểu để đạt ý định đã duyệt. Không refactor, đổi tên, hay dọn code lân cận.

**3. Gate trọng yếu → Spec Kit.**  
Đổi luật chọn mã, cổng lọc, kịch bản V3, ngưỡng bán, pipeline job, hay route API mới → dừng và dùng `/speckit-specify` trước. Không implement thẳng từ chat.

Monorepo: **.NET API** + **Flutter mobile** + **React web**. Production API: `http://103.226.248.6/api/v1`, dev `:5280`.

## Cấu trúc (chỉ mở khi cần)

| Vùng | Path |
|------|------|
| API | `backend/StockRadar.{Api,Application,Domain,Infrastructure}/` |
| Mobile | `mobile/lib/` |
| Web | `frontend/src/` |
| Scripts | `scripts/` (`ship-all.ps1`) |

## Pipeline (tóm tắt)

**V1 — ĐÃ GỠ toàn bộ (spec 016):** phân tích Top 5, xếp hạng ML/HPO, radar trong phiên, theo dõi lô lớn và cảnh báo Darvas không còn chạy. Cách V1 làm: [`docs/features/v1-archive/`](./docs/features/v1-archive/README.md).
**Quét giá (spec 018 phần A):** KBS sync chỉ chạy trong khung bảng giá 08:59–11:31 · 12:59–15:10 ngày giao dịch; Job 2 chỉ chạy sau đóng cửa 15:05 kèm thử lại 15:15/15:30 (chỉ lấy mã còn thiếu nến), lọc lại universe 1 lần/ngày; bộ ghi chặn tạo nến hôm nay ngoài khung và gọi tay trước 14:45.
**V3 (bộ kịch bản P1++):** KB1/KB2/KB4/N1/N3 — 4 nhịp: **08:30** trước phiên (KB1/KB2 → tin mua ATO) · **mỗi 1 phút 09:00–14:45** trong phiên (khớp ATO + **bán realtime** theo ngưỡng đã lưu) · **~14:25** ATC (KB4/N1/N3 + bán hết hạn) · **16:00** sau phiên. **Tải dữ liệu (chốt 26/10/2026):** chỉ nhịp 16:00 nạp đầy đủ (`LayCacMaAsync` toàn universe — lưu chỉ số hôm nay + `UNI` phiên sau vào `ChiSoNgayV3`, tính sẵn `NguongChamMa`/`GiaThamChieu`/`Exchange` vào `ViTheV3`); 08:30 + 14:25 chỉ ~200 mã `UNI` đã lưu, MA50 đọc chuỗi đã lưu (ATC cộng điểm hôm nay, nến ghép bằng khối lượng khớp tích lũy); nhịp mỗi phút chỉ quote mã đang giữ so với ngưỡng đã lưu — **không** `LayCacMaAsync`; khi deploy chạy một lần `v3-nap-chi-so` (từ `V3:TuNgayNapChiSo` = 01/2023) để nạp chuỗi, không tạo tín hiệu/vị thế cũ; mỗi nhịp log thời gian chạy. Bảng: `TinHieuV3` / `ViTheV3` / `ChiSoNgayV3`; endpoint `v3/*` + màn Hiệu quả + chi tiết mã + nhật ký `nhat-ky/tin-hieu/v3`. V2 đã gỡ toàn bộ theo spec 012. V1 đã gỡ toàn bộ theo spec 016: Home mobile/web không còn khối Top, chỉ còn thẻ **Đang giữ** (vị thế V3 từ `v3/hieu-qua/vi-the-dang-giu`) cộng VNINDEX + sparkline. Bắn tin VIP V1 đã gỡ trước đó theo spec 011.

Chi tiết: [`docs/domain/pipeline-jobs.md`](./docs/domain/pipeline-jobs.md).

## Luật sản phẩm → đọc domain

| Chủ đề | Living |
|--------|--------|
| Buy Score / Top / hiển thị | **ĐÃ GỠ** (spec 016) — Buy Score, Entry, TradeState không còn; cách V1 làm: [`docs/features/v1-archive/02-cham-diem.md`](./docs/features/v1-archive/02-cham-diem.md) |
| V3 Scenario Engine (bộ kịch bản P1++) | [`specs/012-v3-scenario-engine/spec.md`](./specs/012-v3-scenario-engine/spec.md) · luật chuẩn: [`docs/features/v3-scenario-engine/research/`](./docs/features/v3-scenario-engine/research/) (normative) |
| Luồng bán V3 (realtime trên giá khớp) | Phụ lục A/C spec 012 · `V3LuatBanThucTe` — bán khi giá khớp ≥ max(chạm MAn, giá vào ×1,004); không chờ giá đóng cửa |
| Danh mục V3 chạy theo lệnh thật | [`specs/014-v3-real-portfolio/spec.md`](./specs/014-v3-real-portfolio/spec.md) · nhịp 16:00 thêm bước đối chiếu Nhật ký (`V3DoiChieuNhatKy`), ghi đè `ViTheV3` theo `V3:TaiKhoanDanhMuc` |
| MA stack & pha tăng trưởng | **ĐÃ GỠ** (spec 016) — tombstone: [`docs/domain/ma-stack-and-market-phase.md`](./docs/domain/ma-stack-and-market-phase.md) | Còn sống: MA50 chỉ số (`ChiSoNgayV3`) + `NguongChamMa` của V3 |
| flatBox / Darvas | **ĐÃ GỠ** (spec 016) — tombstone: [`docs/domain/base-price-flatbox.md`](./docs/domain/base-price-flatbox.md) |
| Lợi nhuận thực (Realized P&L) | **ĐÃ GỠ** (spec 011 — Remove V1 VIP alerting); xem [`docs/domain/realized-pnl.md`](./docs/domain/realized-pnl.md) (tombstone) |
| Win-rate overhaul (Top hygiene + ML + HPO) | **ĐÃ GỠ** (spec 016) — xếp hạng ML/HPO không còn; cách V1 làm: [`docs/features/v1-archive/03-xep-hang-ml.md`](./docs/features/v1-archive/03-xep-hang-ml.md) |
| Điều chỉnh giá theo quyền | [`specs/006-paid-rights-adjust/spec.md`](./specs/006-paid-rights-adjust/spec.md) · seed `Data/su-kien-quyen.json` · `BoDieuChinhGiaTheoQuyen` giữ cho spec 015 | last/chart thô |
| VNINDEX Home overview (giá + biểu đồ) | `GET /api/v1/market/vnindex/chart` · `VnIndexMarketCard` | Nhãn pha thị trường đã gỡ (spec 016) |

Kiến trúc: [`docs/architecture.md`](./docs/architecture.md). AIUP: [`docs/use_cases/`](./docs/use_cases/), [`docs/entity_model.md`](./docs/entity_model.md).

## Quy ước khi sửa

- Backend xong → `backend/restart-api.ps1`
- Ship: `.\scripts\ship-all.ps1 -Message "..."` — user tự chạy
- Build + cài app mobile lên điện thoại: làm theo mục "làm nhanh" trong [`mobile/README.md`](./mobile/README.md), không tự dò lại
- Token: Grep → đọc 3–5 file; không quét `build/` / `node_modules/` / `bin/` / `obj/`
- Entry thường dùng: `Program.cs`, `QuartzSchedulingExtensions.cs`, `V3TruocPhienRunner.cs`/`V3TrongPhienRunner.cs`, `V3BoKichBan.cs`, `KbsMarketSyncJob.cs`, `app_router.dart`

## Skill có sẵn — `.claude/skills/`

Skill được nạp tự động khi gọi đúng tên. Nạp skill **trước** khi viết code.

### Skill nền (nạp cho MỌI task implement/fix)

| Skill | Dùng khi |
|-------|----------|
| `engineering-principles` | mọi lần sửa code |
| `sr-build-run-test` | trước khi build/test/debug/reproduce |

### Theo nhóm task

| Task | Skill |
|------|-------|
| **Fix bug / lỗi runtime** | `debugging-error-recovery` |
| **Code cũ, không rõ owner** | `legacy-code-change` |
| **Migration EF Core** | `efcore-migration-review` |
| **SQL script / data fix** | `sql-data-review` |
| **API / endpoint / DTO** | `api-contract-review` |
| **Hiệu năng / N+1 / JSON scan** | `performance-review` |
| **Background job / SignalR / outbound** | `integration-event-design` · `observability-instrumentation` |
| **Frontend web (React/Vite)** | `frontend-web-dev` |
| **UI/UX review** | `ui-ux-review` |
| **Browser QA / smoke** | `browser-qa-execution` |
| **Viết / review test xUnit** | `test-automation-dotnet` |
| **Trước khi ship** | `release-deploy-gate` |
| **Config / secrets / appsettings** | `secrets-config-review` |
| **Quyết định rủi ro cao** | `doubt-driven-review` |
| **Cập nhật docs sau code** | `doc-update-after-code` |
| **Tạm dừng / tiếp phiên** | `context-handoff` |
