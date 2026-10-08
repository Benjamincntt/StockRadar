# V2 — Sửa luồng bán trong Pha 2 (phương án A)

Trạng thái: **ĐÃ COMMIT 2026-10-08 (`9abe799`)**. Chưa xác nhận deploy, chưa chạy live trong phiên. Bài học ở mục 8.

> **Superseded**: 3 cột `ThoiGianBaoBan` / `LoaiBaoBan` / `ThoiGianCanhBaoBan` đã được thay bằng bộ 7 cột trạng thái vị thế của phương án B ([`v2-sell-plan-tracking`](../v2-sell-plan-tracking/spec.md)). Migration `AddSellPositionTracking` chuyển dữ liệu rồi drop 3 cột cũ cùng lúc. Logic chặn lặp trong `KiemTraSellAsync` dùng cột mới.

Đáp án mục 0 đã chốt:
- Q1: Đủ phiên = `TradingSessionsBetween(ThoiGianKichHoat → hôm nay) >= 3` (them `MinTradingSessionsToSell = 3` vào Pha2Options + appsettings).
- Q2: Chưa đủ phiên → gửi MỘT tin “Cảnh báo — chưa bán được, còn N phiên”. Không lặp.
- Q3: Thêm 3 cột nullable `ThoiGianBaoBan`, `LoaiBaoBan`, `ThoiGianCanhBaoBan` vào KetQuaKichBanEntity. Không đổi TrangThai.
- Q4: Đánh giá mỗi mã 1 lần/lượt. Giá vào từ bản ghi mua mới nhất. Đánh dấu cho tất cả bản ghi đang giữ.

Phạm vi: chỉ luồng bán V2 trong `Pha2TrongPhienRunner.KiemTraSellAsync`. Không đổi điều kiện của hai kịch bản bán Kiệt sức / Gãy nền, không đụng V1.
Đây là thay đổi pipeline V2, nên theo constitution phải đi qua Spec Kit.

## 0. Câu hỏi cần chủ sản phẩm chốt

| # | Câu hỏi | Mặc định đang ghi |
|---|---|---|
| Q1 | Điều kiện "được bán": đủ bao nhiêu phiên kể từ `ThoiGianKichHoat`? | Dùng cùng quy tắc với V1 (`MasterAlertOptions.MinTradingSessionsToSell`), đưa vào `Pha2Options` thành tham số riêng, giá trị mặc định bằng V1. |
| Q2 | Trước khi đủ điều kiện bán mà kịch bản bán đã kích hoạt: gửi gì? | Gửi **một** tin "Cảnh báo — chưa bán được", ghi rõ còn mấy phiên. Không lặp lại. |
| Q3 | Đánh dấu "đã bắn tin bán" bằng gì? | Thêm cột mới trên `KetQuaKichBan`, **không** đổi `TrangThai`. Lý do: Pha 3 đang tìm bản ghi `DaKichHoat` để đo kết quả; nếu đổi trạng thái thì Pha 3 bỏ sót. |
| Q4 | Một mã có nhiều bản ghi mua đang giữ (nhiều ngày hoặc nhiều kịch bản): bắn tin bán thế nào? | Đánh giá bán **một lần cho mỗi mã** mỗi lượt quét. Giá vào lấy từ bản ghi mua **mới nhất**. Tin bán đánh dấu cho **tất cả** bản ghi đang giữ của mã đó. |

## 1. Lỗi hiện tại (đã đối chiếu code ngày 2026-10-08)

| # | Lỗi | Chỗ | Hậu quả |
|---|---|---|---|
| L1 | Danh sách mã đang giữ lọc `NgayDanhGia == ngayDanhGia` | `Pha2TrongPhienRunner.cs:264-268` | Chỉ xét mã mua **trong hôm nay**, đúng lúc chưa bán được (T+2.5). Mã mua từ hôm trước **không bao giờ** được xét bán. |
| L2 | Truyền giá hiện tại làm giá vào lệnh: `DanhGiaBanAsync(symbol, stock.History, gia, ct)` | `Pha2TrongPhienRunner.cs:288`, tham số thứ 3 là `giaVaoLenh` (`IMayNhanKichBan.cs:33-36`) | Kiệt sức tính lãi từ giá vào ≈ 0%, không bao giờ vượt `MinGainFromEntry = 10%`. Vì vậy **Kiệt sức không bao giờ kích hoạt**. |
| L3 | Bắn tin xong không ghi lại gì | `Pha2TrongPhienRunner.cs:303-311` | Pha 2 chạy mỗi phút. Điều kiện bán còn đúng thì tin bán bắn **lặp mỗi phút**. Chưa quan sát trên prod, chỉ suy từ code. |
| L4 | Không kiểm tra đã đủ phiên để bán chưa | cả hàm | Tin "BÁN" có thể gửi khi thực tế chưa bán được. |

Ghi chú: `TrangThaiKichBan.DangGiu` và `ThoatLenh` đã có trong enum nhưng không chỗ nào dùng. Tài liệu này **không** dùng chúng (xem Q3).

## 2. Hành vi mong muốn

Mỗi lượt Pha 2 (mỗi phút, 09:00–14:45):

1. **Lấy vị thế đang giữ:** các bản ghi `KetQuaKichBan` có
   - `TrangThai == DaKichHoat`
   - `LoaiKichBan` khác `KietSuc` và `GayNen`
   - `ThoiGianKichHoat != null`
   - `KetQuaDoLuong == null` (Pha 3 chưa đo)
   - **bỏ** điều kiện `NgayDanhGia == ngayDanhGia`.
2. **Gom theo mã** (Q4). Lấy giá vào từ `KeHoachGiaoDichJson.GiaVaoLenhMin` của bản ghi mới nhất, giống cách Pha 3 lấy giá vào (`Pha3DoLuongRunner.cs:67-76`). Bản ghi không giải mã được hoặc `GiaVaoLenhMin <= 0` thì bỏ qua và log warning.
3. **Gọi** `DanhGiaBanAsync(symbol, history, giaVao, ct)`, truyền **giá vào**, không truyền giá hiện tại.
4. Kiểm tra trigger như cũ (`KiemTraTriggerTrongPhienAsync`).
5. Với mỗi tín hiệu bán đã kích hoạt:
   - Nếu mã đã có đánh dấu cho đúng loại bán này (Q3) thì **bỏ qua**.
   - Nếu **đủ điều kiện bán** (Q1): gửi `V2TelegramFormatter.FormatBan` như hiện tại.
   - Nếu **chưa đủ**: gửi tin cảnh báo (Q2). Thêm dòng "Chưa đủ T+2.5 — còn N phiên" vào formatter, không tạo formatter mới.
   - Ghi đánh dấu **sau khi** gửi Telegram thành công. Gửi lỗi thì không ghi, để lượt sau thử lại.

Nếu chưa đủ điều kiện bán và đã gửi cảnh báo, lúc đủ điều kiện mà tín hiệu vẫn còn thì **được** gửi tin bán chính thức một lần. Cảnh báo và tin bán đánh dấu riêng.

## 3. Thay đổi dữ liệu (theo Q3)

Thêm vào `KetQuaKichBanEntity` (nullable, không có default):

| Cột | Kiểu | Ý nghĩa |
|---|---|---|
| `ThoiGianBaoBan` | `DateTime?` | Lần đầu gửi tin **bán chính thức** |
| `LoaiBaoBan` | `LoaiKichBan?` | Kịch bản bán đã bắn (`KietSuc` / `GayNen`) |
| `ThoiGianCanhBaoBan` | `DateTime?` | Lần đầu gửi **cảnh báo chưa bán được** |

Quy tắc chặn lặp:
- Đã có `ThoiGianBaoBan` thì không gửi tin bán nữa, **trừ khi** tin cũ là Kiệt sức (bán 50%) và tin mới là Gãy nền (bán 100%). Khi đó gửi thêm một lần và ghi đè `LoaiBaoBan = GayNen`.
- Đã có `ThoiGianCanhBaoBan` thì không gửi cảnh báo nữa.

Migration EF Core: **đọc lại file migration sinh ra trước khi apply**, vì `dotnet ef migrations add` trong repo này hay sinh body sai. Dùng skill `efcore-migration-review`.

## 4. File cần sửa

| File | Việc |
|---|---|
| `backend/StockRadar.Infrastructure/MarketData/Pha2TrongPhienRunner.cs` | Viết lại `KiemTraSellAsync` theo mục 2 |
| `backend/StockRadar.Domain/Entities/KetQuaKichBanEntity.cs` | Thêm 3 cột mục 3 |
| `backend/StockRadar.Infrastructure/Persistence/ApplicationDbContext.cs` | Cấu hình cột nếu cần |
| `backend/StockRadar.Infrastructure/Migrations/` | Migration mới |
| `backend/StockRadar.Application/Options/Pha2Options.cs` + `appsettings.json` | Tham số số phiên tối thiểu để bán (Q1) |
| `backend/StockRadar.Infrastructure/Notifications/V2TelegramFormatter.cs` | Thêm dòng "chưa bán được" cho tin cảnh báo |
| `backend/StockRadar.Tests/KichBan/Pha2TrongPhienTests.cs` | Test mục 5 |
| `docs/domain/pipeline-jobs.md`, `docs/features/v2-scenario-engine/spec.md` | Cập nhật luồng bán Pha 2 |

**Không sửa:** `KichBanKietSuc`, `KichBanGayNen` (điều kiện kịch bản), `Pha3DoLuongRunner`, toàn bộ V1.

## 5. Test bắt buộc

1. Mã mua **hôm trước** (`NgayDanhGia` < hôm nay), `DaKichHoat`, chưa đo → được xét bán. Hồi quy cho L1.
2. Kiệt sức nhận đúng **giá vào lệnh**: giá vào 10, giá hiện tại 11.5, các điều kiện khác đạt → kích hoạt. Hồi quy cho L2.
3. Gọi Pha 2 hai lượt liên tiếp, điều kiện bán vẫn đúng → Telegram chỉ nhận **1** tin. Hồi quy cho L3.
4. Chưa đủ số phiên → gửi tin cảnh báo (không phải tin bán), chỉ một lần. Khi đủ phiên → gửi tin bán một lần.
5. Đã báo Kiệt sức, sau đó Gãy nền kích hoạt → gửi thêm đúng 1 tin. Ngược lại (đã Gãy nền, Kiệt sức) → không gửi.
6. Bản ghi đã có `KetQuaDoLuong` → không xét bán.
7. Telegram ném lỗi → không ghi đánh dấu; lượt sau gửi lại.

## 6. Tiêu chí hoàn thành
- `dotnet build` sạch; `dotnet test --filter "FullyQualifiedName~KichBan"` xanh, gồm 7 ca mục 5.
- Đã đọc lại migration và xác nhận chỉ thêm 3 cột nullable.
- Báo cáo kèm: file đã sửa, lệnh test + kết quả, phần chưa kiểm được (chưa chạy live trong phiên, chưa xác nhận L3 trên log prod).

## 7. Rủi ro
- Bỏ lọc theo ngày thì số mã cần xét bán tăng (mọi vị thế đang giữ chưa đo, tối đa ~4 ngày). Mỗi mã gọi `GetBySymbolAsync` một lần mỗi phút. Theo dõi thời gian chạy Pha 2 sau deploy.
- Khi bật, Kiệt sức bắt đầu kích hoạt lần đầu (trước đây không bao giờ). Số tin bán V2 sẽ tăng, có thể bao gồm các vị thế cũ đang giữ.
- `TelegramNotifier.SendAsync` nuốt lỗi (chỉ log, không ném). Gửi hỏng vẫn bị ghi là đã báo → tin bán đó mất, không gửi lại. Chủ sản phẩm chấp nhận rủi ro này (2026-10-08). Test TC7 chỉ đúng với notifier ném lỗi, không phản ánh notifier thật.
- Mã đã có tin bán rồi mà có lệnh mua mới: các bản ghi cũ còn đánh dấu nên vị thế mới không nhận tin bán cho tới khi Pha 3 đo xong bản ghi cũ (~4 ngày).
- Kiệt sức và Gãy nền kích hoạt cùng một lượt quét → gửi 2 tin liền nhau (đúng quy tắc nâng cấp).

## 8. Không lặp lại

| Lỗi | Vì sao lọt | Chặn lại bằng |
|---|---|---|
| Truyền giá hiện tại vào tham số `giaVaoLenh` → Kiệt sức không bao giờ kích hoạt | Hai tham số cùng kiểu `decimal` nên compiler không bắt được. Không có test nào chạy Pha 2 với giá vào khác giá hiện tại. | Comment `DO-NOT-CHANGE` tại dòng gọi + test `TC2_KietSuc_NhanDungGiaVaoLenh` |
| Lọc vị thế theo `NgayDanhGia == hôm nay` → chỉ xét mã chưa được bán | Viết luồng bán mà không đối chiếu với quy tắc T+2.5 | Comment `DO-NOT-CHANGE` tại câu lọc + test `TC1_MaMuaHomTruoc_DuocXetBan` |
| Bắn lặp mỗi phút | Job chạy mỗi phút nhưng không lưu "đã gửi" | Cột đánh dấu (nay là bộ cột trạng thái của B) + test `TC3_HaiLuotLienTiep_ChanTinBanDuoc1Lan` |
| Test gửi lỗi (`TC7`) pass nhưng production không như vậy | Fake notifier ném lỗi, còn `TelegramNotifier` thật thì nuốt lỗi | Đã ghi ở mục 7. Viết test có dùng notifier giả thì phải đối chiếu hành vi với notifier thật. |

**Quy tắc chung cho mọi luồng bán (V1 và V2):**
- Có test cho trường hợp **vừa mua xong**: chưa đủ T+2.5 thì không được gửi tin "BÁN".
- Có test chạy **hai lượt quét liên tiếp** cùng điều kiện: phải chỉ ra đúng một tin.
- Hàm nhận nhiều tham số `decimal` liền nhau (giá vào, giá hiện tại…): gọi bằng **tham số có tên** (`giaVaoLenh: giaVao`) để đọc là thấy ngay.
- **AI hay người sửa tài liệu:** chỉ thêm hoặc sửa đúng mục mình phụ trách, không viết đè cả file. Ngày 2026-10-08 mục này từng bị xoá mất khi một lượt implement ghi lại toàn bộ file.
