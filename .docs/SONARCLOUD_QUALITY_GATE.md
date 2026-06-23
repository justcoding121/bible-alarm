# SonarCloud quality gate — Bible Alarm

Configure in SonarCloud UI (API assignment previously returned 403).

## Gate: "Bible Alarm 80%"

1. SonarCloud → **Quality Gates** → **Create** (or duplicate "Sonar way")
2. Add conditions:
   - **Coverage on New Code** ≥ **80%** (enforce immediately)
   - **Coverage on Overall Code** ≥ **55%** initially; raise in steps (60 → 65 → … → 80) as overall coverage climbs
3. **Projects** → **bible-alarm** → **Project Settings** → **Quality Gate** → assign **Bible Alarm 80%**

## CI enforcement

`build.yml` sets `sonar.qualitygate.wait=true` on `SonarScanner begin`. The `test-windows` job fails when the assigned gate fails.

**Important:** Enable `sonar.qualitygate.wait=true` in `build.yml` only **after** the gate is assigned in SonarCloud with a realistic overall threshold (e.g. 50–55% while ramping). Enabling it before UI setup causes `test-windows` to fail even when tests and coverage upload succeed.

## New code vs overall

| Metric | Target | Notes |
|--------|--------|-------|
| New Code | 80% | Enforce on every PR |
| Overall | 80% (long-term) | Ramp via gate conditions; currently ~53% |

## Tracking after each test batch

1. CI: `Staging N OpenCover XML file(s)` with N ≥ 8
2. Download `coverage-report-html` artifact or run locally:
   ```powershell
   pwsh .tools/scripts/analyze-opencover-gaps.ps1 -ReportsDir TestResults/coverage-windows
   ```
3. SonarCloud → **Measures** → **Coverage** (Overall + New Code on `develop`)
4. When overall ≥ 80%, set **Coverage on Overall Code** gate condition to 80%

## Coverage source

Only the **test-windows** job feeds SonarCloud via `artifacts/coverage-windows/*.opencover.xml`. Android/iOS device jobs do not emit OpenCover by design.
