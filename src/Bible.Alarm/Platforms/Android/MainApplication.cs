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
            // Foreground launch - bootstrap will run on background Task
            MauiAppHolder.CreateAndStore(isForeground: true);
        }

        protected override MauiApp CreateMauiApp() => MauiAppHolder.CreateAndStore(isForeground: true);
    }
}
