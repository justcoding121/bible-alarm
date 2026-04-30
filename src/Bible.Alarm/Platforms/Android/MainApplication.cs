using Android.App;
using Android.Runtime;
using Bible.Alarm.Common;
using Bible.Alarm.Shared.Constants;

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
            Platforms.Android.Services.Media.MediaSessionHelper.Create();
        }
        catch (Exception ex)
        {
            // Serilog may not be initialized yet, but try to log anyway
            try
            {
                Serilog.Log.Error(ex, AppConstants.Logging.MauiPlatformUiDiagnosticsLog.AndroidMainApplicationFailedToCreateMediaSession);
            }
            catch (Exception)
            {
                // If Serilog isn't available, ignore (this happens very early in app lifecycle)
            }
        }

        base.OnCreate();
        // Note: CreateMauiApp() is called automatically by MAUI framework
        // No need to call CreateAndStore here - it will be called when CreateMauiApp() is invoked
        // MainActivity will handle the bootstrap initialization
    }

    protected override MauiApp CreateMauiApp() => MauiAppHolder.CreateAndStore();
}
