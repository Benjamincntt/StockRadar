#!/usr/bin/env bash
# Tạo venv cho Optuna tuning (tránh PEP 668 externally-managed-environment).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VENV="${TUNE_VENV:-$ROOT/.venv-tune}"

if ! python3 -c "import venv" 2>/dev/null; then
  echo "Cần python3-venv: apt install -y python3-venv python3-full" >&2
  exit 1
fi

if [ ! -d "$VENV" ]; then
  echo "==> Tạo venv: $VENV"
  python3 -m venv "$VENV"
fi

echo "==> Cài optuna..."
"$VENV/bin/pip" install -U pip
"$VENV/bin/pip" install -r "$ROOT/scripts/tune-optuna-requirements.txt"

echo ""
echo "OK. Chạy tuning:"
echo "  source $VENV/bin/activate"
echo "  cd $ROOT && python scripts/tune-optuna.py --trials 30"
echo ""
echo "Hoặc một dòng:"
echo "  $VENV/bin/python $ROOT/scripts/tune-optuna.py --trials 30"
