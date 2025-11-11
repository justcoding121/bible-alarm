using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Platforms.Android.Services.AndroidServices;
using Serilog;

namespace Bible.Alarm.Platforms.Android;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private static readonly ILogger Logger = Log.ForContext<MainActivity>();
    private IAndroidAlarmHandler _alarmHandler;
    private DateTime? _lastResumeTime;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Set up fullscreen and system UI for splash screen experience
        SetupSplashScreen();

        // BootstrapHelper is already initialized in MauiProgram.cs
        // No need to call it again here for foreground scenarios

        // Handle incoming intents (e.g., from notifications)
        HandleIncomingIntent();

        // Set up background tasks
        SetupBackgroundTasks();
    }

    private void SetupSplashScreen()
    {
        try
        {
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
                catch
                {
                    // ignored
                }
#pragma warning restore CS0618 // Type or member is obsolete
            }

            if (Window == null) return;
            Window.AddFlags(WindowManagerFlags.DrawsSystemBarBackgrounds);
            Window.SetFlags(WindowManagerFlags.Fullscreen, WindowManagerFlags.Fullscreen);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Error setting up splash screen");
        }
    }

    private void HandleIncomingIntent()
    {
        if (Intent?.Extras == null) return;
        var scheduleId = Intent.Extras.GetInt("schedule_id", int.MinValue);
        if (scheduleId != int.MinValue)
        {
            Task.Run(async () =>
            {
                try
                {
                    // Ensure MauiApp is created exactly once (thread-safe)
                    // This handles incoming intents (e.g., from notifications)
                    MauiAppHolder.CreateAndStore();
                    // Run bootstrapper after CreateAndStore for background launch
                    MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
                    
                    _alarmHandler ??= ServiceProviderManager.GetService<IAndroidAlarmHandler>();
                    await _alarmHandler.Handle(scheduleId, true);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Error handling incoming alarm intent");
                }
            });
        }
    }

    private void SetupBackgroundTasks()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    if (_lastResumeTime.HasValue && DateTime.Now.Subtract(_lastResumeTime.Value).TotalSeconds >= 3)
                    {
                        var intent = new Intent(this, typeof(AlarmSetupService));
                        intent.PutExtra("Action", "SetupBackgroundTasks");
                        StartService(intent);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error setting up background tasks");
                    break;
                }
                await Task.Delay(1000);
            }
        });
    }

    protected override void OnResume()
    {
        base.OnResume();
        _lastResumeTime = DateTime.Now;
    }

    protected override void OnPause()
    {
        base.OnPause();
        _lastResumeTime = null;
    }
}