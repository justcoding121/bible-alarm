using Android.App;
using Android.Runtime;
using Android.Util;
using Bible.Alarm.Common;

namespace Bible.Alarm.Platforms.Android;

[Application]
public class MainApplication(nint handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
{
    public override void OnCreate()
    {
        // Create MediaSession as the very first thing - even before MAUI services are registered
        // This ensures MediaSession is available immediately on process start
        try
        {
            Platforms.Android.Services.AndroidAuto.AndroidAutoMediaSessionHelper.Create();
        }
        catch (Exception ex)
        {
            Log.Error("MainApplication", $"Failed to create MediaSession: {ex.Message}");
        }

        base.OnCreate();
        // Note: CreateMauiApp() is called automatically by MAUI framework
        // No need to call CreateAndStore here - it will be called when CreateMauiApp() is invoked
        // MainActivity will handle the bootstrap initialization
    }

    protected override MauiApp CreateMauiApp() => MauiAppHolder.CreateAndStore();
}
