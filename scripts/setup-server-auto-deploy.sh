#!/usr/bin/env bash
# Cài cron auto-deploy trên server (thay GitHub Actions khi billing bị khóa).
set -euo pipefail

PROJECT_DIR="${PROJECT_DIR:-/var/www/StockRadar}"
CRON_SCHEDULE="${CRON_SCHEDULE:-*/5 * * * *}"
SCRIPT="$PROJECT_DIR/scripts/server-auto-deploy.sh"

chmod +x "$SCRIPT"
touch /var/log/stockradar-auto-deploy.log

MARK="# stockradar-auto-deploy"
(crontab -l 2>/dev/null | grep -v "$MARK" || true
 echo "$CRON_SCHEDULE $SCRIPT $MARK") | crontab -

echo "Da cai cron: $CRON_SCHEDULE"
echo "Log: /var/log/stockradar-auto-deploy.log"
crontab -l | grep stockradar || true
