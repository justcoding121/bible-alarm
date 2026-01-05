#nullable enable
using Android.OS;
using Android.Support.V4.Media;
using AndroidX.Media;
using Bible.Alarm.Common;
using Bible.Alarm.Platforms.Android.Services.AndroidAuto;
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
            await MauiProgram.WaitForBootstrapAsync(timeoutMs: 30000);
        }
        catch (Exception bootstrapEx)
        {
            logger.Warning(bootstrapEx, "Bootstrap timed out in LegacyMediaBrowserService.OnCreate - will retry when schedules are loaded");
            return;
        }

        applicationState = ServiceProviderManager.GetService<IState<ApplicationState>>();
        if (applicationState != null)
        {
            scheduleChangeTracker = new AndroidAutoScheduleChangeTracker();
            scheduleChangeTracker.Initialize(applicationState);
            applicationState.StateChanged += OnApplicationStateChanged;
            logger.Information("✅ LegacyMediaBrowserService subscribed to schedule list changes");
        }
        else
        {
            logger.Warning("IState<ApplicationState> not available - schedule updates will not refresh Android Auto UI");
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

            logger.Debug("OnApplicationStateChanged: Checking for schedule changes");
            var changes = scheduleChangeTracker.GetSpecificChanges();
            if (changes == null || changes.Count == 0)
            {
                logger.Debug("OnApplicationStateChanged: No changes detected (changes is null or empty)");
                return;
            }

            logger.Information("OnApplicationStateChanged: Detected {Count} schedule changes, notifying Android Auto", changes.Count);
            
            if (mediaBrowserService != null)
            {
                var options = CreateChangeNotificationOptions(changes);
                mediaBrowserService.NotifyChildrenChanged(RootId, options);
                LogScheduleChanges(changes);
            }
            else
            {
                logger.Warning("OnApplicationStateChanged: MediaBrowserService is null, cannot notify Android Auto of changes");
                // Fallback: try to notify without options
                if (mediaBrowserService != null)
                {
                    mediaBrowserService.NotifyChildrenChanged(RootId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error checking schedule list changes - falling back to full refresh");
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
                logger.Debug("Detected schedule added: {ScheduleId}", change.ScheduleId);
                break;
            case ScheduleChangeType.Removed:
                removedIds.Add(change.ScheduleId);
                logger.Debug("Detected schedule removed: {ScheduleId}", change.ScheduleId);
                break;
            case ScheduleChangeType.Updated:
                logger.Debug("Detected schedule updated: {ScheduleId}", change.ScheduleId);
                break;
        }
    }

    private void LogScheduleChanges(List<ScheduleChange> changes)
    {
        var addedCount = changes.Count(c => c.ChangeType == ScheduleChangeType.Added);
        var updatedCount = changes.Count(c => c.ChangeType == ScheduleChangeType.Updated);
        var removedCount = changes.Count(c => c.ChangeType == ScheduleChangeType.Removed);

        logger.Debug("Notified Android Auto of schedule changes: {ChangeCount} changes ({AddedCount} added, {UpdatedCount} updated, {RemovedCount} removed)",
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
            logger.Error(fallbackEx, "Error performing fallback full refresh");
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
