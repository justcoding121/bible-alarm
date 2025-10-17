using Android.Content;
using Bible.Alarm.Contracts.Battery;
using Bible.Alarm.Services.Droid.Extensions;
using NLog;
using System;

namespace Bible.Alarm.Droid.Services.Battery
{
    public class BatteryOptimizationManager(IContainer container) : IBatteryOptimizationManager
    {
        private static readonly Lazy<Logger> LazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger Logger => LazyLogger.Value;


        public IContainer Container { get; set; } = container;

        public void ShowBatteryOptimizationExclusionSettingsPage()
        {
            try
            {
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
                {
                    Intent intent = new Intent();

                    intent.SetAction(Android.Provider.Settings.ActionIgnoreBatteryOptimizationSettings);
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