# iOS Deployment to Connected Device and Simulator

## Deploy to iOS Simulator (Emulator)

Use this when you want to run the app on the **currently selected or booted iOS Simulator** (e.g. from Xcode or Cursor).

### Prerequisites

- Xcode installed (Simulator app and runtimes)
- .NET SDK with iOS workload (`dotnet workload install ios`)
- **Xcode version**: Ideally matches the .NET for iOS workload. If the build reports "requires Xcode X.Y, current is X.Z", either install matching Xcode or use the workaround below (ValidateXcodeVersion=false).
- **Xcode license**: Accept the license if prompted: `sudo xcodebuild -license`

### Build and push to simulator (explicit steps)

This is the reliable way to build, install, and launch on a specific simulator (e.g. after a workload update or when `dotnet -t:Run` is not used). It avoids the **static registrar hash mismatch** crash by using a single consistent toolchain per build.

**1. (Optional) Clean first** — Do this if you recently ran `dotnet workload update`, or if the app crashes with "static registrar map for Microsoft.iOS is invalid" / runtime hash mismatch:

```bash
cd src/Bible.Alarm && rm -rf bin obj
cd ../Bible.Alarm.Shared && rm -rf bin obj
cd ../../libraries/CommunityToolkit.Maui.MediaElement && rm -rf bin obj
```

**2. Build** — From repo root or `src/Bible.Alarm`. Use `-p:ValidateXcodeVersion=false` if your Xcode version is newer than the workload expects (e.g. Xcode 26.3 vs 26.2):

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet build -f net10.0-ios -c Debug -r iossimulator-arm64 -p:ValidateXcodeVersion=false
```

Omit `-p:ValidateXcodeVersion=false` when Xcode version matches the workload.

**3. Install and launch** — Use the simulator UDID from `xcrun simctl list devices` (e.g. booted iPhone or a named device like "iPhone 6.5 Store"):

```bash
APP="src/Bible.Alarm/bin/Debug/net10.0-ios/iossimulator-arm64/Bible.Alarm.app"
xcrun simctl install <SIMULATOR_UDID> "$APP"
xcrun simctl launch <SIMULATOR_UDID> com.jthomas.info.Bible.Alarm
open -a Simulator
```

Example with a specific simulator UDID:

```bash
xcrun simctl install A2D00586-09FF-4590-BEF7-0584008CFBAA "$APP"
xcrun simctl launch A2D00586-09FF-4590-BEF7-0584008CFBAA com.jthomas.info.Bible.Alarm
```

**Why clean and explicit RID?** After a workload update, old `obj/` artifacts (e.g. static registrar for Microsoft.iOS) can be built with one runtime hash while the app bundle contains a different runtime, causing a SIGABRT in `load_aot_module`. A clean build ensures the registrar and runtime match.

### Build and run on simulator (via Run target)

From the repo root:

```bash
~/.dotnet/dotnet build src/Bible.Alarm/Bible.Alarm.csproj -f net10.0-ios -c Debug -t:Run
```

This builds, installs on the running (or default) simulator, and launches. No device name or RID is needed.

### Build only (no deploy)

```bash
~/.dotnet/dotnet build src/Bible.Alarm/Bible.Alarm.csproj -f net10.0-ios -c Debug -r iossimulator-arm64
```

Add `-p:ValidateXcodeVersion=false` if you have an Xcode version mismatch.

### If `dotnet` is not on PATH

Use the full path:

```bash
~/.dotnet/dotnet build src/Bible.Alarm/Bible.Alarm.csproj -f net10.0-ios -c Debug -r iossimulator-arm64 -p:ValidateXcodeVersion=false
```

### Xcode version mismatch

**Error**: `This version of .NET for iOS (X.Y) requires Xcode A.B. The current version of Xcode is A.C`

- **Option 1**: Install the Xcode version required by the workload (see [Microsoft docs](https://aka.ms/xcode-requirement)).
- **Option 2**: Update the workload: `dotnet workload update` (may then support newer Xcode).
- **Option 3**: Build with the check disabled: add `-p:ValidateXcodeVersion=false` to the build. If you do this, **clean** (remove `bin`/`obj`) and rebuild so the static registrar and runtime are from the same toolchain; otherwise the app may crash at launch with "static registrar map for Microsoft.iOS is invalid".

---

## Deploy to Connected USB Device

### Prerequisites

- iPhone/iPad connected via USB
- Device unlocked
- Device trusted (may prompt "Trust This Computer" on first connection)
- Development provisioning profile installed (for Debug builds)
- Xcode command-line tools installed (`xcode-select --install`)

## Deploy to Connected USB iPhone

### Step 1: List the connected device

From the repo root:

```bash
xcrun devicectl list devices
```

Use the **Name** column (e.g. `iPhone 13 Pro`) for the deploy and log-copy commands below. Device must be **connected** and **unlocked**.

### Step 2: Build and run on the device

```bash
# From repo root: build and deploy to the connected iPhone
~/.dotnet/dotnet msbuild src/Bible.Alarm/Bible.Alarm.csproj \
  -t:Run \
  -p:TargetFramework=net10.0-ios \
  -p:RuntimeIdentifier=ios-arm64 \
  -p:Configuration=Debug \
  -p:_DeviceName="iPhone 13 Pro"
```

Replace `"iPhone 13 Pro"` with the exact device name from Step 1. The app will build, install, and launch on the device.

**Alternative**: You can also build and run from the IDE (e.g. Run to iPhone) if RID-specific restore or build fails from the command line.

### Build Only (No Deploy)

```bash
~/.dotnet/dotnet msbuild src/Bible.Alarm/Bible.Alarm.csproj \
  -t:Build \
  -p:TargetFramework=net10.0-ios \
  -p:RuntimeIdentifier=ios-arm64 \
  -p:Configuration=Debug \
  -p:BuildIpa=false \
  -p:_IsSimulatorBuild=false
```

## Common Issues and Solutions

### Device Not Found

**Error**: `A device must be specified using --devname`

**Solution**: 
1. List devices: `xcrun devicectl list devices`
2. Use exact device name (including spaces/capitalization) in `-p:_DeviceName="..."`

### Device Locked

**Error**: `The device was not, or could not be, unlocked`

**Solution**: Unlock the iPhone/iPad and try again

### Scene Configuration Not Working (Blank Screen)

**Symptom**: App launches but shows blank screen after splash

**Cause**: iOS caches scene configuration from previous installations

**Solution**: 
1. **Uninstall the app** from the device (long press icon → Remove App → Delete App)
2. Clean build output: `rm -rf src/Bible.Alarm/bin/Debug/net10.0-ios src/Bible.Alarm/obj/Debug/net10.0-ios`
3. Rebuild and redeploy

**Why**: When `UIApplicationSceneManifest` is added/modified in `Info.plist`, iOS must see it on a fresh install. Cached scene configurations from previous installs prevent the new scene delegate from being called.

### Build Cache Issues

If Info.plist changes aren't reflected in the built app:

1. Clean build output:
   ```bash
   rm -rf src/Bible.Alarm/bin/Debug/net10.0-ios src/Bible.Alarm/obj/Debug/net10.0-ios
   ```

2. Rebuild from scratch

3. Verify built Info.plist:
   ```bash
   plutil -convert xml1 -o - src/Bible.Alarm/bin/Debug/net10.0-ios/ios-arm64/Bible.Alarm.app/Info.plist | grep -A 30 "UIApplicationSceneManifest"
   ```

## Scene-Based Lifecycle (CarPlay Support)

When `UIApplicationSceneManifest` is present in `Info.plist`:

- **MAUI's `FinishedLaunching`** skips window creation (deferred to scene delegate)
- **SceneDelegate.WillConnect** creates the window when iOS creates the scene
- **AppDelegate.GetConfiguration** must return the correct scene configuration:
  - Main window: Call `base.GetConfiguration()` (returns `__MAUI_DEFAULT_SCENE_CONFIGURATION__`)
  - CarPlay: Return custom config with `CarPlaySceneDelegate`

**Critical**: Info.plist must use:
- Config name: `__MAUI_DEFAULT_SCENE_CONFIGURATION__` (not `Default Configuration`)
- Delegate class: `SceneDelegate` (Obj-C registered name, not C# namespace)

## Known iOS UI quirks (Schedule page)

**TimePicker AM/PM triangle bug:** On iOS, the native `UIDatePicker` used by MAUI’s `TimePicker` renders an inverted triangle instead of AM/PM text when the format includes `tt`, especially with larger font sizes. This is a known iOS/MAUI behavior that no font-size workaround reliably fixes.

**Current fix:** The Schedule page avoids the bug entirely by **not** including `tt` in the TimePicker format on iOS. Instead:

- TimePicker uses format `hh:mm` on iOS (no `tt`) — the picker wheel still shows AM/PM for selection.
- A separate **Label** next to the TimePicker displays the AM/PM designator via `TimeToMeridianConverter`.
- All platforms use `AlarmTimeFontSize` for the TimePicker; the AM/PM label uses `AlarmMeridianFontSize`.
- On Android/Windows the TimePicker keeps format `hh:mm tt` and the label is hidden.

See `ScheduleDetailsContainer.xaml` (TimePicker + Label) and `TimeToMeridianConverter.cs`.

---

## Viewing Logs (Debugging)

The **only** supported method to access the app’s Serilog logs from a connected iPhone/iPad is to **copy the log file from the device to the Mac** with `devicectl`, then read or filter it locally. Debug and Release both use an async file sink, so a log file is written as soon as the app runs.

### Prerequisites

- Device connected via USB and trusted (same as deploy)
- App has been run at least once so the log file exists
- App bundle identifier: `com.jthomas.info.Bible.Alarm`

### Step 1: List the device name

```bash
xcrun devicectl list devices
```

Use the **Name** column (e.g. `iPhone 13 Pro`) in the copy command. Use the same name as for deployment.

### Step 2: Copy the app’s log file to the Mac

On device, the log file lives under the app container. Use **today’s date** in the filename: `bible-alarm-YYYYMMDD.txt` (e.g. `20260208` for 2026-02-08).

**Try this path first** (typical on device):

```bash
# Replace "iPhone 13 Pro" with your device name from Step 1.
# Replace YYYYMMDD with today's date (e.g. date +%Y%m%d).
xcrun devicectl device copy from \
  --device "iPhone 13 Pro" \
  --domain-type appDataContainer \
  --domain-identifier com.jthomas.info.Bible.Alarm \
  --source "Library/Caches/logs/bible-alarm-YYYYMMDD.txt" \
  --destination /tmp/bible-alarm-log.txt
```

If that fails with a “file node” error, try the alternate path:

```bash
# Fallback: some setups use Documents/logs
--source "Documents/logs/bible-alarm-YYYYMMDD.txt"
```

### Step 3: View or filter the log on the Mac

```bash
# Line count and last 150 lines
wc -l /tmp/bible-alarm-log.txt && tail -150 /tmp/bible-alarm-log.txt

# Last 200 or 300 lines
tail -200 /tmp/bible-alarm-log.txt
tail -300 /tmp/bible-alarm-log.txt

# Full file
cat /tmp/bible-alarm-log.txt

# Filter for bootstrap, navigation, errors
grep -E "BOOTSTRAP|NavigateTo|Error|Exception" /tmp/bible-alarm-log.txt
```

### Notes

- Use this copy-from-device flow for reproducible log inspection. Do **not** rely on deployment-time console streaming or other channels as the canonical way to access logs.
- Log file path on device: **Library/Caches/logs/** (primary) or **Documents/logs/** (fallback), with rolling daily files and 7-day retention.
