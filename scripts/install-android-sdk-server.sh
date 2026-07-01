#!/usr/bin/env bash
# Cài Android SDK command-line (server Linux) để build APK Flutter.
set -euo pipefail

ANDROID_HOME="${ANDROID_HOME:-/opt/android-sdk}"
CMD_TOOLS_ZIP="${CMD_TOOLS_ZIP:-https://dl.google.com/android/repository/commandlinetools-linux-11076708_latest.zip}"

export ANDROID_HOME
export PATH="$ANDROID_HOME/cmdline-tools/latest/bin:$ANDROID_HOME/platform-tools:$PATH"

if [ ! -x "$ANDROID_HOME/cmdline-tools/latest/bin/sdkmanager" ]; then
  echo "==> Cài Java + unzip"
  apt-get update -qq
  apt-get install -y openjdk-17-jdk unzip wget

  echo "==> Tải Android command-line tools -> $ANDROID_HOME"
  rm -rf "$ANDROID_HOME"
  mkdir -p "$ANDROID_HOME/cmdline-tools"
  tmp="$(mktemp -d)"
  wget -q "$CMD_TOOLS_ZIP" -O "$tmp/cmdtools.zip"
  unzip -q "$tmp/cmdtools.zip" -d "$tmp"
  mv "$tmp/cmdline-tools" "$ANDROID_HOME/cmdline-tools/latest"
  rm -rf "$tmp"

  echo "==> sdkmanager: licenses + packages"
  yes | sdkmanager --licenses >/dev/null || true
  sdkmanager "platform-tools" "platforms;android-35" "build-tools;35.0.0"
fi

if [ -x /opt/flutter/bin/flutter ]; then
  /opt/flutter/bin/flutter config --android-sdk "$ANDROID_HOME"
fi

echo "==> Android SDK OK: $ANDROID_HOME"
