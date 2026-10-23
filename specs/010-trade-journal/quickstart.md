# Quickstart: Nhật ký giao dịch thật

**Branch**: `010-trade-journal` | **Date**: 2026-10-21
Hướng dẫn kiểm chứng end-to-end **sau khi implement** (theo `/speckit-tasks` → `/speckit-implement`). Lượt plan này **không** chạy bất kỳ lệnh build/run nào.

## Prerequisites

- .NET 10 SDK; Flutter SDK (Android toolchain) — theo `docs/build-and-deploy.md`.
- SQLite local `backend/StockRadar.Api/stockradar.db` (dev); migration mới `AddNhatKyGiaoDich` tạo 2 bảng `NhatKyGiaoDich`, `NhatKyTinHieuBoQua` — **không** đụng bảng cũ.
- Tài khoản test: `POST /api/v1/users` (email/password/displayName) → nhận JWT (`UsersController.cs:13-28`). Mobile: đăng nhập màn `login_screen.dart` (token tự gắn vào mọi request — `api_client.dart:126-130`).

## Chạy backend + test

```powershell
dotnet test backend/StockRadar.Tests/StockRadar.Tests.csproj   # baseline hiện tại 349/349 + test NhatKy mới
backend/restart-api.ps1                                        # API dev :5280 (constitution: agent restart giúp)
```

## Chạy mobile

```powershell
cd mobile && flutter run -d <device> --dart-define=API_BASE=http://<host>:5280/api/v1
```

## Kịch bản kiểm chứng (ánh xạ thẳng acceptance scenarios của spec)

### KS-1 — Ghi lệnh tự do (US1 AC1–AC4)

1. `POST /api/v1/nhat-ky/lenh` mua HPG 1000 @ 27.5, `phi` null → **201**, row hiện trong `GET /lenh`.
2. Thiếu `giaKhop` → **400** `{"detail":"Thiếu giá khớp."}` (AC2).
3. `PUT` đổi `giaKhop` 27.5 → 28.0 → `GET /vi-the` cho `giaVonBinhQuan` mới ngay, không bảng trung gian (AC3).
4. `POST` bán 1500 HPG khi chỉ giữ 1000 → **422** `khoLuongHienCo: 1000`, không lưu (AC4).

### KS-2 — Gắn tín hiệu V2 (US2 AC1–AC2, SC-003)

1. Cần ≥1 dòng `KetQuaKichBan` `TrangThai=DaKichHoat` (dev: chạy Pha 1 qua `POST /api/v1/market/jobs/pha1` với `X-Sync-Key`, hoặc dùng dữ liệu có sẵn).
2. `GET /nhat-ky/tin-hieu/v2` → picker trả `giaTinHieu` + `keHoach` → form điền sẵn (AC1).
3. `POST /lenh` với `nguonTinHieu:"V2", maTinHieu:"<id>"`, giá khớp lệch +0.2 → response có `lechSoTinHieu: 0.2`, `lechSoTinHieuPhanTram` đúng (AC2).
4. Đối chiếu **FR-007**: ghi `SELECT COUNT(*), MAX(RowVersion)`-style snapshot bảng `KetQuaKichBan` + `GET /api/v1/hieu-qua/tom-tat` **trước và sau** toàn bộ KS-1→KS-5 → hai bản **giống hệt nhau** (SC-004).
5. Xoá mềm/huỷ tín hiệu gốc (đổi `TrangThai`) → `GET /lenh/{id}` vẫn trả `giaTinHieuChup` như lúc gắn (Edge case).

### KS-3 — Bỏ qua tín hiệu (US2 AC3, FR-003)

1. `PUT /nhat-ky/tin-hieu/v2/{ma}/bo-qua` → 200; `GET /thong-ke` `tinHieuBoQua` +1, `tyLeKhopTinHieu` giảm.
2. Sau đó `POST /lenh` gắn cùng tín hiệu → đánh dấu bo-qua tự mất, `tinHieuCoLenh` +1 (note data-model).

### KS-4 — Vị thế & lãi/lỗ ròng (US3, SC-002)

1. Mua 1000 @ 10 (phi null → 0.15%), bán 500 @ 11, bán 500 @ 9 → `GET /thong-ke`:
   - `viThe.thua` = 1? Không — kiểm bằng `GET /lenh`: vị thế đã đóng, P&L ròng theo `RealizedPnlMath.Compute(10.015, [SellLeg(11,500), SellLeg(9,500)], fees(0.15,0.25,0.1))` — kỳ vọng ≈ **−4.9%/vốn** ⇒ phân loại "Thua" (Classify ngưỡng 0).
   - Số tiền tuyệt đối khớp tay calculator ±0.01% (SC-002).
2. Đơn vị test tự động: `StockRadar.Tests/NhatKy/NhatKyServiceTests.cs` — các ca trên + làm tròn + phí tiền override (assert `RealizedPnlMath` cho ra số như V1 path với cùng input — chứng minh "dùng chung công thức").

### KS-5 — Lệnh điều chỉnh quyền (N7, FR-010)

1. Giữ 1000 HPG giá vốn 27.5413; `POST /lenh` `loaiLenh:"DieuChinh", kLieuChinh:+100, ghiChu:"cổ tức 10:1"` → `GET /vi-the`: 1100 cp, `giaVonBinhQuan` giảm còn 27.5413×1000/1100 (không tự đổi nếu không nhập — đúng N7 "không tự điều chỉnh").
2. `DieuChinh` thiếu `ghiChu` → 400.

### KS-6 — Cách ly người dùng (N6, FR-009)

1. User B (token khác) `GET /nhat-ky/lenh` → không thấy lệnh của A.
2. User B `PUT/DELETE /nhat-ky/lenh/{idCuaA}` → **404** (không 403 — không lộ tồn tại).
3. Không token → **401** (controller `[Authorize]`).
4. `GET /thong-ke` của B không lẫn số của A.

### KS-7 — SC-001 (30 giây/mệnh trên mobile)

Trên device: mở tab Nhật ký → chạm tín hiệu trong picker → nhập giá khớp + KL (đã điền sẵn phần còn lại) → Lưu. Bấm giờ; mục tiêu < 30s. Nếu dài → cân nhắc quick-entry sheet (tasks phase).

## Test tự động mong đợi

- `dotnet test` toàn solution: baseline **349/349** + tests mới `NhatKy/*` (service + repository InMemory; cùng pattern `Pha2TrongPhienTests` — EF InMemory, không touch SQLite file).
- `flutter analyze` sạch cho 2 màn mới + router + api_client.

## Follow-up bắt buộc khi ship (theo constitution)

- `docs/domain/pipeline-jobs.md` / `docs/domain/realized-pnl.md`: sửa mục "Nguồn đối chiếu" của realized-pnl (dòng 19-20) vì `PerformanceController` + 2 màn mobile đã bị xoá ở `183adb5` — docs đang lệch code (research D2).
- `docs/entity_model.md`: thêm TRADE_JOURNAL (NhatKyGiaoDich) + quan hệ; ghi chú Q1 về bảng `TradeJournalEntries` cũ nếu chủ sản phẩm chốt giữ/dẹp.
