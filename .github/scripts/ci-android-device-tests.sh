#!/usr/bin/env bash
# Invoked as a single command from reactivecircus/android-emulator-runner's `script:` input.
#
# IMPORTANT: That action's script-parser splits `script:` on newlines and runs each non-# line as
# its own `sh -c '…'`. Multi-line inline YAML therefore breaks (e.g. APK= is lost before `if`).
# Keep all device-test orchestration in this file and call it with one line from build.yml.
set -euo pipefail

cd "${GITHUB_WORKSPACE:?}"

## Bin layout intentionally tracks the build.yml `Build Android test APK` step (Debug + PublishTrimmed=true
## chosen so coverlet's Cecil resolver can find MAUI workload assemblies in bin/Debug; see that step's
## comment block for the full rationale).
APK="$(find tests/Bible.Alarm.Tests.Android/bin/Debug/net10.0-android -name '*-Signed.apk' -print -quit)"
if [ -z "${APK}" ]; then
  echo "No signed APK produced under tests/Bible.Alarm.Tests.Android/bin/Debug/net10.0-android"
  exit 1
fi

PKG=com.jthomas.info.Bible.Alarm.Tests
ACTIVITY="${PKG}/${PKG}.TestRunnerActivity"
DEVICE_RESULTS=/sdcard/Documents/test-results
ART_DIR="${GITHUB_WORKSPACE}/artifacts/coverage-android"
mkdir -p "${ART_DIR}"

adb uninstall "${PKG}" >/dev/null 2>&1 || true
adb install -r "${APK}"

adb shell rm -rf "${DEVICE_RESULTS}" >/dev/null 2>&1 || true
adb shell mkdir -p "${DEVICE_RESULTS}"

adb shell am start -W -n "${ACTIVITY}"

DEADLINE=$(( $(date +%s) + 900 ))
DONE=""
while [ "$(date +%s)" -lt "${DEADLINE}" ]; do
  PROBE="$(adb shell "if [ -f ${DEVICE_RESULTS}/done.txt ]; then cat ${DEVICE_RESULTS}/done.txt; fi" 2>/dev/null | tr -d '\r\n' || true)"
  if [ -n "${PROBE}" ]; then DONE="${PROBE}"; break; fi
  sleep 5
done

adb logcat -d -t 5000 > "${ART_DIR}/logcat.log" || true

# All test artefacts (TestResults.xml, done.txt, error.txt, coverage.android.opencover.xml) live
# under one device dir; pulling the dir in one shot is atomic-enough and avoids races where the
# next adb command's connection drops mid-pull. Belt-and-braces explicit pulls of the two files we
# absolutely need (results + coverage) follow, so a partial first pull still recovers the artefact.
# adb is gone after reactivecircus/android-emulator-runner stops the emulator, so all of this MUST
# happen here, not in a downstream workflow step.
adb shell ls "${DEVICE_RESULTS}" || true
adb pull "${DEVICE_RESULTS}/." "${ART_DIR}/" || true
adb pull "${DEVICE_RESULTS}/coverage.android.opencover.xml" "${ART_DIR}/coverage-android.xml" || true

adb uninstall "${PKG}" >/dev/null 2>&1 || true

if [ -d "${ART_DIR}/TestResults.xml" ]; then
  INNER="$(find "${ART_DIR}/TestResults.xml" -maxdepth 1 -name '*.xml' -print -quit)"
  if [ -n "${INNER}" ]; then
    mv "${INNER}" "${ART_DIR}/TestResults.xml.__hostflatten"
    rm -rf "${ART_DIR}/TestResults.xml"
    mv "${ART_DIR}/TestResults.xml.__hostflatten" "${ART_DIR}/TestResults.xml"
  else
    rm -rf "${ART_DIR}/TestResults.xml"
  fi
elif [ -f "${ART_DIR}/TestResults.xml.__tmp" ] && [ ! -f "${ART_DIR}/TestResults.xml" ]; then
  mv "${ART_DIR}/TestResults.xml.__tmp" "${ART_DIR}/TestResults.xml"
fi

if [ -z "${DONE}" ]; then
  echo "TestRunnerActivity did not produce ${DEVICE_RESULTS}/done.txt within 15 minutes."
  exit 1
fi

if [ ! -f "${ART_DIR}/coverage-android.xml" ] && [ -f "${ART_DIR}/coverage.android.opencover.xml" ]; then
  cp "${ART_DIR}/coverage.android.opencover.xml" "${ART_DIR}/coverage-android.xml"
fi

if [ "${DONE}" != "0" ]; then
  echo "Android test run reported failure (exit ${DONE})."
  [ -f "${ART_DIR}/error.txt" ] && cat "${ART_DIR}/error.txt"
  exit "${DONE}"
fi
