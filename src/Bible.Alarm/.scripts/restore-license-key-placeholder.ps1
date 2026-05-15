# Restore license key placeholder in AppSettings.cs after build

$ProjectRoot = Split-Path $PSScriptRoot -Parent
$SourceFile = Join-Path $ProjectRoot "AppSettings.cs"

if (-not (Test-Path $SourceFile)) {
    Write-Host "AppSettings.cs not found at $SourceFile. Skipping restore."
    exit 0
}

# Read the file content
$Content = Get-Content $SourceFile -Raw

# Check if placeholder already exists
$Placeholder = '{{SYNCFUSION_LICENSE_KEY}}'
if ($Content -match [regex]::Escape($Placeholder)) {
    Write-Host "AppSettings.cs already contains placeholder. No restore needed."
    exit 0
}

# Check if file contains a license key (not placeholder)
if ($Content -match 'SyncfusionLicenseKey\s*=>\s*"([^"]+)"') {
    # Restore the placeholder
    $Content = $Content -replace 'SyncfusionLicenseKey\s*=>\s*"[^"]+"', "SyncfusionLicenseKey => `"$Placeholder`""

    # Write back to file
    Set-Content -Path $SourceFile -Value $Content -NoNewline

    Write-Host "License key placeholder restored in AppSettings.cs"
} else {
    Write-Host "Could not find license key pattern in AppSettings.cs. Skipping restore."
}

exit 0
