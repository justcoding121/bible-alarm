using Android.App;
using Android.Runtime;
using Bible.Alarm.Common;

namespace Bible.Alarm.Platforms.Android
{
    [Application]
    public class MainApplication(nint handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
    {
    public override void OnCreate()
    {
        base.OnCreate();
        // Note: CreateMauiApp() is called automatically by MAUI framework
        // No need to call CreateAndStore here - it will be called when CreateMauiApp() is invoked
        // SplashActivity will handle the bootstrap initialization
    }

    protected override MauiApp CreateMauiApp() => MauiAppHolder.CreateAndStore();
    }
}
