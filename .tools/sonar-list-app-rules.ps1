# List open Sonar issues for selected rules, limited to app + shared source (excludes libraries/.tools).
param(
    [string[]]$Rules = @("csharpsquid:S1481", "csharpsquid:S1172"),
    [string]$IssuesPath = (Join-Path (Split-Path $PSScriptRoot -Parent) "sonar-reports\all-open-issues.json"),
    [int]$PerRule = 30
)
$ErrorActionPreference = "Stop"
$issues = Get-Content $IssuesPath -Raw -Encoding UTF8 | ConvertFrom-Json
$prefix = "justcoding121_bible-alarm:src/Bible.Alarm"

foreach ($rule in $Rules) {
    $filtered = @($issues | Where-Object {
            $_.rule -eq $rule -and
            ($_.component.StartsWith($prefix) -or $_.component -like "*:src/Bible.Alarm.Shared/*")
        })
    Write-Host ""
    Write-Host "$rule  count=$($filtered.Count)"
    $filtered |
        Sort-Object component, { [int]$_.line } |
        Select-Object -First $PerRule |
        ForEach-Object {
            $path = $_.component -replace '^[^:]+:', ''
            "  L$($_.line.ToString().PadLeft(4))  $path  :: $($_.message)"
        }
}
