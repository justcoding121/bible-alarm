#!/usr/bin/env bash
#
# Mac-side iOS Appium UI test runner.
# Invoked by tests/run-ui-tests.ps1 -Platform iOS via SSH from a Windows host.
#
# Responsibilities:
#   1. Boot an iOS simulator (or use one that's already booted).
#   2. Build src/Bible.Alarm/Bible.Alarm.csproj for net10.0-ios (iossimulator-arm64).
#   3. Install the produced .app on the simulator.
#   4. Start a local Appium 2/3 server with the xcuitest driver (we are on macOS).
#   5. Run the UI=iOS slice of tests/Bible.Alarm.UITests against that Appium server.
#   6. Tear Appium down (always); leave the simulator state alone (caller decides).
#
# Why this script exists:
# Appium's xcuitest driver only works on macOS. The Windows orchestrator can't
# run it locally — instead it SSHes here, and we own the whole loop. This is
# symmetric to how tests/run-tests.ps1's Run-IOSTests delegates the unit-test
# pass to the Mac.

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

# Source nvm + dotnet if the (non-interactive) shell didn't pull them in.
# tests/run-ui-tests.ps1 invokes us via `ssh ... "cd ~/bible-alarm && ./tests/run-ui-tests-mac.sh"`,
# which is a non-login shell — ~/.zshrc patches added in Phase 2 may not apply.
export NVM_DIR="${NVM_DIR:-$HOME/.nvm}"
[[ -s "$NVM_DIR/nvm.sh" ]] && \. "$NVM_DIR/nvm.sh" >/dev/null 2>&1
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

SIM_NAME="${SIM_NAME:-iPhone 16}"
APPIUM_PORT="${APPIUM_PORT:-4723}"
APPIUM_URL_LOCAL="http://127.0.0.1:${APPIUM_PORT}/"
APPIUM_LOG="${APPIUM_LOG:-/tmp/appium-bible-alarm.log}"
TEST_RESULTS_DIR="${TEST_RESULTS_DIR:-$repo_root/TestResults/ui}"
mkdir -p "$TEST_RESULTS_DIR"

echo "[ui-mac] dotnet --version: $(dotnet --version)"
echo "[ui-mac] node --version:   $(node --version)"
echo "[ui-mac] appium --version: $(appium --version)"

# ---------------------------------------------------------------------------
# 1. Boot a simulator
# ---------------------------------------------------------------------------
udid=$(xcrun simctl list devices available 2>/dev/null \
        | grep -F "$SIM_NAME" | grep -v 'created by XHarness' | head -n 1 \
        | sed -E 's/.*\(([A-F0-9-]+)\).*/\1/' || true)
if [[ -z "$udid" ]]; then
    # This Mac may not have the named device (the Mini has iPhone 17, not iPhone 16).
    udid=$(xcrun simctl list devices available 2>/dev/null \
            | grep -E 'iPhone [0-9]+ \(' | grep -v 'created by XHarness' | head -n 1 \
            | sed -E 's/.*\(([A-F0-9-]+)\).*/\1/' || true)
fi
if [[ -z "$udid" ]]; then
    echo "[ui-mac] FATAL: no iPhone simulator is available. Install one via Xcode > Window > Devices and Simulators."
    exit 2
fi
echo "[ui-mac] Using simulator UDID $udid"
xcrun simctl boot "$udid" 2>/dev/null || true
xcrun simctl bootstatus "$udid" -b
# Surface the simulator window so visual debugging is possible if a flake hits.
open -ga Simulator || true

# ---------------------------------------------------------------------------
# 2. Build the production app for the simulator
# ---------------------------------------------------------------------------
echo "[ui-mac] Building Bible.Alarm for net10.0-ios (iossimulator-arm64)..."
dotnet build src/Bible.Alarm/Bible.Alarm.csproj \
    -c Debug \
    -f net10.0-ios \
    -p:RuntimeIdentifier=iossimulator-arm64 \
    -p:BUILD_IOS_ONLY=true \
    -p:UiTestHooks=true \
    -p:CodesignKey=- \
    -p:CodesignEntitlements= \
    -p:CodesignRequireProvisioningProfile=false \
    --nologo \
    --verbosity minimal

app_path=$(find src/Bible.Alarm/bin/Debug/net10.0-ios -maxdepth 4 -type d -name '*.app' \
            | sort -u | head -n 1)
if [[ -z "$app_path" || ! -d "$app_path" ]]; then
    echo "[ui-mac] FATAL: produced .app bundle not found under src/Bible.Alarm/bin/Debug/net10.0-ios"
    exit 3
fi
echo "[ui-mac] Built .app: $app_path"

# ---------------------------------------------------------------------------
# 3. Install on the simulator
# ---------------------------------------------------------------------------
echo "[ui-mac] Installing onto simulator $udid..."
xcrun simctl install "$udid" "$app_path"

# ---------------------------------------------------------------------------
# 4. Start Appium
# ---------------------------------------------------------------------------
# Kill any stale Appium on the same port (orphan from a previous run).
if lsof -i ":${APPIUM_PORT}" -sTCP:LISTEN -t >/dev/null 2>&1; then
    echo "[ui-mac] Port ${APPIUM_PORT} already in use; killing existing listener"
    lsof -i ":${APPIUM_PORT}" -sTCP:LISTEN -t | xargs kill -9 2>/dev/null || true
    sleep 1
fi

echo "[ui-mac] Starting Appium server on port ${APPIUM_PORT} (log: ${APPIUM_LOG})"
appium --port "${APPIUM_PORT}" --log-level info >"${APPIUM_LOG}" 2>&1 &
appium_pid=$!
trap 'kill ${appium_pid} 2>/dev/null || true' EXIT

# Wait up to 30s for /status to come up.
ready=0
for _ in $(seq 1 60); do
    if curl -fs "${APPIUM_URL_LOCAL}status" >/dev/null 2>&1; then
        ready=1
        break
    fi
    sleep 0.5
done
if [[ "$ready" -ne 1 ]]; then
    echo "[ui-mac] FATAL: Appium did not become ready within 30s. Tail of log:"
    tail -50 "${APPIUM_LOG}"
    exit 4
fi
echo "[ui-mac] Appium ready (pid ${appium_pid})"

# ---------------------------------------------------------------------------
# 5. Run the iOS UI slice
# ---------------------------------------------------------------------------
export APPIUM_URL="${APPIUM_URL_LOCAL}"
export BIBLE_ALARM_BUNDLE_ID="${BIBLE_ALARM_BUNDLE_ID:-com.jthomas.info.Bible.Alarm}"
export BIBLE_ALARM_APP_PATH="${app_path}"
export IOS_SIMULATOR_UDID="${udid}"
export UI_SCREENSHOT_DIR="${TEST_RESULTS_DIR}"
export CARPLAY_DISPLAY="${CARPLAY_DISPLAY:-0}"

if [[ "$CARPLAY_DISPLAY" == "1" ]]; then
    echo "[ui-mac] Turning the external display into a CarPlay screen"
    plist_dir="$HOME/Library/Developer/CoreSimulator/Devices/$udid/data/Library/Application Support/Simulator"
    mkdir -p "$plist_dir"
    cat > "$plist_dir/extended_display.plist" <<'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>hasTouchScreen</key>
	<true/>
	<key>height</key>
	<integer>480</integer>
	<key>scale</key>
	<integer>2</integer>
	<key>stark</key>
	<integer>1</integer>
	<key>width</key>
	<integer>800</integer>
</dict>
</plist>
EOF
    xcrun -sdk iphonesimulator clang -arch arm64 -fobjc-arc \
        -framework Foundation -framework IOSurface -framework ImageIO -framework CoreGraphics -framework QuartzCore \
        "$repo_root/tests/Bible.Alarm.UITests/Tools/carplay-capture.m" \
        -o /tmp/bible-alarm-carplay-capture
    codesign -s - --force /tmp/bible-alarm-carplay-capture
    export CARPLAY_CAPTURE_BIN=/tmp/bible-alarm-carplay-capture
    xcrun simctl spawn "$udid" launchctl kickstart -k user/foreground/com.apple.backboardd
    sleep 3
    xcrun simctl spawn "$udid" launchctl kickstart -p user/foreground/com.apple.CarPlayApp
    sleep 3
fi

echo "[ui-mac] dotnet test --filter UI=iOS"
dotnet test tests/Bible.Alarm.UITests/Bible.Alarm.UITests.csproj \
    --filter "UI=iOS" \
    --logger "console;verbosity=detailed" \
    --logger "trx;LogFileName=ui-ios.trx" \
    --results-directory "${TEST_RESULTS_DIR}"

echo "[ui-mac] DONE"
