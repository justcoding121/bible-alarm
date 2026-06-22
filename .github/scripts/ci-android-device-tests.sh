#!/usr/bin/env bash
# Invoked as a single command from reactivecircus/android-emulator-runner's `script:` input.
#
# IMPORTANT: That action's script-parser splits `script:` on newlines and runs each non-# line as
# its own `sh -c '…'`. Multi-line inline YAML therefore breaks (e.g. APK= is lost before `if`).
# Keep all device-test orchestration in this file and call it with one line from build.yml.
set -euo pipefail

cd "${GITHUB_WORKSPACE:?}"

# How long the host polls for /sdcard/Documents/test-results/done.txt after launching
# TestRunnerActivity. The full shared suite normally finishes in a few minutes on device;
# raise this only when the suite genuinely needs more wall time — a hung test will still
# burn the full budget. Override in build.yml via env ANDROID_TEST_TIMEOUT_SEC.
ANDROID_TEST_TIMEOUT_SEC="${ANDROID_TEST_TIMEOUT_SEC:-900}"
POLL_INTERVAL_SEC="${ANDROID_TEST_POLL_INTERVAL_SEC:-5}"
HEARTBEAT_INTERVAL_SEC="${ANDROID_TEST_HEARTBEAT_INTERVAL_SEC:-60}"

APK="$(find tests/Bible.Alarm.Tests.Android/bin/Release/net10.0-android -name '*-Signed.apk' -print -quit)"
if [[ -z "${APK}" ]]; then
  echo "No signed APK produced under tests/Bible.Alarm.Tests.Android/bin/Release/net10.0-android"
  exit 1
fi

PKG=com.jthomas.info.Bible.Alarm.Tests
ACTIVITY="${PKG}/${PKG}.TestRunnerActivity"
DEVICE_RESULTS=/sdcard/Documents/test-results
# Test logs only (TestResults.xml, done.txt, error.txt, logcat.log). Coverage from this slice is
# not collected — see the build.yml `Build Android test APK` step comment and the "Android coverage"
# row in [.cursor/rules/testing/multi-platform-tests.mdc] for the rationale.
ART_DIR="${GITHUB_WORKSPACE}/artifacts/android-test-logs"
mkdir -p "${ART_DIR}"

adb_cmd() {
  if [[ -n "${ANDROID_SERIAL:-}" ]]; then
    adb -s "${ANDROID_SERIAL}" "$@"
  else
    adb "$@"
  fi
}

ensure_adb_ready() {
  # android-emulator-runner exposes the booted emulator port to the script.
  if [[ -n "${EMULATOR_PORT:-}" ]]; then
    export ANDROID_SERIAL="emulator-${EMULATOR_PORT}"
    echo "Using ANDROID_SERIAL=${ANDROID_SERIAL}"
  fi

  adb start-server

  echo "Waiting for adb device..."
  adb_cmd wait-for-device

  local boot=""
  local boot_deadline=$(( $(date +%s) + 120 ))
  while [[ "$(date +%s)" -lt "${boot_deadline}" ]]; do
    boot="$(adb_cmd shell getprop sys.boot_completed 2>/dev/null | tr -d '\r\n' || true)"
    if [[ "${boot}" == "1" ]]; then
      echo "Device boot completed."
      return 0
    fi
    sleep 2
  done

  echo "Device did not report sys.boot_completed=1 within 120 seconds."
  adb_cmd devices -l || true
  exit 1
}

ensure_adb_ready

adb_cmd uninstall "${PKG}" >/dev/null 2>&1 || true
adb_cmd install -r "${APK}"

adb_cmd shell rm -rf "${DEVICE_RESULTS}" >/dev/null 2>&1 || true
adb_cmd shell mkdir -p "${DEVICE_RESULTS}"

echo "Launching ${ACTIVITY} (test timeout ${ANDROID_TEST_TIMEOUT_SEC}s)..."
if ! adb_cmd shell am start -W -n "${ACTIVITY}"; then
  echo "am start failed; dumping adb devices and recent logcat."
  adb_cmd devices -l || true
  adb_cmd logcat -d -t 200 > "${ART_DIR}/logcat-launch-failure.log" || true
  exit 1
fi

DEADLINE=$(( $(date +%s) + ANDROID_TEST_TIMEOUT_SEC ))
NEXT_HEARTBEAT=$(( $(date +%s) + HEARTBEAT_INTERVAL_SEC ))
DONE=""
while [[ "$(date +%s)" -lt "${DEADLINE}" ]]; do
  PROBE="$(adb_cmd shell "if [ -f ${DEVICE_RESULTS}/done.txt ]; then cat ${DEVICE_RESULTS}/done.txt; fi" 2>/dev/null | tr -d '\r\n' || true)"
  if [[ -n "${PROBE}" ]]; then
    DONE="${PROBE}"
    break
  fi

  now="$(date +%s)"
  if [[ "${now}" -ge "${NEXT_HEARTBEAT}" ]]; then
    remaining=$(( DEADLINE - now ))
    echo "Still waiting for done.txt (${remaining}s remaining)..."
    NEXT_HEARTBEAT=$(( now + HEARTBEAT_INTERVAL_SEC ))
  fi

  sleep "${POLL_INTERVAL_SEC}"
done

adb_cmd logcat -d -t 5000 > "${ART_DIR}/logcat.log" || true

# Pull every test artefact (TestResults.xml, done.txt, error.txt) in one shot. adb is gone once
# reactivecircus/android-emulator-runner stops the emulator, so all of this MUST happen here, not
# in a downstream workflow step.
adb_cmd shell ls "${DEVICE_RESULTS}" || true
adb_cmd pull "${DEVICE_RESULTS}/." "${ART_DIR}/" || true

adb_cmd uninstall "${PKG}" >/dev/null 2>&1 || true

if [[ -d "${ART_DIR}/TestResults.xml" ]]; then
  INNER="$(find "${ART_DIR}/TestResults.xml" -maxdepth 1 -name '*.xml' -print -quit)"
  if [[ -n "${INNER}" ]]; then
    mv "${INNER}" "${ART_DIR}/TestResults.xml.__hostflatten"
    rm -rf "${ART_DIR}/TestResults.xml"
    mv "${ART_DIR}/TestResults.xml.__hostflatten" "${ART_DIR}/TestResults.xml"
  else
    rm -rf "${ART_DIR}/TestResults.xml"
  fi
elif [[ -f "${ART_DIR}/TestResults.xml.__tmp" ]] && [[ ! -f "${ART_DIR}/TestResults.xml" ]]; then
  mv "${ART_DIR}/TestResults.xml.__tmp" "${ART_DIR}/TestResults.xml"
fi

if [[ -z "${DONE}" ]]; then
  timeout_min=$(( ANDROID_TEST_TIMEOUT_SEC / 60 ))
  echo "TestRunnerActivity did not produce ${DEVICE_RESULTS}/done.txt within ${timeout_min} minutes."
  echo "This usually means the on-device test process hung (not that the suite is slow)."
  echo "Inspect ${ART_DIR}/logcat.log for the last [PASS]/[FAIL] line before the stall."
  exit 1
fi

if [[ "${DONE}" != "0" ]]; then
  echo "Android test run reported failure (exit ${DONE})."
  [[ -f "${ART_DIR}/error.txt" ]] && cat "${ART_DIR}/error.txt"
  exit "${DONE}"
fi
