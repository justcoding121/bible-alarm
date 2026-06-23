#requires -Version 7.0
<#
.SYNOPSIS
  Configures SonarCloud quality gate for bible-alarm (New Code 80%, Overall ramp).

.DESCRIPTION
  Attempts SonarCloud Web API when SONAR_TOKEN is set. On 403/400, prints manual UI steps
  from .docs/SONARCLOUD_QUALITY_GATE.md.
#>
param(
    [string]$Organization = "justcoding121",
    [string]$ProjectKey = "justcoding121_bible-alarm",
    [string]$GateName = "Bible Alarm 80%",
    [double]$NewCodeCoverage = 80,
    [double]$OverallCoverage = 55
)

$ErrorActionPreference = "Stop"
$token = $env:SONAR_TOKEN
if (-not $token) {
    Write-Warning "SONAR_TOKEN not set. Follow manual steps in .docs/SONARCLOUD_QUALITY_GATE.md"
    exit 0
}

$headers = @{
    Authorization = "Bearer $token"
}

function Invoke-SonarApi {
    param([string]$Method, [string]$Uri, [hashtable]$Body = @{})
    try {
        if ($Method -eq "Get") {
            return Invoke-RestMethod -Method Get -Uri $Uri -Headers $headers
        }

        return Invoke-RestMethod -Method Post -Uri $Uri -Headers $headers -Body $Body
    }
    catch {
        Write-Warning "Sonar API call failed: $Uri — $($_.Exception.Message)"
        return $null
    }
}

Write-Host "Listing quality gates for organization $Organization..."
$gates = Invoke-SonarApi -Method Get -Uri "https://sonarcloud.io/api/qualitygates/list?organization=$Organization"
if (-not $gates) {
    Write-Host ""
    Write-Host "Manual configuration required:"
    Write-Host "  1. SonarCloud → Quality Gates → Create '$GateName'"
    Write-Host "  2. New Code coverage ≥ $NewCodeCoverage%"
    Write-Host "  3. Overall Code coverage ≥ $OverallCoverage% (raise toward 80 as coverage improves)"
    Write-Host "  4. Assign gate to project $ProjectKey"
    Write-Host "  See .docs/SONARCLOUD_QUALITY_GATE.md"
    exit 0
}

Write-Host "Found $($gates.qualitygates.Count) gate(s)."
$target = $gates.qualitygates | Where-Object { $_.name -eq $GateName } | Select-Object -First 1
if ($target) {
    Write-Host "Gate '$GateName' already exists (id=$($target.id))."
}
else {
    Write-Host "Create gate '$GateName' manually in SonarCloud UI (create API requires elevated permissions)."
}

Write-Host "Assign gate to project $ProjectKey in SonarCloud → Projects → bible-alarm → Quality Gate."
Write-Host "CI uses sonar.qualitygate.wait=true in .github/workflows/build.yml."
