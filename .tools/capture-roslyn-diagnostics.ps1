# Captures compiler + Roslyn analyzer diagnostics from build output into a text file.
#
# Sonar rules (S####) only appear when SonarScanner "begin" runs before build (same as CI).
# Without -WithSonarCloud, you get CSC warnings (CS####) and other analyzers shipped with packages/SDK.
#
# Usage (from repo root):
#   pwsh .tools/capture-roslyn-diagnostics.ps1 -OutputFile "$env:USERPROFILE\OneDrive\Desktop\Run dotnet build.txt"
#   $env:SONAR_TOKEN = '...'; pwsh .tools/capture-roslyn-diagnostics.ps1 -WithSonarCloud
#
param(
    [string]$OutputFile = (Join-Path $env:USERPROFILE "Desktop\Roslyn-diagnostics.txt"),
    [switch]$WithSonarCloud,
    [string]$Solution = "src/Bible.Alarm.sln",
    [string]$Configuration = "Release",
    [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')]
    [string]$Verbosity = "normal",
    [switch]$WorkloadRestoreFirst
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot

function Get-DiagnosticLines {
    param([string[]]$Lines)
    # Compiler + analyzer IDs: CS (Roslyn/compiler), CA (SDK analyzers), S (Sonar). Case-insensitive.
    # Strip optional MSBuild node prefix (e.g. "     1>").
    $Lines | ForEach-Object {
        $line = ($_ -replace '^\s*\d+>', '')
        if ($line -match '(?i)((warning|error)\s+(CS|CA|S)\d+:)') {
            $line.TrimStart()
        }
    }
}

try {
    $sb = [System.Text.StringBuilder]::new()
    $null = $sb.AppendLine("# Roslyn / compiler / analyzer diagnostics")
    $null = $sb.AppendLine("# Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
    $null = $sb.AppendLine("# Repo: $repoRoot")
    $null = $sb.AppendLine("#")
    if ($WithSonarCloud) {
        $null = $sb.AppendLine("# Mode: WITH SonarScanner begin (matches CI Sonar S-rules during build).")
    } else {
        $null = $sb.AppendLine("# Mode: local build only - Sonar S#### rules require -WithSonarCloud and SONAR_TOKEN (see .github/workflows/build.yml).")
    }
    $null = $sb.AppendLine("#")
    $null = $sb.AppendLine("dotnet build $Solution --configuration $Configuration -m:1 --verbosity $Verbosity --no-incremental")
    $null = $sb.AppendLine("")

    if ($WorkloadRestoreFirst) {
        dotnet workload restore --project src/Bible.Alarm/Bible.Alarm.csproj 2>&1 | Out-Null
        dotnet restore $Solution 2>&1 | Out-Null
    }

    if ($WithSonarCloud) {
        if (-not $env:SONAR_TOKEN) {
            throw "SONAR_TOKEN environment variable is required when using -WithSonarCloud."
        }
        dotnet tool install --global dotnet-sonarscanner 2>$null
        dotnet sonarscanner begin `
            /k:"justcoding121_bible-alarm" `
            /o:"justcoding121" `
            /d:sonar.host.url="https://sonarcloud.io" `
            /d:sonar.token="$env:SONAR_TOKEN" `
            /d:sonar.language="cs" `
            /d:sonar.exclusions="**/bin/**/*,**/obj/**/*,**/*.Tests/**,**/Database/Migrations/**,**/*.pem" `
            /d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml"
    }

    $textLines = @()
    $logPath = Join-Path ([System.IO.Path]::GetTempPath()) ("sdk-build-{0}.log" -f [guid]::NewGuid().ToString("n"))
    try {
        try {
            & dotnet build $Solution --configuration $Configuration -m:1 --verbosity $Verbosity --no-incremental *> $logPath
            if (Test-Path -LiteralPath $logPath) {
                $textLines = @(Get-Content -LiteralPath $logPath -ErrorAction Stop)
            }
        } finally {
            if (Test-Path -LiteralPath $logPath) {
                Remove-Item -LiteralPath $logPath -Force -ErrorAction SilentlyContinue
            }
        }
    }
    finally {
        if ($WithSonarCloud) {
            dotnet sonarscanner end /d:sonar.token="$env:SONAR_TOKEN"
        }
    }

    $diag = @(Get-DiagnosticLines -Lines $textLines)
    $unique = $diag | Sort-Object -Unique

    $null = $sb.AppendLine("=== Build summary (last 15 lines) ===")
    $null = $sb.AppendLine(($textLines | Select-Object -Last 15) -join [Environment]::NewLine)
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("=== Unique diagnostic lines ($($unique.Count)) ===")
    foreach ($u in $unique) {
        $null = $sb.AppendLine($u)
    }

    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("=== Count by rule id (CS#### / CA#### / S####) ===")
    $ruleHits = [System.Collections.Generic.List[string]]::new()
    foreach ($u in $unique) {
        if ($u -match '(?i)(CS|CA|S)\d{4}') {
            $ruleHits.Add($Matches[0].ToUpperInvariant())
        }
    }
    $grouped = $ruleHits | Group-Object | Sort-Object Count -Descending
    foreach ($g in $grouped) {
        $null = $sb.AppendLine(("{0,5} {1}" -f $g.Count, $g.Name))
    }

    $dir = Split-Path $OutputFile -Parent
    if ($dir -and -not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    $sb.ToString() | Set-Content -Path $OutputFile -Encoding utf8

    Write-Host "Wrote $($unique.Count) unique diagnostic lines to $OutputFile"
}
finally {
    Pop-Location
}
