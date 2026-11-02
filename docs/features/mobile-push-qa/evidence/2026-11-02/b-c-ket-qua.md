# Kịch bản B và C — app bị hạ process (tương đương vuốt khỏi đa nhiệm / tắt app)

Ngày: 02/11/2026 · Máy: vivo V2359A `10AE4G1ZE2000K8` · App: `com.juice.stockradar` 3.0.1 (debug)
Payload: đúng hình `KenhDayTinFcm.TaoTinNhan` cho tín hiệu V3 — `android.notification` (channel `v3-signal`,
title `MUA · HPG · KB1`), `data={symbol:HPG, loaiTin:mua, nguon:v3}`, `priority=high`, `ttl=3600s`.
JSON đã che token: `b1-payload.json`, `b2-payload.json`, `c-payload.json`, `control-payload.json`.

## Cách tạo trạng thái (và lý do đổi so với lệnh gốc)

Lệnh yêu cầu "vuốt app khỏi đa nhiệm". Không thao tác được bằng input tổng hợp trên máy này:

- `input keyevent 187` (APP_SWITCH) → launcher vivo mở **ngăn kéo ứng dụng**, không mở đa nhiệm
  (chứng minh bằng `uiautomator dump`: chỉ thấy node nhãn app Zalo/JUICE/Cài đặt).
- `input motionevent DOWN/MOVE/UP` (vuốt chậm giữ giữa) → về Màn hình chính, vẫn không có đa nhiệm.
- `am task remove <id>` → `Error: unknown command 'remove'` (build này không hỗ trợ).

Trạng thái cần test thực chất là **"process đã chết, nhưng KHÔNG force-stop (`stopped=false`)"** —
giống hệt kết quả của thao tác vuốt. Đạt được tất định bằng:

```
input keyevent KEYCODE_HOME      # app xuống nền
am kill com.juice.stockradar     # hạ process nền
```

Sau đó: `pidof` = rỗng, `dumpsys package` = `stopped=false`, `dumpsys notification` = 0 bản ghi.

## Kết quả đo

| Mốc | Gửi (epoch ms trong messageId) | Hiển thị (epoch ms `when`) | Chênh lệch |
|---|---|---|---|
| B1 (app chết, `TEST — không phải tín hiệu thật`) | 1793585960506 | 1793586800637 | **+13 phút 59 s** |
| B2 (`TEST B2 — ...`) | 1793586162570 | 1793587002775 | **+13 phút 59 s** |
| C (force-stop → mở lại → `am kill`, `TEST C — ...`) | 1793586288444 | 1793586648614 | **+6 phút 00 s** |
| Điều khiển (app đang mở, `TEST control — ...`) | 1793586632644 | 1793586632732 | **0,088 s** |

- Lúc gửi B1/B2/C: **không có thông báo nào xuất hiện** (`so_tin_com_juice=0`), process không được đánh thức.
- Dòng quyết định trong `b-c-logcat-gcm.txt`:

  ```
  11-02 09:22:42.689 W/GCM(7926): broadcast intent callback: result=CANCELLED
      forIntent { act=com.google.android.c2dm.intent.RECEIVE ... pkg=com.juice.stockradar (has extras) }
  11-02 09:24:42.717 ... (retry B2)   11-02 09:24:48.544 ... (C)
  ```

  GMS giữ tin trong hàng đợi retry (`AlarmManager: setExactAndAllowWhileIdle [name: FcmRetry]` lặp ~8–16 s)
  và chỉ đẩy được vào app khi process sống lại. Không có dòng `Start proc ... stockradar` nào
  → hệ thống **không** khởi động process để nhận broadcast.
- Các tin B/C về sau đều do **app tự hiển thị** (`tag=null`, id hashCode) vì lúc đó app đang ở foreground —
  không phải SDK (`tag=FCM-Notification:`).
- Ánh xạ tin ↔ bản ghi khớp theo `android.text` trong bản `dumpsys notification` lúc test
  (file đã xoá ngày 02/11/2026 vì chứa thông báo riêng tư của app khác; không dùng `messageId`: `push_service.dart:200` rơi vào nhánh `?? DateTime.now()...` vì id null).

## Nhãn SDK hay app hiển thị (tiêu chí mới, đã kiểm chứng cả hai chiều)

| Bản ghi | `tag` | Kết luận |
|---|---|---|
| A, D | `FCM-Notification:<hash>`, `id=0` | SDK FCM hiển thị |
| B1, B2, C, control | `null` | app hiển thị (`_hienThiKhiDangMo`) |
| (nhiễu) | `0|com.juice.stockradar|g:Aggregate_AlertingSection`, `GROUP_SUMMARY` | chỉ là gộp nhóm của Android |

## Kết luận B và C

**KHÔNG ĐẠT** — nhưng không phải vì chạm sai màn: **không tồn tại thông báo để chạm** khi app chết process.

1. Tin gửi lúc process chết **không hiển thị ngay** (GMS `result=CANCELLED`), cũng **không mất** —
   về muộn 6–14 phút sau khi app mở lại. Với tin cảnh báo giao dịch, độ trễ này vô giá trị.
2. Vì không có thông báo, **không thể chạm để khởi động lạnh** → `getInitialMessage()`
   (`push_service.dart:158`) không có tin để trả về. Nhánh "app đã tắt → chạm → mở đúng màn mã"
   trong `contracts/push-delivery.md:97` **không chạy được trên máy này**.
3. Force-stop trước đó (C) không đổi hành vi so với chỉ hạ process (B): giống hệt B.

## Kiểm chứng bổ sung (đường payload, app đang mở)

Chạm bản ghi `tag=null` (app tự hiển thị) → mở đúng **HPG · CTCP Tập đoàn Hòa Phát**
(`control-cham-tin-app-hien-thi-mo-HPG.png`). Đường `payload` → `moTheoDuongDan` còn tốt.
