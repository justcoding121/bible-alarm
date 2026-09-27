#!/usr/bin/env bash
# xharness --launch-timeout and --timeout do not kill a wedged CoreSimulator.
# The step then sits until GitHub cancels the job, the workflow is "cancelled"
# (the Build badge shows failing), and the log upload step never runs.
# A wall-clock kill turns that hang into a normal non-zero exit so we can reset
# the simulator and retry once, still inside the job's timeout-minutes.
set -euo pipefail

cd "${GITHUB_WORKSPACE:?}"

ATTEMPTS="${IOS_XHARNESS_ATTEMPTS:-2}"
# Healthy runs finish well under this. Two full budgets plus setup stay inside
# test-ios timeout-minutes (45).
DEADLINE_SECONDS="${IOS_XHARNESS_DEADLINE_SEC:-720}"
LOG_ROOT="${GITHUB_WORKSPACE}/artifacts/xharness-ios-logs"

mkdir -p "${LOG_ROOT}"

dotnet tool restore

APP_PATH="$(find tests/Bible.Alarm.Tests.iOS/bin/Debug/net10.0-ios -maxdepth 3 -name '*.app' -print -quit)"
if [[ -z "${APP_PATH}" ]]; then
  echo "iOS .app bundle not found"
  exit 1
fi

run_with_deadline() {
  local seconds="$1"
  shift
  DEADLINE_SECONDS="${seconds}" python3 -c '
import os, signal, subprocess, sys
deadline = int(os.environ["DEADLINE_SECONDS"])
proc = subprocess.Popen(sys.argv[1:], start_new_session=True)
try:
    code = proc.wait(timeout=deadline)
except subprocess.TimeoutExpired:
    print(f"deadline exceeded ({deadline}s); killing process group {proc.pid}", flush=True)
    try:
        os.killpg(proc.pid, signal.SIGKILL)
    except ProcessLookupError:
        pass
    try:
        proc.wait(timeout=30)
    except subprocess.TimeoutExpired:
        proc.kill()
        proc.wait()
    sys.exit(124)
sys.exit(code)
' "$@"
}

reset_simulators() {
  # killall is non-zero when the process is already gone.
  killall Simulator || true
  killall -9 com.apple.CoreSimulator.CoreSimulatorService || true
  run_with_deadline 120 xcrun simctl shutdown all || true
  local udid
  while IFS= read -r udid; do
    [[ -z "${udid}" ]] && continue
    run_with_deadline 60 xcrun simctl delete "${udid}" || true
  done < <(xcrun simctl list devices | grep "created by XHarness" | sed -E 's/.*\(([A-F0-9-]+)\).*/\1/' || true)
}

attempt=1
code=1
while [[ "${attempt}" -le "${ATTEMPTS}" ]]; do
  echo "xharness attempt ${attempt}/${ATTEMPTS} (deadline ${DEADLINE_SECONDS}s)"
  out="${LOG_ROOT}/attempt-${attempt}"
  mkdir -p "${out}"
  set +e
  run_with_deadline "${DEADLINE_SECONDS}" \
    dotnet xharness apple test \
      --app="${APP_PATH}" \
      --target=ios-simulator-64 \
      --launch-timeout=00:08:00 \
      --timeout=00:10:00 \
      --output-directory="${out}"
  code=$?
  set -e
  if [[ "${code}" -eq 0 ]]; then
    exit 0
  fi
  echo "xharness attempt ${attempt} exited ${code}"
  if [[ "${attempt}" -lt "${ATTEMPTS}" ]]; then
    echo "Resetting simulators before retry"
    reset_simulators
    sleep 5
  fi
  attempt=$((attempt + 1))
done

exit "${code}"
