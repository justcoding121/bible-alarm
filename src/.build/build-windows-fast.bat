@echo off
echo Building Windows app with fastest debug settings...

REM Set environment variables for fastest build
set DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
set DOTNET_CLI_TELEMETRY_OPTOUT=1
set MSBUILDDISABLENODEREUSE=1

REM Build with optimizations
dotnet build ../Bible.Alarm/Bible.Alarm.csproj ^
  --configuration Debug ^
  --framework net10.0-windows10.0.19041.0 ^
  --verbosity minimal ^
  --no-restore ^
  --property:UseWinUI=true ^
  --property:WindowsAppSDKSelfContained=false ^
  --property:WindowsPackageType=None ^
  --property:WindowsAppSDKFrameworkPackageReference=false ^
  --property:EnableWindowsTargeting=true ^
  --property:UseWindowsAppSDK=true ^
  --property:WindowsTargetPlatformVersion=10.0.19041.0 ^
  --property:WindowsAppSDKVersion=1.4.231115000 ^
  --property:WindowsAppSDKDeploymentManagerAutoInitialize=false ^
  --property:WindowsAppSDKDeploymentManagerInitializeWithOptions=false ^
  --property:WindowsAppSDKDeploymentManagerAutoInitializeOptions=false

echo Build completed!
pause
