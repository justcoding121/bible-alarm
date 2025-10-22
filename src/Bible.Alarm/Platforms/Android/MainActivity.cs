using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Bible.Alarm.Services.Droid.Helpers;
using Bible.Alarm.Services.Droid.Tasks;
using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.Contracts.Media;
using Serilog;
using Bible.Alarm.Droid.Services.Platform;

namespace Bible.Alarm.Droid;

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

        // Initialize platform-specific services
        BootstrapHelper.Initialize(Logger, this, Application);

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
                Window.SetDecorFitsSystemWindows(false);
#pragma warning restore CA1416, CA1422
            }
            else
            {
#pragma warning disable CS0618 // Type or member is obsolete
                try
                {
                    Window.DecorView.SystemUiVisibility =
                        (StatusBarVisibility)((int)Window.DecorView.SystemUiVisibility ^
                                              (int)SystemUiFlags.LayoutStable ^ (int)SystemUiFlags.LayoutFullscreen);
                }
                catch
                {
                }
#pragma warning restore CS0618 // Type or member is obsolete
            }

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

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        try
        {
            Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
        catch (Exception e)
        {
            Logger.Error(e, "An error happened inside OnRequestPermissionsResult.");
        }

        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
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