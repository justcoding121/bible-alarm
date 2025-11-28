# Replace license key placeholder in AppSettings.cs with actual key from environment variable

$SourceFile = Join-Path $PSScriptRoot "AppSettings.cs"
$LicenseKey = $env:SYNCFUSION_LICENSE_KEY

if ([string]::IsNullOrEmpty($LicenseKey)) {
    Write-Host "SYNCFUSION_LICENSE_KEY environment variable is not set. Using placeholder."
    exit 0
}

if (-not (Test-Path $SourceFile)) {
    Write-Error "Error: AppSettings.cs not found at $SourceFile"
    exit 1
}

# Read the file content
$Content = Get-Content $SourceFile -Raw

# Replace the placeholder
$Content = $Content -replace '\{\{SYNCFUSION_LICENSE_KEY\}\}', $LicenseKey

# Write back to file
Set-Content -Path $SourceFile -Value $Content -NoNewline

Write-Host "License key replaced in AppSettings.cs"

exit 0

