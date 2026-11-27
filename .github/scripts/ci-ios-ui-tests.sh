#!/usr/bin/env bash
# Boots a simulator, installs the UITEST .app, and runs the iOS Appium slice.
# CarPlay external-display tests stay on the Mac Mini (Requires!=CarPlayDisplay).
set -euo pipefail

mkdir -p artifacts/ui-ios
export APPIUM_URL="${APPIUM_URL:-http://127.0.0.1:4723/}"
export UI_SCREENSHOT_DIR="${GITHUB_WORKSPACE:-$(pwd)}/artifacts/ui-ios"
export NVM_DIR="${NVM_DIR:-$HOME/.nvm}"
# shellcheck disable=SC1090
[[ -s "$NVM_DIR/nvm.sh" ]] && . "$NVM_DIR/nvm.sh" >/dev/null 2>&1 || true
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

DEVICE="$(xcrun simctl list devices available | grep -E 'iPhone [0-9]+ \(' | grep -v 'created by XHarness' | head -n 1 | sed -E 's/.*\(([A-F0-9-]+)\).*/\1/')"
echo "Booting simulator ${DEVICE}"
xcrun simctl boot "${DEVICE}" || true
xcrun simctl bootstatus "${DEVICE}" -b
export IOS_SIMULATOR_UDID="${DEVICE}"

APP="$(find src/Bible.Alarm/bin -path '*iossimulator*' -name 'Bible.Alarm.app' -type d | head -n 1 || true)"
if [[ -n "${APP}" ]]; then
  xcrun simctl install "${DEVICE}" "${APP}"
  export BIBLE_ALARM_APP_PATH="${APP}"
fi

npm install -g appium@2
appium driver install xcuitest || true
appium --port 4723 --log artifacts/ui-ios/appium.log &
APPIUM_PID=$!
for _ in $(seq 1 60); do
  if curl -sf "http://127.0.0.1:4723/status" >/dev/null; then
    break
  fi
  sleep 2
done

set +e
dotnet test tests/Bible.Alarm.UITests/Bible.Alarm.UITests.csproj \
  -c Release \
  --filter "UI=iOS&Requires!=CarPlayDisplay" \
  --logger "trx;LogFileName=ui.trx" \
  --results-directory artifacts/ui-ios
STATUS=$?
set -e

xcrun simctl spawn booted log show --last 10m --style compact > artifacts/ui-ios/simulator.log || true
kill "${APPIUM_PID}" || true
exit "${STATUS}"
