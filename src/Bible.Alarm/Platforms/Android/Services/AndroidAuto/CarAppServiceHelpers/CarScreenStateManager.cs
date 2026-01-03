#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto.CarAppServiceHelpers;

/// <summary>
/// Handles state management for MainCarScreen.
/// </summary>
public sealed class CarScreenStateManager(ILogger logger)
{
    private List<ScheduleStateItem>? scheduleItems;
    private IState<ApplicationState>? applicationState;
    private AndroidAutoScheduleChangeTracker? scheduleChangeTracker;

    /// <summary>
    /// Initializes state subscription.
    /// </summary>
    public void InitializeStateSubscription()
    {
        // Subscribe to state changes to refresh the template when schedules are added/updated/removed
        try
        {
            applicationState = ServiceProviderManager.GetService<IState<ApplicationState>>();
            if (applicationState != null)
            {
                scheduleChangeTracker = new AndroidAutoScheduleChangeTracker();
                scheduleChangeTracker.Initialize(applicationState);
                applicationState.StateChanged += OnApplicationStateChanged;
                logger.Information("✅ MainCarScreen subscribed to schedule list changes");

                // Load initial schedule items
                LoadScheduleItems();
            }
            else
            {
                logger.Warning("IState<ApplicationState> not available - schedule updates will not refresh Android Auto UI");
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error subscribing to ApplicationState changes in MainCarScreen");
        }
    }

    /// <summary>
    /// Loads schedule items from state.
    /// </summary>
    public void LoadScheduleItems()
    {
        try
        {
            scheduleItems = AndroidAutoScheduleHelper.LoadScheduleStateItemsFromState();
            logger.Debug("Loaded {Count} schedule items for Android Auto", scheduleItems.Count);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error loading schedule items");
            scheduleItems = new List<ScheduleStateItem>();
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
                logger.Debug("OnApplicationStateChanged: No changes detected");
                return;
            }

            logger.Information("OnApplicationStateChanged: Detected {Count} schedule changes, invalidating template", changes.Count);

            // Reload schedule items and invalidate template
            LoadScheduleItems();
            // Note: Template invalidation would be handled by the screen
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error checking schedule list changes");
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

    /// <summary>
    /// Gets the current schedule items.
    /// </summary>
    public List<ScheduleStateItem>? ScheduleItems => scheduleItems;
}
