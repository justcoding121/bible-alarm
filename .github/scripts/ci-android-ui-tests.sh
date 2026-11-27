#!/usr/bin/env bash
# Runs inside android-emulator-runner. All adb work stays in this script because the emulator
# stops when the action returns.
set -euo pipefail

FILTER="${UI_TEST_FILTER:-UI=Android}"
APK_DIR="${UI_APK_DIR:-artifacts/ui-apks}"
mkdir -p artifacts/ui-android "$APK_DIR"

adb wait-for-device
adb shell input keyevent 82 || true
# A zero animator duration scale spins MAUI's repeating ValueAnimator and the home screen never leaves the spinner.
adb shell settings put global animator_duration_scale 1 || true
adb shell settings put global transition_animation_scale 1 || true
adb shell settings put global window_animation_scale 1 || true
adb shell settings put global hide_error_dialogs 1 || true

APP_APK="$(find src/Bible.Alarm/bin -name '*Signed*.apk' -o -name '*.apk' | head -n 1 || true)"
if [[ -z "${APP_APK}" ]]; then
  APP_APK="$(find . -path ./tests/Bible.Alarm.CarHostSim -prune -o -name '*-Signed.apk' -print | head -n 1 || true)"
fi
HARNESS_APK="$(find tests/Bible.Alarm.CarHostSim/bin -name '*.apk' | head -n 1 || true)"

if [[ -n "${APP_APK}" ]]; then
  adb install -r -t "${APP_APK}"
fi
if [[ -n "${HARNESS_APK}" ]]; then
  adb install -r -t "${HARNESS_APK}" || echo "CarHostSim install failed; Android Auto package may already own this id."
fi

npm install -g appium@2
appium driver install uiautomator2@3.9.9 || true
appium --port 4723 --log artifacts/ui-android/appium.log &
APPIUM_PID=$!

for _ in $(seq 1 60); do
  if curl -sf "http://127.0.0.1:4723/status" >/dev/null; then
    break
  fi
  sleep 2
done

export APPIUM_URL="http://127.0.0.1:4723/"
export UI_SCREENSHOT_DIR="${GITHUB_WORKSPACE}/artifacts/ui-android"
set +e
dotnet test tests/Bible.Alarm.UITests/Bible.Alarm.UITests.csproj \
  -c Release \
  --filter "${FILTER}" \
  --logger "trx;LogFileName=ui.trx" \
  --results-directory artifacts/ui-android
STATUS=$?
set -e

adb logcat -d -v time > artifacts/ui-android/logcat.txt || true
adb shell dumpsys media_session > artifacts/ui-android/media_session.txt || true
kill "${APPIUM_PID}" || true
exit "${STATUS}"
