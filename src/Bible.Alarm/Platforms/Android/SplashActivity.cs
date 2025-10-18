using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.AppCompat.App;
using Bible.Alarm.Droid.Services.Platform;
using Bible.Alarm.Services.Infrastructure;
using Serilog;

namespace Bible.Alarm.Platforms.Android
{
    [Activity(Label = "Bible Alarm", Theme = "@style/MyTheme.Splash", Icon = "@mipmap/ic_launcher",
        MainLauncher = true, NoHistory = true)]
    public class SplashActivity : AppCompatActivity
    {
        private static readonly ILogger Logger = Log.ForContext<SplashActivity>();


        public SplashActivity()
        {
            LogSetup.Initialize(VersionFinder.Default,
                new string[] { $"AndroidSdk {Build.VERSION.SdkInt}" }, "Android");

            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException += UnobserverdTaskException;
        }

        private void UnobserverdTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Logger.Error(e.Exception, "Unobserved task exception.");
        }

        private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Error("Unhandled exception.", e.SerializeObject());
        }

        protected override async void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
                {
                    Window.SetDecorFitsSystemWindows(false);
                }
                else
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    try
                    {
                        Window.DecorView.SystemUiVisibility = (StatusBarVisibility)((int)Window.DecorView.SystemUiVisibility ^ (int)SystemUiFlags.LayoutStable ^ (int)SystemUiFlags.LayoutFullscreen);
                    }
                    catch { }
#pragma warning restore CS0618 // Type or member is obsolete
                }

                Window.AddFlags(WindowManagerFlags.DrawsSystemBarBackgrounds);
                Window.SetFlags(WindowManagerFlags.Fullscreen, WindowManagerFlags.Fullscreen);

                SetContentView(Resource.Layout.SplashScreen);
            }
            catch (Exception e)
            {
                Logger.Fatal(e, "An error happened inside OnCreate.");
                await Task.Delay(1500);
                throw;
            }

        }


        public override async void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            try
            {
                Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            }
            catch (Exception e)
            {
                Logger.Error(e, "An error happened inside OnRequestpermissionResult.");
                await Task.Delay(1500);
            }

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        // Launches the startup task
        protected override void OnResume()
        {
            base.OnResume();

            Task.Run(async () => await DoWork());
        }

        // background work that happens behind the splash screen
        private async Task DoWork()
        {
            try
            {
                var intent = new Intent(this, typeof(Bible.Alarm.Droid.MainActivity));
                intent.SetFlags(ActivityFlags.ReorderToFront);
                StartActivity(intent);
            }
            catch (Exception e)
            {
                Logger.Fatal(e, "An error happened in doWork() task under SplashActivity.");
                await Task.Delay(1500);
                throw;
            }
        }

        private bool _disposed = false;

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            AppDomain.CurrentDomain.UnhandledException -= UnhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException -= UnobserverdTaskException;

            _disposed = true;

            base.Dispose(disposing);
        }
    }
}
