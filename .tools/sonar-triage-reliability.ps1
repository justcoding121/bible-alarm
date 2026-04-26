# Summarize open Sonar issues: BUG, VULNERABILITY, and RELIABILITY impact (from all-open-issues.json).
param(
    [string]$IssuesPath = (Join-Path (Split-Path $PSScriptRoot -Parent) "sonar-reports\all-open-issues.json")
)
$ErrorActionPreference = "Stop"
$issues = Get-Content $IssuesPath -Raw -Encoding UTF8 | ConvertFrom-Json
Write-Host "Total open issues: $($issues.Count)"
Write-Host "`nBy type:"
$issues | Group-Object type | Sort-Object Count -Descending | ForEach-Object { "  $($_.Count.ToString().PadLeft(5)) $($_.Name)" }

function Has-ReliabilityImpact($issue) {
    if (-not $issue.impacts) { return $false }
    foreach ($i in $issue.impacts) {
        if ($i.softwareQuality -eq "RELIABILITY") { return $true }
    }
    return $false
}

$reliability = @($issues | Where-Object { Has-ReliabilityImpact $_ })
Write-Host "`nRELIABILITY impact (any severity): $($reliability.Count)"
$reliability | Group-Object rule | Sort-Object Count -Descending | Select-Object -First 30 | ForEach-Object { "  $($_.Count.ToString().PadLeft(4)) $($_.Name)" }

Write-Host "`nBUG type only:"
($issues | Where-Object { $_.type -eq "BUG" }) | Group-Object rule | Sort-Object Count -Descending | Select-Object -First 25 | ForEach-Object { "  $($_.Count.ToString().PadLeft(4)) $($_.Name)" }

Write-Host "`nVULNERABILITY:"
($issues | Where-Object { $_.type -eq "VULNERABILITY" }) | Group-Object rule | Sort-Object Count -Descending | ForEach-Object { "  $($_.Count.ToString().PadLeft(4)) $($_.Name)" }

Write-Host "`nSample RELIABILITY (first 15):"
$reliability | Select-Object -First 15 | ForEach-Object {
    $comp = $_.component -replace '^[^:]+:', ''
    "  $($_.rule) L$($_.line) $comp :: $($_.message)"
}
