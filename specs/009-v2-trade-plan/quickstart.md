# Quickstart / kiểm chứng: 009-v2-trade-plan

## Trước khi code (bước 0)

1. Kiểm nến hôm nay có được ghi vào lịch sử trong 09:00–14:45 không (log `EfMarketDataWriter`).
2. Kiểm đơn vị khối lượng lịch sử vs KBS `SessionVolume`. Khác → dừng và hỏi.

## Test tự động

```powershell
dotnet test backend/StockRadar.Tests --filter "FullyQualifiedName~KichBan"
dotnet test backend/StockRadar.Tests
```

Kịch bản cần có (gọi qua `Pha2TrongPhienRunner.RunAsync`, không chỉ hàm con):

- Spec US1: DCS ngoài vùng → không kích hoạt; quay về vùng → kích hoạt; biên vùng → kích hoạt; kế hoạch không hợp lệ → không kích hoạt.
- Spec US2: kế hoạch lưu đủ trường; `GiaVaoThucTe` = giá khớp; đổi ATR các phiên sau → `DungLoDuoi` không đổi.
- Spec US3: CL1 → bán nửa + dời cắt; Kiệt sức / Gãy nền → chỉ cảnh báo; thời gian / hạn tối đa đọc từ kế hoạch.
- Spec US4: test 5–12 của `docs/features/v2-pha2-trigger-fix/spec.md`.
- Hết phiên: dòng đang hình thành → Huỷ `HetHanVao`.
- Hồi quy: toàn bộ test V1 và luồng bán A/B/C hiện có.

## Migration

- Đọc lại file migration sinh ra trước khi apply (repo này `migrations add` hay sinh body sai).
- Chạy trên DB dev, kiểm 7 dòng mở có `GiaVaoThucTe` và `LaDuLieuBoSung = true`.

## Sau deploy (chủ sản phẩm tự ship)

```sql
SELECT Id, Symbol, LoaiKichBan, GiaVaoThucTe, ThoiGianKichHoat FROM KetQuaKichBan WHERE ThoiGianKichHoat >= '<ngày deploy>';
```

- SC-001: mọi `GiaVaoThucTe` nằm trong vùng của kế hoạch.
- Log có "bước bán: N vị thế" mỗi phút; tin mua mới đủ mục theo `contracts/telegram-mua.md`.
- DCS 3275: chạy UPDATE `HuyNgoaiVung` chỉ khi chủ sản phẩm duyệt lúc chạy.
