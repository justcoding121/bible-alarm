#nullable enable
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto.LegacyMediaBrowserHelpers;

/// <summary>
/// Handles state subscription and tracking for LegacyMediaBrowserService.
/// </summary>
public sealed class StateSubscriptionManager(ILogger logger)
{
    private IState<ApplicationState>? applicationState;
    private AndroidAutoScheduleChangeTracker? scheduleChangeTracker;

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
        // Handle state changes - this would typically notify about schedule changes
        // The actual implementation would depend on what needs to be done when schedules change
        logger.Debug("Application state changed in LegacyMediaBrowserService");
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
