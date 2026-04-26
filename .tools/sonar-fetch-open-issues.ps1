# Fetches all unresolved issues from SonarCloud (public API) into ..\sonar-reports\all-open-issues.json
# and prints counts by rule. Requires network. Project must be public or use SONAR_TOKEN.
param(
    [string]$ProjectKey = "justcoding121_bible-alarm",
    [string]$OutPath = (Join-Path (Split-Path $PSScriptRoot -Parent) "sonar-reports\all-open-issues.json")
)

$ErrorActionPreference = "Stop"
$headers = @{}
if ($env:SONAR_TOKEN) {
    $headers["Authorization"] = "Bearer $($env:SONAR_TOKEN)"
}

$base = "https://sonarcloud.io/api/issues/search?componentKeys=$ProjectKey&ps=500&resolved=false&p="
$first = Invoke-RestMethod -Uri ($base + "1") -Headers $headers -Method Get
$all = [System.Collections.ArrayList]::new()
[void]$all.AddRange($first.issues)
$pageSize = [int]$first.paging.pageSize
$total = [int]$first.paging.total
$pages = [int][Math]::Ceiling($total / [double]$pageSize)
for ($p = 2; $p -le $pages; $p++) {
    $resp = Invoke-RestMethod -Uri ($base + $p) -Headers $headers -Method Get
    [void]$all.AddRange($resp.issues)
}

$dir = Split-Path $OutPath -Parent
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
$all | ConvertTo-Json -Depth 6 | Set-Content $OutPath -Encoding UTF8
Write-Host "Wrote $($all.Count) issues to $OutPath"

$all | Group-Object rule | Sort-Object Count -Descending | Select-Object -First 25 | ForEach-Object { "{0,4} {1}" -f $_.Count, $_.Name }
