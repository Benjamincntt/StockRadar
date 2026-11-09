# JUICE Flutter App

Mobile client cho StockRadar — giao diện giống mobile web (Obsidian Flow / Silver & Wave).

## Tính năng

- 4 tab: Trang chủ, Lệnh realtime, Watchlist, Phân tích chỉ báo
- Đăng nhập / đăng ký (JWT)
- Chi tiết mã + biểu đồ giá
- Dark / light theme

## Cài môi trường (Windows, ổ D)

1. **Flutter SDK** → `D:\DevTools\flutter` (đã cài nếu `flutter doctor` chạy được)
2. **Android SDK** (không cần Android Studio):

```powershell
cd D:\Source\StockRadar\mobile
.\install-android-sdk.ps1
```

Script cài JDK 17 + SDK vào `D:\DevTools\Android\Sdk`, cấu hình Flutter tự động.

3. Kiểm tra:

```powershell
flutter doctor
```

Phải thấy ✓ **Android toolchain**.

## Build + cài lên điện thoại — làm nhanh (khoảng 5 phút)

Cắm điện thoại qua USB (đã bật USB debugging), rồi chạy lần lượt:

```powershell
adb devices
```

Phải thấy một dòng `<mã máy>  device`. Thấy `unauthorized` thì mở khoá máy, bấm "Cho phép" trên hộp thoại USB debugging.

```powershell
D:\Source\StockRadar\mobile\build-apk.ps1
```

Mất khoảng 4–5 phút (bước Gradle lâu nhất). Xong khi thấy `Xong!` và dòng `Phien ban: x.y.z+n`.

```powershell
adb install -r D:\Source\StockRadar\mobile\build\app\outputs\flutter-apk\app-release.apk
```

Phải thấy `Success`. Cờ `-r` để cài đè, giữ nguyên dữ liệu đăng nhập trên máy.

```powershell
adb shell dumpsys package com.juice.stockradar | Select-String versionName
```

`versionName` phải trùng số phiên bản script vừa in ra.

Lưu ý:
- **Script tự tăng `version` trong `pubspec.yaml`** mỗi lần chạy → nhớ commit file này. `ship-all.ps1` chỉ add `mobile/lib`, **không** add `pubspec.yaml`.
- Mặc định build arm64, trỏ API production — đúng cho điện thoại thật. Không cần thêm cờ.
- Cảnh báo `Kotlin Gradle Plugin (KGP): firebase_core` và `deprecated API` khi build là bình thường, không ảnh hưởng APK.
- App gọi API production, nên đổi backend thì phải ship backend trước khi kiểm app.
- Không gõ thẳng `flutter build apk`: lệnh đó bỏ qua bước tăng phiên bản và không truyền `SYNC_API_KEY`.

## Build APK (chi tiết)

```powershell
cd D:\Source\StockRadar\mobile
.\build-apk.ps1
```

APK chỉ một chỗ (Flutter mặc định):

`mobile\build\app\outputs\flutter-apk\app-release.apk`

Tùy chọn:

```powershell
# API local (backend trên PC, điện thoại cùng WiFi — tu dong lay IP LAN)
.\build-apk.ps1 -Local

# Hoac chi dinh IP thu cong
.\build-apk.ps1 -ApiBase "http://192.168.x.x:5280/api/v1"
```

## Cài lên điện thoại

1. Copy `app-release.apk` sang máy (Zalo, USB, Drive…)
2. Mở file → cho phép **Cài từ nguồn không xác định**
3. Hoặc USB debugging:

```powershell
adb install D:\Source\StockRadar\mobile\build\app\outputs\flutter-apk\app-release.apk
```

## API mặc định (native)

APK release trỏ production qua IP public (DNS `stock.baobiantea.com` chưa có):

`http://103.226.248.6/api/v1`

Khi đã cấu hình subdomain + HTTPS, sửa `lib/config/api_config.dart` rồi build lại APK.

## Chạy trên emulator / máy thật (dev)

```powershell
flutter pub get
flutter devices
flutter run --dart-define=API_BASE=https://stock.baobiantea.com/api/v1
```

## Flutter Web (tùy chọn)

```powershell
flutter build web --release --base-href /app/
```
