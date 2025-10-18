using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Bible.Alarm.Services.Droid.Helpers;
using Bible.Alarm.Services.Droid.Tasks;
using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.Contracts.Media;
using Serilog;

namespace Bible.Alarm.Droid;

[Activity(Label = "Bible Alarm", Icon = "@mipmap/ic_launcher", Theme = "@style/MainTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
public class MainActivity : MauiAppCompatActivity
{
    private static readonly ILogger Logger = Log.ForContext<MainActivity>();
    private IAndroidAlarmHandler _alarmHandler;
    private DateTime? _lastResumeTime;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Initialize platform-specific services
        BootstrapHelper.InitializeUi(Logger, this, Application);

        // Handle incoming intents (e.g., from notifications)
        HandleIncomingIntent();

        // Set up background tasks
        SetupBackgroundTasks();
    }

    private void HandleIncomingIntent()
    {
        if (Intent?.Extras != null)
        {
            var scheduleId = Intent.Extras.GetInt("schedule_id", int.MinValue);
            if (scheduleId != int.MinValue)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        if (_alarmHandler == null)
                        {
                            _alarmHandler = ServiceProviderManager.GetService<IAndroidAlarmHandler>();
                        }
                        await _alarmHandler.Handle(scheduleId, true);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Error handling incoming alarm intent");
                    }
                });
            }
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
        // MAUI handles permissions automatically
#pragma warning disable CA1416
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
#pragma warning restore CA1416
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

    protected override void OnDestroy()
    {
        base.OnDestroy();
        BootstrapHelper.Remove(Application);
    }
}