using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using AndroidX.AppCompat.App;
using Bible.Alarm.Common;
using Serilog;
using System;
using System.Threading.Tasks;

namespace Bible.Alarm.Platforms.Android;

[Activity(Label = "Bible Alarm", Theme = "@style/MyTheme.Splash", Icon = "@mipmap/ic_launcher",
    MainLauncher = true, NoHistory = true)]
public class SplashActivity : AppCompatActivity
{
    private static readonly ILogger Logger = Log.ForContext<SplashActivity>();

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        try
        {
            // Set up fullscreen and system UI for splash screen experience
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
#pragma warning disable CA1416, CA1422
                Window?.SetDecorFitsSystemWindows(false);
#pragma warning restore CA1416, CA1422
            }
            else
            {
#pragma warning disable CS0618 // Type or member is obsolete
                try
                {
                    if (Window?.DecorView != null)
                    {
                        Window.DecorView.SystemUiVisibility =
                            (StatusBarVisibility)((int)Window.DecorView.SystemUiVisibility ^
                                                  (int)SystemUiFlags.LayoutStable ^ (int)SystemUiFlags.LayoutFullscreen);
                    }
                }
                catch { }
#pragma warning restore CS0618 // Type or member is obsolete
            }

            if (Window != null)
            {
                Window.AddFlags(WindowManagerFlags.DrawsSystemBarBackgrounds);
                Window.SetFlags(WindowManagerFlags.Fullscreen, WindowManagerFlags.Fullscreen);
            }

            SetContentView(Resource.Layout.SplashScreen);
        }
        catch (Exception e)
        {
            Logger.Fatal(e, "An error happened inside OnCreate.");
            Task.Delay(1500).Wait();
            throw;
        }
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
            // Initialize MAUI app
            MauiAppHolder.CreateAndStore();
            
            // Run bootstrapper synchronously to ensure it completes before MainActivity starts
            // This ensures all services are initialized before MainActivity (MauiAppCompatActivity) is launched
            MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);

            // Launch MainActivity on UI thread
            RunOnUiThread(() =>
            {
                try
                {
                    var intent = new Intent(this, typeof(MainActivity));
                    intent.SetFlags(ActivityFlags.ReorderToFront);
                    StartActivity(intent);
                    Finish();
                }
                catch (Exception ex)
                {
                    Logger.Fatal(ex, "Error launching MainActivity from SplashActivity");
                    throw;
                }
            });
        }
        catch (Exception e)
        {
            Logger.Fatal(e, "An error happened in doWork() task under SplashActivity.");
            await Task.Delay(1500);
            throw;
        }
    }
}

