#!/usr/bin/env bash
# Pipeline jobs on server or local - NOT Job 1 (history backfill).
# Env: API_BASE, SYNC_KEY (auto-read from appsettings.Production.json if unset)

set -euo pipefail

API_BASE="${API_BASE:-http://127.0.0.1:5281/api/v1}"
API_BASE="${API_BASE%/}"
CRITERIA_DAYS="${CRITERIA_DAYS:-30}"
MONITOR_ROUNDS="${MONITOR_ROUNDS:-2}"
MONITOR_WAIT_SEC="${MONITOR_WAIT_SEC:-8}"

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

post_job "Job 2 - sync session day T" "/market/jobs/session"
post_job "SmartMoney analysis" "/market/jobs/analysis"
post_job "Criteria backfill ${CRITERIA_DAYS}d" "/market/jobs/criteria-backfill?days=${CRITERIA_DAYS}"

i=1
while [ "$i" -le "$MONITOR_ROUNDS" ]; do
  post_job "Job 3 - opportunity monitor ($i/${MONITOR_ROUNDS})" "/market/jobs/opportunity-monitor"
  if [ "$i" -lt "$MONITOR_ROUNDS" ] && [ "$MONITOR_WAIT_SEC" -gt 0 ]; then
    echo "    Wait ${MONITOR_WAIT_SEC}s..."
    sleep "$MONITOR_WAIT_SEC"
  fi
  i=$((i + 1))
done

echo ""
echo "==> Pipeline jobs done"
