# Local Sonar-style report for developers: build, test with coverage, save reports under .\sonar-reports\.
# Use -LocalOnly to skip SonarCloud even if SONAR_TOKEN is set in the user environment.
# No upload to SonarCloud unless you set SONAR_TOKEN (then issues/hotspots/measures are fetched from the cloud after upload).
#
# Without SONAR_TOKEN: build + test with coverage; writes coverage XML, measures.json, and issues-roslyn.sarif (code
# issues from compiler/Roslyn analyzers) to sonar-reports. Not the same as Sonar issues (bugs/vulnerabilities/smells).
# With SONAR_TOKEN: same + upload to SonarCloud + fetch issues.json, hotspots.json, measures.json from API.
#
# Requires (only when using token): dotnet-sonarscanner (dotnet tool install --global dotnet-sonarscanner).

param(
    [Parameter(Mandatory = $false)]
    [string]$SonarToken = $env:SONAR_TOKEN,
    [Parameter(Mandatory = $false)]
    [bool]$ExportJson = $true,
    [Parameter(Mandatory = $false)]
    [switch]$LocalOnly
)

$ErrorActionPreference = "Stop"
$ProjectKey = "justcoding121_bible-alarm"
$Organization = "justcoding121"
$RepoRoot = Split-Path $PSScriptRoot -Parent
$SolutionDir = Join-Path $RepoRoot "src"
$SolutionPath = Join-Path $SolutionDir "Bible.Alarm.sln"
$TestProjectPath = Join-Path $PSScriptRoot "Bible.Alarm.VersionPatcher.Tests\Bible.Alarm.VersionPatcher.Tests.csproj"
$ReportsDir = Join-Path $RepoRoot "sonar-reports"

$Exclusions = "**/bin/**/*,**/obj/**/*,**/*.Tests/**," +
    "**/Database/Migrations/**,**/*.pem"

if ($LocalOnly) {
    $SonarToken = $null
}
elseif ([string]::IsNullOrWhiteSpace($SonarToken)) {
    $SonarToken = [Environment]::GetEnvironmentVariable("SONAR_TOKEN", "User")
}

$UseSonarCloud = -not [string]::IsNullOrWhiteSpace($SonarToken)
$CoverageReportPattern = "**/coverage.opencover.xml"
if (-not (Test-Path $ReportsDir)) {
    New-Item -ItemType Directory -Path $ReportsDir -Force | Out-Null
}

Push-Location $RepoRoot

try {
    if ($UseSonarCloud) {
        Write-Host "SonarScanner: begin (project: $ProjectKey, org: $Organization)" -ForegroundColor Cyan
        $BeginArgs = @(
            "/k:$ProjectKey",
            "/o:$Organization",
            "/d:sonar.host.url=https://sonarcloud.io",
            "/d:sonar.token=$SonarToken",
            "/d:sonar.language=cs",
            "/d:sonar.exclusions=$Exclusions",
            "/d:sonar.cs.opencover.reportsPaths=$CoverageReportPattern"
        )
        & dotnet sonarscanner begin @BeginArgs
    }
    else {
        Write-Host "Local-only mode: no SONAR_TOKEN; reports will be saved locally (no upload)." -ForegroundColor Cyan
    }

    Write-Host "Building solution..." -ForegroundColor Cyan
    $buildArgs = @("build", $SolutionPath, "--configuration", "Release", "--verbosity", "minimal")
    if (-not $UseSonarCloud) {
        $sarifOut = [System.IO.Path]::GetFullPath((Join-Path $ReportsDir "issues-roslyn.sarif"))
        $buildArgs += "/p:ErrorLog=$sarifOut"
    }
    & dotnet @buildArgs

    if (-not $UseSonarCloud) {
        $sarifFiles = Get-ChildItem -Path $RepoRoot -Recurse -Filter "*.sarif" -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notlike "*\obj\*" -and $_.FullName -notlike "*\bin\*" }
        if (-not $sarifFiles) {
            $sarifFiles = Get-ChildItem -Path $RepoRoot -Recurse -Filter "*.sarif" -ErrorAction SilentlyContinue
        }
        foreach ($f in $sarifFiles) {
            if ($f.DirectoryName -ne $ReportsDir) {
                Copy-Item -Path $f.FullName -Destination (Join-Path $ReportsDir $f.Name) -Force
                Write-Host "Saved code issues (SARIF) to $ReportsDir\$($f.Name)" -ForegroundColor Green
            }
        }
        if ((Get-ChildItem -Path $ReportsDir -Filter "*.sarif" -ErrorAction SilentlyContinue).Count -eq 0) {
            @{ source = "local"; note = "Code issues (SARIF) were not produced by this build. Sonar-style issues (bugs/vulnerabilities/hotspots) require SONAR_TOKEN and upload." } | ConvertTo-Json | Set-Content (Join-Path $ReportsDir "issues-readme.json") -Encoding UTF8
        }
    }

    Write-Host "Running tests with coverage (OpenCover + Cobertura)..." -ForegroundColor Cyan
    dotnet test $TestProjectPath --configuration Release --no-build `
        --collect:"XPlat Code Coverage;Format=opencover,cobertura"

    if ($UseSonarCloud) {
        Write-Host "SonarScanner: end (uploading to SonarCloud)" -ForegroundColor Cyan
        dotnet sonarscanner end /d:sonar.token=$SonarToken
    }

    $coverageOpenCover = Get-ChildItem -Path $RepoRoot -Recurse -Filter "coverage.opencover.xml" -ErrorAction SilentlyContinue | Select-Object -First 1
    $coverageCobertura = Get-ChildItem -Path $RepoRoot -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($coverageOpenCover) {
        Copy-Item -Path $coverageOpenCover.FullName -Destination (Join-Path $ReportsDir "coverage.opencover.xml") -Force
        Write-Host "Saved coverage (OpenCover) to $ReportsDir\coverage.opencover.xml" -ForegroundColor Green
    }
    if ($coverageCobertura) {
        Copy-Item -Path $coverageCobertura.FullName -Destination (Join-Path $ReportsDir "coverage.cobertura.xml") -Force
        Write-Host "Saved coverage (Cobertura) to $ReportsDir\coverage.cobertura.xml" -ForegroundColor Green
    }

    if ($ExportJson) {
        if ($coverageCobertura) {
            $covPath = $coverageCobertura.FullName
            $xml = [xml](Get-Content -Path $covPath -Raw)
            $lineRate = [double]$xml.coverage.'line-rate'
            $branchRate = if ($xml.coverage.'branch-rate') { [double]$xml.coverage.'branch-rate' } else { $null }
            $linesCovered = [int]$xml.coverage.'lines-covered'
            $linesValid = [int]$xml.coverage.'lines-valid'
            $measures = @{
                source = "local"
                coverage = [math]::Round($lineRate * 100, 2)
                line_coverage = [math]::Round($lineRate * 100, 2)
                branch_coverage = if ($null -ne $branchRate) { [math]::Round($branchRate * 100, 2) } else { $null }
                lines_to_cover = $linesValid
                uncovered_lines = $linesValid - $linesCovered
            }
            $measures | ConvertTo-Json | Set-Content (Join-Path $ReportsDir "measures.json") -Encoding UTF8
            Write-Host "Exported coverage summary to $ReportsDir\measures.json" -ForegroundColor Green
        }

        if ($UseSonarCloud) {
            Write-Host "Waiting for SonarCloud to process the report..." -ForegroundColor Cyan
            $maxWait = 90
            $waited = 0
            $interval = 5
            $baseUrl = "https://sonarcloud.io/api"
            $headers = @{ "Authorization" = "Bearer $SonarToken" }
            while ($waited -lt $maxWait) {
                Start-Sleep -Seconds $interval
                $waited += $interval
                try {
                    $resp = Invoke-RestMethod -Uri "$baseUrl/measures/component?component=$ProjectKey&metricKeys=coverage" -Headers $headers -Method Get -ErrorAction Stop
                    if ($resp.component.measures) { break }
                }
                catch { }
            }
            $allIssues = @()
            $page = 1
            $pageSize = 500
            do {
                $issuesResp = Invoke-RestMethod -Uri "$baseUrl/issues/search?componentKeys=$ProjectKey&pageSize=$pageSize&p=$page" -Headers $headers -Method Get
                $allIssues += $issuesResp.issues
                $page++
            } while ($page -le $issuesResp.paging.pages)
            $allIssues | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $ReportsDir "issues.json") -Encoding UTF8
            Write-Host "Exported $($allIssues.Count) issues to $ReportsDir\issues.json" -ForegroundColor Green
            $allHotspots = @()
            $hpPage = 1
            do {
                $hotspotsResp = Invoke-RestMethod -Uri "$baseUrl/hotspots/search?projectKey=$ProjectKey&p=$hpPage&ps=500" -Headers $headers -Method Get
                $allHotspots += $hotspotsResp.hotspots
                $hpPage++
            } while ($hpPage -le $hotspotsResp.paging.pages)
            $allHotspots | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $ReportsDir "hotspots.json") -Encoding UTF8
            Write-Host "Exported $($allHotspots.Count) hotspots to $ReportsDir\hotspots.json" -ForegroundColor Green
            $metricKeys = "coverage,branch_coverage,line_coverage,lines_to_cover,uncovered_lines,bugs,vulnerabilities,code_smells"
            $measuresResp = Invoke-RestMethod -Uri "$baseUrl/measures/component?component=$ProjectKey&metricKeys=$metricKeys" -Headers $headers -Method Get
            $measuresResp | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $ReportsDir "measures.json") -Encoding UTF8
            Write-Host "Exported measures (SonarCloud) to $ReportsDir\measures.json" -ForegroundColor Green
        }
        elseif (-not $coverageCobertura) {
            Write-Host "No coverage file found; measures.json not written." -ForegroundColor Yellow
        }
    }
}
finally {
    Pop-Location
}

Write-Host "Done. Reports: $ReportsDir" -ForegroundColor Green
if ($UseSonarCloud) {
    Write-Host "SonarCloud: https://sonarcloud.io" -ForegroundColor Green
}
