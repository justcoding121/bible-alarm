# PowerShell script for fastest debug builds for all platforms
param(
    [string]$Platform = "all"
)

Write-Host "Building MAUI app with fastest debug settings for platform: $Platform" -ForegroundColor Green

# Set environment variables for fastest build
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:MSBUILDDISABLENODEREUSE = "1"

$buildSuccess = $true

if ($Platform -eq "all" -or $Platform -eq "android") {
    Write-Host "`nBuilding Android..." -ForegroundColor Yellow
    dotnet build ../Bible.Alarm/Bible.Alarm.csproj `
      --configuration Debug `
      --framework net9.0-android `
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
    
    if ($LASTEXITCODE -ne 0) { $buildSuccess = $false }
}

if ($Platform -eq "all" -or $Platform -eq "ios") {
    Write-Host "`nBuilding iOS..." -ForegroundColor Yellow
    dotnet build ../Bible.Alarm/Bible.Alarm.csproj `
      --configuration Debug `
      --framework net9.0-ios `
      --verbosity minimal `
      --no-restore `
      --property:MtouchLink=None `
      --property:MtouchDebug=true `
      --property:MtouchUseLlvm=false `
      --property:MtouchInterpreter=false `
      --property:MtouchAot=false `
      --property:MtouchArch=ARM64 `
      --property:MtouchSdkVersion=latest `
      --property:MtouchMinimumOSVersion=11.0 `
      --property:MtouchEnableBitcode=false `
      --property:MtouchEnableIncrementalBuilds=true `
      --property:MtouchFastDev=true `
      --property:MtouchUseSGen=true `
      --property:MtouchSGenConcurrent=true
    
    if ($LASTEXITCODE -ne 0) { $buildSuccess = $false }
}

if ($Platform -eq "all" -or $Platform -eq "windows") {
    Write-Host "`nBuilding Windows..." -ForegroundColor Yellow
    dotnet build ../Bible.Alarm/Bible.Alarm.csproj `
      --configuration Debug `
      --framework net9.0-windows10.0.19041.0 `
      --verbosity minimal `
      --no-restore `
      --property:UseWinUI=true `
      --property:WindowsAppSDKSelfContained=false `
      --property:WindowsPackageType=None `
      --property:WindowsAppSDKFrameworkPackageReference=false `
      --property:EnableWindowsTargeting=true `
      --property:UseWindowsAppSDK=true `
      --property:WindowsTargetPlatformVersion=10.0.19041.0 `
      --property:WindowsAppSDKVersion=1.4.231115000 `
      --property:WindowsAppSDKDeploymentManagerAutoInitialize=false `
      --property:WindowsAppSDKDeploymentManagerInitializeWithOptions=false `
      --property:WindowsAppSDKDeploymentManagerAutoInitializeOptions=false
    
    if ($LASTEXITCODE -ne 0) { $buildSuccess = $false }
}

if ($buildSuccess) {
    Write-Host "`nAll builds completed successfully!" -ForegroundColor Green
} else {
    Write-Host "`nSome builds failed. Check the output above." -ForegroundColor Red
    exit 1
}
