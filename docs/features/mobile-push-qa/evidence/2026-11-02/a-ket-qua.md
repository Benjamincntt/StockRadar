# Kịch bản A — app chạy nền (HOME), SDK FCM hiển thị, chạm → mở đúng màn mã

Ngày: 02/11/2026 · Máy: vivo V2359A `10AE4G1ZE2000K8` · App `com.juice.stockradar` 3.0.1 (debug), đã đăng nhập.

## Trạng thái trước khi gửi

```
focus   = com.bbk.launcher2/.Launcher        (app ở nền, không foreground)
pid     = 22150                                (process còn sống)
stopped = stopped=false
records = 0                                    (không còn thông báo cũ)
```

Đã mở app (MainActivity foreground) → `KEYCODE_HOME` → chờ 10 s → gửi.

## Gửi

Payload đúng `KenhDayTinFcm.TaoTinNhan` (chỉ `android.notification`, không có `notification` gốc):
`channel_id=v3-signal`, `title=MUA · HPG · KB1`, `data={symbol:HPG, loaiTin:mua, nguon:v3}`,
`priority=high`, `ttl=3600s`. FCM trả `name=projects/juice-stockradar/messages/0:1793585176595515%…`
→ `messageId` epoch **1793585176595**. JSON đã che token: `a-payload.json`.

## Ai hiển thị

```
id=0  tag=FCM-Notification  channel=v3-signal  when=1793585176728  → SDK FCM hiển thị
```

Độ trễ **133 ms** (device `when` − messageId epoch). Chi tiết: `a-trang-thai-sau-gui.txt`, `a-logcat.txt`.

## Chạm

Mở ngăn kéo thông báo (`cmd statusbar expand-notifications`) → `uiautomator dump` cho toạ độ thật
`[266,599][685,665]` → `input tap 475 632`.

```
mCurrentFocus = com.juice.stockradar/.MainActivity
pid           = 22150 (giữ nguyên → warm start, không khởi động lại process)
bản ghi       = biến mất (AUTO_CANCEL)
màn hình      = "‹ HPG"  → a-02-cham-tin-sdk-mo-man-HPG.png
Quay lại (‹)  = "Trang chủ" → a-03-quay-lai-ve-trang-chu.png
```

## Kết luận

**ĐẠT.** Chạm thông báo do **SDK Firebase hiển thị** khi app chạy nền **mở đúng màn chi tiết HPG**,
Quay lại về màn trước, không crash.

`pid` giữ nguyên 22150 → đây là **khởi động ấm (warm start)**, không phải khởi động lạnh. Vì vậy nhánh
điều hướng chạy là **`onMessageOpenedApp`** (`push_service.dart:152`), không phải `getInitialMessage()`
(`:158` — chỉ trả tin khi chính cú chạm sinh ra process). Đường đi:
`contentIntent` của bản ghi `tag=FCM-Notification` đưa task lên trước → plugin bắn `onMessageOpenedApp`
→ `duongDanTuTin()` đọc `data.symbol` (`push_service.dart:171-176`) → `moTheoDuongDan()` (`:179-186`)
→ `GoRouter.push('/stocks/HPG')`.

Điều này cũng giải thích vì sao **không kịch bản nào trong lệnh được kiểm chứng bằng
`getInitialMessage()`**: muốn có khởi động lạnh do chạm thì phải có thông báo trên màn hình lúc app
chết process — và B/C chứng minh điều đó không xảy ra trên máy này.

Kết quả này **bác** kết luận 30/10 ("chạm tin do SDK hiển thị chỉ mở Trang chủ — giới hạn nền tảng"):
tin 30/10 gửi lúc app bị force-stop (`stopped=true`) nên tin còn nằm ở máy chủ FCM, chạm không có
`data` để điều hướng. Với app chạy nền bình thường, đường này hoạt động.
