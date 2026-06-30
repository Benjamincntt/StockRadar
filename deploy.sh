#!/usr/bin/env bash
# Deploy StockRadar trên cùng server với PhuKienTuiLoc (Gdata)
# Cách dùng:
#   cd /var/www/StockRadar && git pull && bash deploy.sh
# Tham số:
#   bash deploy.sh fe        -> chỉ build frontend
#   bash deploy.sh be        -> publish + restart API
#   bash deploy.sh all       -> frontend + backend (mặc định)

set -euo pipefail

PROJECT_DIR="${PROJECT_DIR:-/var/www/StockRadar}"
FE_OUT="${FE_OUT:-/var/www/publish/stockradar}"
BE_OUT="${BE_OUT:-/var/www/publish/stockradar-api}"
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
  ASPNETCORE_ENVIRONMENT=Production dotnet publish "$API_PROJ" -c Release -o "$BE_OUT"
  chown -R www-data:www-data "$BE_OUT"
  systemctl restart "$SERVICE"
  systemctl --no-pager --full status "$SERVICE" | head -15
  echo "==> Backend xong -> $BE_OUT"
}

case "$TARGET" in
  fe|frontend) build_frontend ;;
  be|backend)  build_backend ;;
  all)         build_frontend; build_backend ;;
  *) echo "Tham so khong hop le: $TARGET (dung: fe | be | all)"; exit 1 ;;
esac

echo "==> DEPLOY HOAN TAT ($TARGET)"
