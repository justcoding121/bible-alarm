using Android.OS;
using AndroidX.Media3.Session;
using Bible.Alarm.Common;
using Bible.Alarm.Platforms.Android.Services.AndroidAuto;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Media3;

/// <summary>
/// Copy of the legacy schedule subscription. Notifies the Media3 session instead of MediaBrowserServiceCompat.
/// </summary>
internal sealed class Media3StateSubscription(ILogger logger)
{
    private IState<ApplicationState>? applicationState;
    private AndroidAutoScheduleChangeTracker? scheduleChangeTracker;
    private MediaLibraryService.MediaLibrarySession? session;

    public void SetSession(MediaLibraryService.MediaLibrarySession mediaSession)
    {
        session = mediaSession;
    }

    public async Task InitializeAsync()
    {
        try
        {
            await MauiProgram.WaitForBootstrapAsync();
        }
        catch (Exception bootstrapEx)
        {
            logger.Warning(bootstrapEx, AppConstants.Logging.LegacyMediaBrowserStateSubscriptionDiagnosticsLog.BootstrapTimedOutOnCreateWillRetry);
            return;
        }

        applicationState = ServiceProviderManager.GetService<IState<ApplicationState>>();
        if (applicationState == null)
        {
            logger.Warning(AppConstants.Logging.LegacyMediaBrowserStateSubscriptionDiagnosticsLog.ApplicationStateNotAvailable);
            return;
        }

        scheduleChangeTracker = new AndroidAutoScheduleChangeTracker();
        scheduleChangeTracker.Initialize(applicationState);
        applicationState.StateChanged += OnApplicationStateChanged;
    }

    public void Cleanup()
    {
        if (applicationState != null)
        {
            applicationState.StateChanged -= OnApplicationStateChanged;
        }
    }

    private void OnApplicationStateChanged(object? sender, EventArgs e)
    {
        if (scheduleChangeTracker == null || session == null)
        {
            return;
        }

        var changes = scheduleChangeTracker.GetSpecificChanges();
        if (changes == null || changes.Count == 0)
        {
            return;
        }

        var extras = new Bundle();
        extras.PutIntArray("changed_schedule_ids", changes.Select(change => change.ScheduleId).ToArray());
        extras.PutIntArray("added_schedule_ids", changes.Where(change => change.ChangeType == ScheduleChangeType.Added).Select(change => change.ScheduleId).ToArray());
        extras.PutIntArray("removed_schedule_ids", changes.Where(change => change.ChangeType == ScheduleChangeType.Removed).Select(change => change.ScheduleId).ToArray());
        var libraryParams = new MediaLibraryService.LibraryParams.Builder().SetExtras(extras)!.Build();
        var childCount = AndroidAutoScheduleHelper.LoadScheduleStateItemsFromState().Count;
        session.NotifyChildrenChanged(Media3LibraryCatalog.RootId, childCount, libraryParams);
    }
}
