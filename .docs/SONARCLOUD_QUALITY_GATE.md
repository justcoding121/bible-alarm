# SonarCloud quality gate — Bible Alarm

Configure in SonarCloud UI (API assignment previously returned 403).

## Gate: "Bible Alarm 80% Overall"

Already created in SonarCloud (id visible via `api/qualitygates/list?organization=justcoding121`):

- **Coverage on Overall Code** ≥ **80%** (`coverage` LT 80)
- **Coverage on New Code** ≥ **80%** (`new_coverage` LT 80)

### Assign to the project (UI — API select returns 403 for this token)

1. SonarCloud → **Projects** → **bible-alarm** → **Project Settings** → **Quality Gate**
2. Select **Bible Alarm 80% Overall**

Do **not** enable `sonar.qualitygate.wait=true` in `build.yml` until a Windows OpenCover analysis after the latest coverage exclusions + tests reports overall ≥ 80% on `develop`.

## CI enforcement

`sonar.qualitygate.wait` should stay **off** until `develop` overall coverage is ≥ 80% under the expanded exclusions. Then enable `/d:sonar.qualitygate.wait=true` on `SonarScanner begin` so `test-windows` fails the job when the gate fails.

## New code vs overall

| Metric | Target | Notes |
|--------|--------|-------|
| New Code | 80% | Condition on gate **Bible Alarm 80% Overall** |
| Overall | 80% | Condition on same gate; assign gate in UI (API select is 403). Local OpenCover after latest exclusions + tests ≈ **70.4%** (need ~3.3k more covered lines) |

## Tracking after each test batch

1. CI: `Staging N OpenCover XML file(s)` with N ≥ 8
2. Download `coverage-report-html` artifact or run locally:
   ```powershell
   pwsh .tools/scripts/analyze-opencover-gaps.ps1 -ReportsDir TestResults/coverage-windows
   ```
3. SonarCloud → **Measures** → **Coverage** (Overall + New Code on `develop`)
4. When overall ≥ 80%: assign **Bible Alarm 80% Overall** in the UI and enable `sonar.qualitygate.wait=true`

## Coverage source

Only the **test-windows** job feeds SonarCloud via `artifacts/coverage-windows/*.opencover.xml`. Android/iOS device jobs do not emit OpenCover by design.

`sonar.coverage.exclusions` drops:

- Android/iOS `Platforms/` sources and `*.android.cs` / `*.ios.cs` (Coverlet cannot collect device slices; see multi-platform-tests hard rule #6)
- XAML `Views/**` code-behind (device/Appium surface)
- Vendored `libraries/**` (MAUI MediaElement fork; instrumented on Windows but not app business logic)
- Tool entrypoints `**/.tools/**/Program.cs`
- Bootstrap / DI / MediaElement host glue: `MauiProgram.cs`, `App.xaml.cs`, `ServiceRegistrationHelper.cs`, `MediaElementService.cs`
- Mobile permission UI ViewModels (`NotificationPermissionViewModel`, `IosNotificationPermissionViewModel`) — bodies are `#if ANDROID`/`#if IOS` and are not exercised by the Windows OpenCover pass in a meaningful way
- MAUI navigation / modal / scroll host glue that requires a live visual tree or WinUI dispatcher (`NavigationService`, `NavigationServiceHelpers/**`, `PlaybackModalService`, `WindowSetupService`, `ModalScrollHelper`, `CollectionViewScrollExecutor`)
- `AudioPlayer` MediaElement wrappers (`AudioPlayerHelpers/**`) and Windows System Media Transport Controls (`WindowsSmtcService`) — same Coverlet/host limits as `MediaElementService`
- Windows toast / flyout UI under `Platforms/Windows/Services/UI/**` (WinUI popup surface; device/Appium territory)
- Entire `.tools/**` tree (Cataloger / DbMigration / VersionPatcher / etc.) — measured via dedicated tool test projects locally, but excluded from the app’s Sonar overall coverage denominator so seeders and network catalogers do not dominate the gate

`Platforms/Windows/**` non-UI services stay in scope where Coverlet can instrument them on the Windows host pass.
