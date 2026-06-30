#!/usr/bin/env bash
# Cài Flutter SDK trên server Linux (chạy 1 lần, idempotent).
set -euo pipefail

FLUTTER_ROOT="${FLUTTER_ROOT:-/opt/flutter}"

if [ ! -x "$FLUTTER_ROOT/bin/flutter" ]; then
  echo "==> Clone Flutter stable -> $FLUTTER_ROOT"
  rm -rf "$FLUTTER_ROOT"
  git clone https://github.com/flutter/flutter.git -b stable --depth 1 "$FLUTTER_ROOT"
fi

export PATH="$FLUTTER_ROOT/bin:$PATH"
flutter config --enable-web --no-analytics
flutter precache --web
flutter --version
