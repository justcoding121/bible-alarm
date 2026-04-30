# Export SonarCloud issues to JSON (paginated).
# Usage:
#   pwsh .github/scripts/export-sonar-issues.ps1
#   pwsh .github/scripts/export-sonar-issues.ps1 -OutFile ./my-issues.json
#   pwsh .github/scripts/export-sonar-issues.ps1 -Branch develop
#
# Public projects work without a token. For private projects set SONAR_TOKEN (User token from SonarCloud).

param(
    [string] $Organization = 'justcoding121',
    [string] $ProjectKey = 'justcoding121_bible-alarm',
    [string] $OutFile = 'sonar-issues-open-maintainability.json',
    [ValidateSet('OPEN', 'CONFIRMED', 'REOPENED')]
    [string[]] $Statuses = @('OPEN', 'CONFIRMED'),
    [ValidateSet('MAINTAINABILITY', 'RELIABILITY', 'SECURITY')]
    [string[]] $ImpactSoftwareQualities = @('MAINTAINABILITY'),
    [string] $Branch = '',
    [int] $PageSize = 500
)

$statusParam = ($Statuses | ForEach-Object { $_.ToUpperInvariant() }) -join ','
$impactParam = ($ImpactSoftwareQualities | ForEach-Object { $_.ToUpperInvariant() }) -join ','

$headers = @{}
$token = [Environment]::GetEnvironmentVariable('SONAR_TOKEN')
if ($token) {
    $headers['Authorization'] = "Bearer $token"
}

$all = [System.Collections.Generic.List[object]]::new()
$page = 1
$total = [int]::MaxValue

while ($all.Count -lt $total) {
    $pairs = @(
        "organization=$([Uri]::EscapeDataString($Organization))",
        "componentKeys=$([Uri]::EscapeDataString($ProjectKey))",
        "statuses=$([Uri]::EscapeDataString($statusParam))",
        "impactSoftwareQualities=$([Uri]::EscapeDataString($impactParam))",
        "ps=$PageSize",
        "p=$page"
    )
    if ($Branch) {
        $pairs += "branch=$([Uri]::EscapeDataString($Branch))"
    }

    $uri = "https://sonarcloud.io/api/issues/search?" + ($pairs -join '&')
    $response = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get
    foreach ($issue in $response.issues) {
        $all.Add($issue)
    }

    $total = [int]$response.paging.total
    if ($response.issues.Count -eq 0) {
        break
    }

    $page++
}

$payload = [ordered]@{
    pulledAtUtc             = (Get-Date).ToUniversalTime().ToString('o')
    organization            = $Organization
    componentKeys           = $ProjectKey
    branch                  = $Branch
    filters                 = @{
        statuses                 = $Statuses
        impactSoftwareQualities  = $ImpactSoftwareQualities
    }
    total                   = $all.Count
    issues                  = @($all)
}

$json = $payload | ConvertTo-Json -Depth 20 -Compress
$path = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutFile)
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $($all.Count) issues to $path"
