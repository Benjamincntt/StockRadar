# JUICE Flutter App

Mobile client cho StockRadar — giao diện giống mobile web (Obsidian Flow / Silver & Wave).

## Tính năng

- 4 tab: Trang chủ, Lệnh realtime, Watchlist, Phân tích chỉ báo
- Đăng nhập / đăng ký (JWT)
- Chi tiết mã + biểu đồ giá
- Dark / light theme

## Cài môi trường (Windows, ổ D)

1. **Flutter SDK** → `D:\flutter` (đã cài nếu `flutter doctor` chạy được)
2. **Android SDK** (không cần Android Studio):

```powershell
cd D:\Source\StockRadar\mobile
.\install-android-sdk.ps1
```

Script cài JDK 17 + SDK vào `D:\Android\Sdk`, cấu hình Flutter tự động.

3. Kiểm tra:

```powershell
flutter doctor
```

Phải thấy ✓ **Android toolchain**.

## Build APK (local)

```powershell
cd D:\Source\StockRadar\mobile
.\build-apk.ps1
```

APK mặc định copy ra: **`D:\JUICE-build\juice-app.apk`**

Tùy chọn:

```powershell
# Thư mục output khác
.\build-apk.ps1 -OutDir "D:\Downloads"

# API local (backend trên PC, điện thoại cùng WiFi — tu dong lay IP LAN)
.\build-apk.ps1 -Local

# Hoac chi dinh IP thu cong
.\build-apk.ps1 -ApiBase "http://192.168.x.x:5280/api/v1"
```

Hoặc build thủ công:

```powershell
cd D:\Source\StockRadar\mobile
flutter pub get
flutter build apk --release
# APK: build\app\outputs\flutter-apk\app-release.apk
```

## Cài lên điện thoại

1. Copy `juice-app.apk` sang máy (Zalo, USB, Drive…)
2. Mở file → cho phép **Cài từ nguồn không xác định**
3. Hoặc USB debugging:

```powershell
adb install D:\JUICE-build\juice-app.apk
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
