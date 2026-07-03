#!/usr/bin/env bash
# Chay pipeline job tren server (hoac local) — KHONG Job 1 (history backfill).
# Env: API_BASE, SYNC_KEY (tu dong doc tu appsettings.Production.json neu thieu)

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
  echo "LOI: Khong tim thay SYNC_KEY. Dat env SYNC_KEY hoac appsettings.Production.json" >&2
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
    echo "LOI HTTP $http_code:" >&2
    cat /tmp/stockradar-job-out.json >&2
    exit 1
  fi
  python3 -m json.tool /tmp/stockradar-job-out.json 2>/dev/null || cat /tmp/stockradar-job-out.json
}

echo "========================================"
echo " StockRadar pipeline (khong Job 1)"
echo " API: $API_BASE"
echo "========================================"

post_job "Universe rescreen" "/market/jobs/universe-rescreen"
post_job "Job 2 - sync phien ngay T" "/market/jobs/session"
post_job "Phan tich SmartMoney" "/market/jobs/analysis"
post_job "Criteria backfill ${CRITERIA_DAYS}d" "/market/jobs/criteria-backfill?days=${CRITERIA_DAYS}"

i=1
while [ "$i" -le "$MONITOR_ROUNDS" ]; do
  post_job "Job 3 - opportunity monitor ($i/${MONITOR_ROUNDS})" "/market/jobs/opportunity-monitor"
  if [ "$i" -lt "$MONITOR_ROUNDS" ] && [ "$MONITOR_WAIT_SEC" -gt 0 ]; then
    echo "    Cho ${MONITOR_WAIT_SEC}s..."
    sleep "$MONITOR_WAIT_SEC"
  fi
  i=$((i + 1))
done

echo ""
echo "==> Pipeline job xong"
