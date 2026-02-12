# Local SonarCloud scan for Bible.Alarm solution.
# Requires: dotnet-sonarscanner (dotnet tool install --global dotnet-sonarscanner)
# Set SONAR_TOKEN before running (from https://sonarcloud.io/account/security).

param(
    [Parameter(Mandatory = $false)]
    [string]$SonarToken = $env:SONAR_TOKEN
)

$ErrorActionPreference = "Stop"
$ProjectKey = "justcoding121_bible-alarm"
$Organization = "justcoding121"
$SolutionDir = Join-Path $PSScriptRoot "src"
$SolutionPath = Join-Path $SolutionDir "Bible.Alarm.sln"
$TestProjectPath = Join-Path $PSScriptRoot ".tools\Bible.Alarm.VersionPatcher.Tests\Bible.Alarm.VersionPatcher.Tests.csproj"

$Exclusions = "**/bin/**/*,**/obj/**/*,**/*.Tests/**," +
    "**/Database/Migrations/**,**/*.pem"

if (-not $SonarToken) {
    Write-Error "SONAR_TOKEN is required. Set it with: `$env:SONAR_TOKEN = 'your-token'"
    exit 1
}

Push-Location $PSScriptRoot

try {
    Write-Host "SonarScanner: begin (project: $ProjectKey, org: $Organization)" -ForegroundColor Cyan
    dotnet sonarscanner begin `
        /k:$ProjectKey `
        /o:$Organization `
        /d:sonar.host.url="https://sonarcloud.io" `
        /d:sonar.token=$SonarToken `
        /d:sonar.language="cs" `
        /d:sonar.exclusions=$Exclusions

    Write-Host "Building solution..." -ForegroundColor Cyan
    dotnet build $SolutionPath --configuration Release --verbosity minimal

    Write-Host "Running tests..." -ForegroundColor Cyan
    dotnet test $TestProjectPath --configuration Release --no-build

    Write-Host "SonarScanner: end" -ForegroundColor Cyan
    dotnet sonarscanner end /d:sonar.token=$SonarToken
}
finally {
    Pop-Location
}

Write-Host "Done. Check https://sonarcloud.io for results." -ForegroundColor Green
