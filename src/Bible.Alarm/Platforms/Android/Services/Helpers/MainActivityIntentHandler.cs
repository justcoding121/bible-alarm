#nullable enable

using System;
using System.Threading.Tasks;
using Android.Content;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Interfaces.Media;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Helpers;

/// <summary>
/// Handles incoming intents for MainActivity, particularly alarm intents.
/// </summary>
public class MainActivityIntentHandler
{
    private readonly ILogger logger;
    private IAndroidAlarmHandler? alarmHandler;

    public MainActivityIntentHandler(ILogger logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Handles incoming intents from the activity's Intent.
    /// </summary>
    public void HandleIncomingIntent(Intent? intent)
    {
        if (!HasValidScheduleIdFromIntent(intent))
        {
            return;
        }

        var extras = intent?.Extras;
        if (extras == null)
        {
            return;
        }

        var scheduleId = extras.GetInt("schedule_id", int.MinValue);
        Task.Run(() => HandleAlarmIntentAsync(scheduleId));
    }

    /// <summary>
    /// Checks if the intent contains a valid schedule ID.
    /// </summary>
    private static bool HasValidScheduleIdFromIntent(Intent? intent)
    {
        return intent?.Extras != null &&
               intent.Extras.GetInt("schedule_id", int.MinValue) != int.MinValue;
    }

    /// <summary>
    /// Handles an alarm intent asynchronously.
    /// </summary>
    private async Task HandleAlarmIntentAsync(int scheduleId)
    {
        try
        {
            await InitializeAppForBackgroundLaunchAsync();
            await HandleAlarmAsync(scheduleId);
        }
        catch (Exception e)
        {
            logger.Error(e, "Error handling incoming alarm intent");
        }
    }

    /// <summary>
    /// Initializes the app for background launch (e.g., from notification).
    /// </summary>
    private static async Task InitializeAppForBackgroundLaunchAsync()
    {
        // Ensure MauiApp is created exactly once (thread-safe)
        // This handles incoming intents (e.g., from notifications)
        MauiAppHolder.CreateAndStore();
        // Run bootstrapper after CreateAndStore for background launch
        MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
        // Wait for bootstrap to complete before using database services
        await MauiProgram.WaitForBootstrapAsync();
    }

    /// <summary>
    /// Handles the alarm for the given schedule ID.
    /// </summary>
    private async Task HandleAlarmAsync(int scheduleId)
    {
        alarmHandler ??= ServiceProviderManager.GetService<IAndroidAlarmHandler>();
        await alarmHandler.HandleAsync(scheduleId, false);
    }
}
