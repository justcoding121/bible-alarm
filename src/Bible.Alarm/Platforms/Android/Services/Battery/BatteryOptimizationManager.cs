using Android.Content;
using Bible.Alarm.Contracts.Battery;
// using Bible.Alarm.Services.Droid.Extensions; // Removed - no longer needed
using Serilog;
using System;

namespace Bible.Alarm.Droid.Services.Battery
{
    public class BatteryOptimizationManager(IContainer container) : IBatteryOptimizationManager
    {
        private static readonly ILogger Logger = Log.ForContext<BatteryOptimizationManager>();


        public IContainer Container { get; set; } = container;

        public void ShowBatteryOptimizationExclusionSettingsPage()
        {
            try
            {
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
                {
                    Intent intent = new Intent();

#pragma warning disable CA1416
                    intent.SetAction(Android.Provider.Settings.ActionIgnoreBatteryOptimizationSettings);
#pragma warning restore CA1416
                    Container.AndroidContext().StartActivity(intent);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to show batter optimization dialog.");
            }
        }

        public bool CanShowOptimizeActivity()
        {
            return Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M;
        }

        public void Dispose()
        {

        }
    }
}