# Asserts the merged Android manifest has the expected media-browse services.
# Usage:
#   pwsh tests/scripts/assert-android-manifest.ps1 -Path <AndroidManifest.xml> -ExpectedBrowseServices 1
# Optional -ExpectedMediaSessionServices fails when the MediaSessionService action count differs.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Path,

    [Parameter(Mandatory = $true)]
    [int]$ExpectedBrowseServices,

    [int]$ExpectedMediaSessionServices = -1
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path -LiteralPath $Path)) {
    Write-Error "Manifest not found: $Path"
    exit 1
}

[xml]$manifest = Get-Content -LiteralPath $Path -Raw
$androidNs = 'http://schemas.android.com/apk/res/android'
$ns = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
$ns.AddNamespace('android', $androidNs)

$browseActions = @(
    'android.media.browse.MediaBrowserService',
    'androidx.media3.session.MediaLibraryService'
)
$sessionAction = 'androidx.media3.session.MediaSessionService'

$services = @($manifest.SelectNodes('//service'))
$browseCount = 0
$sessionCount = 0

foreach ($service in $services) {
    $actions = @($service.SelectNodes('.//action'))
    $names = foreach ($action in $actions) {
        $action.GetAttribute('name', $androidNs)
    }

    $isBrowse = $false
    $isSession = $false
    foreach ($name in $names) {
        if ($browseActions -contains $name) {
            $isBrowse = $true
        }
        if ($name -eq $sessionAction) {
            $isSession = $true
        }
    }

    if ($isSession) {
        $sessionCount++
        $serviceName = $service.GetAttribute('name', $androidNs)
        Write-Host "MediaSessionService: $serviceName"
    }

    if ($isBrowse) {
        $browseCount++
        $serviceName = $service.GetAttribute('name', $androidNs)
        Write-Host "Browse service: $serviceName"
    }
}

Write-Host "Browse services: $browseCount (expected $ExpectedBrowseServices)"
Write-Host "MediaSessionService actions: $sessionCount"

$carApp = @($manifest.SelectNodes("//*[@*[local-name()='name' and .='androidx.car.app.CarAppService']]"))
if ($carApp.Count -gt 0) {
    Write-Error "Merged manifest declares androidx.car.app.CarAppService ($($carApp.Count))"
    exit 1
}

$carMeta = $false
foreach ($meta in @($manifest.SelectNodes('//meta-data'))) {
    if ($meta.GetAttribute('name', $androidNs) -eq 'com.google.android.gms.car.application') {
        $carMeta = $true
        break
    }
}

if (-not $carMeta) {
    Write-Error "Missing com.google.android.gms.car.application meta-data"
    exit 1
}

if ($browseCount -ne $ExpectedBrowseServices) {
    Write-Error "Expected $ExpectedBrowseServices browse service(s), found $browseCount"
    exit 1
}

if ($ExpectedMediaSessionServices -ge 0 -and $sessionCount -ne $ExpectedMediaSessionServices) {
    Write-Error "Expected $ExpectedMediaSessionServices MediaSessionService action(s), found $sessionCount"
    exit 1
}

Write-Host "Manifest assertion passed."
exit 0
