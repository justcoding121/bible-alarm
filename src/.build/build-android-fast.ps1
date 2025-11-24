# PowerShell script for fastest Android debug builds
Write-Host "Building Android app with fastest debug settings..." -ForegroundColor Green

# Set environment variables for fastest build
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:MSBUILDDISABLENODEREUSE = "1"

# Build with optimizations
dotnet build ../Bible.Alarm/Bible.Alarm.csproj `
  --configuration Debug `
  --framework net10.0-android `
  --verbosity minimal `
  --no-restore `
  --property:AndroidUseSharedRuntime=true `
  --property:AndroidLinkMode=None `
  --property:AndroidUseAapt2=true `
  --property:AndroidCreatePackagePerAbi=false `
  --property:AndroidEnableProguard=false `
  --property:AndroidEnableMultiDex=false `
  --property:AndroidDexTool=d8 `
  --property:AndroidLinkResources=false `
  --property:AndroidEnableSGenConcurrent=true `
  --property:AndroidAotEnable=false `
  --property:AndroidEnableAssemblyCompression=false

Write-Host "Build completed!" -ForegroundColor Green
