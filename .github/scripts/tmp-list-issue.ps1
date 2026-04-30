$path = 'C:\Work\Repositories\bible-alarm\sonar-issues-open-maintainability.json'
$d = Get-Content $path -Raw | ConvertFrom-Json
$rule = 'csharpsquid:S3604'
$d.issues | Where-Object { $_.rule -eq $rule } | ForEach-Object {
    "{0}|{1}|{2}|{3}" -f $_.key, ($_.component -replace '[^:]+:'), $_.textRange.startLine, $_.message
}
