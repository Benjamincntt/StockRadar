#!/usr/bin/env bash
# Tự deploy trên server khi origin/master đổi (cron hoặc webhook).
# Cài: bash scripts/setup-server-auto-deploy.sh
set -euo pipefail

PROJECT_DIR="${PROJECT_DIR:-/var/www/StockRadar}"
BRANCH="${BRANCH:-master}"
LOG="${LOG:-/var/log/stockradar-auto-deploy.log}"
DEPLOY_TARGET="${DEPLOY_TARGET:-all}"

cd "$PROJECT_DIR"

before="$(git rev-parse HEAD)"
git fetch origin "$BRANCH" --quiet

if ! git rev-parse "origin/$BRANCH" >/dev/null 2>&1; then
  echo "$(date -Is) ERROR: origin/$BRANCH not found" >>"$LOG"
  exit 1
fi

after="$(git rev-parse "origin/$BRANCH")"
if [ "$before" = "$after" ]; then
  exit 0
fi

echo "$(date -Is) Deploy $before -> $after ($DEPLOY_TARGET)" >>"$LOG"
git checkout "$BRANCH"
git reset --hard "origin/$BRANCH"

if bash deploy.sh "$DEPLOY_TARGET" >>"$LOG" 2>&1; then
  echo "$(date -Is) OK" >>"$LOG"
else
  echo "$(date -Is) FAILED (exit $?)" >>"$LOG"
  exit 1
fi
