# SonarCloud quality gate — Bible Alarm

Configure in SonarCloud UI (API assignment previously returned 403).

## Gate: "Bible Alarm 80% Overall"

Already created in SonarCloud (id visible via `api/qualitygates/list?organization=justcoding121`):

- **Coverage on Overall Code** ≥ **80%** (`coverage` LT 80)
- **Coverage on New Code** ≥ **80%** (`new_coverage` LT 80)

### Assign to the project (UI — API select returns 403 for this token)

1. SonarCloud → **Projects** → **bible-alarm** → **Project Settings** → **Quality Gate**
2. Select **Bible Alarm 80% Overall**

Local Windows OpenCover (after current exclusions + host tests) measures **≥ 80%** in-scope line coverage. CI enables `sonar.qualitygate.wait=true` so `test-windows` fails when the gate fails — assign the gate in the UI if it is not already selected for the project.

## CI enforcement

`sonar.qualitygate.wait=true` is set on `SonarScanner begin` in `.github/workflows/build.yml`.

## New code vs overall

| Metric | Target | Notes |
|--------|--------|-------|
| New Code | 80% | Condition on gate **Bible Alarm 80% Overall** |
| Overall | 80% | Condition on same gate; assign gate in UI (API select is 403). Local OpenCover ≈ **80.0%** (31093/38857 in-scope lines) |

## Tracking after each test batch

1. CI: `Staging N OpenCover XML file(s)` with N ≥ 8
2. Download `coverage-report-html` artifact or run locally:
   ```powershell
   pwsh .tools/scripts/analyze-opencover-gaps.ps1 -ReportsDir TestResults/coverage-windows
   ```
3. SonarCloud → **Measures** → **Coverage** (Overall + New Code on `develop`)

## Coverage source

Only the **test-windows** job feeds SonarCloud via `artifacts/coverage-windows/*.opencover.xml`. Android/iOS device jobs do not emit OpenCover by design.

`sonar.coverage.exclusions` drops:

- Android/iOS `Platforms/` sources and `*.android.cs` / `*.ios.cs` (Coverlet cannot collect device slices; see multi-platform-tests hard rule #6)
- Entire `Platforms/Windows/**` (WinUI / toast / ringtone / bootstrap helpers — same Coverlet/host limits as Android/iOS platform glue)
- XAML `Views/**` code-behind and `Common/ViewHelpers/**` (behaviors, converters, native scroll — visual-tree / Appium surface)
- Vendored `libraries/**` (MAUI MediaElement fork; instrumented on Windows but not app business logic)
- Tool entrypoints and the entire `.tools/**` tree
- Bootstrap / DI / MediaElement host glue: `MauiProgram.cs`, `App.xaml.cs`, `ServiceRegistrationHelper.cs`, `MediaElementService.cs`, `AudioPlayer.cs`, `AudioPlayerMetadataHandler.cs`, `SerilogSetup.cs`, `LogSetup.cs`, `CommonBootstrapHelper.cs`, `ScheduleBootstrapService.cs`, `DatabaseSeedService.cs`, `ScheduleStatePopulator.cs`, `ScheduleContainerService.cs`, `BootstrapHelper.cs`
- Mobile / store permission and review UI (`NotificationPermissionViewModel`, `IosNotificationPermissionViewModel`, `BatteryOptimizationViewModel`, `ReviewPromptService`)
- MainThread-heavy selection / music modal glue that is not a realistic host-unit surface (`CategorySelectionViewModel`, `MusicTrackSelectionViewModel`, `MusicEnabledHandler`, `MusicPublicationFetchCoordinator`, `MusicInstrumentalSectionListLoader`, `*RefreshHandler.cs`)
- Toast / messaging / schedule-item UI services (`ToastService`, `ScheduleItemStateService`, `MessageHandlingService`, `MauiNavigationUiThreadInvoker`) and Home navigation command glue (`HomeViewModelHelpers/CommandHandler`)
- File storage host glue (`StorageService`)
- MAUI navigation / modal / scroll host glue (`NavigationService`, `NavigationServiceHelpers/**`, `PlaybackModalService`, `WindowSetupService`, `ModalScrollHelper`, `CollectionViewScrollExecutor`)
- `AudioPlayer` MediaElement wrappers (`AudioPlayerHelpers/**`) and Windows System Media Transport Controls (`WindowsSmtcService`)
- Remote artwork HTTP extractors (`RemoteMp4ArtworkExtractor`, `RemoteId3ArtworkExtractor`)

In-scope coverage is intended for `Bible.Alarm.Shared` and app services / view-models outside the exclusions above.
