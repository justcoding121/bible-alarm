using Android.Content;
using Android.OS;
using Bible.Alarm.Common.Interfaces.Battery;
using Serilog;
using AndroidBuild = Android.OS.Build;
using AndroidProvider = Android.Provider;
using AndroidApplication = Android.App.Application;

namespace Bible.Alarm.Platforms.Android.Services.Battery;

public class AndroidBatteryOptimizationManager : IBatteryOptimizationManager
{
    private static readonly ILogger Logger = Log.ForContext<AndroidBatteryOptimizationManager>();

    public void ShowBatteryOptimizationExclusionSettingsPage()
    {
        try
        {
            if (AndroidBuild.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                var intent = new Intent();

#pragma warning disable CA1416
                intent.SetAction(AndroidProvider.Settings.ActionIgnoreBatteryOptimizationSettings);
#pragma warning restore CA1416
                AndroidApplication.Context.StartActivity(intent);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to show batter optimization dialog.");
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