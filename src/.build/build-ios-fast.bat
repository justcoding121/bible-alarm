@echo off
echo Building iOS app with fastest debug settings...

REM Set environment variables for fastest build
set DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
set DOTNET_CLI_TELEMETRY_OPTOUT=1
set MSBUILDDISABLENODEREUSE=1

REM Build with optimizations
dotnet build ../Bible.Alarm/Bible.Alarm.csproj ^
  --configuration Debug ^
  --framework net9.0-ios ^
  --verbosity minimal ^
  --no-restore ^
  --property:MtouchLink=None ^
  --property:MtouchDebug=true ^
  --property:MtouchUseLlvm=false ^
  --property:MtouchInterpreter=false ^
  --property:MtouchAot=false ^
  --property:MtouchArch=ARM64 ^
  --property:MtouchSdkVersion=latest ^
  --property:MtouchMinimumOSVersion=11.0 ^
  --property:MtouchEnableBitcode=false ^
  --property:MtouchEnableIncrementalBuilds=true ^
  --property:MtouchFastDev=true ^
  --property:MtouchUseSGen=true ^
  --property:MtouchSGenConcurrent=true

echo Build completed!
pause
