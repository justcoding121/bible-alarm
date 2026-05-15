<#
.SYNOPSIS
    Run Appium-based UI integration tests on Windows, Android (emulator) or iOS (simulator on a
    networked Mac).

.DESCRIPTION
    UI tests live in tests/Bible.Alarm.UITests/ and target the production Bible.Alarm app via the
    Appium WebDriver protocol. They are FUNDAMENTALLY different from the host/device unit tests
    driven by run-tests.ps1: they need (a) the production app already deployed, (b) a running
    Appium 2 server, and (c) the platform-specific driver (windows / uiautomator2 / xcuitest).

    This script handles (a) and (b) for local invocations on a Windows host with a networked Mac
    available for iOS. See tests/UITESTS.md for one-time installation steps.

.PARAMETER Platform
    Windows | Android | iOS | All. Default: Windows.

.PARAMETER Aumid
    Windows-only. Optional override for BIBLE_ALARM_AUMID (the AppUserModelId of the deployed
    Bible Alarm MSIX). If omitted, the script auto-discovers it via Get-StartApps.

.PARAMETER SkipDeploy
    Skip the production-app build/deploy step. Useful when the app is already on the target.

.PARAMETER AppiumPort
    Port for the local Appium server. Default: 4723.

.PARAMETER MacHost
    Hostname/IP of the macOS machine for iOS runs (must be reachable via SSH). Reads MAC_HOST env var if not provided.

.EXAMPLE
    ./tests/run-ui-tests.ps1 -Platform Windows
    ./tests/run-ui-tests.ps1 -Platform Android -SkipDeploy
    ./tests/run-ui-tests.ps1 -Platform All
#>

[CmdletBinding()]
param(
    [ValidateSet('Windows','Android','iOS','All')]
    [string]$Platform = 'Windows',
    [string]$Aumid,
    [switch]$SkipDeploy,
    [int]$AppiumPort = 4723,
    [string]$MacHost = $env:MAC_HOST
)

$ErrorActionPreference = 'Stop'
$repoRoot   = Split-Path -Parent $PSScriptRoot
$uiTestProj = Join-Path $repoRoot 'tests/Bible.Alarm.UITests/Bible.Alarm.UITests.csproj'
$appiumUrl  = "http://127.0.0.1:$AppiumPort/"

function Assert-Tool($name, $hint) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "Required tool '$name' not found on PATH. $hint"
    }
}

function Start-Appium {
    Write-Output "[ui] starting Appium 2 server on $appiumUrl ..."
    $appiumLog = Join-Path $env:TEMP "appium-ui-tests.log"
    if (Test-Path $appiumLog) { Remove-Item $appiumLog -Force }
    $proc = Start-Process -FilePath 'appium' -ArgumentList @('--port', $AppiumPort, '--log-level', 'info') `
        -RedirectStandardOutput $appiumLog -RedirectStandardError $appiumLog -PassThru -WindowStyle Hidden

    # Poll the /status endpoint for up to 30s.
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        try {
            $r = Invoke-WebRequest -Uri "$appiumUrl/status" -UseBasicParsing -TimeoutSec 2
            if ($r.StatusCode -eq 200) {
                Write-Output "[ui] Appium ready (pid $($proc.Id), log $appiumLog)"
                return $proc
            }
        } catch { Start-Sleep -Milliseconds 500 }
    }
    throw "Appium did not become ready within 30s. See log: $appiumLog"
}

function Stop-Appium($proc) {
    if ($null -ne $proc -and -not $proc.HasExited) {
        Write-Output "[ui] stopping Appium (pid $($proc.Id))"
        try {
            Stop-Process -Id $proc.Id -Force -ErrorAction Stop
        } catch {
            Write-Output "[ui] Appium process $($proc.Id) already exited."
        }
    }
}

function Invoke-WindowsUITests {
    Assert-Tool 'appium' 'Install with: npm install -g appium@2 (see tests/UITESTS.md)'
    if (-not (Get-Command 'WinAppDriver.exe' -ErrorAction SilentlyContinue) -and `
        -not (Test-Path 'C:\Program Files (x86)\Windows Application Driver\WinAppDriver.exe') -and `
        -not (Test-Path 'C:\Program Files\Windows Application Driver\WinAppDriver.exe')) {
        throw "WinAppDriver not found. Install: winget install Microsoft.WinAppDriver (see tests/UITESTS.md)."
    }

    if (-not $SkipDeploy) {
        Write-Output "[ui] deploying Bible.Alarm Windows MSIX (Debug, win-x64)..."
        dotnet build (Join-Path $repoRoot 'src/Bible.Alarm/Bible.Alarm.csproj') `
            -c Debug -f net10.0-windows10.0.19041.0 -p:RuntimeIdentifier=win-x64 `
            -p:WindowsPackageType=MSIX -t:Build,Deploy `
            -p:BUILD_WINDOWS_ONLY=true
        if ($LASTEXITCODE -ne 0) { throw "Windows deploy failed ($LASTEXITCODE)." }
    }

    if (-not $Aumid) {
        $Aumid = (Get-StartApps | Where-Object Name -eq 'Bible Alarm' | Select-Object -First 1).AppId
        if (-not $Aumid) {
            throw "Could not discover Bible Alarm AUMID via Get-StartApps. Pass -Aumid or run after deploy completes."
        }
    }
    Write-Output "[ui] BIBLE_ALARM_AUMID=$Aumid"
    $env:BIBLE_ALARM_AUMID = $Aumid
    $env:APPIUM_URL = $appiumUrl

    $appium = Start-Appium
    try {
        dotnet test $uiTestProj --no-build --verbosity normal `
            --filter 'UI=Windows' `
            --logger 'console;verbosity=detailed'
        if ($LASTEXITCODE -ne 0) { throw "Windows UI tests failed ($LASTEXITCODE)." }
    } finally {
        Stop-Appium $appium
        Remove-Item Env:\BIBLE_ALARM_AUMID -ErrorAction SilentlyContinue
    }
}

function Invoke-AndroidUITests {
    Assert-Tool 'appium' 'Install with: npm install -g appium@2'
    Assert-Tool 'adb' 'Install Android command-line tools and add platform-tools to PATH.'

    $devices = & adb devices | Select-String -Pattern '^\S+\s+device$'
    if (-not $devices) {
        throw "No connected ADB device. Boot an emulator first: emulator -avd <name> &"
    }

    if (-not $SkipDeploy) {
        Write-Output "[ui] building + installing Android APK..."
        dotnet build (Join-Path $repoRoot 'src/Bible.Alarm/Bible.Alarm.csproj') `
            -c Debug -f net10.0-android -t:Install -p:BUILD_ANDROID_ONLY=true
        if ($LASTEXITCODE -ne 0) { throw "Android install failed ($LASTEXITCODE)." }
    }

    $env:APPIUM_URL = $appiumUrl
    $appium = Start-Appium
    try {
        dotnet test $uiTestProj --no-build --verbosity normal `
            --filter 'UI=Android' `
            --logger 'console;verbosity=detailed'
        if ($LASTEXITCODE -ne 0) { throw "Android UI tests failed ($LASTEXITCODE)." }
    } finally {
        Stop-Appium $appium
    }
}

function Invoke-IosUITests {
    if (-not $MacHost) {
        throw "iOS UI tests need a networked macOS host. Set -MacHost or `$env:MAC_HOST."
    }
    Write-Output "[ui] iOS Appium runs on the Mac itself; this script is a thin SSH driver."
    Write-Output "[ui] One-time Mac setup is in tests/UITESTS.md ('Mac side' section)."

    # Invoke via explicit `bash` so the script doesn't need its executable bit preserved across the
    # Windows -> Mac sync. The repo must already be at ~/bible-alarm — typically synced by a
    # preceding `./tests/run-tests.ps1 -Platform iOS -MacHost ...` pass; if you're running the UI
    # smoke standalone, run that orchestrator first or set up the sync separately.
    $remoteCmd = "cd ~/bible-alarm && bash ./tests/run-ui-tests-mac.sh"
    Write-Output "[ui] running on ${MacHost}: $remoteCmd"
    & ssh $MacHost $remoteCmd
    if ($LASTEXITCODE -ne 0) { throw "Remote iOS UI tests failed ($LASTEXITCODE)." }
}

switch ($Platform) {
    'Windows' { Invoke-WindowsUITests }
    'Android' { Invoke-AndroidUITests }
    'iOS'     { Invoke-IosUITests }
    'All'     {
        Invoke-WindowsUITests
        Invoke-AndroidUITests
        Invoke-IosUITests
    }
}

Write-Output "[ui] done."
