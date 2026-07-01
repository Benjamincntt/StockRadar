# JUICE Flutter App

Mobile client cho StockRadar — giao diện giống mobile web (Obsidian Flow / Silver & Wave).

## Tính năng

- 4 tab: Trang chủ, Lệnh realtime, Watchlist, Phân tích chỉ báo
- Đăng nhập / đăng ký (JWT)
- Chi tiết mã + biểu đồ giá
- Dark / light theme

## Chạy local

```bash
cd mobile
flutter pub get
flutter run -d chrome --dart-define=API_BASE=http://localhost:5280/api/v1
```

## Deploy (Flutter Web)

Trên server:

```bash
cd /var/www/StockRadar && git pull && bash deploy.sh mobile
```

App: `https://stock.baobiantea.com/app/`

## Android APK

Build trên server (cần Android SDK — script tự cài):

```bash
bash deploy.sh apk
```

Tải APK: `https://stock.baobiantea.com/juice-app.apk`

## API

- Web production: relative `/api/v1` (cùng domain nginx)
- Native / dev: `https://stock.baobiantea.com/api/v1` hoặc `--dart-define=API_BASE=...`
