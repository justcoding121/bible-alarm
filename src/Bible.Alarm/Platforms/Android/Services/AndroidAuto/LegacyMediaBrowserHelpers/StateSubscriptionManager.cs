#nullable enable
using Android.OS;
using AndroidX.Media;
using Bible.Alarm.Common;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto.LegacyMediaBrowserHelpers;

/// <summary>
/// Handles state subscription and tracking for LegacyMediaBrowserService.
/// </summary>
public sealed class StateSubscriptionManager(ILogger logger)
{
    private const string RootId = "__ID_ROOT__";
    private IState<ApplicationState>? applicationState;
    private AndroidAutoScheduleChangeTracker? scheduleChangeTracker;
    private MediaBrowserServiceCompat? mediaBrowserService;

    /// <summary>
    /// Sets the MediaBrowserService instance to use for NotifyChildrenChanged.
    /// </summary>
    public void SetMediaBrowserService(MediaBrowserServiceCompat service)
    {
        mediaBrowserService = service;
    }

    /// <summary>
    /// Initializes state subscription asynchronously.
    /// </summary>
    public async Task InitializeStateSubscriptionAsync()
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
        if (applicationState != null)
        {
            scheduleChangeTracker = new AndroidAutoScheduleChangeTracker();
            scheduleChangeTracker.Initialize(applicationState);
            applicationState.StateChanged += OnApplicationStateChanged;
            logger.Information(AppConstants.Logging.LegacyMediaBrowserStateSubscriptionDiagnosticsLog.SubscribedToScheduleListChanges);
        }
        else
        {
            logger.Warning(AppConstants.Logging.LegacyMediaBrowserStateSubscriptionDiagnosticsLog.ApplicationStateNotAvailable);
        }
    }

    /// <summary>
    /// Handles application state changes.
    /// </summary>
    private void OnApplicationStateChanged(object? sender, EventArgs e)
    {
        try
        {
            if (scheduleChangeTracker == null)
            {
                logger.Debug("OnApplicationStateChanged: scheduleChangeTracker is null, skipping");
                return;
            }

            logger.Debug(AppConstants.Logging.LegacyMediaBrowserStateSubscriptionDiagnosticsLog.OnApplicationStateChangedCheckingForScheduleChanges);
            var changes = scheduleChangeTracker.GetSpecificChanges();
            if (changes == null || changes.Count == 0)
            {
                logger.Debug(AppConstants.Logging.LegacyMediaBrowserStateSubscriptionDiagnosticsLog.OnApplicationStateChangedNoChangesDetected);
                return;
            }

            logger.Information(AppConstants.Logging.LegacyMediaBrowserStateSubscriptionDiagnosticsLog.OnApplicationStateChangedDetectedScheduleChangesNotifying, changes.Count);

            if (mediaBrowserService != null)
            {
                var options = CreateChangeNotificationOptions(changes);
                mediaBrowserService.NotifyChildrenChanged(RootId, options);
                LogScheduleChanges(changes);
            }
            else
            {
                logger.Warning(AppConstants.Logging.LegacyMediaBrowserStateSubscriptionDiagnosticsLog.OnApplicationStateChangedMediaBrowserServiceNull);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.LegacyMediaBrowserStateSubscriptionDiagnosticsLog.ErrorCheckingScheduleListChangesFallbackRefresh);
            PerformFallbackRefresh();
        }
    }

    private Bundle CreateChangeNotificationOptions(List<ScheduleChange> changes)
    {
        var options = new Bundle();
        var changedIds = new List<int>();
        var addedIds = new List<int>();
        var removedIds = new List<int>();

        foreach (var change in changes)
        {
            changedIds.Add(change.ScheduleId);
            CategorizeChange(change, addedIds, removedIds);
        }

        options.PutIntArray("changed_schedule_ids", changedIds.ToArray());
        options.PutIntArray("added_schedule_ids", addedIds.ToArray());
        options.PutIntArray("removed_schedule_ids", removedIds.ToArray());

        return options;
    }

    private void CategorizeChange(ScheduleChange change, List<int> addedIds, List<int> removedIds)
    {
        switch (change.ChangeType)
        {
            case ScheduleChangeType.Added:
                addedIds.Add(change.ScheduleId);
                logger.Debug(AppConstants.Logging.LegacyMediaBrowserStateSubscriptionDiagnosticsLog.DetectedScheduleAdded, change.ScheduleId);
                break;
            case ScheduleChangeType.Removed:
                removedIds.Add(change.ScheduleId);
                logger.Debug(AppConstants.Logging.LegacyMediaBrowserStateSubscriptionDiagnosticsLog.DetectedScheduleRemoved, change.ScheduleId);
                break;
            case ScheduleChangeType.Updated:
                logger.Debug(AppConstants.Logging.LegacyMediaBrowserStateSubscriptionDiagnosticsLog.DetectedScheduleUpdated, change.ScheduleId);
                break;
        }
    }

    private void LogScheduleChanges(List<ScheduleChange> changes)
    {
        var addedCount = changes.Count(c => c.ChangeType == ScheduleChangeType.Added);
        var updatedCount = changes.Count(c => c.ChangeType == ScheduleChangeType.Updated);
        var removedCount = changes.Count(c => c.ChangeType == ScheduleChangeType.Removed);

        logger.Debug(AppConstants.Logging.LegacyMediaBrowserStateSubscriptionDiagnosticsLog.NotifiedAndroidAutoOfScheduleChanges,
            changes.Count, addedCount, updatedCount, removedCount);
    }

    private void PerformFallbackRefresh()
    {
        try
        {
            if (mediaBrowserService != null)
            {
                mediaBrowserService.NotifyChildrenChanged(RootId);
            }
        }
        catch (Exception fallbackEx)
        {
            logger.Error(fallbackEx, AppConstants.Logging.LegacyMediaBrowserStateSubscriptionDiagnosticsLog.ErrorPerformingFallbackFullRefresh);
        }
    }

    /// <summary>
    /// Cleans up state subscriptions.
    /// </summary>
    public void Cleanup()
    {
        if (applicationState != null)
        {
            applicationState.StateChanged -= OnApplicationStateChanged;
        }
    }
}
