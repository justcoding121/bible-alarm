using Android.Content;
using Bible.Alarm.Contracts.Battery;
using NLog;
using System;

namespace Bible.Alarm.Droid.Services.Battery
{
    public class BatteryOptimizationManager : IBatteryOptimizationManager
    {
        private static readonly Lazy<Logger> lazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger logger => lazyLogger.Value;


        public IContainer container { get; set; }

        public BatteryOptimizationManager(IContainer container)
        {
            this.container = container;
        }

        public void ShowBatteryOptimizationExclusionSettingsPage()
        {
            try
            {
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
                {
                    Intent intent = new Intent();

                    intent.SetAction(Android.Provider.Settings.ActionIgnoreBatteryOptimizationSettings);
                    container.AndroidContext().StartActivity(intent);
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to show batter optimization dialog.");
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