#!/usr/bin/env bash
# Cài StockRadar lần đầu trên server Gdata (đã có PhuKienTuiLoc).
# Chạy với quyền root:
#   DOMAIN=stock.baobiantea.com bash scripts/gdata-stockradar-setup.sh
#
# Bien moi truong (tuy chon):
#   DOMAIN=stock.baobiantea.com
#   REPO=https://github.com/Benjamincntt/StockRadar.git
#   SQL_USER=hieunl
#   SQL_PASSWORD=your_sql_password

set -euo pipefail

DOMAIN="${DOMAIN:-stock.baobiantea.com}"
REPO="${REPO:-https://github.com/Benjamincntt/StockRadar.git}"
PROJECT_DIR="${PROJECT_DIR:-/var/www/StockRadar}"
SQL_USER="${SQL_USER:-hieunl}"
SQL_PASSWORD="${SQL_PASSWORD:-}"

echo "==> 1. Clone / cap nhat repo"
if [ -d "$PROJECT_DIR/.git" ]; then
  cd "$PROJECT_DIR"
  git pull
else
  git clone "$REPO" "$PROJECT_DIR"
  cd "$PROJECT_DIR"
fi

echo "==> 2. Thu muc publish"
mkdir -p /var/www/publish/stockradar /var/www/publish/stockradar-api
chown -R www-data:www-data /var/www/publish/stockradar /var/www/publish/stockradar-api

echo "==> 3. appsettings.Production.json"
PROD_FILE="$PROJECT_DIR/backend/StockRadar.Api/appsettings.Production.json"
EXAMPLE="$PROJECT_DIR/backend/StockRadar.Api/appsettings.Production.json.example"
if [ ! -f "$PROD_FILE" ]; then
  if [ ! -f "$EXAMPLE" ]; then
    echo "Thieu file example: $EXAMPLE"
    exit 1
  fi
  cp "$EXAMPLE" "$PROD_FILE"
  if [ -n "$SQL_PASSWORD" ]; then
    sed -i "s/YOUR_SQL_USER/${SQL_USER}/g" "$PROD_FILE"
    sed -i "s/YOUR_SQL_PASSWORD/${SQL_PASSWORD}/g" "$PROD_FILE"
  fi
  sed -i "s|https://stock.baobiantea.com|http://${DOMAIN}|g" "$PROD_FILE" || true
  echo "    Da tao $PROD_FILE — SUA Jwt:Secret va MarketData:SyncApiKey truoc khi deploy!"
else
  echo "    Giu nguyen $PROD_FILE"
fi

echo "==> 4. Tao database StockRadarDb (neu chua co)"
if command -v sqlcmd >/dev/null 2>&1 && [ -n "$SQL_PASSWORD" ]; then
  sqlcmd -S localhost -U sa -Q "
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'StockRadarDb')
  CREATE DATABASE StockRadarDb;
" || true
  sqlcmd -S localhost -U sa -Q "
USE StockRadarDb;
IF NOT EXISTS (SELECT name FROM sys.database_principals WHERE name = N'${SQL_USER}')
BEGIN
  CREATE USER ${SQL_USER} FOR LOGIN ${SQL_USER};
  ALTER ROLE db_owner ADD MEMBER ${SQL_USER};
END
" || true
else
  echo "    Bo qua sqlcmd — tao DB thu cong neu can:"
  echo "      CREATE DATABASE StockRadarDb;"
fi

echo "==> 5. systemd stockradar (port 5281)"
cp "$PROJECT_DIR/scripts/stockradar.service" /etc/systemd/system/stockradar.service
systemctl daemon-reload
systemctl enable stockradar

echo "==> 6. nginx"
NGINX_CONF="/etc/nginx/sites-available/stockradar"
cp "$PROJECT_DIR/scripts/nginx-stockradar.conf" "$NGINX_CONF"
sed -i "s/YOUR_DOMAIN/${DOMAIN}/g" "$NGINX_CONF"
ln -sf "$NGINX_CONF" /etc/nginx/sites-enabled/stockradar
nginx -t
systemctl reload nginx

echo "==> 7. Build + deploy"
bash "$PROJECT_DIR/deploy.sh" all

echo ""
echo "========================================"
echo " StockRadar setup xong"
echo " URL: http://${DOMAIN}/"
echo " API: http://${DOMAIN}/api/v1/market"
echo ""
echo " Buoc tiep theo:"
echo "  1. Sua $PROD_FILE (JWT secret, SyncApiKey)"
echo "  2. DNS A record ${DOMAIN} -> IP server"
echo "  3. (Tuy chon) certbot --nginx -d ${DOMAIN}"
echo "  4. bash deploy.sh be"
echo "========================================"
