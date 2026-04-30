$path = 'C:\Work\Repositories\bible-alarm\sonar-issues-open-maintainability.json'
$d = Get-Content $path -Raw | ConvertFrom-Json
Write-Host "issues count:" $d.issues.Count
$d.issues | Group-Object rule | Sort-Object Count -Descending | Select-Object -First 40 | ForEach-Object { "$($_.Count) $($_.Name)" }
