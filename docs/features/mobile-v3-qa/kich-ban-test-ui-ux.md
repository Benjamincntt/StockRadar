# Kịch bản test UI/UX toàn bộ app mobile (bản V3)

Mục tiêu: chạy hết các màn của app Flutter trên **máy thật** để tìm lỗi UI/UX — chưa đẹp, chưa chuẩn, chưa đồng bộ với phần còn lại của app, hoặc sai chức năng — rồi ghi báo cáo theo mẫu cuối file. **Chỉ test và báo cáo, không sửa code.**

Phạm vi: 8 màn + khung app + hộp thoại. App phiên bản `2.0.0+20` (`mobile/pubspec.yaml`), commit tham chiếu `dc936d2`.

| Mã | Màn / khu vực | File chính |
|---|---|---|
| SH | Khung app: thanh trên, thanh tab dưới, nút Back, hộp thoại thoát | `widgets/app_shell.dart`, `app_bottom_nav.dart`, `exit_confirm_dialog.dart` |
| LG | Đăng nhập / Đăng ký `/login` | `screens/login_screen.dart` |
| HM | Trang chủ `/` | `screens/home_screen.dart`, `widgets/vnindex_market_card.dart` |
| SB | Ô tìm mã (trên Trang chủ) | `widgets/stock_search_bar.dart` |
| WL | Watchlist `/watchlist` | `screens/watchlist_screen.dart` |
| HQ | Hiệu quả V3 `/performance` | `screens/hieu_qua_screen.dart` |
| NK | Nhật ký `/journal` (3 tab Lệnh / Vị thế / Thống kê) | `screens/nhat_ky_screen.dart` |
| NKL | Thêm / sửa lệnh nhật ký | `screens/nhat_ky_lenh_screen.dart` |
| NKP | Chọn tín hiệu V3 cho lệnh | `screens/nhat_ky_tin_hieu_picker.dart` |
| SD | Chi tiết mã `/stocks/:symbol` (+ biểu đồ toàn màn hình) | `screens/stock_detail_screen.dart`, `widgets/chart_widgets.dart`, `widgets/scenario_widgets.dart` |
| SKQ | Sự kiện quyền `/stocks/:symbol/su-kien-quyen` | `screens/su_kien_quyen_screen.dart` |
| XC | Kiểm chéo: sáng/tối, cỡ chữ, mạng, phiên đăng nhập, realtime, xoay màn | toàn app |

Đường dẫn file tính từ `mobile/lib/`.

Chuẩn để so "đúng / đồng bộ":
- **Nghiệp vụ V3**: [`specs/012-v3-scenario-engine/spec.md`](../../../specs/012-v3-scenario-engine/spec.md) — Phụ lục A (luật 5 kịch bản + ngoại lệ hoảng loạn sâu), Phụ lục E (mẫu tin), Phụ lục H (màn Hiệu quả: khối 1 tóm tắt theo kịch bản, khối 2 đường giá trị danh mục so `MKT`, khối 3 danh sách lệnh; màn chi tiết mã: khối tín hiệu V3), FR-014a/FR-020a (lệnh chờ mua, huỷ).
- **Giao diện**: token trong `core/theme/app_colors.dart` + `app_theme.dart` — chữ Inter, số JetBrains Mono (`dataFont()`), thẻ `GlassCard` bo 16 đệm 16, nút chính cao 52 bo 12, nội dung rộng tối đa 430; tăng = `primary`, giảm = `error`. Màu / cỡ chữ gõ cứng ngoài token là lỗi đồng bộ.
- **Quy ước hiển thị**: nhãn tiếng Việt có dấu; ngày `dd/MM/yyyy`; giá nghìn đồng; tiền lớn quy ra triệu / tỷ có ghi đơn vị.

---

## 1. Môi trường

### 1.1 Hai lượt chạy

| Lượt | API | Dùng để | Được ghi dữ liệu? |
|---|---|---|---|
| **A — local** | API local `:5280` + DB local, qua `adb reverse` | Mọi kịch bản, kể cả thêm / sửa / xoá (watchlist, nhật ký, sự kiện quyền). DB local có sẵn 5 tín hiệu + 5 vị thế V3 → màn Hiệu quả và khối V3 có dữ liệu | Có, chỉ DB local, dữ liệu thử phải gắn nhãn và dọn |
| **B — production** | `http://103.226.248.6/api/v1` | Kiểm hiển thị với dữ liệu thật, realtime trong phiên, tốc độ | **Không.** Chỉ đọc. Không thêm watchlist, không ghi nhật ký, không lưu sự kiện quyền (sự kiện quyền ảnh hưởng chấm điểm thật) |

Lượt B: production hiện chưa có vị thế V3 (bắt đầu chạy 27/10/2026) → màn Hiệu quả sẽ ở trạng thái rỗng; đó là trạng thái cần kiểm, không phải lỗi.

### 1.2 Chạy app

- Máy: `adb devices` phải thấy đúng 1 máy ở trạng thái `device`.
- **Không dùng `mobile/build-apk.ps1`** cho việc test: script tự tăng `version` trong `pubspec.yaml` (sửa file repo).
- Lượt A:
  1. Chạy API local: `backend/restart-api.ps1`.
  2. `adb reverse tcp:5280 tcp:5280` (điện thoại gọi `127.0.0.1:5280` đi qua cáp USB, không cần chung Wi-Fi).
  3. `flutter run -d <serial> --dart-define=API_BASE=http://127.0.0.1:5280/api/v1` trong thư mục `mobile/`.
- Lượt B: `flutter run -d <serial>` (không truyền `API_BASE` → mặc định production).
- Chạy bản **debug** để thấy vạch vàng-đen khi tràn bố cục (RenderFlex overflow) và lỗi in ra console. Đo độ mượt (giật khi cuộn, mở biểu đồ) chạy thêm `--profile`.
- Cài bản debug sẽ đè app đang cài trên máy. Nếu báo lỗi khác chữ ký (signature mismatch) → **DỪNG hỏi owner** trước khi gỡ app (gỡ là mất phiên đăng nhập và dữ liệu cục bộ của owner).

### 1.3 Thu bằng chứng

- Ảnh: `adb exec-out screencap -p > <thư mục báo cáo>/<mã TC>.png` (đặt tên theo mã kịch bản).
- Cây giao diện (đọc nhãn, kích thước vùng chạm): `adb shell uiautomator dump /sdcard/ui.xml` rồi `adb pull`.
- Thao tác: `adb shell input tap x y`, `input swipe`, `input text`, `input keyevent KEYCODE_BACK`.
- Lỗi: console của `flutter run` + `adb logcat -s flutter`. Mọi `Exception`, `RenderFlex overflowed`, `setState() called after dispose` là lỗi phải ghi.
- Ảnh và log để trong `docs/features/mobile-v3-qa/evidence/<yyyy-mm-dd>/` — **không commit**, xoá ảnh có thông tin cá nhân (email đăng nhập).

### 1.4 Đăng nhập

- Owner tự gõ tài khoản trên máy. Người test **không** nhập mật khẩu, không đọc / in token.
- Lượt A cần tài khoản trên DB local; nếu chưa có, dùng màn Đăng ký để tạo tài khoản thử tên `qa-test-<ngày>` (chỉ trên DB local) và ghi lại để dọn.

### 1.5 Thay đổi cài đặt máy

Đổi cỡ chữ hệ thống, chế độ tối hệ thống, tắt mạng (chế độ máy bay) là đổi cài đặt máy của owner → **hỏi owner trước**, ghi giá trị cũ, trả về như cũ khi xong.

---

## 2. Mức lỗi

| Mức | Nghĩa | Ví dụ |
|---|---|---|
| **P0** | Hỏng chức năng / sai số liệu / văng app / mất dữ liệu | Số phiên giữ luôn 0; lọc kỳ không áp; nút không làm gì; app crash |
| **P1** | Cản trở dùng: khó hiểu, không có đường thoát, thiếu trạng thái (đang tải / rỗng / lỗi), vùng chạm quá nhỏ, chữ bị cắt mất thông tin | Lỗi tải chỉ hiện chữ, không có nút thử lại; thao tác ẩn (nhấn giữ) không có gợi ý |
| **P2** | Không đồng bộ với app: màu / cỡ chữ / bo góc / khung trang khác chuẩn, nhãn tiếng Anh lẫn tiếng Việt, định dạng ngày / số khác nhau | `Colors.orange` thay token; ngày ISO `2026-10-28` giữa các màn `dd/MM/yyyy` |
| **P3** | Thẩm mỹ nhỏ: lệch căn, khoảng cách không đều | Đệm dưới 96 trên trang không có thanh tab |

Mỗi lỗi ghi: mã kịch bản, màn, mức, mô tả, bước tái hiện, **kỳ vọng ⇔ thực tế**, ảnh, `file:dòng` nghi là nguồn (nếu tìm được), lượt A/B, sáng/tối.

---

## 3. Checklist chung — áp cho MỌI màn (ghi kết quả theo mã C)

| Mã | Kiểm | Đạt khi |
|---|---|---|
| C1 | Trạng thái **đang tải** | Có khung chờ / vòng quay; không nháy trắng, không giật bố cục khi dữ liệu về |
| C2 | Trạng thái **rỗng** | Có câu giải thích tiếng Việt, nói được bước tiếp theo |
| C3 | Trạng thái **lỗi** (tắt API local / tắt mạng) | Thông báo dễ hiểu + nút **Thử lại**; không kẹt vòng quay mãi |
| C4 | **Kéo để làm mới** | Có trên màn danh sách; làm mới đúng phần đang xem |
| C5 | **Sáng / tối** | Cả hai chế độ đọc được, đủ tương phản; không còn khối nền tối trong chế độ sáng (hoặc ngược lại) |
| C6 | **Tràn bố cục** | Không có vạch vàng-đen; chữ dài (tên công ty, tên ngành) có `…` chứ không đè chữ khác |
| C7 | **Vùng chạm** | Mọi nút / biểu tượng bấm được ≥ 48×48dp (đo bằng `uiautomator dump`) |
| C8 | **Nhãn biểu tượng** | Nút chỉ có biểu tượng phải có tooltip / nhãn đọc màn hình |
| C9 | **Ngôn ngữ & định dạng** | Tiếng Việt có dấu; không hiện mã thô (`BAN`, `KB1` đứng một mình, `v3`, `ChamMA`, `DaBao`); ngày `dd/MM/yyyy`; giá cùng một cách làm tròn giữa các màn |
| C10 | **Đồng bộ thiết kế** | Thẻ, chip, nút, tiêu đề cùng kiểu với màn khác; màu tăng/giảm theo token |
| C11 | **Quay lại** | Nút Back hệ thống, vuốt mép trái (Android) và nút quay lại trên màn đều đưa về đúng chỗ, giữ vị trí cuộn của màn trước |
| C12 | **Console sạch** | Không có exception / overflow / cảnh báo `setState` sau khi rời màn |
| C13 | **Tai thỏ & thanh điều hướng** | Nội dung không chui dưới tai thỏ / thanh điều hướng của máy; nút nổi (FAB) không che nội dung cuối danh sách |

---

## 4. Kịch bản theo màn

Ghi chú "**Nghi vấn**": điểm thấy khi đọc code (ngày 28/10/2026), cần **xác nhận trên máy** — đúng thì ghi lỗi, không đúng thì ghi "không tái hiện".

### SH — Khung app

| Mã | Bước | Kỳ vọng |
|---|---|---|
| SH-01 | Mở app (chưa đăng nhập) | Vào Trang chủ; thanh trên có logo, tiêu đề tab, huy hiệu Live, nút đổi sáng/tối, nút "Đăng nhập" |
| SH-02 | Bấm lần lượt 4 tab dưới | Đúng màn, đúng tiêu đề, tab đang chọn được tô; chuyển tab không mất dữ liệu đã tải của tab trước (kiểm khi quay lại) |
| SH-03 | Huy hiệu Live trong giờ giao dịch / ngoài giờ / tắt mạng | Trạng thái đổi đúng (`Live` / `…` / `↻` / `Off`) và người dùng hiểu được nghĩa. Nghi vấn: nhãn tiếng Anh, không có giải thích |
| SH-04 | Đổi sáng/tối từ thanh trên, rồi đóng-mở lại app | Đổi tức thì toàn app; nhớ lựa chọn sau khi mở lại |
| SH-05 | Đăng nhập rồi bấm biểu tượng đăng xuất | Có xác nhận hoặc ít nhất rõ là đã đăng xuất; về đúng trạng thái chưa đăng nhập. Nghi vấn: biểu tượng không có tooltip, 36dp |
| SH-06 | Back hệ thống ở tab khác Trang chủ | Về Trang chủ |
| SH-07 | Back ở Trang chủ | Hộp thoại "Thoát ứng dụng?" — "Ở lại" đóng hộp, "Thoát" thoát app; hộp đúng màu ở cả sáng/tối. Nghi vấn: chữ trắng gõ cứng |
| SH-08 | Thanh tab dưới | Nhãn đọc được (nghi vấn: cỡ 10), vùng chạm đủ, không che nội dung cuối trang |

### LG — Đăng nhập / Đăng ký

| Mã | Bước | Kỳ vọng |
|---|---|---|
| LG-01 | Mở từ nút "Đăng nhập" trên thanh trên | Màn đăng nhập, có đường quay lại (nút + vuốt + Back) |
| LG-02 | Bấm "Đăng nhập →" khi để trống cả hai ô | Báo lỗi ngay tại ô, không gửi lên server. Nghi vấn: không kiểm phía app |
| LG-03 | Sai mật khẩu (owner gõ) | Thông báo rõ, tiếng Việt, không lộ chi tiết kỹ thuật; nút trở lại bấm được |
| LG-04 | Chuyển tab "Đăng ký" | Hiện thêm ô "Tên hiển thị"; tiêu đề đổi; chuyển qua lại không mất chữ đã gõ ở ô chung |
| LG-05 | Ô mật khẩu | Có nút hiện/ẩn mật khẩu (nghi vấn: không có); Enter gửi form |
| LG-06 | Bàn phím mở | Nút gửi không bị bàn phím che; cuộn được |
| LG-07 | Đăng nhập thành công | Về Trang chủ; thanh trên đổi sang nút đăng xuất; Watchlist / Nhật ký mở được |
| LG-08 | Tab "Đăng nhập / Đăng ký" | Có phản hồi khi chạm, vùng chạm đủ (nghi vấn: không có hiệu ứng chạm) |

### HM — Trang chủ

| Mã | Bước | Kỳ vọng |
|---|---|---|
| HM-01 | Lần tải đầu | C1; thứ tự: ô tìm → thẻ VN-INDEX → "Top cổ phiếu xác suất cao" → "Tín hiệu mới nhất" |
| HM-02 | Thẻ VN-INDEX | Giá trị, điểm và % tăng/giảm đúng màu, pha thị trường, đỉnh gần nhất, KL / GT có đơn vị, thanh độ rộng có chú thích. Nghi vấn: dòng "làm mới mỗi 1 phút" nhưng app làm mới 2 phút |
| HM-03 | Thẻ Top | Mỗi dòng: hạng, mã, điểm, nhãn trạng thái, lý do 1 dòng, biểu đồ nhỏ, giá live. Mã dài + nhãn dài không tràn (nghi vấn) |
| HM-04 | Chạm một dòng Top | Mở chi tiết đúng mã |
| HM-05 | Trạng thái Top: chưa chạy phân tích / 0 mã / danh sách tham khảo | Banner đúng ý, màu theo token (nghi vấn: `Colors.orange`) |
| HM-06 | Thẻ "Tín hiệu mới nhất" | Dòng trạng thái tiếng Việt (nghi vấn: tên module "SessionRadar"), tối đa 8 ô, ô trần/sàn có màu nền, chạm mở chi tiết |
| HM-07 | Kéo làm mới; để app chạy nền 3 phút rồi mở lại | Làm mới đúng; không nháy, không nhân đôi dữ liệu |
| HM-08 | Giá live trong giờ giao dịch (lượt B) | Giá trên dòng Top / ô tín hiệu tự cập nhật; mở chi tiết một mã rồi quay lại → giá live ở Trang chủ **vẫn chạy**. Nghi vấn: mở chi tiết thay mất danh sách mã đang theo dõi realtime, Trang chủ đứng giá tới lần tải 2 phút sau |
| HM-09 | Không có trạng thái "thị trường đóng cửa" | Ngoài giờ có cho người dùng biết giá là giá đóng cửa không (nghi vấn: không có) |

### SB — Ô tìm mã

| Mã | Bước | Kỳ vọng |
|---|---|---|
| SB-01 | Gõ `fpt` | Sau ~0,3 giây hiện gợi ý; mã chữ in, tên công ty `…` nếu dài |
| SB-02 | Gõ tên công ty có dấu | Tìm ra |
| SB-03 | Gõ mã không tồn tại `ZZZZ` + Enter | Không mở màn lỗi trống trơn; nếu mở chi tiết thì màn chi tiết báo "Không tìm thấy mã…" rõ ràng |
| SB-04 | Nút xoá chữ | Xoá được, vùng chạm đủ (nghi vấn: nhỏ) |
| SB-05 | Tắt API giữa lúc tìm | Không treo vòng quay; có thông báo |

### WL — Watchlist

| Mã | Bước | Kỳ vọng |
|---|---|---|
| WL-01 | Chưa đăng nhập | Lời mời đăng nhập có **nút** đăng nhập, không trình bày như lỗi (nghi vấn: banner lỗi, không có nút) |
| WL-02 | Đăng nhập, dải chip danh sách | Thứ tự: mặc định → tuỳ chỉnh → theo ngành A→Z; chip đang chọn rõ; tên ngành dài không tràn |
| WL-03 | "+ Thêm" → tạo danh sách `QA-test` (lượt A) | Hộp thoại tiếng Việt, nút Huỷ/Tạo; tên trống thì không tạo và có báo |
| WL-04 | Đổi tên / xoá danh sách tuỳ chỉnh | Có cách phát hiện được thao tác (nghi vấn: chỉ nhấn giữ, không gợi ý); xoá có xác nhận |
| WL-05 | Thêm mã `fpt`, `FPT`, ` hpg `, `ZZZZ`, chuỗi rỗng | Chuẩn hoá chữ in, cắt khoảng trắng; mã sai báo lỗi rõ; rỗng báo "Nhập mã cổ phiếu trước đã." |
| WL-06 | Dòng mã | Mã, tên, ngành, điểm, % đúng màu; chạm mở chi tiết |
| WL-07 | Vuốt xoá một mã | Xoá ngay, có cách hoàn tác hoặc thông báo; lỗi mạng thì mã quay lại |
| WL-08 | Danh sách theo ngành | Ghi rõ chỉ đọc; không có ô thêm |
| WL-09 | % thay đổi trong giờ giao dịch (lượt B) | Có cập nhật hay đứng yên — ghi nhận (nghi vấn: không live, khác Trang chủ) |
| WL-10 | Màu nền gõ cứng `#002022` | Nhìn đúng ở chế độ sáng (nghi vấn) |

### HQ — Hiệu quả (V3) — trọng tâm

Lượt A có 5 vị thế V3 trên DB local; lượt B kiểm trạng thái rỗng.

| Mã | Bước | Kỳ vọng |
|---|---|---|
| HQ-01 | Mở tab | C1; 4 ô tóm tắt "Giá trị DM / Tiền mặt / Đang giữ / Sụt giảm TBĐ" |
| HQ-02 | Đơn vị ô tiền | Vốn production 40 triệu phải hiện ra con số hợp lý (vd "40 triệu" hoặc "0,04 tỷ" — ghi lại đúng chữ đang hiện và đánh giá có dễ đọc không); không bị cắt `…` ở màn rộng 360dp |
| HQ-03 | Chip kỳ Tuần / Tháng / Quý / Tất cả | Đổi kỳ thì **cả** tóm tắt **và** "Lịch sử vị thế" đổi theo. Nghi vấn P0: lịch sử không đổi |
| HQ-04 | "Theo kịch bản" | 5 dòng KB1/KB2/KB4/N1/N3 có tên tiếng Việt, tỉ lệ thắng, thanh màu, lãi TB / lỗ TB / tệ nhất / giữ TB. Định nghĩa thắng / hoà vốn khớp Phụ lục H |
| HQ-05 | Thẻ "Lịch sử vị thế" | Mã · trạng thái tiếng Việt, kết quả, kịch bản **có tên** (nghi vấn: chỉ "KB1"), giá vào / thoát (cùng cách làm tròn với màn khác — nghi vấn: 1 chữ số lẻ), % ±, **số phiên giữ đúng** (nghi vấn P0: luôn "Giữ 0 phiên" vì app đọc `soPhienDaGiu`, API trả `soPhienDaGi`), ngày vào → ngày thoát |
| HQ-06 | Lý do thoát | Vị thế đã thoát hiện lý do: chạm MA / chốt +15% hoảng loạn sâu / hết hạn ATC / ATO theo lịch. Nghi vấn P1: app không đọc `lyDoThoat` |
| HQ-07 | Lệnh hoảng loạn sâu | Vị thế có `laHoangLoanSau = true` có dấu hiệu nhận biết + "giữ tối đa 40". Nghi vấn: app không đọc trường này. (DB local có thể không có lệnh loại này → ghi "không có dữ liệu để kiểm", kèm đọc code) |
| HQ-08 | Trạng thái Chờ mua / Bị huỷ | Nhãn tiếng Việt, màu phân biệt; lệnh huỷ không tính vào thắng/thua |
| HQ-09 | "Xem thêm" | Tải trang tiếp, không trùng dòng; lỗi mạng thì báo (nghi vấn: nuốt lỗi im lặng) |
| HQ-10 | Chạm thẻ vị thế | Mở chi tiết mã đúng |
| HQ-11 | Khối "đường giá trị danh mục so `MKT`" (Phụ lục H khối 2) | Có hay không — nghi vấn: mobile **chưa có** khối này (ghi là thiếu chức năng so spec) |
| HQ-12 | Lượt B (production, 0 vị thế) | Thông điệp rỗng dễ hiểu, không lộ thuật ngữ nội bộ ("P1++") khó hiểu với người dùng — đánh giá |
| HQ-13 | Màu | Nghi vấn: dùng mã màu Tailwind gõ cứng thay token — so với màu tăng/giảm ở Trang chủ trong cả sáng/tối |

### NK — Nhật ký

Chỉ ghi dữ liệu ở **lượt A**. Lệnh thử dùng mã thật nhưng ghi chú `QA-TEST <ngày>` để dọn.

| Mã | Bước | Kỳ vọng |
|---|---|---|
| NK-01 | Chưa đăng nhập | Câu giải thích + nút "Đăng nhập" |
| NK-02 | 3 tab Lệnh / Vị thế / Thống kê | Chuyển được bằng chạm và (nên) vuốt ngang (nghi vấn: không vuốt được); kiểu tab giống phần còn lại của app |
| NK-03 | Tab Lệnh — rỗng | "Chưa có lệnh nào. Chạm + để ghi lệnh đầu tiên." |
| NK-04 | Thẻ lệnh | Nhãn chiều lệnh tiếng Việt có dấu (nghi vấn: hiện `BAN`), số lượng có "cp", giá + đơn vị, ngày giờ `dd/MM/yyyy HH:mm`, dòng gắn tín hiệu không hiện mã thô `v3` |
| NK-05 | Xoá lệnh | Hộp xác nhận "Xoá lệnh?"; nút xoá có tooltip, vùng chạm đủ |
| NK-06 | Tab Vị thế | Kéo làm mới (nghi vấn: không có); ngày đóng `dd/MM/yyyy` (nghi vấn: chuỗi ISO thô); màu theo token |
| NK-07 | Tab Thống kê | Chip kỳ đổi số liệu; chỉ tải lại phần cần (nghi vấn: tải lại cả 3 API) |
| NK-08 | Lỗi tải | Có nút Thử lại (nghi vấn: chỉ chữ); lỗi không phải lỗi API không làm kẹt vòng quay (nghi vấn) |
| NK-09 | Nút nổi "+" | Không che dòng cuối, không chồng lên thanh tab |

### NKL — Thêm / sửa lệnh

| Mã | Bước | Kỳ vọng |
|---|---|---|
| NKL-01 | Mở từ "+" | Khung trang (thanh tiêu đề, nền, nút quay lại, vuốt-back) giống các màn đẩy khác. Nghi vấn: khung Material mặc định, không có nền sóng, không vuốt-back |
| NKL-02 | Bấm "Ghi lệnh" khi trống | Báo lỗi tại từng ô bắt buộc |
| NKL-03 | Giá `25,5` / `25.5` / `25500` | Hiểu đúng ý người nhập, hoặc hướng dẫn rõ định dạng. Nghi vấn P0: `25.5` bị đọc thành `255` |
| NKL-04 | Khối lượng `0`, `-100`, `150` (lẻ lô) | Chặn số ≤ 0; cảnh báo hoặc cho phép lô lẻ có chủ đích |
| NKL-05 | Mã `fpt` / `ZZZZ` | Chuẩn hoá chữ in; mã không tồn tại có báo |
| NKL-06 | Chọn ngày giờ | Không chọn được ngày tương lai; giờ theo giờ VN |
| NKL-07 | Loại "Điều chỉnh" | Đổi bộ ô đúng; ghi chú bắt buộc |
| NKL-08 | Ghi lệnh thành công (lượt A) | Về danh sách, lệnh mới ở đầu, có thông báo |
| NKL-09 | Sửa lệnh vừa tạo | Điền sẵn đúng; lưu đúng |

### NKP — Chọn tín hiệu V3

| Mã | Bước | Kỳ vọng |
|---|---|---|
| NKP-01 | Mở từ "Gắn tín hiệu" | Khung trang đồng bộ (nghi vấn như NKL-01) |
| NKP-02 | Dòng tín hiệu | Mã, giá có đơn vị, kịch bản có tên, ngày `dd/MM/yyyy` (nghi vấn: ngày thô, nguồn `v3` thô) |
| NKP-03 | Chọn một tín hiệu | Quay về form, điền mã + giá nếu ô đang trống; không ghi đè chữ người dùng đã gõ |
| NKP-04 | Đánh dấu "bỏ qua" / bỏ đánh dấu | Biểu tượng có tooltip, trạng thái hiện rõ; màu theo token |
| NKP-05 | Rỗng / lỗi | Câu rỗng dễ hiểu; lỗi có nút thử lại (nghi vấn: không có) |

### SD — Chi tiết mã

Dùng mã có tín hiệu V3 trên DB local (lấy từ màn Hiệu quả, lượt A) và một mã bluechip bất kỳ (lượt B).

| Mã | Bước | Kỳ vọng |
|---|---|---|
| SD-01 | Mở từ Trang chủ / Watchlist / Hiệu quả / tìm kiếm | Đúng mã; quay lại về đúng màn trước và giữ vị trí cuộn |
| SD-02 | Chế độ sáng | Nền màn theo chế độ sáng. Nghi vấn P2: nền tối gõ cứng `darkBackground` |
| SD-03 | Đầu trang | Nút quay lại có nhãn, mã, tên công ty `…`, nút "Sự kiện quyền" có tooltip |
| SD-04 | Dải huy hiệu V3 | Chữ đầy đủ không tràn (nghi vấn: không giới hạn dòng) |
| SD-05 | Thẻ giá | Giá live, % đúng màu, "TỶ LỆ KHỐI LƯỢNG", "RS" có giải thích hoặc nhãn Việt |
| SD-06 | Biểu đồ: đổi khung 1m / 5m / 15m / 1h / 1D, bật Ichimoku, chạm-kéo xem OHLC | Đổi nhanh, có trạng thái đang tải / không có dữ liệu; nút khung ≥ 48dp (nghi vấn ~30dp); màu nút theo token (nghi vấn: xanh dương gõ cứng) |
| SD-07 | Biểu đồ toàn màn hình | Xoay ngang, ẩn thanh hệ thống; thoát bằng nút + Back trả lại hướng dọc và thanh hệ thống |
| SD-08 | Thẻ "Nền giá" | Nhãn tiếng Việt; nghi vấn: còn nhãn kiểu V2 ("Nổ hướng lên", "Gãy nền", "Cắt lỗ") — ghi lại chữ xuất hiện, đánh giá có mâu thuẫn với V3 (V3 không cắt lỗ) không |
| SD-09 | Thẻ "Tín hiệu kịch bản V3" | Tên kịch bản, trạng thái, hạng, giá tín hiệu / vào / thoát, thước xếp hạng, ảnh chụp chỉ báo. **Luật bán hiển thị đúng Phụ lục A**: KB1/KB2/KB4 chạm MA10, N1 chạm MA20, N3 ATO phiên TDM=2 tháng sau; **lệnh hoảng loạn sâu: giá vào +15%, giữ tối đa 40** (nghi vấn: chưa có biến thể này). Sắp xếp "mới nhất trước" như phụ đề nói (nghi vấn: sắp theo trạng thái + hạng) |
| SD-10 | V3 rỗng / lỗi | Câu rỗng đúng ý; lỗi gợi ý kéo để tải lại |
| SD-11 | "+ Thêm vào Watchlist" (lượt A) | Thêm vào danh sách nào phải rõ; đổi thành "Đã thêm Watchlist"; mở tab Watchlist thấy mã. Chưa đăng nhập → sang đăng nhập rồi quay lại đúng mã |
| SD-12 | Thanh tab dưới trên màn này | Không tab nào sáng; bấm tab đi đúng — đánh giá có gây rối không |
| SD-13 | Mã không tồn tại / máy chủ chậm | "Không tìm thấy mã…" / "Máy chủ phản hồi chậm (quá 20s). Thử lại." có nút |
| SD-14 | Mã đã ngưng theo dõi | Banner cảnh báo giá là giá đóng cửa phiên cuối |

### SKQ — Sự kiện quyền

| Mã | Bước | Kỳ vọng |
|---|---|---|
| SKQ-01 | Mở từ biểu tượng trên chi tiết mã | Khung trang chuẩn (màn này dùng `PushedPageScaffold` — làm mốc so cho NKL/NKP) |
| SKQ-02 | Danh sách "Đã ghi nhận" | Ngày `dd/MM/yyyy` (nghi vấn: ISO thô); rỗng có câu |
| SKQ-03 | Form thêm (lượt A, mã thử) | Chưa chọn ngày thì nút tắt và **có lý do hiển thị**; số sai báo lỗi, không lặng lẽ thành 0 / 1 (nghi vấn) |
| SKQ-04 | Lỗi tải | Báo ở đúng chỗ, có thử lại (nghi vấn: lỗi nằm trong thẻ "Thêm", danh sách hiện như rỗng) |
| SKQ-05 | Đệm dưới | Không có khoảng trống thừa (nghi vấn: đệm 96 dù không có thanh tab); kéo làm mới (nghi vấn: không có) |
| SKQ-06 | **Lượt B: KHÔNG bấm "Lưu sự kiện"** | — (ghi dữ liệu production ảnh hưởng chấm điểm thật) |

### XC — Kiểm chéo toàn app

| Mã | Bước | Kỳ vọng |
|---|---|---|
| XC-01 | Đi hết các màn ở **chế độ tối** rồi **chế độ sáng** | C5 trên mọi màn; ghi mọi khối màu gõ cứng lạc tông |
| XC-02 | Cỡ chữ hệ thống 1,3× (hỏi owner trước, mục 1.5) | Không tràn, không cắt số tiền / giá |
| XC-03 | Tắt API local giữa chừng (lượt A) trên từng tab | C3 trên mọi màn |
| XC-04 | Phiên đăng nhập hết hạn / bị thu hồi (xoá token phía server local hoặc chờ hết hạn — chỉ lượt A) | App báo phải đăng nhập lại, không lặng lẽ mất chức năng. Nghi vấn: lỗi 401 tự đăng xuất im lặng, không chuyển trang |
| XC-05 | Đưa app xuống nền 5 phút rồi mở lại | Dữ liệu làm mới, realtime nối lại, không lỗi |
| XC-06 | Xoay ngang ở các màn thường | Hoặc khoá dọc, hoặc bố cục vẫn dùng được (ghi lại hành vi) |
| XC-07 | Tổng hợp **định dạng** | Bảng so cách hiện: giá, %, tiền (đ / triệu / tỷ), ngày, giờ giữa HM / WL / HQ / NK / SD — đánh dấu chỗ lệch |
| XC-08 | Tổng hợp **khung trang đẩy** | So 4 kiểu khung: chi tiết mã, đăng nhập, NKL/NKP (Material), SKQ (`PushedPageScaffold`) |
| XC-09 | Tổng hợp **chip / tab** | So kiểu chip Watchlist, Hiệu quả, Nhật ký, khung biểu đồ |
| XC-10 | Tổng hợp **nhãn** | Liệt kê mọi chữ tiếng Anh / mã thô còn hiện cho người dùng (Watchlist, Live/Off, SessionRadar, RS, VOL, BAN, KB1, v3…) — owner quyết cái nào giữ |
| XC-11 | Nội dung V2 còn sót | Mọi chữ / màn còn dấu vết V2 (VIP, lãi/lỗ đã thực hiện kiểu cũ, Nổ hướng lên, Cắt lỗ…) đang hiện cho người dùng |

---

## 5. Mẫu báo cáo

Tạo file `docs/features/mobile-v3-qa/bao-cao-<yyyy-mm-dd>.md`:

```markdown
# Báo cáo test UI/UX mobile V3 — <ngày>

Máy: <hãng, model, Android x, độ phân giải, dp>. App: <commit>, debug/profile.
Lượt A (local): <có/không, giờ>. Lượt B (production): <có/không, trong/ngoài giờ giao dịch>.
Cài đặt máy đã đổi và đã trả lại: <...>.

## Tóm tắt
| Mức | Số lỗi |
|---|---|
| P0 | |
| P1 | |
| P2 | |
| P3 | |

## Kết quả theo kịch bản
| Mã | Đạt / Lỗi / Không kiểm được | Ghi chú (lý do nếu không kiểm được) |

## Danh sách lỗi (P0 → P3)
### <Mã lỗi, vd BUG-01> — <mô tả ngắn>
- Kịch bản: <mã TC> · Màn: <...> · Mức: <P0..P3> · Lượt: <A/B> · Chế độ: <sáng/tối>
- Tái hiện: 1… 2… 3…
- Kỳ vọng: … · Thực tế: …
- Bằng chứng: <evidence/…png>, log: <…>
- Nguồn nghi: <file:dòng> (nếu tìm được)

## Nghi vấn từ đọc code
| Nghi vấn | Xác nhận / Không tái hiện |

## Bảng tổng hợp đồng bộ (XC-07 → XC-10)

## Không kiểm được
<mục, lý do>

## Dọn dẹp
Dữ liệu thử đã xoá trên DB local: <bảng: trước/sau>. Danh sách watchlist thử: <...>. Tài khoản thử: <...>.
```
