# Multi-platform tests

This folder ships:

- `Platforms/Android/**` — Android-only smoke tests, only compiled by `Bible.Alarm.Tests.Android` (`net10.0-android`).
- `Platforms/iOS/**` — iOS-only smoke tests, only compiled by `Bible.Alarm.Tests.iOS` (`net10.0-ios`).
- `run-tests.ps1` — local orchestrator that drives the same three test passes CI runs (`test-windows`, `test-android`, `test-ios`).
- `Bible.Alarm.UITests/` + `run-ui-tests.ps1` — Appium-driven UI integration tests against the *deployed* production app (Windows / Android / iOS). See [`UITESTS.md`](./UITESTS.md). These run on a separate gate from the coverage pass.

The cross-platform tests now live under `tests/Bible.Alarm.Tests/` (Windows host) and `tests/Bible.Alarm.Shared.Tests/` (cross-platform host); the Android/iOS hosts include them via a `Compile` glob so test code is not duplicated.

## Test-host architecture

```
tests/Bible.Alarm.Tests              net10.0-windows10.0.19041.0   dotnet test (host-based)
tests/Bible.Alarm.Shared.Tests       net10.0                       dotnet test (host-based)
tests/Bible.Alarm.Tests.Android      net10.0-android (.apk)        xharness android test (emulator)
tests/Bible.Alarm.Tests.iOS          net10.0-ios     (.app)        xharness apple test  (simulator)
```

Each host runs the *same* xunit fixtures plus its own platform-only smoke tests. Only the Windows host produces an OpenCover XML — SonarCloud reads `artifacts/coverage-windows/coverage-windows.xml` directly inside the `test-windows` CI job (the SonarScanner `begin`/`end` steps wrap that job's build + test pass; there is no separate `sonar` job). Android and iOS hosts intentionally do **not** emit coverage; see Hard rule #6 and the "Android/iOS coverage" rows in [`.cursor/rules/testing/multi-platform-tests.mdc`](../.cursor/rules/testing/multi-platform-tests.mdc) for the full rationale.

## Local prerequisites

### One-time, on every dev box

```pwsh
dotnet workload install maui
```

### Windows-only host (used by `-Platform Windows`)

Nothing extra. The `net10.0` Shared.Tests and `net10.0-windows…` Bible.Alarm.Tests projects run from `dotnet test`.

### Android (used by `-Platform Android` and `-Platform All`)

1. Install Android Studio + SDK Platform-Tools (gives you `adb`, `emulator`).
2. Create an AVD with API level 30+ (Pixel 8, Tiramisu image works).
3. Make sure `adb` and `emulator` are on `PATH`, or set `$env:ANDROID_HOME` and `run-tests.ps1` will find them.

xharness itself is installed automatically by `run-tests.ps1` as a *local* dotnet tool (manifest in `.config/dotnet-tools.json`). No global tool installs are required.

### iOS (used by `-Platform iOS` and `-Platform All`)

iOS device tests are built and executed on macOS. The script uses SSH to talk to a Mac on your LAN.

On the Mac:

```bash
# 1. Install the .NET 10 SDK matching this repo's global.json
# 2. Install workloads
dotnet workload install maui-ios

# 3. Install Xcode 16+ from the App Store (with iOS 18 simulator runtime)

# 4. Install xharness as a local tool inside the repo (script does this on first run)
dotnet new tool-manifest --force
dotnet tool install Microsoft.DotNet.XHarness.CLI \
    --version "10.0.0-prerelease.26230.1" \
    --add-source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json
```

On the Windows side:

```pwsh
# Generate an SSH key and copy it to the Mac so authentication is passwordless
ssh-keygen -t ed25519
ssh-copy-id user@mac.local         # if `ssh-copy-id` is available on Windows OpenSSH
# Otherwise: cat ~/.ssh/id_ed25519.pub | ssh user@mac.local "cat >> ~/.ssh/authorized_keys"

# Confirm the connection
ssh user@mac.local 'dotnet --info'
```

## Running tests

```pwsh
# Windows + Shared only (fast inner-loop)
./tests/run-tests.ps1 -Platform Windows

# Android only (boots an AVD if none is already running)
./tests/run-tests.ps1 -Platform Android -AndroidAvd Pixel_8_API_34

# iOS only (over SSH to a networked Mac)
./tests/run-tests.ps1 -Platform iOS -MacHost user@mac.local

# Everything (Windows + Android + iOS). Only the Windows pass emits coverage XML; the Android
# and iOS slices are pass/fail-only (see Hard rule #6 in
# .cursor/rules/testing/multi-platform-tests.mdc).
./tests/run-tests.ps1 -Platform All -MacHost user@mac.local
```

For local cross-platform runs, an HTML report is written to `TestResults/merged/index.html` for human inspection. **In CI, only the Windows OpenCover XML is produced**: `test-windows`'s `Stage coverage artifacts` step copies every per-project `coverage.opencover.xml` into `artifacts/coverage-windows/` (minimum 8 files enforced), which SonarCloud unions via `sonar.cs.opencover.reportsPaths=artifacts/coverage-windows/*.opencover.xml`. CI also uploads a merged `coverage-report-html` artifact (ReportGenerator). Android and iOS hosts upload only test logs (`android-test-logs`, `xharness-ios-logs`), not coverage. See `.github/workflows/build.yml` and `.docs/SONARCLOUD_QUALITY_GATE.md`.

### Coverage gap analysis (local)

```pwsh
# After downloading CI artifact coverage-windows into TestResults/coverage-windows:
pwsh .tools/scripts/analyze-opencover-gaps.ps1 -ReportsDir TestResults/coverage-windows -Top 25

# SonarCloud trend (requires SONAR_TOKEN):
pwsh .tools/scripts/track-sonar-coverage.ps1
```

Baseline top-25 gaps from run 28003978806: `TestResults/coverage-baseline-top25.txt`.

## Adding a platform-only test

1. **Android**: drop a `.cs` file under `tests/Platforms/Android/` (or any subfolder). Tag the class with `[Trait("Platform","Android")]` so the Windows runner can filter it out (`--filter Platform!=Android`). It will be picked up by the `Bible.Alarm.Tests.Android` Compile glob.
2. **iOS**: same, under `tests/Platforms/iOS/` with `[Trait("Platform","iOS")]`.
3. **Windows**: drop the file under `tests/Bible.Alarm.Tests/Platforms/Windows/`. The Android/iOS hosts already exclude `..\Bible.Alarm.Tests\Platforms\**` from their Compile glob.
4. **Cross-platform** (the common case): drop it anywhere under `tests/Bible.Alarm.Tests/` *outside* `Platforms/`. All three hosts (Windows / Android / iOS) compile and run it.

## Troubleshooting

- **Android build fails with `XA0119`** — disable AOT for the test app: it's already off in `Bible.Alarm.Tests.Android.csproj`. If you've added `-p:AndroidAotEnable=true` on the command line, drop it.
- **`adb: device offline`** — the emulator finished its boot animation but the IDE hasn't reconnected yet. Run `adb kill-server; adb start-server` and retry.
- **`xharness apple test --target=ios-simulator-64` exits with `Apple Simulator runtime missing`** — open Xcode → Settings → Components → install the iOS simulator runtime, then `xcrun simctl list runtimes` to confirm.
- **Several booted simulators after repeating iOS tests locally** — `tests/run-tests.ps1 -Platform iOS` passes xharness `reset-simulator` on the networked Mac so each pass shuts down afterward. GitHub Actions uses plain `apple test` without reset (runners are ephemeral). For raw CLI like CI, omit `reset-simulator`.
- **SSH hangs on `Verifying host fingerprint`** — first connection only; the script does not pass `-o StrictHostKeyChecking=no` so you have to accept the fingerprint once.
- **Local merge prints `No OpenCover XML reports found`** — only the Windows pass produces coverage XML; if you ran `-Platform Android` or `-Platform iOS` standalone there is nothing to merge by design (Android/iOS hosts intentionally do not collect coverage — see Hard rule #6 in [`.cursor/rules/testing/multi-platform-tests.mdc`](../.cursor/rules/testing/multi-platform-tests.mdc)). Run `-Platform Windows` (or `-Platform All`) and confirm the Windows test pass actually finished. The Windows host uses `--collect:"XPlat Code Coverage;Format=opencover,cobertura"` (via `coverlet.collector`) automatically — there is no longer a `-p:CollectCoverage=true` knob to forget. Re-run with `-Verbose` to see the underlying commands.
