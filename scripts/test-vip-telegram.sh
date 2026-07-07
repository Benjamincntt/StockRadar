#!/usr/bin/env bash
# Gửi 4 tin Telegram VIP mẫu (fake GAS) — cùng format production.
# Usage: bash scripts/test-vip-telegram.sh
# Env: API_BASE (default http://127.0.0.1:5281/api/v1), SYNC_KEY (auto từ appsettings.Production.json)

set -euo pipefail

API_BASE="${API_BASE:-http://127.0.0.1:5281/api/v1}"
PROD_JSON="${PROD_JSON:-/var/www/publish/stockradar-api/appsettings.Production.json}"

if [[ -z "${SYNC_KEY:-}" ]]; then
  SYNC_KEY="$(python3 -c "import json; print(json.load(open('$PROD_JSON'))['MarketData']['SyncApiKey'])" 2>/dev/null || true)"
fi

if [[ -z "$SYNC_KEY" ]]; then
  echo "ERROR: Set SYNC_KEY hoặc MarketData:SyncApiKey trong $PROD_JSON" >&2
  exit 1
fi

echo "POST $API_BASE/market/jobs/telegram/vip-test"
curl -sS -X POST "$API_BASE/market/jobs/telegram/vip-test" \
  -H "X-Sync-Key: $SYNC_KEY" \
  -H "Content-Type: application/json"
echo
