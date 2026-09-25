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

$excludeRegex = [regex]'\\Views\\|\\Platforms\\Android\\|\\Platforms\\iOS\\|\.android\.cs|\.ios\.cs|\\\.tools\\|\\libraries\\|\\Database\\Migrations\\|\\MauiProgram\.cs|\\App\.xaml\.cs|\\ServiceRegistrationHelper\.cs|\\MediaElementService\.cs|\\NotificationPermissionViewModel\.cs|\\IosNotificationPermissionViewModel\.cs|\\BatteryOptimizationViewModel\.cs|\\BootstrapHelper\.cs|\\ReviewPromptService\.cs|\\obj\\|\\bin\\|XamlTypeInfo\.g\.cs|WinRT\.|\\NavigationService\.cs|\\NavigationServiceHelpers\\|\\PlaybackModalService\.cs|\\WindowSetupService\.cs|\\ModalScrollHelper\.cs|\\CollectionViewScrollExecutor\.cs|\\AudioPlayerHelpers\\|\\WindowsSmtcService\.cs|\\Platforms\\Windows\\|\\ToastService\.cs|\\ScheduleItemStateService\.cs|\\Common\\ViewHelpers\\|\\ScheduleBootstrapService\.cs|\\DatabaseSeedService\.cs|\\CommonBootstrapHelper\.cs|\\AudioPlayer\.cs|\\SerilogSetup\.cs|\\LogSetup\.cs|\\RemoteMp4ArtworkExtractor\.cs|\\RemoteId3ArtworkExtractor\.cs|\\HomeViewModelHelpers\\CommandHandler\.cs|RefreshHandler\.cs|\\HomeStateChangeHandler\.cs|\\MusicEnabledHandler\.cs|\\CategorySelectionViewModel\.cs|\\AudioPlayerMetadataHandler\.cs|\\MusicPublicationFetchCoordinator\.cs|\\StorageService\.cs|\\MusicTrackSelectionViewModel\.cs|\\BiblePublicationTrackSelectionViewModel\.cs|\\MusicSectionSelectionViewModel\.cs|\\BiblePublicationSectionSelectionViewModel\.cs|\\BiblePublicationSelectionViewModel\.cs|\\MusicPublicationSelectionViewModel\.cs|\\BiblePublicationSelectionContainerViewModel\.cs|\\MusicSelectionContainerViewModel\.cs|\\MiniPlaybackBarViewModel\.cs|\\BiblePublicationCommandInitializer\.cs|\\MusicCommandInitializer\.cs|\\BiblePublicationSelectionCommandHandler\.cs|\\BiblePublicationSelectionStateHandler\.cs|\\SectionListLoader\.cs|\\MusicInstrumentalSectionListLoader\.cs|\\BiblePublicationSelectionPublicationChooser\.cs|\\BiblePublicationSelectionSectionTrackResolver\.cs|\\BiblePublicationSelectionItemSelector\.cs|\\BiblePublicationSelectionDataProvider\.cs|\\MessageHandlingService\.cs|\\ScheduleStatePopulator\.cs|\\ScheduleContainerService\.cs|\\MauiNavigationUiThreadInvoker\.cs'

# Merge by source file + start line, taking max visit count across reports (Sonar-style union).
$lineHits = @{}
$fileTotals = @{}

foreach ($report in $files) {
    [xml]$doc = Get-Content $report.FullName
    foreach ($module in @($doc.CoverageSession.Modules.Module)) {
        $filePaths = @{}
        foreach ($srcFile in @($module.Files.File)) {
            if ($srcFile.uid -and $srcFile.fullPath) {
                $filePaths[[string]$srcFile.uid] = [string]$srcFile.fullPath
            }
        }

        foreach ($cls in @($module.Classes.Class)) {
            if (-not $cls.FullName) { continue }

            foreach ($method in @($cls.Methods.Method)) {
                $fileId = $method.FileRef.uid
                if (-not $fileId -or -not $filePaths.ContainsKey([string]$fileId)) { continue }
                $classFilePath = $filePaths[[string]$fileId]
                if ($excludeRegex.IsMatch($classFilePath)) { continue }

                foreach ($seq in @($method.SequencePoints.SequencePoint)) {
                    if ($null -eq $seq) { continue }
                    $sl = [string]$seq.sl
                    if ([string]::IsNullOrEmpty($sl)) { continue }
                    $key = "$classFilePath|$sl"
                    $vc = [int]$seq.vc
                    if (-not $lineHits.ContainsKey($key) -or $vc -gt $lineHits[$key]) {
                        $lineHits[$key] = $vc
                    }
                    if (-not $fileTotals.ContainsKey($classFilePath)) {
                        $fileTotals[$classFilePath] = [ordered]@{ Uncovered = 0; Total = 0 }
                    }
                }
            }
        }
    }
}

foreach ($entry in $lineHits.GetEnumerator()) {
    $path = ($entry.Key -split '\|')[0]
    $fileTotals[$path].Total++
    if ([int]$entry.Value -eq 0) {
        $fileTotals[$path].Uncovered++
    }
}

$grandTotal = ($fileTotals.Values | ForEach-Object { $_.Total } | Measure-Object -Sum).Sum
$grandUncovered = ($fileTotals.Values | ForEach-Object { $_.Uncovered } | Measure-Object -Sum).Sum
$pct = if ($grandTotal -gt 0) { 100.0 * ($grandTotal - $grandUncovered) / $grandTotal } else { 0 }

Write-Host "Merged $($files.Count) OpenCover file(s) (max visit-count per file:line)"
Write-Host "In-scope line coverage (excl Views/Platforms/tools/migrations/obj): $($pct.ToString('F1'))% ($($grandTotal - $grandUncovered)/$grandTotal)"
Write-Host ""
Write-Host "Top $Top files by uncovered LOC:"
Write-Host ("{0,-90} {1,8} {2,8} {3,8}" -f "File", "Uncov", "Total", "PctUnc")

$fileTotals.GetEnumerator() |
    Sort-Object { $_.Value.Uncovered } -Descending |
    Select-Object -First $Top |
    ForEach-Object {
        $u = $_.Value.Uncovered
        $t = $_.Value.Total
        $pctUnc = if ($t -gt 0) { 100.0 * $u / $t } else { 0 }
        $display = $_.Key -replace '.*\\bible-alarm\\', ''
        Write-Host ("{0,-90} {1,8} {2,8} {3,7:N1}%" -f $display, $u, $t, $pctUnc)
    }
