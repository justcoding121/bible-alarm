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
            
            // Ensure MauiApp is created exactly once (thread-safe)
            MauiAppHolder.CreateAndStore();
        }

        protected override MauiApp CreateMauiApp() => MauiAppHolder.CreateAndStore();
    }
}
