using System;
using Microsoft.Maui.Devices;

namespace Bible.Alarm.Contracts.Battery
{
    public interface IBatteryOptimizationManager : IDisposable
    {
        void ShowBatteryOptimizationExclusionSettingsPage();
        bool CanShowOptimizeActivity();
    }
}
