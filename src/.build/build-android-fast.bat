@echo off
echo Building Android app with fastest debug settings...

REM Set environment variables for fastest build
set DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
set DOTNET_CLI_TELEMETRY_OPTOUT=1
set MSBUILDDISABLENODEREUSE=1

REM Build with optimizations
dotnet build ../Bible.Alarm/Bible.Alarm.csproj ^
  --configuration Debug ^
  --framework net9.0-android ^
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

echo Build completed!
pause
