# Research: 009-v2-trade-plan

Khảo sát code ngày 2026-10-14 (đọc code, không chạy). Đường dẫn tính từ `backend/`.

## R1. Kế hoạch lưu ở đâu, mở rộng thế nào

- Hiện trạng: record `KeHoachGiaoDich` (`StockRadar.Domain/ValueObjects/KeHoachGiaoDich.cs:7`) chỉ có `GiaVaoLenhMin/Max`, `GiaDungLo`, `GiaChotLoi1/2`, `DieuKienHuy`, `TyLeLaiLo` (tính sẵn từ `GiaVaoLenhMin`). Ghi vào `KeHoachGiaoDichJson` ở `Pha1TruocPhienRunner.cs:103,121` và `Pha2TrongPhienRunner.cs:241`.
- **Quyết định:** thêm property `init` vào record (hạn vào, tỷ lệ bán CL1, dời dừng lỗ, có đuổi + khoảng cách, tham số thời gian, hạn tối đa, `LaDuLieuBoSung`). JSON cũ thiếu trường thì đọc ra giá trị mặc định → cần migration dữ liệu (R7) chứ không dựa vào mặc định ngầm.
- **Lý do:** đúng mục 5 tài liệu; không đổi cột.
- **Đã loại:** tách bảng kế hoạch riêng — thêm join, không có lợi ích.
- `TyLeLaiLo` đổi công thức sang dùng `GiaVaoLenhMax` (mục 1 tài liệu). Ảnh hưởng hiển thị `HieuQuaKichBanService` và xếp hạng nếu có đọc → kiểm lúc implement.

## R2. Ai điền các trường mới

- `TinhKeHoach(history, giaHienTai)` của 3 kịch bản (`KichBanHoiHoTro.cs:225`, `KichBanNoHuongLen.cs:188`, `KichBanQuetThanhKhoan.cs:199`) đã có ATR cục bộ.
- **Quyết định:** giữ công thức mức giá; điền các trường quy tắc ở **một chỗ** trong `MayNhanKichBanService.KiemTraTriggerTrongPhienAsync` ngay sau `TinhKeHoach` (dòng 130), từ `Pha2Options` + ATR lúc đó. `KhoangCachDuoi = HeSoAtrDungLoDuoi × ATR(ChuKyAtr)` chỉ cho loại trong `KichBanDungLoDuoi`.
- **Đã loại:** điền trong từng `TinhKeHoach` — lặp 3 lần, và Pha 1 cũng gọi `TinhKeHoach` (kế hoạch nháp) nên sẽ chốt quy tắc quá sớm.

## R3. Kiểm vùng vào và kế hoạch hợp lệ

- Hiện trạng: `MayNhanKichBanService.cs:124-136` cò đạt → tính kế hoạch → `DaKichHoat`. Không kiểm vùng.
- **Quyết định:** sau dòng 130, nếu kế hoạch không hợp lệ (FR-002) → không kích hoạt, ghi bằng chứng `KeHoachKhongHopLe`; nếu giá ngoài `[Min, Max]` → không kích hoạt, giữ đang hình thành, log Debug. Biên tính là trong vùng.
- Q5 chốt "không vào trạng thái chờ vào". Vì kế hoạch tính lại theo `history` mỗi lượt (trước khi mua), lượt sau có thể hợp lệ → **không** huỷ hẳn, chỉ không kích hoạt lượt đó; cuối phiên mới huỷ (R4) với lý do cuối cùng ghi nhận. *Cần chủ sản phẩm xác nhận cách hiểu này lúc duyệt plan.*

## R4. Hết hạn vào cuối phiên

- Hiện trạng: **không có** logic huỷ kịch bản đang hình thành. Pha 2 chỉ lọc `NgayDanhGia == hôm nay` (`Pha2TrongPhienRunner.cs:92`); dòng cũ nằm im.
- **Quyết định:** cuối phiên (lượt Pha 2 cuối, hoặc Pha 3 16:00) đánh dấu các dòng đang hình thành của hôm nay là huỷ, `LyDoThoatHet = HetHanVao`, `KetQuaDoLuong = Huy`. Chọn **Pha 3 16:00** vì chạy một lần, chắc chắn sau giờ khớp; plan chi tiết ở tasks.
- Dòng đang hình thành của các ngày cũ: không động (ngoài phạm vi, đã nằm im).

## R5. Luồng bán đọc từ kế hoạch

`Pha2TrongPhienRunner.KiemTraSellAsync` (292-678):

| Chỗ | Hiện tại | Đổi |
|---|---|---|
| 341-356 giá vào | `GiaVaoLenhMin` | `GiaVaoThucTe ?? GiaVaoLenhMin` |
| 361-376 hạn 20 phiên | `cfg.SoPhienTheoDoiToiDa` | `keHoach.SoPhienToiDa` |
| 407-435 dừng lỗ đuổi | ATR tính lại mỗi phiên (`GetOrComputeAtr`) | `DinhTuLucMua − keHoach.KhoangCachDuoi` nếu `CoDungLoDuoi`; bỏ gọi ATR |
| 447-450 mức cắt hiệu lực | max(SL, entry nếu `DaDoiDungLo`, `DungLoDuoi`) | giữ; entry = giá vào thực tế; dời chỉ khi `keHoach.DoiDungLoVeGiaVao` |
| 498-505 dừng theo thời gian | `cfg` | tham số từ kế hoạch |
| 558-565 bán nửa CL1 | cứng 50% | ghi tỷ lệ từ kế hoạch (hiện chỉ ghi giá, không ghi khối lượng → tỷ lệ chỉ dùng cho tin và Pha 3) |
| 590-676 Kiệt sức / Gãy nền | Kiệt sức bán nửa, Gãy nền thoát hết | chỉ gửi cảnh báo kèm mức cắt hiệu lực, chống gửi lặp bằng `CanhBaoDaGui` |

- **Chạm CL1 và mức cắt cùng lượt (edge case spec):** code hiện tại kiểm mức cắt trước (452), CL1 ở nhánh `else` → mức cắt thắng. **Giữ nguyên** (thận trọng). Lưu ý: mức cắt cần 2 lượt xác nhận liên tiếp (3 lượt sau 14:30), CL1 không cần → giữ nguyên, không thuộc phạm vi.
- `Pha2Options` giữ các khoá cũ làm **nguồn giá trị lúc lập kế hoạch**; không xoá.

## R6. Pha 3 và hiệu quả

- `Pha3DoLuongRunner.cs:69-75` và `HieuQuaKichBanService.cs:184` dùng `GiaVaoLenhMin` → đổi sang `GiaVaoThucTe ?? GiaVaoLenhMin`.
- Tách nhóm bổ sung (SC-006): đọc `keHoach.LaDuLieuBoSung`.

## R7. Dữ liệu và migration

- Cột mới `GiaVaoThucTe decimal(18,2) NULL` trên `KetQuaKichBan`. Migration mới sau `20261009031049_AddSellTrailingStopFields`.
- **Ràng buộc:** `LyDoThoatHet` MaxLength **16** (`ApplicationDbContext.cs:485-513`). `KeHoachKhongHopLe` dài 17 ký tự → **không lưu vào `LyDoThoatHet`**; chỉ ghi trong bằng chứng (đúng Q5). `HetHanVao` (9), `HuyNgoaiVung` (12) vừa.
- Bổ sung 7 vị thế đang mở: SQL trong migration cập nhật `GiaVaoThucTe = GiaVaoLenhMin` (từ JSON) và chèn trường mặc định vào JSON với `LaDuLieuBoSung = true`. `KhoangCachDuoi` cho vị thế theo đà = khoảng cách hiện đang dùng (`DinhTuLucMua − DungLoDuoi`) nếu có, để không đổi mức cắt đang chạy. 7 vị thế hiện đều Hồi hỗ trợ → nhánh này có thể không có dòng nào.
- Nhớ bộ nhớ dự án: `dotnet ef migrations add` sinh body sai trong repo này → đọc lại file migration trước khi apply.
- DCS 3275: script UPDATE riêng (FR-018), không nằm trong migration.

## R8. Cò kích hoạt (phần 2 `v2-pha2-trigger-fix`)

Giữ nguyên thiết kế đã chốt ở `docs/features/v2-pha2-trigger-fix/spec.md` mục 2. Hai điểm chưa xác nhận (nến hôm nay có trong lịch sử trong phiên không; đơn vị khối lượng) là bước 0 của implement; khác đơn vị → dừng và hỏi.

### Kết quả kiểm bước 0 (T001–T002, chạy 2026-10-19 trên code disk + DB dev + gọi lại KBS trực tiếp)

**T001 — Nến hôm nay CÓ được ghi vào `Stock.HistoryJson` trong 09:00–14:45.** Chuỗi ghi: `KbsMarketSyncJob` (mỗi 60s, chặn ngoài giờ bằng `VietnamMarketCalendar.IsMarketOpen()`) và `DailySessionSyncJob` — Job 2 (mỗi 5 phút trong giờ GD, `IntervalMinutes=5`) → `MarketSyncService.ApplyAsync` → `EfMarketDataWriter.UpsertQuotesAsync` (file đã chuyển vị trí: `StockRadar.Infrastructure/Persistence/Repositories/EfMarketDataWriter.cs:20-73`) merge bar có `date = TodayVietnam()` (Open/High/Low/Close/Volume lấy từ bảng giá KBS) vào `HistoryJson` qua `MergeBars` + `ApplyHistory`. Hệ quả cho US4: lịch sử truyền vào `KiemTraCoKichHoat` (`MayNhanKichBanService.cs:119-124`, đọc `stock.History`) đã gồm nến hôm nay đang chạy — đúng lỗi "đỉnh hộp gồm luôn phiên hiện tại" mà phần 2 phải loại.

**T002 — Đơn vị khối lượng GIỐNG NHAU (cổ phiếu), không cần quy đổi, không dừng.** Bằng chứng:
- Lịch sử: `KbsHistoryClient.ParseBar` đọc `v` thô, không scale (`KbsHistoryClient.cs:143`); DB dev 30/9: HPG `volume` 18.777.000–24.184.800; STB 2.221.800; VCB 3.824.000.
- Bảng giá realtime: `KbsPriceBoardClient` đọc `TT` thô, chỉ giá mới scale `/1000` (`KbsPriceBoardClient.cs:62-90`); gọi KBS trực tiếp 19/10: HPG TT=13.224.600, STB TT=2.180.600, VCB TT=4.165.400 — cùng bậc khối lượng với lịch sử của chính các mã đó.
- Nến hôm nay trong HistoryJson do Job 2/sync ghi chính bằng `SessionVolume` (`DailySessionSyncRunner.cs:78-79`), nên hai nguồn là một trường KBS.
→ `volumeHienTai` (Pass vào cò) so với trung bình `volume` lịch sử là so cùng đơn vị. US4 chỉ phải loại phiên hôm nay khỏi mốc so sánh, không đổi đơn vị.

**Chưa kiểm được:** đối chiếu log production cho T001 (phiên dev không mở SSH production); kết luận dựa trên code path được scheduler gọi — đủ chắc vì cả hai job dùng chung `UpsertQuotesAsync`.

## R9. Tin Telegram

`Infrastructure/Notifications/V2TelegramFormatter.cs:15` (`FormatMua`, header dòng 28, vùng vào 45, % CL dòng 47-48 tính từ `GiaVaoLenhMin`). Đổi theo contract `contracts/telegram-mua.md`; % tính từ giá vào thực tế. Thêm định dạng tin cảnh báo Kiệt sức / Gãy nền.

## R10. Luật chu kỳ C-0→C-23 (chốt 2026-10-19 + 2026-10-20) — đối chiếu code hiện tại, chỗ nào phải đổi

Khảo sát 2026-10-19 trên disk (code Phase 5 đang dở, chưa commit). Đường dẫn từ `backend/`. C-16→C-23 chốt 2026-10-20 (Q-g→C-22 phương án A, Q-h→C-23 không reset mốc). Ghi chú: số dòng lấy từ bản disk hiện tại; cái gì *chốt* (C-x) vs *đề xuất* ghi rõ.

### R10.0 KẾT LUẬN QUAN TRỌNG: toàn bộ Phase 4–5 đã làm phải xét lại một phần

Phase 4 (T016–T020, đã commit `4f8c31f`) và Phase 5 dở dang (T021–T028, chưa commit) xây trên giả định **một lần mua duy nhất + còn CL1/CL2 tới khi thoát**. C-1/C-9/C-14 phá cả hai giả định:

- **Tin mua (T020, `V2TelegramFormatter.cs` `FormatMua` ~dòng 15–70)**: PHẢI in thêm mức điểm 2 + ngưỡng rút mốc 4%/6% (C-9, C-11; hợp đồng `contracts/telegram-mua.md` phải đổi). Khi `GiaMuaDiem2 = null` (điểm 2 ≥ CL1) in "không có điểm 2" (C-9) *(đề xuất: in cả ATR chốt lúc mua điểm 1 để người theo dõi tái hiện được band 0.3×ATR)*.
- **Kế hoạch chốt lúc mua (T018, `MayNhanKichBanService.cs` sau `TinhKeHoach` ~dòng 130–190, diff +48 dòng)**: giữ nguyên vị trí nhưng **bổ sung** tính `GiaMuaDiem2` (cần `DinhHop`/`ChieuCaoHop` từ kịch bản — hiện chỉ Nổ hướng lên có; hai kịch bản kia cần exposing hoặc tính từ ATR + giá khớp theo C-9) và hai ngưỡng 4/6.
- **Test Phase 5 dở (T021–T024, `Pha2TrongPhienTests.cs` diff +470 dòng)**: các test CL1/mức cắt/thời gian vẫn đúng cho chu kỳ CHƯA mua điểm 2; chưa có test nào cho điểm 2 / rút mốc 4-6 / chặn chu kỳ mở → **tuyển thêm tasks mới**, không tính T021–T024 là xong.
- **Nhánh bán nửa CL1 (`Pha2TrongPhienRunner.cs` ~545–565)**: phải thêm điều kiện bỏ khi chu kỳ đã mua điểm 2 (C-14 — BỎ CL1/CL2); C-16 chốt: mỗi chu kỳ tối đa MỘT lần bán nửa (bất kể nguồn), sau đó chỉ còn bán hết.

### R10.1 C-0 — chặn mua lặp ở Pha 1 và Pha 2

- Pha 1: `Pha1TruocPhienRunner.cs:110` (`db.KetQuaKichBan.Add`) — trước khi lưu kịch bản đạt cho một Symbol, kiểm Symbol đó có chu kỳ mở không; có → không tạo dòng mới, ghi lý do vào bằng chứng *(đề xuất: mã `ChuKyDangMo`, không lưu `LyDoThoatHet`)*.
- Pha 2: `MayNhanKichBanService.KiemTraTriggerTrongPhienAsync` (chặn sau khi cò đạt, cùng chỗ kiểm vùng vào R3) — Symbol có chu kỳ mở → không kích hoạt.
- "Chu kỳ mở" = *(đề xuất cách biểu thị)*: còn ≥1 dòng `KetQuaKichBan` cùng Symbol có `TrangThai ∈ {DangHinhThanh (ngày đánh giá hôm nay), DaKichHoat}` và `ThoiGianThoatHet = null`. **C-22 chốt phương án A**: một dòng = một chu kỳ, điểm 2 lưu trên cột của chính dòng đó (`GiaMuaDiem2ThucTe`/`ThoiGianMuaDiem2`) — không có dòng riêng.
- Nguồn kiểm: một hàm dùng chung (vd. `CoChuKyMoAsync(symbols)`) gọi từ cả hai pha — tránh hai định nghĩa lệch nhau.

### R10.2 C-9 — mức điểm 2 và vùng khớp [điểm 2, điểm 2 + 0.3×ATR]

- ATR dùng là **ATR chốt lúc mua điểm 1** (như `KhoangCachDuoi` ở R2 — không tính lại; đúng FR-006). Lưu `GiaMuaDiem2` + ATR đó vào kế hoạch (data-model.md).
- `DinhHop`/`ChieuCaoHop` hiện chỉ nằm trong `KichBanNoHuongLen.TinhKeHoach` (`KichBanNoHuongLen.cs:188`vùng đó), không phơi ra ngoài record kế hoạch → cần truyền vào chỗ điền kế hoạch (cùng vấn đề R2: Pha 1 cũng gọi `TinhKeHoach`).
- Logic khớp điểm 2: nhánh MỚI trong luồng mua `Pha2TrongPhienRunner` (hiện chỉ có một hướng kích hoạt từ kịch bản Pha 1, ~dòng 111 `KiemTraTriggerTrongPhienAsync`); kiểm mỗi lượt: với mọi chu kỳ có `GiaMuaDiem2 != null` và chưa mua điểm 2 **và chưa bán nửa** (C-17), giá khớp ∈ band → ghi `GiaMuaDiem2ThucTe`/`ThoiGianMuaDiem2` trên cùng dòng (C-22), bắn tin.
- **C-23**: khớp điểm 2 xong **không reset `DinhTuLucMua`** — mốc rút mốc vẫn tính từ lúc mua điểm 1.
- C-18: cùng lượt chạm điểm 2 và luật bán → luật bán thắng (xử lý nhánh bán trước, điểm 2 chỉ xét sau).

### R10.3 C-11→C-18 — luật rút từ mốc 4%/6% cho mọi vị thế V2

- Thêm nhánh trong `KiemTraSellAsync` (`Pha2TrongPhienRunner.cs:294`, trước/song song khối mức cắt ~447–530): `moc = max(DinhTuLucMua, giaVao)`; `giaKhop <= moc × (1 − 4%)` → bán nửa nếu CHƯA bán nửa (C-16: tối đa một lần, bất kể nguồn); `<= moc × (1 − 6%)` → bán hết. Chạy realtime trên giá khớp — đúng như luồng bán hiện tại (phần 1 đã ship).
- **Code V1 tham khảo (commit `baa7315`, không sửa V1)**:
  - Ngưỡng: `Application/Options/MasterAlertOptions.cs:55,58` (`SellPoint1DropFromAnchorPercent = 4`, `SellPoint2DropFromAnchorPercent = 6`) — trùng số với C-11/C-12 nhưng V1 **nhân hệ số pha** trước đây đã bỏ, còn C-12 yêu cầu cố định mọi mã.
  - Hình tháiStateMachine đáng học: `TopOpportunityVipAlertEvaluator.cs:444-455` — `soldHalf` kiểm từ `FiredAlertKinds`, `>= stop2 → SellAll; !soldHalf && >= stop1 → SellPoint1Half` — đúng semantics C-16 (mỗi chu kỳ tối đa một lần bán nửa, sau đó chỉ bán hết). **Không dùng chung code**: V1 đo theo **anchor hồi cứu lịch sử** (`VipPositionHistoryCache.ComputeAnchorPrice`, Publisher ~dòng 543-549) chứ không phải "giá cao nhất kể từ lúc mua" của C-11; lại có confirm-n-lượt + cooldown + T+2.5 gate (`MinTradingSessionsToSell`, dòng 442-443) không thuộc phạm vi V2.
  - Kết luận *(đề xuất)*: **tái sử dụng cách tính, không tái sử dụng hàm** — V2 tự tính `moc` từ `DinhTuLucMua` có sẵn; sao chép hình thái `!soldHalf &&` và thứ tự stop2 trước stop1.
- C-13 + C-18: nhánh rút mốc đặt **trong cùng vòng xét vị thế** của `KiemTraSellAsync`, chung cơ chế "mức nào chạm trước bán trước"; khi nhiều mức cùng lượt → luật bán (cả rút mốc + mức cắt) xử trước luật mua điểm 2 (C-18).
- C-16: chỉ cần biết "đã bán nửa chưa" (không cần biết nguồn); dùng `GiaBanNua != null` làm điều kiện (đã có cột, không thêm). **Bán nửa không ghi `LyDoThoatHet`** (cột chỉ dùng khi bán hết); nguồn CL1/RutMoc4 chỉ in tin + log.

### R10.4 C-10, C-21 — dọn vị thế mua lặp đang mở (ABB 5 dòng, DCM 3 dòng)

- Giữ dòng mua đầu (đã `DaKichHoat` sớm nhất của Symbol) làm điểm 1; các dòng sau cùng Symbol: `TrangThai = Huỷ`, `LyDoThoatHet = HuyMuaLap`, `KetQuaDoLuong = Huy` (không đo Pha 3).
- **Không nằm trong migration schema** — là UPDATE data trên production, viết sẵn trong `quickstart.md` (mẫu DCS 3275, T036), **không chạy**; chủ sản phẩm duyệt lúc chạy. Trước đó phải kiểm production thật ABB/DCM có bao nhiêu dòng `DaKichHoat` (danh sách 5+3 dòng là lời chủ sản phẩm).
- C-21 chốt: điểm 1 của ABB/DCM có `GiaMuaDiem2` nếu lấy lại được ATR lúc mua điểm 1; không thì `GiaMuaDiem2 = null`.

### R10.5 tasks.md — viết lại từ Phase 5 (2026-10-20)

C-0→C-23 đổi gate/pipeline trọng yếu → tasks.md cũ (T021–T028) **không phủ** luật mới; đã viết lại Phase 5+ (T021+ đánh giá từng task cũ; T029+ thêm phase mới cho C-0→C-23). Lỗi compile test T024 (`Pha2TrongPhienTests.cs:1317`) **đã sửa 2026-10-20**: bỏ tham số `soPhienDungTheoThoiGian`/`nguongMfe` khỏi `TaoSellRunner`, knob đọc từ kế hoạch (`MakeKeHoachJsonDayDu`). Toàn suite **297/297 pass**.

## R11. Pha 3 chấm theo chu kỳ — C-24, C-26, C-27 (chốt 2026-10-20)

Đối chiếu code hiện tại (disk, chưa commit Phase 5). Đường dẫn từ `backend/`.

### R11.1 bug gốc (C-26/PVS)

`Pha3DoLuongRunner.cs:25` `SoNgayChoDo = 4`; dòng 52–60 lọc `ThoiGianKichHoat <= cutoff && KetQuaDoLuong == null` → **đo một lần sau 4 ngày lịch rồi giữ nguyên**, kể cả vị thế còn mở. Dòng 130–151 (nhánh `else`, chưa có giá thoát Pha 2) lấy `nenCuoi.Close` làm giá thoát → PVS bị chấm "Thắng" 12/10 theo giá đóng cửa khi còn mở, tới 16/10 cắt lỗ thật (mua 33.60 → bán 31.50) nhưng `KetQuaDoLuong` đã khoá → không sửa. Đây là lỗ hổng C-27 nhắm tới.

### R11.2 P1 — chỉ chấm khi chu kỳ đóng (C-19, C-27)

- Điều kiện chấm mới = `ThoiGianThoatHet != null` (đã bán hết theo mọi luật — C-19). Bỏ hẳn bộ lọc "4 ngày"; vị thế đang mở → `KetQuaDoLuong` giữ null ("đang giữ").
- Bỏ nhánh `else` dùng `nenCuoi.Close` cho vị thế còn mở (dòng 130–151): không còn đo khi chưa đóng.

### R11.3 P2 — lãi/lỗ cả chu kỳ

- `giaVao`: chưa điểm 2 = `GiaVaoThucTe`; có điểm 2 = `(GiaVaoThucTe + GiaMuaDiem2ThucTe)/2` (C-4, C-22). *(Cần cột điểm 2 — Phase 7; nếu chưa có thì nhánh bình quân để trống.)*
- `giaThoat`: bán nửa (50%) + bán hết (50%) → bình quân trọng số `(GiaBanNua + GiaThoatHet)/2`. Code hiện tại dòng 108–112 **đã tính đúng** cho trường hợp có cả hai giá (trung bình giản đơn = trọng số 50/50 vì `TyLeBanChotLoi1 = 0.5`). Chỉ cần bỏ phần dùng `nenCuoi.Close` khi thiếu `GiaThoatHet`.
- Ngưỡng `NguongPhanTram = 1` (±1%) Thắng/Thua/Ngang: **giữ nguyên**.

### R11.4 P3 + hiển thị — HieuQua/web/mobile

- **P3**: dòng đang mở còn `KetQuaDoLuong` cũ → set null chờ đóng; dòng đã đóng chấm lại bằng runner mới (không cần UPDATE tay từng dòng) — **trừ DGW/PVS**: C-32 (2026-10-21) chốt phải sửa `GiaVaoThucTe` = 45.80/33.60 bằng UPDATE trong script bước 2 trước, rồi bước 4 mới xóa kết quả cũ.
- `HieuQuaKichBanService.cs:63`: `choDo = count(KetQuaDoLuong null)` → đã ánh xạ "Chờ đo"; `KetQuaHienThi` (dòng 222) null → "Chờ đo". **Không cần đổi API/DTO**.
- Web `frontend/src/pages/HieuQuaPage.tsx:32` default → "Chờ đo"; `types/index.ts:526` `KetQuaHienThi` có sẵn "Chờ đo". Mobile `hieu_qua_screen.dart:639` `contains('chờ'/'cho')` → màu cho-do. → both đã an toàn khi `KetQuaDoLuong` null.
- **Câu hỏi (không tự quyết)**: C-27 gọi vị thế mở là "đang giữ" nhưng UI đang hiển thị "Chờ đo". Đổi nhãn thành "Đang giữ" là thay đổi hiển thị → nêu cho chủ sản phẩm, không tự sửa API/DTO.

### R11.5 C-24 — sửa `GiaVaoThucTe` vị thế mua ngoài vùng

- Migration `AddGiaVaoThucTe` (20261019075021) backfill `GiaVaoThucTe = GiaVaoLenhMin` cho vị thế mở → giá **sai** so với giá khớp thật. C-24 sửa về giá khớp trong log TRIGGERED: thực chất chỉ **NTP 2114 = 52.80**, **QNS 5765 = 51.00** (ABB 4106/5444/5891 + DCM 4619/5957 sẽ bị `HuyMuaLap` theo C-10 → không cần sửa). Cùng đợt script C-10 (tasks T051 gộp). Không đụng DCS 3275 (C-25 chủ sản phẩm tự chạy).
