#!/usr/bin/env bash
# Deploy StockRadar trên cùng server với PhuKienTuiLoc (Gdata)
# Cách dùng:
#   cd /var/www/StockRadar && git pull && bash deploy.sh
# Tham số:
#   bash deploy.sh fe        -> chỉ build frontend
#   bash deploy.sh be        -> publish + restart API
#   bash deploy.sh mobile    -> Flutter web app (/app/)
#   bash deploy.sh all       -> frontend + backend (mặc định)

set -euo pipefail

PROJECT_DIR="${PROJECT_DIR:-/var/www/StockRadar}"
FE_OUT="${FE_OUT:-/var/www/publish/stockradar}"
BE_OUT="${BE_OUT:-/var/www/publish/stockradar-api}"
MOBILE_OUT="${MOBILE_OUT:-$FE_OUT/app}"
SERVICE="${SERVICE:-stockradar}"
API_PROJ="$PROJECT_DIR/backend/StockRadar.Api/StockRadar.Api.csproj"

TARGET="${1:-all}"

build_frontend() {
  echo "==> Build frontend StockRadar"
  cd "$PROJECT_DIR/frontend"
  npm install
  npm run build
  mkdir -p "$FE_OUT"
  rm -rf "$FE_OUT"/*
  cp -r dist/. "$FE_OUT/"
  echo "==> Frontend xong -> $FE_OUT"
}

build_backend() {
  echo "==> Publish backend + restart $SERVICE"
  cd "$PROJECT_DIR/backend"
  systemctl stop "$SERVICE" 2>/dev/null || true
  ASPNETCORE_ENVIRONMENT=Production dotnet publish "$API_PROJ" -c Release -o "$BE_OUT"
  chown -R www-data:www-data "$BE_OUT"
  systemctl restart "$SERVICE"
  systemctl --no-pager --full status "$SERVICE" | head -15
  echo "==> Backend xong -> $BE_OUT"
}

build_mobile() {
  echo "==> Build Flutter web (JUICE mobile app)"
  bash "$PROJECT_DIR/scripts/install-flutter-server.sh"
  export PATH="/opt/flutter/bin:$PATH"
  cd "$PROJECT_DIR/mobile"
  if [ ! -f pubspec.lock ]; then
    flutter create . --platforms=web --project-name juice_app
  fi
  flutter pub get
  flutter build web --release --base-href /app/
  mkdir -p "$MOBILE_OUT"
  rm -rf "$MOBILE_OUT"/*
  cp -r build/web/. "$MOBILE_OUT/"
  echo "==> Flutter web xong -> $MOBILE_OUT (https://YOUR_DOMAIN/app/)"
}

case "$TARGET" in
  fe|frontend) build_frontend ;;
  be|backend)  build_backend ;;
  mobile|app)  build_mobile ;;
  all)         build_frontend; build_backend ;;
  *) echo "Tham so khong hop le: $TARGET (dung: fe | be | mobile | all)"; exit 1 ;;
esac

echo "==> DEPLOY HOAN TAT ($TARGET)"
