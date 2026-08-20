#requires -Version 7.0
param(
    [string]$ReportsDir = "TestResults/coverage-windows",
    [int]$Top = 25
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$pattern = Join-Path $repoRoot "$ReportsDir/*.opencover.xml"
$files = Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue
if (-not $files) {
    Write-Error "No OpenCover XML files under $pattern"
    exit 1
}

$excludeRegex = [regex]'\\Views\\|\\Platforms\\Android\\|\\Platforms\\iOS\\|\.android\.cs|\.ios\.cs|\\\.tools\\|\\Database\\Migrations\\|ScheduleStateServiceAndroidEnableWithPermission|ScheduleStateServiceIosEnableWithPermission|IsEnabledIosPermissionChecker|SchedulePlatformEnablePermissionRequests'
$results = @{}

foreach ($file in $files) {
    [xml]$doc = Get-Content $file.FullName
    foreach ($module in @($doc.CoverageSession.Modules.Module)) {
        $filePaths = @{}
        foreach ($file in @($module.Files.File)) {
            if ($file.uid -and $file.fullPath) {
                $filePaths[[string]$file.uid] = [string]$file.fullPath
            }
        }

        foreach ($cls in @($module.Classes.Class)) {
            if (-not $cls.FullName) { continue }

            $classFilePath = $null
            foreach ($method in @($cls.Methods.Method)) {
                $fileId = $method.FileRef.uid
                if ($fileId -and $filePaths.ContainsKey([string]$fileId)) {
                    $classFilePath = $filePaths[[string]$fileId]
                    break
                }
            }

            if ($classFilePath -and $excludeRegex.IsMatch($classFilePath)) { continue }
            if (-not $classFilePath -and $excludeRegex.IsMatch($cls.FullName)) { continue }

            $uncovered = 0
            $total = 0
            foreach ($method in @($cls.Methods.Method)) {
                foreach ($seq in @($method.SequencePoints.SequencePoint)) {
                    if ($null -eq $seq) { continue }
                    $total++
                    if ([int]$seq.vc -eq 0) { $uncovered++ }
                }
            }

            if ($total -eq 0) { continue }

            $key = if ($classFilePath) { $classFilePath } else { $cls.FullName }
            if (-not $results.ContainsKey($key)) {
                $results[$key] = [ordered]@{ Uncovered = 0; Total = 0; Class = $cls.FullName }
            }

            $results[$key].Uncovered += $uncovered
            $results[$key].Total += $total
        }
    }
}

$grandTotal = ($results.Values | ForEach-Object { $_.Total } | Measure-Object -Sum).Sum
$grandUncovered = ($results.Values | ForEach-Object { $_.Uncovered } | Measure-Object -Sum).Sum
$pct = if ($grandTotal -gt 0) { 100.0 * ($grandTotal - $grandUncovered) / $grandTotal } else { 0 }

Write-Host "Merged $($files.Count) OpenCover file(s)"
Write-Host "In-scope line coverage (excl Views/Platforms/tools/migrations): $($pct.ToString('F1'))% ($($grandTotal - $grandUncovered)/$grandTotal)"
Write-Host ""
Write-Host "Top $Top classes by uncovered LOC:"
Write-Host ("{0,-90} {1,8} {2,8} {3,8}" -f "File", "Uncov", "Total", "PctUnc")

$results.GetEnumerator() |
    Sort-Object { $_.Value.Uncovered } -Descending |
    Select-Object -First $Top |
    ForEach-Object {
        $u = $_.Value.Uncovered
        $t = $_.Value.Total
        $pctUnc = if ($t -gt 0) { 100.0 * $u / $t } else { 0 }
        $display = $_.Key -replace '.*\\bible-alarm\\', ''
        Write-Host ("{0,-90} {1,8} {2,8} {3,7:N1}%" -f $display, $u, $t, $pctUnc)
    }
