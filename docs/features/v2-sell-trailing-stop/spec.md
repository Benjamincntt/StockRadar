# V2 — Ghi MAE/MFE + dừng lỗ đuổi theo bằng ATR theo loại kịch bản + dừng lỗ theo thời gian (phương án C)

Trạng thái: **ĐÃ IMPLEMENT + SỬA XONG 2026-10-09, đang ship.** Đã sửa lỗi quét dữ liệu cũ (mục 4.1). Migration đã chạy thử Up/Down trên DB dev. Chưa chạy live trong phiên. Mục 0 chốt theo cột "Mặc định" (Q1=20 phiên · Q2=k=2.5 · Q3=2 lượt, 14:30→3 lượt · Q4=`NoHuongLen`+`QuetThanhKhoan` · Q5=5 phiên / MFE<3%). Chưa commit. KBS có `HI`/`LO` nên các phiên sau phiên mua dùng High/Low trong phiên để cập nhật `DinhTuLucMua`/`Mfe`/`Mae`.
Điều kiện tiên quyết: phương án A ([`v2-sell-fix`](../v2-sell-fix/spec.md)) và B ([`v2-sell-plan-tracking`](../v2-sell-plan-tracking/spec.md)) đã ship.
Phạm vi: luồng bán V2 trong Pha 2 (realtime mỗi phút) và phần ghi số liệu cho Pha 3. Không đụng V1.
**Nguyên tắc chủ sản phẩm đặt ra:** mọi luật thoát đều chạy **realtime trong phiên** trên giá khớp. Không dùng luật nào chờ giá đóng cửa.
Đây là thay đổi pipeline V2, nên theo constitution phải đi qua Spec Kit.

## 0. Câu hỏi cần chủ sản phẩm chốt

| # | Câu hỏi | Mặc định đang ghi |
|---|---|---|
| Q1 | Theo dõi vị thế bao lâu? Hiện chỉ tới khi Pha 3 đo xong (~4 ngày lịch). Sau T+2.5 chỉ còn ~1 phiên, nên dừng lỗ đuổi theo và dừng lỗ theo thời gian gần như không có tác dụng. | **Theo dõi tới khi thoát hết hoặc tối đa `SoPhienTheoDoiToiDa = 20` phiên.** Pha 3 vẫn đo kết quả ở T+3 như hiện tại, để số liệu màn `hieu-qua` không đổi nghĩa. Xem mục 4. |
| Q2 | Hệ số k cho dừng lỗ đuổi theo bằng ATR | **k = 2.5**. Đây là giá trị ước lượng, chưa backtest. Cấu hình được. |
| Q3 | Số lượt quét liên tiếp phải nằm dưới mức dừng lỗ mới báo | **2 lượt** (~2 phút), giống V1 `SellConfirmationTicks`. Từ 14:30 trở đi (ATC) dùng **3 lượt**. |
| Q4 | Kịch bản nào dùng dừng lỗ đuổi theo? | Theo đà: **`NoHuongLen`, `QuetThanhKhoan`** → bật. Hồi về hỗ trợ: **`HoiHoTro`** → không bật, chỉ dùng dừng lỗ kế hoạch + dừng lỗ theo thời gian. Cấu hình được theo từng loại. |
| Q5 | Dừng lỗ theo thời gian: sau bao nhiêu phiên, giá phải đi được bao nhiêu? | Sau **5 phiên** kể từ kích hoạt mà **MFE < 3%** (chưa từng lãi tới 3%) → báo bán hết. Cấu hình được theo từng loại kịch bản. |

## 1. Cơ sở

| Nguồn | Điều rút ra | Áp vào đây |
|---|---|---|
| Kaminski & Lo, "When Do Stop-Loss Rules Stop Losses?" | Giá đi ngẫu nhiên thì cắt lỗ đơn giản luôn làm giảm lợi nhuận kỳ vọng. Cắt lỗ chỉ có lợi khi thị trường có quán tính. | Dừng lỗ đuổi theo chỉ bật cho kịch bản theo đà (Q4). |
| Han, Zhou & Zhu, "Taming Momentum Crashes" | Cắt lỗ 10% trên danh mục mua theo đà giảm mạnh tháng lỗ nặng nhất và tăng lợi nhuận trung bình. Số liệu trước phí giao dịch. | Ủng hộ cắt lỗ cho kịch bản theo đà. Lưu ý thanh khoản khi mã đang rơi. |
| Sweeney (MAE/MFE); bài MQL5 2025 về chọn dừng lỗ theo phân vị MAE | Ngưỡng dừng lỗ nên lấy từ phân bố MAE của **lệnh thắng**. Dưới ~100 lệnh thì số liệu rất nhiễu. | Ghi MAE/MFE ngay (mục 2). Chọn lại k, ngưỡng thời gian **sau khi đủ ~100 lệnh mỗi loại kịch bản**. |
| López de Prado, triple-barrier | Mỗi lệnh có 3 rào: chốt lời, cắt lỗ, hết thời gian. | Mô hình C chính là 3 rào này. Công cụ backtest / ML để sau. |

Các tham số mặc định ở mục 0 là **ước lượng**. Chưa có backtest hay dữ liệu MAE/MFE nào chứng minh chúng tối ưu.

## 2. Ghi MAE / MFE (làm đầu tiên, độc lập với phần còn lại)

`KetQuaKichBanEntity` đã có cột `Mfe`, `Mae` (`decimal(7,4)`, %) nhưng **chưa có code nào ghi vào** (grep ngày 2026-10-09).

Mỗi lượt Pha 2, với mọi vị thế đang theo dõi:
- `phanTram = (gia − giaVao) / giaVao × 100`
- `Mfe = max(Mfe ?? 0, phanTram)`; `Mae = min(Mae ?? 0, phanTram)`
- Chỉ ghi khi giá trị đổi, để tránh `SaveChanges` thừa.
- Sau khi vị thế **thoát hết** thì ngừng cập nhật. MAE/MFE là của khoảng thời gian **đang giữ**.

Không lấy giá cao / thấp nhất phiên của **phiên mua**, vì có thể xảy ra trước lúc mua. Các phiên sau thì được dùng giá cao / thấp nhất phiên từ bảng giá KBS nếu có, vì mỗi phút một lần có thể lỡ đỉnh / đáy. Người thực hiện phải kiểm bảng giá KBS có trả High/Low trong phiên không; không có thì chỉ dùng giá khớp.

## 3. Luật thoát mới (realtime, mỗi lượt Pha 2)

Thứ tự trong `KiemTraSellAsync`: các luật mức giá của B → **luật C** → Kiệt sức / Gãy nền. Mỗi mã, mỗi lượt chỉ phát **một** sự kiện mới.

### 3.1 Dừng lỗ đuổi theo bằng ATR (chỉ kịch bản theo đà — Q4)

- `DinhTuLucMua` = giá cao nhất từ lúc mua (cột mới, cập nhật mỗi lượt, cùng quy tắc mục 2).
- `ATR` = `IndicatorMath.Atr(history, 14)` trên **lịch sử giá ngày**, tính một lần mỗi mã mỗi phiên rồi giữ lại (không tính lại mỗi phút).
- `DungLoDuoi = DinhTuLucMua − k × ATR`, **chỉ được tăng, không bao giờ giảm** (lưu cột `DungLoDuoi`).
- **Dừng lỗ hiệu lực** = max(`GiaDungLo` của kế hoạch, giá vào nếu `DaDoiDungLo`, `DungLoDuoi`).
- Luật "chạm dừng lỗ" của B đổi thành so với **dừng lỗ hiệu lực**. Lý do thoát:
  - `DungLo` nếu mức cao nhất trong ba là dừng lỗ kế hoạch / dời về giá vào;
  - `DungLoDuoi` nếu mức cao nhất là `DungLoDuoi`.

### 3.2 Xác nhận chống nhiễu (Q3)

- Giá phải nằm ≤ dừng lỗ hiệu lực **N lượt quét liên tiếp** mới phát sự kiện. N = 2, từ 14:30 trở đi N = 3.
- Lưu số lượt liên tiếp trong **bộ nhớ** (theo mã, reset theo phiên), không ghi DB. Restart API thì đếm lại từ 0, chấp nhận được.
- Áp cho mọi luật dừng lỗ (kế hoạch, dời về giá vào, đuổi theo). **Không** áp cho chốt lời.

### 3.3 Dừng lỗ theo thời gian (Q5)

- Đủ `SoPhienDungTheoThoiGian` phiên kể từ kích hoạt, mà `Mfe < NguongMfeToiThieu` → báo **bán hết**, lý do `HetThoiGian`.
- Áp cho mọi loại kịch bản mua.
- Vị thế đã bán nửa (chốt lời 1 / Kiệt sức) thì không áp, vì kịch bản đã chạy đúng.

### 3.4 Biên độ giá

`k × ATR` có thể vượt biên độ ±7% (HOSE), khi đó dừng lỗ đuổi theo không bao giờ chạm trong một phiên. Không chặn; chỉ ghi log warning khi `k × ATR / giá > 7%`, để xem có cần giảm k cho mã biến động mạnh không.

### 3.5 T+2.5

Giữ nguyên quy tắc A/B: chưa đủ phiên thì chỉ gửi **một** cảnh báo mỗi loại sự kiện (thêm tên sự kiện mới vào `CanhBaoDaGui`), không đổi trạng thái vị thế.

## 4. Tách "theo dõi vị thế" khỏi "đo kết quả" (Q1)

Hiện Pha 2 chỉ xét bản ghi có `KetQuaDoLuong == null`. Pha 3 điền `KetQuaDoLuong` ở T+3, nên vị thế bị ngừng theo dõi.

Thay đổi:
- Pha 2 xét bản ghi `DaKichHoat` / `ChotLoi` / `HuyLenh`... **chưa thoát hết** (`ThoiGianThoatHet == null`) và **chưa quá `SoPhienTheoDoiToiDa` phiên** từ kích hoạt. **Bỏ** điều kiện `KetQuaDoLuong == null`.
  Lưu ý: Pha 3 đổi `TrangThai` sang `ChotLoi` / `HuyLenh`, nên điều kiện lọc **không** được dựa vào `TrangThai == DaKichHoat` nữa. Người thực hiện phải liệt kê chính xác điều kiện lọc mới và giải thích trong báo cáo.
- Quá `SoPhienTheoDoiToiDa` mà chưa thoát → báo **bán hết**, lý do `HetHanTheoDoi`. Đây chính là rào thời gian của triple-barrier.
- **Pha 3 không đổi**: vẫn đo ở T+3 với dữ liệu có tại thời điểm đó. Sự kiện thoát xảy ra **sau** khi Pha 3 đã đo thì **không** làm đo lại. Ghi chú vào `docs/domain/pipeline-jobs.md`.
- **Rủi ro:** số mã Pha 2 phải xét mỗi phút tăng (tối đa ~20 phiên vị thế thay vì ~4 ngày). Mỗi mã gọi `GetBySymbolAsync` mỗi phút. Phải đo thời gian chạy Pha 2 trước và sau.
  Hiện với kịch bản theo đà, runner gọi `GetBySymbolAsync` **hai lần** mỗi mã mỗi phút (một lần cho ATR, một lần cho Kiệt sức / Gãy nền). ATR đã cache theo phiên nên lần gọi cho ATR thừa từ phút thứ hai. Pha 2 chậm sau deploy thì sửa chỗ này trước.

### 4.1 Đóng im lặng dữ liệu cũ khi deploy (bổ sung 2026-10-09)

**Lỗi phát hiện khi review:** cột `ThoiGianThoatHet` mới có từ phương án B, nên mọi kịch bản kích hoạt từ tháng 9 đều có cột này trống. Bỏ điều kiện `KetQuaDoLuong == null` thì lượt Pha 2 đầu tiên sau deploy sẽ:
- bắn tin "hết hạn theo dõi — bán hết" cho mọi mã kích hoạt quá 20 phiên;
- theo dõi lại và bắn tin dừng lỗ / chốt lời cho mã đã được Pha 3 đo từ trước, kể cả khi người dùng đã tự bán.

Test không bắt được vì DB test chỉ có dữ liệu mới.

**Cách xử lý (chủ sản phẩm chọn):** migration `AddSellTrailingStopFields` thêm một câu `UPDATE` đóng **im lặng** (không gửi tin) mọi bản ghi đã kích hoạt, chưa thoát, **đã được Pha 3 đo trước lúc deploy** (`KetQuaDoLuong IS NOT NULL`), trừ `KietSuc` / `GayNen`:
- `ThoiGianThoatHet` = thời điểm chạy migration; `LyDoThoatHet` = `HetHanTheoDoi`; `GiaThoatHet` để trống (không có giá thật).
- `Down` **không** hoàn tác câu này, vì không phân biệt được dòng nào do migration đóng.
- Nghĩa là phương án C **chỉ áp cho vị thế chưa được đo tại thời điểm deploy**. Vị thế cũ coi như đã kết thúc theo cách đo của B.

Test hồi quy: mục 7, ca 12.

## 5. Dữ liệu và cấu hình

**Cột mới trên `KetQuaKichBanEntity`** (nullable):

| Cột | Kiểu | Ý nghĩa |
|---|---|---|
| `DinhTuLucMua` | `decimal(18,2)?` | Giá cao nhất từ lúc mua |
| `DungLoDuoi` | `decimal(18,2)?` | Mức dừng lỗ đuổi theo hiện tại, chỉ tăng |

`Mfe`, `Mae` đã có, chỉ cần ghi.

**Hằng số mới trong `SuKienBan`:** sự kiện `ChamDungLoDuoi`, `HetThoiGian`, `HetHanTheoDoi`; lý do thoát `LyDoDungLoDuoi`, `LyDoHetThoiGian`, `LyDoHetHanTheoDoi`. Kiểm `LyDoThoatHet` (`nvarchar(16)`) và `CanhBaoDaGui` (`nvarchar(64)`) còn đủ độ dài; không đủ thì nới trong migration.

**`Pha2Options`** (giá trị mặc định ở mục 0):
- `SoPhienTheoDoiToiDa = 20`
- `HeSoAtrDungLoDuoi = 2.5`
- `ChuKyAtr = 14`
- `SoLuotXacNhanDungLo = 2`; `SoLuotXacNhanDungLoAtc = 3`; `GioBatDauAtc = "14:30"`
- `KichBanDungLoDuoi = ["NoHuongLen", "QuetThanhKhoan"]`
- `SoPhienDungTheoThoiGian = 5`; `NguongMfeToiThieuPhanTram = 3`

Thêm các key tương ứng vào `appsettings.json`.

## 6. Pha 3

Lý do thoát mới (`DungLoDuoi`, `HetThoiGian`, `HetHanTheoDoi`) không cần xử lý riêng: Pha 3 đã chấm theo giá thoát với ngưỡng ±1% bất kể lý do (mục 4 của B).

## 7. Test bắt buộc

1. MFE/MAE: giá 100 → 104 → 97 → 101 → `Mfe = 4`, `Mae = −3`. Vị thế đã thoát hết thì không cập nhật nữa.
2. Phiên mua: giá cao nhất phiên (xảy ra trước lúc mua) **không** được tính vào `DinhTuLucMua` / `Mfe`.
3. `NoHuongLen`, ATR = 2, k = 2.5: đỉnh 110 → `DungLoDuoi = 105`. Giá về 112 rồi 108 → `DungLoDuoi` lên 107 và **không** giảm khi giá rơi.
4. `HoiHoTro`: cùng dữ liệu như ca 3 → `DungLoDuoi` không được tính, chỉ dùng dừng lỗ kế hoạch.
5. Giá ≤ dừng lỗ hiệu lực 1 lượt rồi hồi → không báo. 2 lượt liên tiếp → báo. Lúc 14:35 cần 3 lượt.
6. Dừng lỗ hiệu lực = max của ba mức: đã dời về giá vào 100 nhưng `DungLoDuoi` = 104 → chạm 104 là thoát, lý do `DungLoDuoi`.
7. Dừng lỗ theo thời gian: phiên thứ 5, `Mfe = 2.5` → bán hết lý do `HetThoiGian`. `Mfe = 3.5` → không. Đã bán nửa → không.
8. Vị thế đã được Pha 3 đo (`KetQuaDoLuong` có giá trị) mà chưa thoát hết → **vẫn** được Pha 2 xét. Pha 3 **không** đo lại.
9. Quá `SoPhienTheoDoiToiDa` → bán hết, lý do `HetHanTheoDoi`. Sau đó không xét nữa.
10. Chưa đủ T+2.5 mà chạm dừng lỗ đuổi theo → một cảnh báo, không đổi trạng thái.
11. Hồi quy: toàn bộ test A/B hiện có vẫn pass.
12. Dữ liệu cũ (mục 4.1): bản ghi kích hoạt 30 phiên trước đã đo, và 6 phiên trước đã đo, sau khi áp UPDATE của migration → **không** nhận tin nào. Bản ghi 2 phiên trước chưa đo → xử lý bình thường.

## 8. File cần sửa

| File | Việc |
|---|---|
| `backend/StockRadar.Infrastructure/MarketData/Pha2TrongPhienRunner.cs` | Mục 2, 3, 4 |
| `backend/StockRadar.Domain/Entities/KetQuaKichBanEntity.cs` | 2 cột mới |
| `backend/StockRadar.Domain/Constants/SuKienBan.cs` | Hằng số mới |
| `backend/StockRadar.Application/Options/Pha2Options.cs` + `backend/StockRadar.Api/appsettings.json` | Tham số mục 5 |
| `backend/StockRadar.Infrastructure/Notifications/V2TelegramFormatter.cs` | Tin cho 3 sự kiện mới; tin dừng lỗ ghi rõ đang dùng mức nào |
| `backend/StockRadar.Infrastructure/Migrations/` | Migration thêm 2 cột (nới độ dài cột nếu cần) |
| `backend/StockRadar.Tests/KichBan/` | Test mục 7 |
| `docs/domain/pipeline-jobs.md` | Chỉ sửa đúng mô tả luồng bán Pha 2 và ghi chú Pha 3 |

**Không sửa:** `KichBan*.cs`, cách tính `KeHoachGiaoDich`, `Pha3DoLuongRunner`, V1.

## 9. Sau khi chạy

- Sau ~100 lệnh mỗi loại kịch bản: lấy phân vị MAE của lệnh thắng để chọn lại `GiaDungLo` / k; lấy phân bố MFE để chọn lại chốt lời và ngưỡng dừng lỗ theo thời gian. Việc này là một tài liệu riêng.
- Công cụ backtest triple-barrier trên lịch sử giá ngày, và ML (meta-labeling): tài liệu riêng, sau bước trên.

## 10. Rủi ro

- Tham số mặc định là ước lượng. Có thể cắt quá sớm (k nhỏ) hoặc trả lại nhiều lãi (k lớn) cho tới khi chọn lại từ dữ liệu.
- Mã đang rơi thường kém thanh khoản: tin dừng lỗ đến đúng lúc vẫn có thể khớp thấp hơn mức dừng lỗ.
- Theo dõi lâu hơn thì Pha 2 phải xét nhiều mã hơn mỗi phút (mục 4).
- `TelegramNotifier` nuốt lỗi: gửi hỏng vẫn ghi là đã báo (rủi ro đã chấp nhận từ A).

## 11. Không lặp lại

- **Đổi điều kiện lọc của một job chạy trên bảng có sẵn dữ liệu thì phải hỏi: "lần chạy đầu tiên sau deploy sẽ chọn trúng những dòng cũ nào?"** Cột mới thêm ở đợt trước sẽ trống trên toàn bộ dữ liệu cũ. Điều kiện kiểu `CotMoi IS NULL` vì vậy chọn trúng hết.
- Test chỉ seed dữ liệu mới thì không bắt được lỗi này. Phải có ít nhất một test seed dữ liệu ở **trạng thái giống production trước deploy**.
- Với job gửi Telegram: thay đổi nào có thể làm tăng đột biến số tin thì phải có đường đóng im lặng dữ liệu cũ (migration) hoặc giới hạn số tin mỗi lượt.

## 12. Kiểm sau deploy

- Log khởi động `stockradar.service`: migration `AddSellTrailingStopFields` chạy xong, API lên bình thường.
- Đếm số dòng bị đóng bởi migration: `SELECT COUNT(*) FROM KetQuaKichBan WHERE LyDoThoatHet = 'HetHanTheoDoi' AND GiaThoatHet IS NULL`.
- Lượt Pha 2 đầu tiên trong phiên: số tin Telegram bán phải ở mức bình thường. Ra hàng loạt tin "hết hạn theo dõi" / "chạm dừng lỗ" là dấu hiệu dữ liệu cũ chưa được đóng hết.
- Theo dõi thời gian chạy Pha 2 mỗi lượt trong log; so với trước deploy.
