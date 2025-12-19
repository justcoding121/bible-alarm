using System.Runtime.Versioning;
using Android.Content;
using Android.OS;
using Bible.Alarm.Common.Interfaces.Battery;
using Serilog;
using AndroidApplication = Android.App.Application;
using AndroidBuild = Android.OS.Build;
using AndroidProvider = Android.Provider;

namespace Bible.Alarm.Platforms.Android.Services.Battery;

public class AndroidBatteryOptimizationManager : IBatteryOptimizationManager, IDisposable
{
    private static readonly ILogger logger = Log.ForContext<AndroidBatteryOptimizationManager>();

    public void ShowBatteryOptimizationExclusionSettingsPage()
    {
        try
        {
            // Minimum supported is API 26, so this is always available
            var intent = new Intent();
            intent.SetAction(AndroidProvider.Settings.ActionIgnoreBatteryOptimizationSettings);
            AndroidApplication.Context.StartActivity(intent);
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to show batter optimization dialog.");
        }
    }

    public bool CanShowOptimizeActivity()
    {
        return Build.VERSION.SdkInt >= BuildVersionCodes.M;
    }

    public void Dispose()
    {
    }
}
