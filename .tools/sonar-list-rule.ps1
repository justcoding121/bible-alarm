param(
    [string]$Rule = "csharpsquid:S107",
    [string]$IssuesPath = (Join-Path (Split-Path $PSScriptRoot -Parent) "sonar-reports\all-open-issues.json")
)
$issues = Get-Content $IssuesPath -Raw -Encoding UTF8 | ConvertFrom-Json
$filtered = @($issues | Where-Object { $_.rule -eq $Rule })
Write-Host "Count: $($filtered.Count)"
$filtered |
    Sort-Object component, { [int]$_.line } |
    ForEach-Object {
        $path = $_.component -replace '^[^:]+:', ''
        "L$($_.line.ToString().PadLeft(4))  $path"
    }
