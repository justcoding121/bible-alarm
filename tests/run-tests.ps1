#requires -Version 7.0
<#
    .SYNOPSIS
        Local-developer test orchestrator for the Bible.Alarm multi-platform test suite.

    .DESCRIPTION
        Runs Windows + Shared host-based tests directly via `dotnet test`, drives an Android emulator
        via plain adb (`am start` against TestRunnerActivity, then poll for done.txt), and
        (optionally) builds + runs the iOS test bundle on a networked macOS machine over SSH. After
        all selected platforms pass, merges the OpenCover XML reports into a single HTML report
        under TestResults/merged via dotnet-reportgenerator-globaltool.

        Designed to mirror the CI fan-out described in .github/workflows/build.yml so an issue
        reproduces the same way locally as it does on a PR.

        Why no `xharness android test` here: the Mono runtime is not initialized when Android
        instantiates a custom Instrumentation, so its native methods crash with UnsatisfiedLinkError
        before any tests execute. The activity runs *after* Application.OnCreate, side-stepping the
        race. See .cursor/rules/testing/multi-platform-tests.mdc for the full story.

    .PARAMETER Platform
        Which test set to run. One of: Windows, Android, iOS, All.

    .PARAMETER Configuration
        MSBuild configuration. Defaults to Release to match CI.

    .PARAMETER MacHost
        SSH host (e.g. mac.local or user@mac.local) where the iOS build runs. Required when
        -Platform is iOS or All. The host needs:
          * passwordless SSH (key auth)
          * `dotnet` 10.x with the maui-ios workload
          * Xcode 16+ with iOS 18 simulator runtime
          * `xharness` installed via dotnet tool on the Mac (see tests/README.md). The Mac restores
            it from the repo's local tool manifest; this Windows script no longer touches xharness.

    .PARAMETER MacRepoRoot
        Absolute path on the Mac where the repo will be checked out / synced. Defaults to
        ~/work/bible-alarm.

    .PARAMETER AndroidAvd
        Name of the AVD to boot for Android tests. Defaults to the first installed AVD.

    .PARAMETER SkipMerge
        Skip the ReportGenerator merge step (useful when iterating on a single platform).

    .EXAMPLE
        ./tests/run-tests.ps1 -Platform Windows

    .EXAMPLE
        ./tests/run-tests.ps1 -Platform Android -AndroidAvd Pixel_8_API_34

    .EXAMPLE
        ./tests/run-tests.ps1 -Platform All -MacHost user@mac.local
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Windows', 'Android', 'iOS', 'All')]
    [string]$Platform,

    [string]$Configuration = 'Release',

    [string]$MacHost,

    [string]$MacRepoRoot = '~/work/bible-alarm',

    [string]$AndroidAvd,

    [switch]$SkipMerge
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$testResultsRoot = Join-Path $repoRoot 'TestResults'
$null = New-Item -ItemType Directory -Path $testResultsRoot -Force

function Write-Section {
    param([string]$Title)
    Write-Host ''
    Write-Host ('=' * 80) -ForegroundColor Cyan
    Write-Host "  $Title" -ForegroundColor Cyan
    Write-Host ('=' * 80) -ForegroundColor Cyan
}

function Invoke-CommandChecked {
    param(
        [string]$File,
        [string[]]$ArgList,
        [string]$WorkingDirectory
    )
    Push-Location ($WorkingDirectory ?? (Get-Location))
    try {
        & $File @ArgList
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed with exit ${LASTEXITCODE}: $File $($ArgList -join ' ')"
        }
    } finally {
        Pop-Location
    }
}

function Test-Tool {
    param([string]$Name)
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

function Resolve-AndroidEmulatorExe {
    $first = Get-Command emulator -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($first) {
        foreach ($propName in @('Path', 'Source')) {
            $prop = $first.PSObject.Properties[$propName]
            if (-not $prop) { continue }
            $candidate = [string]$prop.Value
            if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
                return $candidate
            }
        }
    }

    foreach ($root in @($env:ANDROID_HOME, $env:ANDROID_SDK_ROOT)) {
        if ([string]::IsNullOrWhiteSpace($root)) { continue }
        $exe = Join-Path $root 'emulator/emulator.exe'
        if (Test-Path -LiteralPath $exe) { return $exe }
    }

    $defaultSdk = Join-Path $env:LOCALAPPDATA 'Android\Sdk'
    $defaultExe = Join-Path $defaultSdk 'emulator/emulator.exe'
    if (Test-Path -LiteralPath $defaultExe) { return $defaultExe }

    return $null
}

function Run-WindowsTests {
    Write-Section 'Windows + Shared (host-based dotnet test)'
    $coverageDir = Join-Path $testResultsRoot 'windows'
    $null = New-Item -ItemType Directory -Path $coverageDir -Force

    Invoke-CommandChecked -File 'dotnet' -ArgList @(
        'test', (Join-Path $repoRoot 'Bible.Alarm.sln'),
        '--configuration', $Configuration,
        '--logger', 'trx;LogFileName=windows.trx',
        '--results-directory', $coverageDir,
        '--collect:XPlat Code Coverage;Format=opencover',
        # Exclude both: (a) device-test smoke tests (they run on Android emulator via the
        # Activity-based runner / on iOS simulator via xharness in their own jobs) and (b) Appium
        # UI smoke tests (they need a running Appium server + deployed app and are driven by
        # run-ui-tests.ps1, not this orchestrator).
        '--filter', 'Platform!=Android&Platform!=iOS&UI!=Windows&UI!=Android&UI!=iOS'
    )

    # Collect into a deterministic location so the merge step finds it without globbing the random GUID directories.
    $opencover = Get-ChildItem -Path $coverageDir -Recurse -Filter 'coverage.opencover.xml' | Select-Object -First 1
    if ($opencover) {
        Copy-Item -Force $opencover.FullName (Join-Path $coverageDir 'coverage.windows.opencover.xml')
    } else {
        Write-Warning 'No OpenCover XML produced for Windows pass; merge will skip Windows.'
    }
}

function Run-AndroidTests {
    Write-Section 'Android (Activity-based runner on local emulator)'

    if (-not (Test-Tool adb)) {
        throw 'adb is not on PATH. Install Android SDK platform-tools and re-run.'
    }

    $androidProj = Join-Path $repoRoot 'tests/Bible.Alarm.Tests.Android/Bible.Alarm.Tests.Android.csproj'
    $coverageDir = Join-Path $testResultsRoot 'android'

    # Wipe the local pull directory before every run. `adb pull` only adds/overwrites; without this,
    # files from a previous failed run (error.txt, stale TestResults.xml/ subdir) survive into the
    # next run's artifacts and confuse triage.
    if (Test-Path $coverageDir) {
        Remove-Item -Path $coverageDir -Recurse -Force
    }
    $null = New-Item -ItemType Directory -Path $coverageDir -Force

    # Boot the emulator if none is online. We don't kill it — the developer almost always wants to
    # keep the same emulator warm for iteration.
    $deviceList = & adb devices | Select-String -Pattern '\bdevice\b' | Where-Object { $_ -notmatch 'List of devices' }
    if (-not $deviceList) {
        $emulatorExe = Resolve-AndroidEmulatorExe
        if (-not $emulatorExe) {
            throw 'No running emulator and emulator.exe could not be found (PATH, ANDROID_HOME, ANDROID_SDK_ROOT, or "%LOCALAPPDATA%\Android\Sdk"). Boot an AVD manually or install the Android Emulator package, then re-run.'
        }
        $avd = if ($AndroidAvd) { $AndroidAvd } else {
            (& $emulatorExe -list-avds | Select-Object -First 1)
        }
        if (-not $avd) {
            throw 'No AVD installed. Create one in Android Studio (API 30+) before running Android tests.'
        }
        Write-Host "Booting AVD '$avd'..."
        Start-Process -FilePath $emulatorExe -ArgumentList @('-avd', $avd, '-no-snapshot-save', '-no-window') -PassThru | Out-Null
        & adb wait-for-device
        $bootCompleted = ''
        $deadline = (Get-Date).AddMinutes(3)
        while ((Get-Date) -lt $deadline -and $bootCompleted.Trim() -ne '1') {
            Start-Sleep -Seconds 5
            $bootCompleted = (& adb shell getprop sys.boot_completed 2>$null)
        }
        if ($bootCompleted.Trim() -ne '1') {
            throw 'Emulator failed to finish booting within 3 minutes.'
        }
    }

    # IMPORTANT: CoverletOutput is baked into the assembly at build time and resolved at runtime
    # *on the device*. A Windows host path silently no-ops there; we have to point coverlet at a
    # device-writable path and `adb pull` the result back. Keep it under the same Documents tree
    # the activity uses for TestResults.xml/done.txt so a single pull captures everything.
    $deviceResultsDir = '/sdcard/Documents/test-results'
    $deviceCoveragePath = "$deviceResultsDir/coverage.android.opencover.xml"

    Write-Host 'Building Android test APK with coverlet instrumentation...'
    # -m:1 (single-threaded MSBuild): MAUI's XamlCTask races against itself on parallel builds and
    # intermittently fails with `MSB3371: ...XamlC.stamp ... being used by another process` while
    # building MediaElement. Linux CI hits this every run; Windows local less often, but applying
    # uniformly keeps local repros green and matches build.yml. ~10s extra build time on first run.
    Invoke-CommandChecked -File 'dotnet' -ArgList @(
        'build', $androidProj,
        '-c', $Configuration,
        '-f', 'net10.0-android',
        '-m:1',
        '-p:BUILD_ANDROID_ONLY=true',
        '-p:CollectCoverage=true',
        '-p:CoverletOutputFormat=opencover',
        "-p:CoverletOutput=$deviceCoveragePath"
    )

    $apk = Get-ChildItem -Path (Join-Path $repoRoot "tests/Bible.Alarm.Tests.Android/bin/$Configuration/net10.0-android") `
        -Filter '*-Signed.apk' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $apk) {
        throw 'Could not locate the signed APK after build. Did the build succeed?'
    }

    $packageName = 'com.jthomas.info.Bible.Alarm.Tests'
    $activityComponent = "$packageName/$packageName.TestRunnerActivity"

    # Fresh install (uninstall first to avoid stale UID/permission mismatches on /sdcard).
    & adb uninstall $packageName 2>$null | Out-Null
    Write-Host "Installing $($apk.Name)..."
    Invoke-CommandChecked -File 'adb' -ArgList @('install', '-r', $apk.FullName)

    # Reset the device-side results dir. Without this a previous run's UID owns the directory and
    # the new install (different UID) cannot write into it — observed locally.
    & adb shell rm -rf $deviceResultsDir 2>$null | Out-Null
    & adb shell mkdir -p $deviceResultsDir | Out-Null

    Write-Host "Launching $activityComponent..."
    Invoke-CommandChecked -File 'adb' -ArgList @(
        'shell', 'am', 'start', '-W',
        '-n', $activityComponent
    )

    # Poll for done.txt every 5s up to 15 minutes. The activity always writes done.txt (success or
    # failure) before calling Environment.Exit, so absence here means the test process hung or
    # crashed before reaching the finally block.
    Write-Host 'Waiting for tests to finish (up to 15 minutes)...'
    $donePath = "$deviceResultsDir/done.txt"
    $doneRaw = $null
    $deadline = (Get-Date).AddMinutes(15)
    while ((Get-Date) -lt $deadline) {
        # Use a single shell command: `test -f && cat` keeps stdout empty until the file appears,
        # which gives us a clean truthy/falsy probe without race-y two-step ls/cat sequences.
        $probe = & adb shell "if [ -f $donePath ]; then cat $donePath; fi" 2>$null
        if ($probe) {
            $doneRaw = $probe.Trim()
            break
        }
        Start-Sleep -Seconds 5
    }

    # Always pull whatever is on the device so the developer can inspect partial results, error.txt,
    # and the logcat tail even when the run hung.
    & adb logcat -d -t 5000 | Out-File -FilePath (Join-Path $coverageDir 'logcat.log') -Encoding utf8
    Invoke-CommandChecked -File 'adb' -ArgList @('pull', "$deviceResultsDir/.", $coverageDir)
    & adb uninstall $packageName 2>$null | Out-Null

    # Flatten xharness's `<resultsPath>/TestResults.xml` directory-as-results layout into a single
    # file. xharness's DefaultAndroidEntryPoint creates the supplied results path as a *directory*
    # and writes the actual xunit XML inside it, but the SonarCloud merge step (and every reader of
    # this artifact) expects a regular XML file. We deliberately do this on the host side only —
    # write-after-delete on /sdcard is racy under the FUSE-backed MediaStore and tends to leave
    # stragglers like `TestResults.xml.__tmp`. The host filesystem is plain NTFS / ext4 and the
    # rename is atomic. We also handle the leftover-staging case in case a previous flatten attempt
    # was interrupted.
    $resultsPath = Join-Path $coverageDir 'TestResults.xml'
    $stagingPath = "$resultsPath.__tmp"
    if ((Test-Path $resultsPath -PathType Container)) {
        $inner = Get-ChildItem -Path $resultsPath -Filter '*.xml' -File | Select-Object -First 1
        if ($inner) {
            $tmp = "$resultsPath.__hostflatten"
            if (Test-Path $tmp) { Remove-Item $tmp -Force }
            Move-Item -Path $inner.FullName -Destination $tmp
            Remove-Item -Path $resultsPath -Recurse -Force
            Move-Item -Path $tmp -Destination $resultsPath
        } else {
            Remove-Item -Path $resultsPath -Recurse -Force
        }
    } elseif ((Test-Path $stagingPath -PathType Leaf) -and -not (Test-Path $resultsPath -PathType Leaf)) {
        Move-Item -Path $stagingPath -Destination $resultsPath
    }

    if (-not $doneRaw) {
        throw "Test runner did not produce $donePath within 15 minutes. See $coverageDir/logcat.log for the device-side trail."
    }

    $returnCode = 0
    if (-not [int]::TryParse($doneRaw, [ref]$returnCode)) {
        throw "done.txt contained non-integer payload '$doneRaw'. See $coverageDir."
    }
    if ($returnCode -ne 0) {
        $errorFile = Join-Path $coverageDir 'error.txt'
        if (Test-Path $errorFile) {
            Write-Host (Get-Content $errorFile -Raw) -ForegroundColor Red
        }
        throw "Android test run reported failure (exit $returnCode). See $coverageDir."
    }
}

function Run-IOSTests {
    Write-Section 'iOS (build + xharness on remote Mac)'

    if (-not $MacHost) {
        throw '-MacHost is required when -Platform includes iOS. Example: -MacHost user@mac.local'
    }

    $coverageDir = Join-Path $testResultsRoot 'ios'
    $null = New-Item -ItemType Directory -Path $coverageDir -Force

    # Sync the working tree to the Mac. rsync over SSH is fast on a LAN and skips bin/obj/TestResults;
    # if rsync is not on PATH (typical on stock Windows) we fall back to bsdtar + scp, which Windows 10+
    # has natively. The tar fallback is one-shot (no incremental), so first-run on big trees is slower —
    # acceptable for a local dev orchestrator. CI runs on Linux/macOS and always has rsync.
    # Resolve $MacRepoRoot to an absolute Mac path before any shell sees it. Tilde-prefixed values
    # break in two ways otherwise:
    #   1. `mkdir -p '~/work/bible-alarm'` (single-quoted) creates a literal `~` directory because
    #      tilde expansion never runs inside single quotes — we end up with `$HOME/~` polluting
    #      the user's home, and every subsequent `cd $MacRepoRoot` lands in the wrong place.
    #   2. rsync's destination `user@host:~/path/` expands tilde server-side, but `mkdir`/`scp` in
    #      the bash heredoc below do not (the heredoc preserves single quotes).
    # Resolving once via the Mac's own shell removes the ambiguity for everything downstream.
    if ($MacRepoRoot.StartsWith('~')) {
        $resolved = & ssh $MacHost "echo $MacRepoRoot"
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($resolved)) {
            throw "Failed to resolve MacRepoRoot '$MacRepoRoot' on $MacHost (ssh exit $LASTEXITCODE)."
        }
        $MacRepoRoot = ($resolved | Select-Object -First 1).Trim()
        Write-Host "  Resolved MacRepoRoot to $MacRepoRoot"
    }

    Write-Host "Syncing repo to $MacHost`:$MacRepoRoot..."
    if (Get-Command rsync -ErrorAction SilentlyContinue) {
        $rsyncArgs = @(
            '-az', '--delete',
            '--exclude=bin/', '--exclude=obj/',
            '--exclude=TestResults/', '--exclude=artifacts/',
            '--exclude=.vs/', '--exclude=.idea/', '--exclude=.git/',
            ("$repoRoot/").Replace('\', '/'),
            "${MacHost}:${MacRepoRoot}/"
        )
        Invoke-CommandChecked -File 'rsync' -ArgList $rsyncArgs
    } else {
        Write-Host '  rsync not found on PATH; using tar + scp fallback.' -ForegroundColor DarkGray
        $tarball = Join-Path $env:TEMP "bible-alarm-sync-$(Get-Date -Format yyyyMMddHHmmss).tar.gz"
        try {
            Invoke-CommandChecked -File 'tar' -ArgList @(
                '-czf', $tarball,
                '-C', $repoRoot,
                '--exclude=./bin', '--exclude=./obj',
                '--exclude=./TestResults', '--exclude=./artifacts',
                '--exclude=./.vs', '--exclude=./.idea', '--exclude=./.git',
                '--exclude=*/bin', '--exclude=*/obj', '--exclude=*/TestResults',
                '.'
            )
            Invoke-CommandChecked -File 'ssh' -ArgList @($MacHost, "mkdir -p `"$MacRepoRoot`" && rm -rf `"$MacRepoRoot/repo.tar.gz`"")
            Invoke-CommandChecked -File 'scp' -ArgList @($tarball, "${MacHost}:${MacRepoRoot}/repo.tar.gz")
            Invoke-CommandChecked -File 'ssh' -ArgList @($MacHost, "cd `"$MacRepoRoot`" && tar -xzf repo.tar.gz && rm repo.tar.gz")
        } finally {
            Remove-Item $tarball -ErrorAction SilentlyContinue
        }
    }

    # Run the build + xharness on the Mac. We MUST NOT pass the multi-line script directly as an
    # ssh arg — Windows OpenSSH joins argv with spaces, so embedded newlines collapse and bash sees
    # `set -euo pipefail cd "..." dotnet ...` on line 0 (failing immediately with
    # `set: pipefail: invalid option name`). Base64-encoding side-steps argv handling entirely:
    # we ship one opaque token, decode on the Mac, then exec the script via stdin into a fresh
    # bash. Newlines, quotes, and `$` substitution all round-trip cleanly.
    $remoteScript = @"
set -euo pipefail
cd "$MacRepoRoot"
dotnet tool restore
dotnet build tests/Bible.Alarm.Tests.iOS/Bible.Alarm.Tests.iOS.csproj \
    -c $Configuration \
    -f net10.0-ios \
    -p:BUILD_IOS_ONLY=true \
    -p:RuntimeIdentifier=iossimulator-arm64 \
    -p:CollectCoverage=true \
    -p:CoverletOutputFormat=opencover \
    -p:CoverletOutput=TestResults/ios/coverage.ios.opencover.xml
APP_PATH=`$(find tests/Bible.Alarm.Tests.iOS/bin/$Configuration/net10.0-ios -maxdepth 3 -name "*.app" | head -n 1)
if [ -z "`$APP_PATH" ]; then echo "iOS .app bundle not found"; exit 1; fi
dotnet xharness apple test \
    --app="`$APP_PATH" \
    --target=ios-simulator-64 \
    --output-directory=TestResults/ios
"@

    # PowerShell here-strings emit CRLF endings; macOS bash treats the trailing \r as part of the
    # option name (`set -euo pipefail\r` → `set: pipefail<CR>: invalid option name`). Normalise to
    # LF before encoding so the decoded script is plain Unix text.
    $remoteScript = $remoteScript -replace "`r`n", "`n"
    $encoded = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($remoteScript))
    Invoke-CommandChecked -File 'ssh' -ArgList @(
        $MacHost,
        "echo $encoded | base64 -d | bash -l"
    )

    Write-Host "Pulling iOS coverage back from $MacHost..."
    Invoke-CommandChecked -File 'scp' -ArgList @(
        '-r',
        "${MacHost}:${MacRepoRoot}/TestResults/ios/.",
        $coverageDir
    )
}

function Merge-Coverage {
    if ($SkipMerge) { return }
    Write-Section 'Merging coverage with ReportGenerator'

    Ensure-ReportGenerator

    $opencoverFiles = Get-ChildItem -Path $testResultsRoot -Recurse -Include '*.opencover.xml' -ErrorAction SilentlyContinue
    if (-not $opencoverFiles) {
        Write-Warning 'No OpenCover XML reports found; skipping merge.'
        return
    }

    $reportsArg = ($opencoverFiles | ForEach-Object { $_.FullName }) -join ';'
    $mergedDir = Join-Path $testResultsRoot 'merged'
    $null = New-Item -ItemType Directory -Path $mergedDir -Force

    Invoke-CommandChecked -File 'dotnet' -ArgList @(
        'reportgenerator',
        "-reports:$reportsArg",
        "-targetdir:$mergedDir",
        '-reporttypes:Html;Cobertura;OpenCover'
    ) -WorkingDirectory $repoRoot

    Write-Host ''
    Write-Host "Merged report: $mergedDir/index.html" -ForegroundColor Green
}

function Ensure-ReportGenerator {
    $toolsManifest = Join-Path $repoRoot '.config/dotnet-tools.json'
    if (-not (Test-Path $toolsManifest)) {
        Invoke-CommandChecked -File 'dotnet' -ArgList @('new', 'tool-manifest') -WorkingDirectory $repoRoot
    }
    $manifest = Get-Content $toolsManifest -Raw | ConvertFrom-Json
    $hasReportGen = $false
    if ($manifest.PSObject.Properties.Name -contains 'tools' -and $manifest.tools) {
        $hasReportGen = $manifest.tools.PSObject.Properties.Name -contains 'dotnet-reportgenerator-globaltool'
    }
    if (-not $hasReportGen) {
        Invoke-CommandChecked -File 'dotnet' -ArgList @('tool', 'install', 'dotnet-reportgenerator-globaltool') -WorkingDirectory $repoRoot
    }
    Invoke-CommandChecked -File 'dotnet' -ArgList @('tool', 'restore') -WorkingDirectory $repoRoot
}

switch ($Platform) {
    'Windows' { Run-WindowsTests }
    'Android' { Run-AndroidTests }
    'iOS'     { Run-IOSTests }
    'All'     {
        Run-WindowsTests
        Run-AndroidTests
        Run-IOSTests
    }
}

Merge-Coverage

Write-Host ''
Write-Host 'Done.' -ForegroundColor Green
