@echo off
setlocal enabledelayedexpansion

set PLATFORM=%1
if "%PLATFORM%"=="" set PLATFORM=all

echo Building MAUI app with fastest debug settings for platform: %PLATFORM%

REM Set environment variables for fastest build
set DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
set DOTNET_CLI_TELEMETRY_OPTOUT=1
set MSBUILDDISABLENODEREUSE=1

set BUILD_SUCCESS=1

if "%PLATFORM%"=="all" goto :build_android
if "%PLATFORM%"=="android" goto :build_android
if "%PLATFORM%"=="ios" goto :build_ios
if "%PLATFORM%"=="windows" goto :build_windows
goto :end

:build_android
echo.
echo Building Android...
dotnet build ../Bible.Alarm/Bible.Alarm.csproj ^
  --configuration Debug ^
  --framework net10.0-android ^
  --verbosity minimal ^
  --no-restore ^
  --property:AndroidUseSharedRuntime=true ^
  --property:AndroidLinkMode=None ^
  --property:AndroidUseAapt2=true ^
  --property:AndroidCreatePackagePerAbi=false ^
  --property:AndroidEnableProguard=false ^
  --property:AndroidEnableMultiDex=false ^
  --property:AndroidDexTool=d8 ^
  --property:AndroidLinkResources=false ^
  --property:AndroidEnableSGenConcurrent=true ^
  --property:AndroidAotEnable=false ^
  --property:AndroidEnableAssemblyCompression=false

if %ERRORLEVEL% neq 0 set BUILD_SUCCESS=0
if "%PLATFORM%"=="android" goto :end

:build_ios
echo.
echo Building iOS...
dotnet build ../Bible.Alarm/Bible.Alarm.csproj ^
  --configuration Debug ^
  --framework net10.0-ios ^
  --verbosity minimal ^
  --no-restore ^
  --property:MtouchLink=None ^
  --property:MtouchDebug=true ^
  --property:MtouchUseLlvm=false ^
  --property:MtouchInterpreter=false ^
  --property:MtouchAot=false ^
  --property:MtouchArch=ARM64 ^
  --property:MtouchSdkVersion=latest ^
  --property:MtouchMinimumOSVersion=15.0 ^
  --property:MtouchEnableBitcode=false ^
  --property:MtouchEnableIncrementalBuilds=true ^
  --property:MtouchFastDev=true ^
  --property:MtouchUseSGen=true ^
  --property:MtouchSGenConcurrent=true

if %ERRORLEVEL% neq 0 set BUILD_SUCCESS=0
if "%PLATFORM%"=="ios" goto :end

:build_windows
echo.
echo Building Windows...
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

if %ERRORLEVEL% neq 0 set BUILD_SUCCESS=0

:end
if %BUILD_SUCCESS%==1 (
    echo.
    echo All builds completed successfully!
) else (
    echo.
    echo Some builds failed. Check the output above.
    exit /b 1
)

pause
