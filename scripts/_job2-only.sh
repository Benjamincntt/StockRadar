#!/usr/bin/env bash
set -euo pipefail
API_BASE="${API_BASE:-http://127.0.0.1:5281/api/v1}"
SYNC_KEY="${SYNC_KEY:-$(python3 -c "import json; print(json.load(open('/var/www/publish/stockradar-api/appsettings.Production.json'))['MarketData']['SyncApiKey'])")}"
echo "==> Job 2 session"
curl -sfS -X POST -H "X-Sync-Key: $SYNC_KEY" -H "Content-Type: application/json" -d "{}" \
  "$API_BASE/market/jobs/session" | python3 -m json.tool
