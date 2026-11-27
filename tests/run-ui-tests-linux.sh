#!/usr/bin/env bash
# Headless Android phone or Android Automotive UI tests on LinuxMini (KVM).
# Usage: ./tests/run-ui-tests-linux.sh phone|auto
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"
kind="${1:-phone}"
avd="${AVD_NAME:-BA_Pixel6_API34}"
filter="UI=Android"
if [[ "${kind}" == "auto" ]]; then
  avd="${AVD_NAME:-BA_Auto_API34}"
  filter="UI=AndroidAuto"
fi

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH:${ANDROID_HOME:-$HOME/Android/Sdk}/platform-tools:${ANDROID_HOME:-$HOME/Android/Sdk}/emulator"

dotnet build src/Bible.Alarm/Bible.Alarm.csproj -c Release -f net10.0-android -m:1 \
  -p:BUILD_ANDROID_ONLY=true -p:UiTestHooks=true -p:AndroidPackageFormat=apk
dotnet build tests/Bible.Alarm.CarHostSim/Bible.Alarm.CarHostSim.csproj -c Release -f net10.0-android -m:1
dotnet build tests/Bible.Alarm.UITests/Bible.Alarm.UITests.csproj -c Release

if ! adb devices | grep -q $'\tdevice$'; then
  emulator -avd "${avd}" -no-window -gpu swiftshader_indirect -no-audio -no-boot-anim >/tmp/bible-alarm-emulator.log 2>&1 &
  adb wait-for-device
fi

export UI_TEST_FILTER="${filter}"
bash .github/scripts/ci-android-ui-tests.sh
