# Implementation Plan: Gỡ phần bắn tin mua/bán + theo dõi vị thế V1 (VIP)

**Branch**: `011-remove-v1-vip-alerting` | **Date**: 2026-10-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/011-remove-v1-vip-alerting/spec.md`

## Summary

Gỡ sạch đường **bắn tin mua/bán + theo dõi vị thế + AI chấm tin + ML nội phiên** của V1 (VIP), vì V2 (spec 009 Pha 1/2/3 → `KetQuaKichBan`) đã thay thế. **Giữ nguyên** đường lọc → Top của V1 (Job 1 universe, đồng bộ giá/nến, DailyAnalysis, Top 5, Home) và dữ liệu thời gian thực dùng chung (lô lớn/VSA/dòng tiền/SignalR) mà runner intraday đang sinh. Picker tín hiệu V1 trong nhật ký (spec 010) mất nguồn → bỏ nhánh VIP, giữ V2; lệnh cũ `NguonTinHieu='VIP'` giữ nguyên giá chụp + nhãn "VIP (đã ngừng)".

**Chiến lược 2 đợt ship** (theo chỉ đạo chủ sản phẩm):
- **ĐỢT A** (lượt này): gỡ **code** (backend/web/mobile/test) + tách các phần 010 đang dùng. **KHÔNG** xoá bảng. Entity + DbSet 3 bảng VIP được **GIỮ ở đợt A** (xoá chúng sẽ buộc phải có migration — dồn sang đợt B). Picker/backend vẫn biên dịch với entity tồn tại nhưng không còn đường ghi.
- **ĐỢT B** (sau khi chủ sản phẩm xác nhận đã **xuất dữ liệu production**): migration **drop 3 bảng** + gỡ entity/DbSet/config tương ứng + xoá snapshot migration. Viết sẵn ở đợt A nhưng **không áp**.

## Technical Context

**Language/Version**: C# / .NET 10 (backend layered), React+TypeScript (frontend web), Dart/Flutter (mobile)
**Primary Dependencies**: EF Core + SQL Server, Quartz, SignalR, xUnit; không thêm dependency mới
**Storage**: SQL Server (`stockradar.db` dev; production qua deploy) — 3 bảng VIP sẽ drop ở đợt B
**Testing**: `dotnet test backend/StockRadar.Tests` (xUnit, EF InMemory); baseline trước đợt A = **418 pass**
**Target Platform**: API `:5280` dev / `103.226.248.6` prod; web Vite; mobile Flutter
**Project Type**: monorepo web + mobile + API
**Constraints**: không đụng V2 + phần lọc Top; không đổi API còn dùng; comment chỉ 4 nhãn ≤2 dòng; xoá sạch không shim
**Scale/Scope**: ~20 file backend xoá + ~8 sửa; ~6 file web xoá + 2 sửa; 4 file mobile sửa + vài nhóm model chết; 51 file test (xoá ~10, sửa 3)

## Constitution Check

*GATE: Pass trước Phase 0. Re-check sau Phase 1.*

- [x] **I. Code as truth**: Plan bám file:line đã mở kiểm chứng ở Phụ lục A spec; không tin CLAUDE.md/graph.
- [x] **II. Spec-first**: Đây là đổi cấu trúc trọng yếu (job/pipeline/API) → đã có `spec.md` + quyết định chốt Q1–Q3 trước plan. OK.
- [x] **III. Minimal surface**: Phạm vi = đúng ý định "bỏ bắn tin V1". Không refactor tuỳ tiện; giữ nguyên phần lọc Top + V2 + SignalR.
- [x] **IV. Domain gates**: Không đổi Buy Score/cổng Top/MA/flatBox → không đụng living doc cổng. Cập nhật `CLAUDE.md` map + `docs/domain/pipeline-jobs.md` (mô tả job `opportunity-monitor` hết chữ VIP) trong cùng change set (nhiệm vụ polish).
- [x] **V. Simplicity**: Không thêm abstraction; chỉ gỡ. Đọc file có mục tiêu. Không build cả SLN trừ khi cần (sẽ build sln để bắt lỗi Api — cần cho removal).
- [x] **Stack**: Tôn trọng biên Domain/Application/Infrastructure/Api/mobile/frontend. Restart bằng `backend/restart-api.ps1`. Ship `scripts/ship-all.ps1` (user tự chạy — đợt này không ship).

**Cổng**: PASS — không vi phạm cần Complexity Tracking. (Ghi chú: việc build toàn sln là bắt buộc để phát hiện lỗi biên dịch ở Api sau khi gỡ controller/DI, không phải over-abstraction.)

## Phases & Wave Mapping

- **Phase 0 (Setup)**: tách `RealizedPnlOptions` ra file riêng, giữ `RealizedPnlMath`/`OutcomeBucketNames` — **mở khoá** cho mọi bước gỡ (bẫy thứ tự #1). Xác định `MasterAlertKinds` leg-label 010 còn dùng (bẫy #3).
- **Phase 1 (Foundational — runner + job)**: gỡ lời gọi `vipAlerts.*` khỏi `OpportunityIntradayMonitorRunner`, giữ vòng quét lô lớn/VSA/dòng tiền/SignalR (bẫy #4); `opportunity-monitor` job vẫn chạy cho dữ liệu app. Đây là điểm nối sống còn — làm trước để không sập realtime.
- **Phase 2 (US1 — bảo vệ Home/Top + realtime)**: xác minh đường Top/DailyAnalysis/Opportunities/radar/market-hub **không** tham chiếu lớp đã gỡ; xoá `AlertsController`+`AlertService` feed VIP-only.
- **Phase 3 (US2 — gỡ backend bắn tin/vị thế/ML/AI)**: xoá publisher/evaluator/formatter/judge/caches/session-tracker, VipIntraday services+ML+store+features, RealizedPnl service/backfill/accuracy-query (orphan), `EfVipAlertFireRepository`+`EfMasterAlertPositionRepository`+perf-read VIP, gỡ DI + endpoint `telegram/vip-test`+`/ml/vip-intraday`+`/alerts`, gỡ options+appsettings VIP. **GIỮ** entity/DbSet 3 bảng (dồn đợt B).
- **Phase 4 (US3 — picker 010 + FE)**: bỏ nhánh VIP backend (`NhatKyController`/`NhatKyService`/`EfNhatKyTinHieuRepository`), nhãn "VIP (đã ngừng)", thống kê an toàn; mobile: bỏ tab VIP picker + xoá mã chết; web: xoá mã chết VIP.
- **Phase 5 (Test + build)**: xoá test VIP, sửa 3 file NhatKy, `dotnet test` xanh; `frontend` `npm run build` sạch; `flutter analyze` sạch.
- **Phase 6 (Đợt B — chỉ viết, KHÔNG áp)**: migration drop 3 bảng + gỡ entity/DbSet; hướng dẫn xuất production.
- **Polish**: cập nhật `CLAUDE.md` pipeline map + `docs/domain/pipeline-jobs.md`; smoke dev sau đợt A.

## Project Structure (paths touched)

```text
backend/
  StockRadar.Api/            # AlertsController(delete), MarketJobs/Ml/NhatKy controllers(sửa), Program/cs
  StockRadar.Application/    # Services(NhatKy sửa; Vip*/RealizedPnl* delete), Abstractions(delete), Options(split+delete), DI(sửa)
  StockRadar.Domain/         # MasterAlerts(keep OutcomeBucketNames; MasterAlertKinds tỉa), Services/RealizedPnlMath(keep), OpportunityRanking/VipIntradayFeatures(delete)
  StockRadar.Infrastructure/ # Notifications(VIP delete, giữ TelegramNotifier+V2*+OrderFlowSnapshotTracker), MarketData(runner sửa), Ml(VipIntraday delete), Persistence(repos+DbContext tỉa), Scheduling(job/ids sửa)
  StockRadar.Tests/          # VipAlerts/SellExit/RealizedPnl tỉa; NhatKy sửa
frontend/src/                # hooks/components/lib/types (alerts VIP chết) delete/sửa
mobile/lib/                  # screens(nhat_ky picker/lenh sửa), core/api+models tỉa, widgets xoá
docs/ · CLAUDE.md            # cập nhật map pipeline sau code
scripts/export-db.ps1        # dùng cho hướng dẫn xuất đợt B
```

**Structure Decision**: Monorepo hiện có; không tạo project/thư mục mới. Removal trải đều 4 tầng + 2 FE; giữ nguyên vị trí file dùng chung.

## Complexity Tracking

Không có vi phạm cổng → không cần mục này.

## Risks & Guardrails

- **R1 — xoá quá đà sang đường dùng chung**: `RealizedPnlMath`/`RealizedPnlOptions`/`OutcomeBucketNames`/`MasterAlertKinds`(leg)/`OrderFlowSnapshotTracker`+flow/detector/aggregator+publisher/`quoteCache`/`sync` → **GIỮ**. Kiểm bằng: `dotnet test` phải xanh (6 file NhatKy dùng RealizedPnlOptions/Math).
- **R2 — SignalR chết**: runner intraday nuôi `PublishTradeEventAsync`/`PublishQuotesAsync` cho app. Chỉ gỡ khối `vipAlerts.*`; giữ `RunAsync` phần còn lại. Smoke: `/market/intraday-monitor` vẫn trả, log vẫn "Trade scan: N sự kiện".
- **R3 — entity/DbSet VIP**: GIỮ ở đợt A để tránh migration ép buộc; chỉ gỡ ở đợt B kèm drop migration. Backend vẫn "nhìn thấy" bảng nhưng không còn writer/reader production (chỉ picker đã bị gỡ).
- **R4 — migration sai body**: repo có tiền lệ `Up()` sinh sai. Đợt B: đọc kỹ body, test DEV apply→rollback→apply trước khi giao.
- **R5 — leftover DI**: gỡ class mà quên `DependencyInjection.cs`/`Program.cs`/`QuartzSchedulingExtensions` → build fail. Build sln (không chỉ `dotnet test`) để bắt.
