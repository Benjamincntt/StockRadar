# V2 — Theo dõi dừng lỗ / chốt lời trong phiên (phương án B)

Trạng thái: **ĐÃ IMPLEMENT + refinement 2026-10-08** — chưa commit/ship/chạy live.

Đáp án mục 0 đã chốt (tất cả chọn Mặc định):
- Q1: So bằng **giá khớp hiện tại** mỗi lượt quét.
- Q2: Chạm chốt lời 1 → báo **bán nửa**, dời **dừng lỗ lên giá vào** (`DaDoiDungLo = true`).
- Q3: Sau TP1, giá quay về giaVao → ChamDungLo → báo **bán hết** (`LyDoThoatHet = "DungLo"`).
- Q4: Theo dõi tới khi **thoát hết** hoặc **Pha 3 đo xong**, tuỳ cái nào đến trước.
- Q5: Pha 3 ưu tiên giá Pha 2: `GiaBanNua` + `GiaThoatHet` → TB hai giá. Chỉ có `GiaThoatHet` → dùng nó. Chỉ có `GiaBanNua` → TB với close. Không có gì → giữ cách cũ.
- Q6: Kiệt sức = bán nửa (`ThoiGianBanNua`), KHÔNG dời SL. Gãy nền = thoát hết (`ThoiGianThoatHet + LyDoThoatHet="GayNen"`). Chung bộ cột trạng thái.

Câu hỏi mở mục 7: **Drop 3 cột cũ trong cùng migration** — xác nhận.
Điều kiện tiên quyết: phương án A ([`v2-sell-fix`](../v2-sell-fix/spec.md)), đã commit `9abe799`. Tài liệu này dùng lại danh sách vị thế đang giữ, cách lấy giá vào, cách đếm phiên T+2.5 và cách chặn lặp của A.
Phạm vi: luồng bán V2 trong Pha 2 và cách Pha 3 chấm kết quả. Không đụng V1.
Đây là thay đổi pipeline V2, nên theo constitution phải đi qua Spec Kit.

## 0. Câu hỏi cần chủ sản phẩm chốt

| # | Câu hỏi | Mặc định đang ghi |
|---|---|---|
| Q1 | So mức dừng lỗ / chốt lời bằng giá nào? | **Giá khớp hiện tại** mỗi lượt quét. Không dùng giá thấp/cao nhất phiên, vì trong phiên mua giá đó có thể xảy ra **trước** lúc mua. |
| Q2 | Chạm chốt lời 1 thì làm gì? | Báo **bán nửa**, rồi **dời dừng lỗ lên giá vào** (hoà vốn). |
| Q3 | Sau chốt lời 1, giá quay về giá vào thì sao? | Báo **bán phần còn lại** (chạm dừng lỗ đã dời). |
| Q4 | Theo dõi vị thế bao lâu? | Tới khi vị thế **thoát hết** (dừng lỗ / chốt lời 2 / Gãy nền) **hoặc** Pha 3 đo xong, tuỳ cái nào đến trước. Pha 3 giữ mốc đo hiện tại (≥ 4 ngày lịch). |
| Q5 | Pha 3 chấm kết quả thế nào khi Pha 2 đã báo thoát? | Ưu tiên **giá báo thoát** Pha 2 đã ghi. Vị thế bán nửa rồi bán nốt thì lãi = trung bình hai lần thoát. Không có báo thoát thì giữ cách chấm hiện tại (giá đóng cửa). |
| Q6 | Hai luồng bán (theo mức giá của B, và Kiệt sức / Gãy nền của A) gặp nhau thế nào? | Dùng **chung một trạng thái vị thế** (mục 3). Kiệt sức = bán nửa, tương đương chốt lời 1 nhưng **không** dời dừng lỗ. Gãy nền = thoát hết. Vị thế đã thoát hết thì không báo gì thêm. |

## 1. Hiện trạng (code ngày 2026-10-08)

- Mỗi kịch bản mua tạo `KeHoachGiaoDich` lúc kích hoạt: `GiaVaoLenhMin/Max`, `GiaDungLo`, `GiaChotLoi1`, `GiaChotLoi2`, `DieuKienHuy` (`Domain/ValueObjects/KeHoachGiaoDich.cs`). Các mức này được in trong tin Telegram mua.
- **Trong phiên không có gì theo dõi các mức đó.** Chúng chỉ được Pha 3 dùng để chấm kết quả sau ≥ 4 ngày lịch, và chỉ so với **giá đóng cửa** của nến cuối (`Pha3DoLuongRunner.cs`). Giá có thể chạm dừng lỗ trong phiên rồi hồi lại mà Pha 3 không biết.
- Hai kịch bản bán bằng chỉ báo (Kiệt sức, Gãy nền) để các mức này bằng 0. Phương án A đã loại hai kịch bản này khỏi danh sách vị thế đang giữ.

## 2. Hành vi mong muốn

Mỗi lượt Pha 2, trong `KiemTraSellAsync`, **trước** bước đánh giá Kiệt sức / Gãy nền:

1. Với mỗi mã đang giữ, lấy kế hoạch của bản ghi mua mới nhất (giống A). Bỏ qua nếu `GiaDungLo <= 0` hoặc `GiaChotLoi1 <= 0`.
2. Lấy `gia` = giá khớp hiện tại (Q1).
3. Tính dừng lỗ đang hiệu lực: `DungLoHienTai = DaChotLoi1 ? GiaVao : GiaDungLo` (Q2).
4. Xét theo thứ tự, mỗi lượt quét chỉ phát **một** sự kiện mới cho mỗi mã:

| Điều kiện | Sự kiện | Hành động |
|---|---|---|
| `gia <= DungLoHienTai` và chưa thoát hết | `ChamDungLo` | Bán phần còn lại |
| `GiaChotLoi2 > 0`, `gia >= GiaChotLoi2`, đã chốt lời 1, chưa thoát hết | `ChamChotLoi2` | Bán phần còn lại |
| `gia >= GiaChotLoi1` và chưa chốt lời 1 | `ChamChotLoi1` | Bán nửa, dời dừng lỗ về giá vào |

   Nếu giá nhảy thẳng qua chốt lời 2 khi chưa chốt lời 1 thì phát `ChamChotLoi1` trước. Lượt quét kế tiếp mới phát `ChamChotLoi2`.
5. Đủ / chưa đủ T+2.5: dùng đúng quy tắc của A. Đủ phiên thì gửi tin bán. Chưa đủ thì gửi **một** tin cảnh báo cho mỗi loại sự kiện, ghi rõ còn mấy phiên.
6. **Chưa đủ phiên mà đã chạm mức:** chỉ ghi nhận là đã cảnh báo, **không** đổi trạng thái vị thế. Khi đủ phiên, nếu điều kiện vẫn đúng thì gửi tin bán thật một lần. Lý do: chưa bán được thì trạng thái "đã bán nửa / đã thoát" là sai sự thật.
7. Ghi trạng thái sau khi gửi Telegram. Lưu ý: `TelegramNotifier` nuốt lỗi, nên gửi hỏng vẫn ghi là đã báo, giống A.

Sau bước này mới chạy Kiệt sức / Gãy nền như A, cùng đọc và ghi chung trạng thái ở mục 3 (Q6).

## 3. Thay đổi dữ liệu

Trạng thái thoát gắn với **vị thế của một mã**. Theo cách gom của A, trạng thái được ghi lên **tất cả** bản ghi mua đang giữ của mã đó.

Thêm vào `KetQuaKichBanEntity` (nullable):

| Cột | Kiểu | Ý nghĩa |
|---|---|---|
| `GiaBanNua` | `decimal?` | Giá lúc báo bán nửa (chốt lời 1 hoặc Kiệt sức) |
| `ThoiGianBanNua` | `DateTime?` | Lúc báo bán nửa |
| `DaDoiDungLo` | `bool` (mặc định `false`) | Đã dời dừng lỗ về giá vào (chỉ khi bán nửa bằng chốt lời 1) |
| `GiaThoatHet` | `decimal?` | Giá lúc báo thoát hết |
| `ThoiGianThoatHet` | `DateTime?` | Lúc báo thoát hết |
| `LyDoThoatHet` | `string?` (≤ 16) | `DungLo` / `ChotLoi2` / `GayNen` |
| `CanhBaoDaGui` | `string?` (≤ 64) | Danh sách sự kiện đã gửi cảnh báo khi chưa đủ phiên, phân tách bằng dấu phẩy |

Các cột `ThoiGianBaoBan`, `LoaiBaoBan`, `ThoiGianCanhBaoBan` của A được **thay** bằng bộ cột trên. Phải chuyển dữ liệu cũ (một câu `UPDATE` trong migration) rồi drop cột cũ trong **cùng** migration. Cách này thay vì giữ song song hai bộ đánh dấu (xem câu hỏi mở ở mục 7).

## 4. Pha 3 chấm kết quả (Q5)

Trong `Pha3DoLuongRunner`, trước khi chấm theo giá đóng cửa:
- Có `GiaThoatHet` và có `GiaBanNua`: `giaThoat = (GiaBanNua + GiaThoatHet) / 2`.
- Chỉ có `GiaThoatHet`: `giaThoat = GiaThoatHet`.
- Chỉ có `GiaBanNua`: `giaThoat = (GiaBanNua + giá đóng cửa nến cuối) / 2`.
- Không có cột nào: giữ cách chấm hiện tại.

Phân loại Thắng / Thua / Ngang: dùng `giaThoat` ở trên với ngưỡng ±1% hiện có. `LyDoThoatHet = DungLo` → `Thua`, trừ khi dừng lỗ đã dời về giá vào (khi đó là `Ngang`, hoặc `Thang` nếu đã bán nửa có lãi ≥ 1%). Đổi `TrangThai` (`ChotLoi` / `HuyLenh`) vẫn chỉ do Pha 3 làm.

**Lưu ý:** đổi cách chấm sẽ làm số liệu màn `hieu-qua` trước và sau ngày deploy không so được với nhau. Cần ghi ngày áp dụng trong `docs/domain/pipeline-jobs.md`.

## 5. File cần sửa

| File | Việc |
|---|---|
| `backend/StockRadar.Infrastructure/MarketData/Pha2TrongPhienRunner.cs` | Thêm bước theo dõi mức giá trong `KiemTraSellAsync`; chuyển chặn lặp Kiệt sức / Gãy nền sang bộ cột mới |
| `backend/StockRadar.Infrastructure/MarketData/Pha3DoLuongRunner.cs` | Chấm theo mục 4 |
| `backend/StockRadar.Domain/Entities/KetQuaKichBanEntity.cs` | Cột mục 3 |
| `backend/StockRadar.Infrastructure/Migrations/` | Migration: thêm cột, chuyển dữ liệu, drop 3 cột của A |
| `backend/StockRadar.Infrastructure/Notifications/V2TelegramFormatter.cs` | Tin: chạm dừng lỗ / chốt lời 1 (kèm "dừng lỗ dời về X") / chốt lời 2; cảnh báo chưa đủ phiên |
| `backend/StockRadar.Tests/KichBan/` | Test mục 6 |
| `docs/domain/pipeline-jobs.md`, `docs/features/v2-scenario-engine/spec.md`, `docs/features/v2-sell-fix/spec.md` | Cập nhật |

**Không sửa:** điều kiện các kịch bản (`KichBan*.cs`), cách tính `KeHoachGiaoDich`, V1.

## 6. Test bắt buộc

1. Đủ phiên, giá ≤ dừng lỗ → tin "chạm dừng lỗ — bán hết", ghi `ThoatHet` lý do `DungLo`.
2. Đủ phiên, giá ≥ chốt lời 1 → tin bán nửa, `DaDoiDungLo = true`. Lượt sau giá = giá vào → tin bán phần còn lại.
3. Giá nhảy qua chốt lời 2 khi chưa chốt lời 1 → lượt 1 chỉ phát chốt lời 1, lượt 2 phát chốt lời 2.
4. Chưa đủ phiên, chạm dừng lỗ → 1 tin cảnh báo, trạng thái vị thế không đổi. Lượt sau vẫn chạm → không gửi thêm. Đủ phiên, vẫn chạm → 1 tin bán.
5. Đã thoát hết → Kiệt sức / Gãy nền kích hoạt → không gửi.
6. Kiệt sức bán nửa rồi giá về giá vào → **không** coi là chạm dừng lỗ (vì không dời dừng lỗ); chỉ chạm `GiaDungLo` gốc mới bán.
7. Pha 3: có `GiaBanNua` + `GiaThoatHet` → `GiaThoat` = trung bình hai giá. Không có → giữ kết quả như hiện tại (hồi quy).
8. Migration: bản ghi đã có `ThoiGianBaoBan` / `LoaiBaoBan = GayNen` trước migration → sau migration có `ThoiGianThoatHet`, lý do `GayNen`.

## 7. Rủi ro và câu hỏi mở

- **Giá vào = `GiaVaoLenhMin`** (giống Pha 3), trong khi người dùng có thể đã mua ở mức gần `GiaVaoLenhMax`. Dừng lỗ dời về "giá vào" có thể thấp hơn giá vốn thật.
- **Theo dõi bị ngắt khi Pha 3 đo xong** (≥ 4 ngày lịch). Vị thế còn mở sau mốc đó không còn được báo dừng lỗ / chốt lời. Nếu muốn giữ lâu hơn thì phải tách "đo kết quả" khỏi "theo dõi vị thế". Việc này nằm ở phương án C: [`v2-sell-trailing-stop`](../v2-sell-trailing-stop/spec.md) mục 4.
- **Quét mỗi phút với giá khớp** có thể bỏ lỡ một cú chạm nhanh rồi hồi lại giữa hai lượt quét. Chấp nhận được, vì người dùng cũng chỉ hành động theo tin nhắn.
- **Câu hỏi mở:** drop 3 cột của A trong cùng migration (mặc định ở mục 3), hay giữ song song một thời gian để dễ quay lui?
