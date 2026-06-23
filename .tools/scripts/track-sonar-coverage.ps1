#requires -Version 7.0
<#
.SYNOPSIS
  Fetches SonarCloud coverage metrics for bible-alarm develop branch.
#>
param(
    [string]$ProjectKey = "justcoding121_bible-alarm",
    [string]$Branch = "develop"
)

$token = $env:SONAR_TOKEN
if (-not $token) {
    Write-Error "SONAR_TOKEN not set"
    exit 1
}

$headers = @{ Authorization = "Bearer $token" }
$base = "https://sonarcloud.io/api/measures/component?component=$ProjectKey&branch=$Branch"
$metricKeys = "coverage,new_coverage,lines_to_cover,uncovered_lines"
$uri = "$base&metricKeys=$metricKeys"

try {
    $result = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get
}
catch {
    Write-Warning "Could not fetch Sonar metrics: $($_.Exception.Message)"
    Write-Host "Check SonarCloud → Measures → Coverage on branch '$Branch'"
    exit 0
}

Write-Host "SonarCloud coverage ($ProjectKey @ $Branch):"
foreach ($m in $result.component.measures) {
    Write-Host ("  {0,-20} {1}" -f $m.metric, $m.value)
}

Write-Host ""
Write-Host "When overall coverage ≥ 80%, set Overall Code gate condition to 80% (see .docs/SONARCLOUD_QUALITY_GATE.md)"
