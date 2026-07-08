#!/usr/bin/env bash
# Chấm ngược tiêu chí N ngày (rolling 7/30) — chạy nền, không phụ thuộc SSH.
# Usage on server:
#   bash /var/www/StockRadar/scripts/criteria-backfill-only.sh
#   bash /var/www/StockRadar/scripts/criteria-backfill-only.sh 30

set -euo pipefail

DAYS="${1:-30}"
API_BASE="${API_BASE:-http://127.0.0.1:5281/api/v1}"
API_BASE="${API_BASE%/}"
LOG="/tmp/criteria-backfill.log"
OUT="/tmp/criteria-backfill.json"

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
  echo "ERROR: SYNC_KEY not found" >&2
  exit 1
fi

echo "==> Criteria backfill ${DAYS}d (nohup) -> ${OUT}"
nohup curl -sS -X POST \
  -H "X-Sync-Key: ${SYNC_KEY}" \
  -H "Content-Type: application/json" \
  -d '{}' \
  "${API_BASE}/market/jobs/criteria-backfill?days=${DAYS}" \
  -o "${OUT}" \
  > "${LOG}" 2>&1 &

echo "PID: $!"
echo "Theo doi: tail -f ${LOG}"
echo "Ket qua:  cat ${OUT} | python3 -m json.tool"
