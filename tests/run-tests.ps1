#requires -Version 7.0
<#
    .SYNOPSIS
        Local-developer test orchestrator for the Bible.Alarm multi-platform test suite.

    .DESCRIPTION
        Runs Windows + Shared host-based tests directly via `dotnet test`, drives an Android emulator
        through `xharness android`, and (optionally) builds + runs the iOS test bundle on a networked
        macOS machine over SSH. After all selected platforms pass, merges the OpenCover XML reports
        into a single HTML report under TestResults/merged via dotnet-reportgenerator-globaltool.

        Designed to mirror the CI fan-out described in .github/workflows/build.yml so an issue
        reproduces the same way locally as it does on a PR.

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
          * `xharness` installed via dotnet tool (see tests/README.md)

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

$xharnessVersion = '10.0.0-prerelease.26230.1'

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
            throw "Command failed with exit $LASTEXITCODE: $File $($ArgList -join ' ')"
        }
    } finally {
        Pop-Location
    }
}

function Test-Tool {
    param([string]$Name)
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

function Ensure-Xharness {
    # We install xharness as a *local* tool inside the repo's TestResults/.tools folder so the user's
    # global tool list is not polluted and CI parity is preserved (CI installs the same way).
    $toolsManifest = Join-Path $repoRoot '.config/dotnet-tools.json'
    if (-not (Test-Path $toolsManifest)) {
        Write-Host 'Creating local dotnet-tools manifest...'
        $null = New-Item -ItemType Directory -Path (Split-Path $toolsManifest) -Force
        Invoke-CommandChecked -File 'dotnet' -ArgList @('new', 'tool-manifest') -WorkingDirectory $repoRoot
    }

    $manifest = Get-Content $toolsManifest -Raw | ConvertFrom-Json
    $hasXharness = $false
    if ($manifest.PSObject.Properties.Name -contains 'tools' -and $manifest.tools) {
        $hasXharness = $manifest.tools.PSObject.Properties.Name -contains 'microsoft.dotnet.xharness.cli'
    }

    if (-not $hasXharness) {
        Write-Host "Installing xharness $xharnessVersion (local tool)..."
        Invoke-CommandChecked -File 'dotnet' -ArgList @(
            'tool', 'install', 'Microsoft.DotNet.XHarness.CLI',
            '--version', $xharnessVersion,
            '--add-source', 'https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json'
        ) -WorkingDirectory $repoRoot
    }

    Invoke-CommandChecked -File 'dotnet' -ArgList @('tool', 'restore') -WorkingDirectory $repoRoot
}

function Run-WindowsTests {
    Write-Section 'Windows + Shared (host-based dotnet test)'
    $coverageDir = Join-Path $testResultsRoot 'windows'
    $null = New-Item -ItemType Directory -Path $coverageDir -Force

    Invoke-CommandChecked -File 'dotnet' -ArgList @(
        'test', (Join-Path $repoRoot 'src/Bible.Alarm.sln'),
        '--configuration', $Configuration,
        '--logger', 'trx;LogFileName=windows.trx',
        '--results-directory', $coverageDir,
        '--collect:XPlat Code Coverage;Format=opencover',
        '--filter', 'Platform!=Android&Platform!=iOS'
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
    Write-Section 'Android (xharness on local emulator)'

    if (-not (Test-Tool adb)) {
        throw 'adb is not on PATH. Install Android SDK platform-tools and re-run.'
    }
    Ensure-Xharness

    $androidProj = Join-Path $repoRoot 'src/Bible.Alarm.Tests.Android/Bible.Alarm.Tests.Android.csproj'
    $coverageDir = Join-Path $testResultsRoot 'android'
    $null = New-Item -ItemType Directory -Path $coverageDir -Force

    # Boot the emulator if none is online. We don't kill it — the developer almost always wants to
    # keep the same emulator warm for iteration.
    $deviceList = & adb devices | Select-String -Pattern '\bdevice\b' | Where-Object { $_ -notmatch 'List of devices' }
    if (-not $deviceList) {
        $emulatorExe = Get-Command emulator -ErrorAction SilentlyContinue
        if (-not $emulatorExe -and $env:ANDROID_HOME) {
            $emulatorExe = Join-Path $env:ANDROID_HOME 'emulator/emulator.exe'
        }
        if (-not $emulatorExe -or -not (Test-Path $emulatorExe)) {
            throw 'No running emulator and `emulator` is not on PATH. Boot an AVD manually then re-run.'
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
        # Wait for the boot animation to actually finish (otherwise xharness install fails).
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

    Write-Host 'Building Android test APK with coverlet instrumentation...'
    Invoke-CommandChecked -File 'dotnet' -ArgList @(
        'build', $androidProj,
        '-c', $Configuration,
        '-f', 'net10.0-android',
        '-p:BUILD_ANDROID_ONLY=true',
        '-p:CollectCoverage=true',
        '-p:CoverletOutputFormat=opencover',
        "-p:CoverletOutput=$coverageDir/coverage.android.opencover.xml"
    )

    # Resolve the actual APK path produced by the build (Signed APK suffix varies between configurations).
    $apk = Get-ChildItem -Path (Join-Path $repoRoot "src/Bible.Alarm.Tests.Android/bin/$Configuration/net10.0-android") `
        -Filter '*-Signed.apk' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $apk) {
        throw 'Could not locate the signed APK after build. Did the build succeed?'
    }

    Write-Host "Running xharness against $($apk.Name)..."
    Invoke-CommandChecked -File 'dotnet' -ArgList @(
        'xharness', 'android', 'test',
        "--app=$($apk.FullName)",
        '--package-name=com.jthomas.info.Bible.Alarm.Tests',
        "--output-directory=$coverageDir",
        '--instrumentation=com.jthomas.info.Bible.Alarm.Tests.TestInstrumentation'
    ) -WorkingDirectory $repoRoot
}

function Run-IOSTests {
    Write-Section 'iOS (build + xharness on remote Mac)'

    if (-not $MacHost) {
        throw '-MacHost is required when -Platform includes iOS. Example: -MacHost user@mac.local'
    }

    $coverageDir = Join-Path $testResultsRoot 'ios'
    $null = New-Item -ItemType Directory -Path $coverageDir -Force

    # Sync the working tree to the Mac. rsync over SSH is fast on a LAN and skips bin/obj/TestResults.
    Write-Host "Syncing repo to $MacHost`:$MacRepoRoot..."
    $rsyncArgs = @(
        '-az', '--delete',
        '--exclude=bin/', '--exclude=obj/',
        '--exclude=TestResults/', '--exclude=artifacts/',
        '--exclude=.vs/', '--exclude=.idea/',
        ("$repoRoot/").Replace('\', '/'),
        "${MacHost}:${MacRepoRoot}/"
    )
    Invoke-CommandChecked -File 'rsync' -ArgList $rsyncArgs

    # Run the build + xharness on the Mac. Use a heredoc so the local pwsh substitutes once and then
    # bash on the Mac runs the literal commands.
    $remoteScript = @"
set -euo pipefail
cd "$MacRepoRoot"
dotnet tool restore
dotnet build src/Bible.Alarm.Tests.iOS/Bible.Alarm.Tests.iOS.csproj \
    -c $Configuration \
    -f net10.0-ios \
    -p:BUILD_IOS_ONLY=true \
    -p:RuntimeIdentifier=iossimulator-arm64 \
    -p:CollectCoverage=true \
    -p:CoverletOutputFormat=opencover \
    -p:CoverletOutput=TestResults/ios/coverage.ios.opencover.xml
APP_PATH=`$(find src/Bible.Alarm.Tests.iOS/bin/$Configuration/net10.0-ios -maxdepth 3 -name "*.app" | head -n 1)
if [ -z "`$APP_PATH" ]; then echo "iOS .app bundle not found"; exit 1; fi
dotnet xharness apple test \
    --app="`$APP_PATH" \
    --target=ios-simulator-64 \
    --output-directory=TestResults/ios
"@

    Invoke-CommandChecked -File 'ssh' -ArgList @($MacHost, 'bash', '-lc', "'$remoteScript'")

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
