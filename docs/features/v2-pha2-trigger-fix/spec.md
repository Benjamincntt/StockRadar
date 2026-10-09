# V2 Pha 2 — Luồng bán chạy mọi lượt + sửa cò kích hoạt Nổ hướng lên / Quét thanh khoản

Trạng thái: **PHẦN 1 ĐÃ IMPLEMENT 2026-10-09** (luồng bán chạy mọi lượt). Chưa commit. Phần 2 chưa code.
Phạm vi: `Pha2TrongPhienRunner`, `MayNhanKichBanService` (phần kiểm cò), `KichBanNoHuongLen`, `KichBanQuetThanhKhoan`. Không đổi điều kiện bối cảnh / hình thái ở Pha 1, không đụng V1.
Phần 2 đổi cổng kịch bản nên theo constitution phải đi qua Spec Kit. Phần 1 là sửa lỗi, làm trước và ship riêng được.

## 0. Câu hỏi cần chủ sản phẩm chốt

| # | Câu hỏi | **Đã chốt** (chủ sản phẩm, 2026-10-09) |
|---|---|---|
| Q1 | Ship phần 1 (luồng bán) riêng, trước phần 2? | **Có.** |
| Q2 | Nến hôm nay dùng khi kiểm cò: dựng từ dữ liệu realtime hay bỏ hẳn? | **Dựng từ realtime** (bảng giá KBS), thay cho nến hôm nay có sẵn trong lịch sử (nếu có). |
| Q3 | Xử lý khối lượng trong cò kích hoạt thế nào? | **D — bỏ khối lượng khỏi điều kiện bắt buộc** của cò Nổ hướng lên và Quét thanh khoản. Khối lượng chỉ còn là điểm cộng khi xếp hạng (đã có sẵn trong tiêu chí "chất lượng trigger" của `XepHangCoHoiService`). Lý do: khối lượng cộng dồn trong phiên chưa chắc đạt ngưỡng; quy đổi tuyến tính kiểu V1 sai lệch vì khối lượng dồn ở đầu phiên và ATC. Các lựa chọn đã cân nhắc và loại: quy đổi tuyến tính (A), so cùng giờ các ngày trước (B, chưa có dữ liệu phút), đường phân bố chung (C). |
| Q4 | Giữ nguyên ngưỡng 1.5 lần / 2.0 lần? | **Giữ nguyên**, nhưng sau Q3 chỉ còn dùng để **hiển thị** trong bằng chứng (đạt / chưa đạt), không chặn kích hoạt. |
| Q5 | Nới điều kiện hình thái của Nổ hướng lên? | **Không.** |

## 1. Phần 1 — Luồng bán chạy ở mọi lượt Pha 2

### Lỗi (đã đối chiếu code ngày 2026-10-09)

`Pha2TrongPhienRunner.RunAsync` chỉ gọi `KiemTraSellAsync` ở cuối hàm (dòng 121). Trước đó có ba chỗ `return` sớm:

| Dòng | Điều kiện return |
|---|---|
| 66–70 | Hôm nay không có kịch bản đang hình thành |
| 76–80 | Không lấy được giá realtime cho các mã đang hình thành |
| 87–91 | Có kịch bản đang hình thành nhưng lượt này không cái nào kích hoạt |

→ Luồng bán (A, B, C: dừng lỗ, chốt lời, dừng lỗ đuổi theo, dừng lỗ theo thời gian, Kiệt sức / Gãy nền, MAE/MFE) **chỉ chạy đúng những phút có kịch bản mua vừa kích hoạt**. Đến ngày 09/10, production mới có 8 lần kích hoạt mua. Test A/B/C không bắt được vì chúng gọi thẳng `KiemTraSellAsync`.

### Sửa

- Tách `RunAsync` thành hai bước độc lập: **(1) kiểm mua**, **(2) kiểm bán**. Bước 2 chạy ở **mọi** lượt trong giờ giao dịch, kể cả khi bước 1 không có gì làm hoặc lỗi.
- Lỗi ở bước mua (ví dụ không lấy được giá) **không** được chặn bước bán: bắt lỗi bước mua, log, rồi vẫn chạy bước bán. Ngược lại cũng vậy.
- Giữ nguyên kiểm giờ / ngày nghỉ ở đầu hàm: ngoài giờ thì cả hai bước đều không chạy.
- `KiemTraSellAsync` tự lấy giá cho các mã đang giữ (đã làm như vậy), không phụ thuộc giá của bước mua.
- Thêm comment `DO-NOT-CHANGE` tại chỗ gọi bước bán: đặt lời gọi sau các `return` của bước mua thì luồng bán chỉ chạy khi có mua kích hoạt.

### Rủi ro khi bật

Luồng bán sẽ chạy thật lần đầu trên 8 vị thế đang mở. Trong lượt đầu có thể có cùng lúc: cảnh báo chưa đủ T+2.5, tin chạm dừng lỗ / chốt lời, tin hết hạn theo dõi (vị thế kích hoạt từ 05/10). Đây là hành vi đúng thiết kế, nhưng chủ sản phẩm cần biết trước.

### Test

1. Không có kịch bản đang hình thành → `KiemTraSellAsync` vẫn chạy (vị thế đang giữ chạm dừng lỗ → có tin).
2. Có kịch bản đang hình thành, không cái nào kích hoạt → bán vẫn chạy.
3. Bước mua ném lỗi (giả lập không lấy được giá cho mã mua) → bán vẫn chạy.
4. Ngoài giờ → không bước nào chạy.

## 2. Phần 2 — Sửa cò kích hoạt

### Lỗi

| # | Lỗi | Chỗ | Hậu quả |
|---|---|---|---|
| T1 | Nếu nến hôm nay đã có trong lịch sử, "đỉnh hộp 20 phiên" gồm luôn giá cao nhất hôm nay. Comment ghi "không gồm phiên hiện tại" nhưng code không loại. | `KichBanNoHuongLen.cs:140-142`, `MaxHigh` dòng 213–220 | Giá khớp không thể lớn hơn giá cao nhất của chính phiên đang chạy, nên **Nổ hướng lên không bao giờ kích hoạt** |
| T2 | So khối lượng cộng dồn từ đầu phiên với trung bình **cả ngày**, không quy đổi theo thời gian | `KichBanNoHuongLen.cs:144-146`, `KichBanQuetThanhKhoan.cs:155-157` | Đầu phiên gần như không thể đạt 1.5 / 2.0 lần |
| T3 | Trung bình khối lượng 20 phiên có thể gồm nến hôm nay đang dở | cả hai file trên | Mẫu số bị kéo xuống / lệch theo việc job đồng bộ có chạy hay không |
| T4 | RSI / MACD "hiện tại vs trước" tính trên lịch sử, không dùng giá hiện tại | `KichBanNoHuongLen.cs:149-152`, `KichBanHoiHoTro.cs:177-184` | Nếu nến hôm nay không có trong lịch sử thì hai chỉ báo đứng yên cả ngày; nếu có thì phụ thuộc lúc job đồng bộ chạy. Kết quả không ổn định. |

**Chưa xác nhận (bước 0 của người thực hiện):** nến hôm nay có thật sự được ghi vào `Stock.HistoryJson` trong giờ 09:00–14:45 không. Code ghi nến có ở `EfMarketDataWriter.cs:42-54` (gọi từ `OpportunityIntradayMonitorRunner`, `KbsMarketSyncRunner`, `DailySessionSyncRunner`), nhưng chưa kiểm job nào chạy trong phiên. Cách sửa dưới đây đúng **dù có hay không**, nên không chặn việc làm; chỉ cần ghi kết quả vào báo cáo.

**Cũng chưa xác nhận:** đơn vị khối lượng của lịch sử giá và của KBS `SessionVolume` có giống nhau (cổ phiếu hay lô) không. Nếu khác, mọi tỷ lệ khối lượng lệch một hằng số. Người thực hiện phải kiểm và báo; khác đơn vị thì **dừng và hỏi**, không tự quy đổi.

### Sửa (Q2, Q3 = D)

**Khối lượng (T2):** bỏ điều kiện khối lượng khỏi kết quả cò:
- Nổ hướng lên: `Dat = datVuotDinh && datMacd` (bỏ `datVolumeNo`).
- Quét thanh khoản: `Dat = datReclaim` (bỏ `datVolume`).
- **Giữ** bằng chứng khối lượng trong danh sách `BangChung` (tỷ lệ thực tế, ngưỡng 1.5 / 2.0 lần, đạt / chưa đạt) để hiển thị và để xếp hạng đọc. Ghi rõ trong `MoTa` hoặc `Nguong` là "tham khảo, không bắt buộc".
- **Không** thêm quy đổi khối lượng theo thời gian. Tỷ lệ hiển thị vẫn là khối lượng cộng dồn / trung bình 20 phiên **trước hôm nay** (T3).
- Comment `BUSINESS-RULE` tại dòng tính `Dat`: khối lượng không bắt buộc vì khối lượng cộng dồn trong phiên chưa phản ánh cả ngày (chốt 2026-10-09).

Lưu ý xếp hạng: điểm khối lượng trong `XepHangCoHoiService` dùng tỷ lệ chưa quy đổi, nên đầu phiên điểm này thấp. Chỉ ảnh hưởng thứ tự TopN tin Telegram, không chặn kích hoạt. Không sửa trong tài liệu này.

**Dữ liệu (T1, T3, T4):** chuẩn hoá như dưới đây.

Chuẩn hoá dữ liệu **một chỗ**, trong bước kiểm cò của `MayNhanKichBanService.KiemTraTriggerTrongPhienAsync`, trước khi gọi `KiemTraCoKichHoat` của từng kịch bản:

1. `lichSuTruoc` = các nến có `Date < hôm nay` (bỏ nến hôm nay nếu có).
2. `nenHomNay` = nến dựng từ bảng giá KBS: `Open` = giá mở cửa, `High` = max(giá cao nhất phiên, giá khớp), `Low` = min(giá thấp nhất phiên, giá khớp), `Close` = giá khớp, `Volume` = khối lượng phiên. Thiếu giá mở cửa / cao / thấp thì dùng giá khớp.
3. `lichSuChuan = lichSuTruoc + nenHomNay`. Mọi chỉ báo (RSI, MACD, EMA) tính trên `lichSuChuan`, nên "hiện tại" luôn là hôm nay theo giá khớp.
4. Các mốc so sánh lấy từ `lichSuTruoc`, **không** gồm hôm nay:
   - đỉnh hộp 20 phiên (Nổ hướng lên);
   - đáy nền (Quét thanh khoản; hàm này vốn đã bỏ 5 nến cuối, giữ nguyên cách đó trên `lichSuTruoc`);
   - trung bình khối lượng 20 phiên.
5. Tỷ lệ khối lượng (chỉ để hiển thị / xếp hạng) = khối lượng phiên / trung bình 20 phiên trước hôm nay. Không quy đổi theo thời gian.

Không đụng code quy đổi khối lượng của V1.

Mức giá kế hoạch (`TinhKeHoach`) tính trên `lichSuTruoc` + giá khớp như hiện tại, **không** đổi trong tài liệu này.

### Ảnh hưởng tới Hồi hỗ trợ

Hồi hỗ trợ dùng chung dữ liệu chuẩn hoá (T4), nên kết quả kiểm cò có thể khác trước. Đây là chủ đích (kết quả ổn định, không phụ thuộc job đồng bộ), nhưng phải có test hồi quy và ghi nhận số lần kích hoạt trước / sau khi deploy.

### Test

5. Nổ hướng lên: lịch sử có nến hôm nay với High = 105, đỉnh 20 phiên trước = 100, giá khớp 102, MACD tăng → **kích hoạt** (trước đây không thể).
6. Nổ hướng lên: giá khớp 99 < đỉnh 100 → không kích hoạt.
7. Nổ hướng lên: khối lượng chỉ 0.3 lần trung bình, giá và MACD đạt → **vẫn kích hoạt**; bằng chứng khối lượng ghi "chưa đạt (tham khảo)".
8. Trung bình khối lượng không gồm nến hôm nay: thêm nến hôm nay khối lượng rất nhỏ vào lịch sử → tỷ lệ hiển thị không đổi.
9. Quét thanh khoản: giá > đáy nền × 1.01, khối lượng 0.5 lần → **vẫn kích hoạt**.
10. RSI / MACD theo giá khớp: cùng lịch sử, giá khớp tăng → RSI hiện tại tăng; kết quả giống nhau dù lịch sử có hay không có nến hôm nay.
11. Hồi hỗ trợ: các test hiện có vẫn pass, hoặc nêu rõ test nào đổi kỳ vọng và vì sao.
12. Hồi quy: toàn bộ test V1 và test luồng bán A/B/C vẫn pass.

### Rủi ro (Q3 = D)

- Breakout không có khối lượng xác nhận dễ là breakout giả. Bỏ khối lượng khỏi điều kiện bắt buộc sẽ **tăng số lần kích hoạt** Nổ hướng lên / Quét thanh khoản, kèm tỷ lệ tín hiệu sai có thể cao hơn. Phương án B/C (so cùng giờ) đã cân nhắc nhưng chưa có dữ liệu phút.
- Theo dõi tỷ lệ thắng của hai kịch bản này qua Pha 3, tách theo bằng chứng khối lượng "đạt" / "chưa đạt", để quyết định có đưa khối lượng trở lại không.

## 2b. Chỉ mua khi giá nằm trong kịch bản (bổ sung 2026-10-09)

### Nguyên tắc (chủ sản phẩm)

Trước khi mua phải biết sẵn chốt ở đâu, cắt ở đâu. Lệnh ra chỉ kích hoạt khi chạm đúng mức đã định trong kịch bản. **Không** tính lại mục tiêu / điểm cắt sau khi đã thấy giá.

### Sự cố

DCS ngày 09/10, theo log production:
```
03:02:00  Kịch bản HoiHoTro TRIGGERED cho DCS — giá 700.00
03:02:03  Pha 2 SELL WARNING — ChamChotLoi1 DCS: con 3 phien.
```
Kế hoạch: giá vào 610.72 (vùng EMA20 ± 0.5%), dừng lỗ 483.68, chốt lời 1 = 700. Giá khớp lúc kích hoạt 700, cao hơn vùng vào khoảng 15%. Mua và "chạm chốt lời 1" xảy ra trong cùng một phút. Lãi/lỗ, MAE/MFE và kết quả Pha 3 của DCS đều tính theo giá vào 610.72 nên lệch khoảng 15%.

**Gốc:** `MayNhanKichBanService.KiemTraTriggerTrongPhienAsync` (dòng ~124–135) chỉ xét cò rồi tính kế hoạch. Không kiểm giá khớp có nằm trong vùng vào lệnh của kế hoạch không, cũng không kiểm kế hoạch còn "đường ra" không.

### Sửa

Trong `KiemTraTriggerTrongPhienAsync`, **sau** khi cò đạt và đã tính `keHoach = TinhKeHoach(history, gia)`, chỉ chuyển sang `DaKichHoat` khi **cả ba** điều kiện đúng:
1. `keHoach.GiaVaoLenhMin ≤ gia ≤ keHoach.GiaVaoLenhMax`: giá khớp nằm trong vùng vào của kịch bản.
2. `keHoach.GiaChotLoi1 > keHoach.GiaVaoLenhMax`: còn lãi tới mục tiêu.
3. `keHoach.GiaDungLo < keHoach.GiaVaoLenhMin`: điểm cắt nằm dưới giá vào.

Không đạt thì **không kích hoạt**: giữ trạng thái đang hình thành để lượt sau xét lại (giá quay về vùng trong ngày vẫn kích hoạt được). Log Debug lý do (ngoài vùng / không có đường ra). Không gửi tin.

- **Không** thêm ngưỡng tỷ lệ lãi/lỗ tuỳ chọn. Tỷ lệ đã cố định trong kế hoạch, tính từ chính các mức của kịch bản.
- **Không** tính lại kế hoạch theo giá khớp (hướng "b" đã cân nhắc và loại vì trái nguyên tắc).
- Comment `BUSINESS-RULE` tại chỗ kiểm: chỉ mua khi giá nằm trong vùng vào của kịch bản; mua ngoài vùng thì kế hoạch không còn đúng với giá mua thật (sự cố DCS 09/10).

### Ảnh hưởng theo kịch bản

| Kịch bản | Vùng vào lệnh | Ảnh hưởng |
|---|---|---|
| Hồi hỗ trợ | EMA20 ± 0.5% (không phụ thuộc giá khớp) | **Chặn thật**: giá đã chạy xa EMA20 thì không mua đuổi |
| Nổ hướng lên | `[giá khớp, giá khớp + 0.3×ATR]` | Điều kiện 1 luôn đạt (đúng bản chất breakout: mua lúc vượt). Điều kiện 2, 3 vẫn có tác dụng |
| Quét thanh khoản | giá khớp ± 0.3% | Như trên |

### Chỉ báo — đã đánh giá

- **RSI, MACD** không cho ra mức giá; chỉ dùng để lọc vào / báo thoát (đang dùng ở cò Hồi hỗ trợ và Kiệt sức).
- **Ichimoku** (Kijun-sen, mây Kumo) cho ra mức giá, có thể thay EMA/ATR để đặt dừng lỗ. **Chưa làm**: chưa có dữ liệu chứng minh tốt hơn. So sánh sau khi có đủ MAE/MFE (xem `v2-sell-trailing-stop` mục 9).

### Test

13. DCS: vùng vào 607.66–613.77, chốt lời 1 = 700, giá khớp 700, cò đạt → **không kích hoạt**.
14. Hồi hỗ trợ: giá khớp nằm trong vùng vào, chốt lời 1 > giá vào tối đa, dừng lỗ < giá vào tối thiểu → kích hoạt; kế hoạch lưu đúng như tính.
15. Giá ngoài vùng ở lượt 1, quay về vùng ở lượt 2 → lượt 2 kích hoạt.
16. Kế hoạch có chốt lời 1 ≤ giá vào tối đa → không kích hoạt, dù giá trong vùng.
17. Nổ hướng lên: giá khớp vượt đỉnh, kế hoạch hợp lệ → vẫn kích hoạt (không bị điều kiện 1 chặn).

### Dữ liệu sai đang có

Vị thế DCS (Id 3275) mở với giá vào 610.72, sai so với giá mua thật khoảng 700. **Đã xử lý tay trên production ngày 2026-10-09** (chủ sản phẩm duyệt):
- `ThoiGianThoatHet` = 2026-10-09 10:27 UTC, `LyDoThoatHet` = `HetHanTheoDoi`, `GiaThoatHet` để trống → Pha 2 không xét bán nữa, không gửi tin.
- `KetQuaDoLuong` = `Huy` → Pha 3 không đo (Pha 3 chỉ lọc `KetQuaDoLuong == null`, không xét `ThoiGianThoatHet`).
- `Huy` là giá trị ngoài Thắng / Thua / Ngang: màn `hieu-qua` không đếm vào tỷ lệ thắng, chỉ đếm vào tổng kích hoạt; lịch sử lệnh hiển thị "Chờ đo" (nhánh mặc định). Web / mobile không đọc giá trị thô.

## 3. File cần sửa

| File | Phần |
|---|---|
| `backend/StockRadar.Infrastructure/MarketData/Pha2TrongPhienRunner.cs` | 1; truyền dữ liệu bảng giá (mở cửa, cao, thấp) để dựng nến hôm nay cho phần 2 |
| `backend/StockRadar.Application/Services/MayNhanKichBanService.cs` | 2: chuẩn hoá lịch sử trước khi kiểm cò; 2b: chỉ kích hoạt khi giá trong vùng vào và kế hoạch có đường ra |
| `backend/StockRadar.Application/Services/KichBanNoHuongLen.cs` | 2: đỉnh hộp và trung bình khối lượng trên lịch sử trước hôm nay; bỏ khối lượng khỏi `Dat` |
| `backend/StockRadar.Application/Services/KichBanQuetThanhKhoan.cs` | 2: như trên |
| `backend/StockRadar.Tests/KichBan/` | Test mục 1 và 2 |
| `docs/domain/pipeline-jobs.md` | Chỉ sửa mô tả Pha 2: bán chạy mọi lượt; khối lượng không còn bắt buộc trong cò Nổ hướng lên / Quét thanh khoản |

**Không sửa:** điều kiện bối cảnh / hình thái (Pha 1), `TinhKeHoach`, luồng bán A/B/C (chỉ đổi chỗ gọi), V1 ngoài việc chuyển hàm dùng chung.

## 4. Kiểm sau deploy

- Phần 1: trong phiên, log phải có dòng của `KiemTraSellAsync` ở **mỗi** lượt Pha 2, không chỉ lượt có mua kích hoạt. Theo dõi tin bán của 8 vị thế đang mở.
- Phần 2: đếm số lần kích hoạt theo loại kịch bản mỗi ngày, so với trước deploy; theo dõi tỷ lệ thắng tách theo khối lượng đạt / chưa đạt (`SELECT LoaiKichBan, COUNT(*) FROM KetQuaKichBan WHERE ThoiGianKichHoat >= '<ngày>' GROUP BY LoaiKichBan`).

## 5. Không lặp lại

- Đặt bước độc lập (bán) **sau** các `return` sớm của bước khác (mua) thì bước độc lập bị tắt ngầm. Test gọi thẳng hàm con không bắt được. Phải có test gọi qua **hàm ngoài cùng** (`RunAsync`).
- So một đại lượng **cộng dồn trong phiên** với trung bình **cả ngày** là so lệch. Quy đổi tuyến tính cũng lệch vì khối lượng dồn đầu phiên và ATC. Muốn dùng làm điều kiện bắt buộc thì phải so cùng giờ các ngày trước (cần lưu dữ liệu phút).
- Mốc so sánh "N phiên trước" phải loại phiên đang chạy một cách tường minh, không dựa vào comment.
- Kiểm cò đạt **chưa đủ** để mua. Phải kiểm thêm giá khớp có nằm trong vùng vào của chính kế hoạch không, và kế hoạch còn đường ra không. Thiếu bước này thì mua và tín hiệu bán xảy ra cùng phút (VIB ở V1 ngày 07/10, DCS ở V2 ngày 09/10).
