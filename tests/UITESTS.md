# UI integration tests (Appium)

This folder ships **Appium-driven UI integration tests** as a separate concern from the host /
device unit tests covered by `run-tests.ps1` and `tests/README.md`. UI tests exercise the
**production app**, not the test bundle, and need a running Appium 2 server plus a deployed app.

| Project | TFM | Drives | Trait |
| --- | --- | --- | --- |
| `tests/Bible.Alarm.UITests/` | `net10.0` | All three platforms via WebDriver | `[Trait("UI", "<Windows\|Android\|iOS>")]` |

The current state ships **one smoke test per platform** — each launches the production app and
asserts the home page renders. The home `Grid` already has `AutomationId="HomePage"` (see
`src/Bible.Alarm/Views/Home.xaml`), so the assertion is `MobileBy.AccessibilityId("HomePage")` on
all three platforms.

---

## 1. One-time installation (this Windows machine)

### 1.1 Node + Appium 2

You already have Node 22.22 and npm 11.4 — both are well above Appium 2's `node>=20` floor.

```pwsh
# Install Appium 2 globally.
npm install -g appium@2

# Sanity-check.
appium --version              # expect 2.x

# Install platform drivers. These are managed by Appium itself, NOT npm.
appium driver install --source=npm appium-windows-driver
appium driver install uiautomator2

# Optional (only if you ever run iOS tests directly on this box, which is unsupported on
# Windows — iOS runs on the networked Mac, see section 4).
# appium driver install xcuitest
```

`appium driver list --installed` should print `windows` and `uiautomator2` once both succeed.

### 1.2 WinAppDriver (legacy but the only stable Windows option)

The Appium `windows` driver is a thin shim over WinAppDriver, so WinAppDriver itself must also
be installed. Microsoft archived the repo in 2024 but the v1.2.1 MSI still works on Windows 10
and 11.

```pwsh
# Preferred: winget (if the source is reachable).
winget install Microsoft.WinAppDriver

# Fallback: download the MSI from
# https://github.com/microsoft/WinAppDriver/releases/tag/v1.2.1
# and run it as administrator.
```

After install, **enable Developer Mode** (Settings → For developers → Developer Mode), otherwise
WinAppDriver refuses to attach to MSIX apps.

### 1.3 Android command-line tools (only if you run `-Platform Android` locally)

The existing host-tests setup already requires the Android SDK. Confirm `adb` is on PATH:

```pwsh
adb version
```

If not, install via Android Studio or `winget install Google.AndroidStudio` and add
`%LOCALAPPDATA%\Android\Sdk\platform-tools` to PATH.

### 1.4 iOS — Mac side (only if you run `-Platform iOS` locally)

iOS UI tests cannot run on Windows. Setup is **on the networked Mac**:

```bash
# On the Mac, once:
brew install node
npm install -g appium@2
appium driver install xcuitest
xcodebuild -version    # Xcode + simulators must be installed
```

Then in this repo on the Mac, copy the production iOS .app bundle into a known path and create
`tests/run-ui-tests-mac.sh` (template provided as a follow-up; not required for Windows /
Android runs).

---

## 2. Local invocation

### 2.1 Windows

```pwsh
# Default flow: build+deploy MSIX, auto-discover AUMID, start Appium, run, stop.
./tests/run-ui-tests.ps1 -Platform Windows

# Skip the deploy if Bible Alarm is already installed on this machine.
./tests/run-ui-tests.ps1 -Platform Windows -SkipDeploy

# Override the AUMID explicitly (useful when multiple builds are installed).
./tests/run-ui-tests.ps1 -Platform Windows -SkipDeploy `
  -Aumid '7610JehonathanThomas.BibleAlarm_<hash>!App'
```

To find the AUMID manually:

```pwsh
(Get-StartApps | Where-Object Name -eq 'Bible Alarm').AppId
```

### 2.2 Android

```pwsh
# Boot an emulator first.
emulator -avd Pixel_7_API_34 &   # in a separate terminal
adb wait-for-device

# Then:
./tests/run-ui-tests.ps1 -Platform Android
```

### 2.3 iOS

```pwsh
# Requires SSH access to a Mac with the Bible Alarm .app already built and run-ui-tests-mac.sh
# deployed. The script SSHes over and runs the Mac-side flow.
./tests/run-ui-tests.ps1 -Platform iOS -MacHost user@my-mac.local
```

---

## 3. Direct `dotnet test` invocation

If you've already started `appium` manually and have the app deployed, you can drive a single
trait without the orchestrator:

```pwsh
$env:BIBLE_ALARM_AUMID = (Get-StartApps | ? Name -eq 'Bible Alarm').AppId
$env:APPIUM_URL = 'http://127.0.0.1:4723/'

dotnet test tests/Bible.Alarm.UITests/Bible.Alarm.UITests.csproj `
    --filter 'UI=Windows' --logger 'console;verbosity=detailed'
```

The exact same `--filter` switches `UI=Android` / `UI=iOS` work for the other platforms.

---

## 4. Why these tests are NOT in the coverage pass

The coverage-collecting test pass (`run-tests.ps1`, the `test-windows` CI job) explicitly
excludes UI tests via:

```text
--filter Platform!=Android&Platform!=iOS&UI!=Windows&UI!=Android&UI!=iOS
```

Reasons:

1. **They don't produce coverage** — Appium tests drive a process out-of-band; coverlet has no
   visibility into the deployed MSIX/APK/IPA's line execution.
2. **They need infrastructure** — running them inside the coverage pass would fail when Appium
   isn't up, polluting the gate.
3. **They are inherently flakier** — UI timing, simulator boot, device sleep. Keeping them on a
   separate gate lets coverage stay green even when a UI smoke test needs a retry.

When CI wires up `ui-windows`, `ui-android`, `ui-ios` jobs (see `multi-platform-tests.mdc` for
the eventual layout), they'll publish their own pass/fail badge and never block the SonarCloud
coverage rollup.

---

## 5. Adding a new UI test

1. Pick (or add) a fixture under `tests/Bible.Alarm.UITests/Fixtures/` for your platform.
2. Write a class in `tests/Bible.Alarm.UITests/Smoke/` (or a new subfolder for non-smoke flows).
3. Tag it with the right `[Trait("UI", "<Platform>")]` so the filters keep working.
4. Use `_fixture.WaitForAccessibilityId("<AutomationId>")`. If the production view doesn't have
   an `AutomationId` on the element you need, **add one in the XAML first** — that's a
   one-line, zero-risk change and is the canonical anchor for cross-platform Appium queries.
5. Run locally with the orchestrator before pushing.
