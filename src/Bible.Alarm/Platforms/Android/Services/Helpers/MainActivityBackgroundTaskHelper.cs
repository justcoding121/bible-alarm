#nullable enable

using Android.App;
using Android.Content;
using Bible.Alarm.Platforms.Android.Services.AndroidServices;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Helpers;

/// <summary>
/// Handles background task setup for MainActivity.
/// </summary>
public class MainActivityBackgroundTaskHelper
{
    private readonly ILogger logger;
    private readonly Activity activity;
    private DateTime? lastResumeTime;

    public MainActivityBackgroundTaskHelper(ILogger logger, Activity activity)
    {
        this.logger = logger;
        this.activity = activity;
    }

    /// <summary>
    /// Sets up background tasks if conditions are met.
    /// </summary>
    public void SetupBackgroundTasks()
    {
        Task.Run(() =>
        {
            try
            {
                if (ShouldSetupBackgroundTasks())
                {
                    StartAlarmSetupService();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, AppConstants.Logging.MainActivityBackgroundTaskHelperDiagnosticsLog.ErrorSettingUpBackgroundTasks);
            }
        });
    }

    /// <summary>
    /// Updates the last resume time. Should be called from OnResume.
    /// </summary>
    public void UpdateResumeTime()
    {
        lastResumeTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Clears the last resume time. Should be called from OnPause.
    /// </summary>
    public void ClearResumeTime()
    {
        lastResumeTime = null;
    }

    /// <summary>
    /// Determines if background tasks should be set up based on resume timing.
    /// </summary>
    private bool ShouldSetupBackgroundTasks()
    {
        return lastResumeTime.HasValue &&
               DateTime.UtcNow.Subtract(lastResumeTime.Value).TotalSeconds >= 3;
    }

    /// <summary>
    /// Starts the AlarmSetupService to set up background tasks.
    /// </summary>
    private void StartAlarmSetupService()
    {
        var intent = new Intent(activity, typeof(AlarmSetupService));
        intent.PutExtra("Action", "SetupBackgroundTasks");
        activity.StartService(intent);
    }
}
