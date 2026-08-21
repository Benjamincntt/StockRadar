#!/usr/bin/env bash
# Pipeline jobs on server or local - NOT Job 1 (history backfill).
# Chi chay SmartMoney analysis (Top) - Job 2 session sync, criteria backfill,
# va opportunity monitor VIP da co lich Quartz tu dong rieng, khong can trigger
# lai ngay sau deploy.
# Env: API_BASE, SYNC_KEY (auto-read from appsettings.Production.json if unset)

set -euo pipefail

API_BASE="${API_BASE:-http://127.0.0.1:5281/api/v1}"
API_BASE="${API_BASE%/}"

if [ -z "${SYNC_KEY:-}" ]; then
  for CFG in \
    "/var/www/publish/stockradar-api/appsettings.Production.json" \
    "/var/www/StockRadar/backend/StockRadar.Api/appsettings.Production.json"
  do
    if [ -f "$CFG" ]; then
      SYNC_KEY="$(python3 -c "import json; print(json.load(open('$CFG'))['MarketData']['SyncApiKey'])")"
      break
    fi
  done
fi

if [ -z "${SYNC_KEY:-}" ]; then
  echo "ERROR: SYNC_KEY not found. Set env or appsettings.Production.json" >&2
  exit 1
fi

wait_for_api() {
  local url="${API_BASE}/ml/ranker/status"
  local max="${API_READY_TIMEOUT_SEC:-90}"
  local waited=0
  echo "==> Cho API san sang (toi da ${max}s)..."
  while [ "$waited" -lt "$max" ]; do
    if curl -sS -f -o /dev/null "$url" 2>/dev/null; then
      echo "    API ready sau ${waited}s"
      return 0
    fi
    sleep 2
    waited=$((waited + 2))
  done
  echo "ERROR: API khong phan hoi tai ${API_BASE} sau ${max}s" >&2
  echo "       Kiem tra: systemctl status stockradar && journalctl -u stockradar -n 30" >&2
  exit 1
}

post_job() {
  local name="$1"
  local path="$2"
  echo ""
  echo "==> $name"
  echo "    POST ${API_BASE}${path}"
  local http_code
  http_code=$(curl -sS -w "%{http_code}" -o /tmp/stockradar-job-out.json -X POST \
    -H "X-Sync-Key: ${SYNC_KEY}" \
    -H "Content-Type: application/json" \
    -d "{}" \
    "${API_BASE}${path}")
  if [ "$http_code" -lt 200 ] || [ "$http_code" -ge 300 ]; then
    echo "ERROR HTTP $http_code:" >&2
    cat /tmp/stockradar-job-out.json >&2
    exit 1
  fi
  python3 -m json.tool /tmp/stockradar-job-out.json 2>/dev/null || cat /tmp/stockradar-job-out.json
}

echo "========================================"
echo " StockRadar pipeline (no Job 1)"
echo " API: $API_BASE"
echo "========================================"

wait_for_api

post_job "SmartMoney analysis" "/market/jobs/analysis"

echo ""
echo "==> Pipeline jobs done"
