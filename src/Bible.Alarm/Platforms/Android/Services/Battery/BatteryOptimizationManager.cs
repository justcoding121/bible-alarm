using Android.Content;
using AndroidBuild = global::Android.OS.Build;
using AndroidBuildVersionCodes = global::Android.OS.Build.VERSION_CODES;
using AndroidProvider = global::Android.Provider;
using AndroidApplication = global::Android.App.Application;
using Bible.Alarm.Contracts.Battery;


using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Battery;

public class BatteryOptimizationManager() : IBatteryOptimizationManager
{
    private static readonly ILogger Logger = Log.ForContext<BatteryOptimizationManager>();

    public void ShowBatteryOptimizationExclusionSettingsPage()
    {
        try
        {
            if (AndroidBuild.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.M)
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
        return AndroidBuild.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.M;
    }

    public void Dispose()
    {
    }
}