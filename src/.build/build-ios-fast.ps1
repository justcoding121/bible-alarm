# PowerShell script for fastest iOS debug builds
Write-Host "Building iOS app with fastest debug settings..." -ForegroundColor Green

# Set environment variables for fastest build
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:MSBUILDDISABLENODEREUSE = "1"

# Build with optimizations
dotnet build ../Bible.Alarm/Bible.Alarm.csproj `
  --configuration Debug `
  --framework net10.0-ios `
  --verbosity minimal `
  --no-restore `
  --property:MtouchLink=None `
  --property:MtouchDebug=true `
  --property:MtouchUseLlvm=false `
  --property:MtouchInterpreter=false `
  --property:MtouchAot=false `
  --property:MtouchArch=ARM64 `
  --property:MtouchSdkVersion=latest `
  --property:MtouchMinimumOSVersion=15.0 `
  --property:MtouchEnableBitcode=false `
  --property:MtouchEnableIncrementalBuilds=true `
  --property:MtouchFastDev=true `
  --property:MtouchUseSGen=true `
  --property:MtouchSGenConcurrent=true

Write-Host "Build completed!" -ForegroundColor Green
