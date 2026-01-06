#nullable enable

using System;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Shared.DataStructures;
using Microsoft.Maui.Essentials;
using Serilog;

namespace Bible.Alarm.ViewModels.HomeViewModelHelpers;

/// <summary>
/// Manages bootstrap ready state tracking for HomeViewModel.
/// Polls for bootstrap completion and notifies when ready.
/// </summary>
public class BootstrapReadyManager : IDisposable
{
    private readonly ILogger logger;
    private bool isBootstrapReady;
    private CancellationTokenSource? bootstrapPollingCancellation;

    public BootstrapReadyManager(ILogger logger)
    {
        this.logger = logger;
    }

    public event Action<bool>? BootstrapReadyChanged;

    /// <summary>
    /// Indicates if bootstrap is complete and the add button should be enabled.
    /// This property updates automatically when bootstrap completes.
    /// </summary>
    public bool IsBootstrapReady
    {
        get => isBootstrapReady;
        private set
        {
            if (isBootstrapReady != value)
            {
                isBootstrapReady = value;
                BootstrapReadyChanged?.Invoke(value);
                logger.Debug("IsBootstrapReady changed to {Value}", value);
            }
        }
    }

    /// <summary>
    /// Starts tracking bootstrap completion status.
    /// Checks immediately and then polls until bootstrap is complete.
    /// </summary>
    public void StartTracking()
    {
        // Check immediately first
        UpdateBootstrapReadyState();

        // If already complete, don't start polling
        if (IsBootstrapReady)
        {
            logger.Debug("Bootstrap already complete, no polling needed");
            return;
        }

        // Use Task-based polling for better reliability
        bootstrapPollingCancellation = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                var maxWaitTime = TimeSpan.FromSeconds(10); // Maximum time to wait
                var startTime = DateTime.UtcNow;
                
                while (!bootstrapPollingCancellation.Token.IsCancellationRequested)
                {
                    await Task.Delay(100, bootstrapPollingCancellation.Token);
                    
                    // Check if we've waited too long - if so, enable button anyway
                    // (bootstrap might be complete but flag not set, or schedules might be empty)
                    if (DateTime.UtcNow - startTime > maxWaitTime)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            if (!IsBootstrapReady)
                            {
                                IsBootstrapReady = true;
                                logger.Information("Bootstrap timeout reached - enabling Add button (bootstrap likely complete)");
                            }
                        });
                        break;
                    }
                    
                    var isComplete = BootstrapHelper.IsBootstrapCompleted();
                    logger.Debug("Bootstrap polling: IsBootstrapCompleted={IsComplete}", isComplete);
                    
                    if (isComplete)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            if (!IsBootstrapReady)
                            {
                                IsBootstrapReady = true;
                                logger.Information("Bootstrap ready - Add button enabled");
                            }
                        });
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in bootstrap polling task");
            }
        });
    }

    /// <summary>
    /// Updates the bootstrap ready state by checking if bootstrap is complete.
    /// Can be called when schedules are loaded (schedules only load after bootstrap).
    /// </summary>
    public void UpdateBootstrapReadyState()
    {
        try
        {
            var isComplete = BootstrapHelper.IsBootstrapCompleted();
            logger.Debug("UpdateBootstrapReadyState: IsBootstrapCompleted={IsComplete}, IsBootstrapReady={IsReady}", isComplete, IsBootstrapReady);
            
            if (isComplete)
            {
                if (!IsBootstrapReady)
                {
                    IsBootstrapReady = true;
                    // Stop polling once bootstrap is complete
                    bootstrapPollingCancellation?.Cancel();
                    logger.Information("Bootstrap ready - Add button enabled");
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error checking bootstrap status");
        }
    }

    /// <summary>
    /// Checks if schedules are loaded and enables button if so.
    /// Schedules only load after bootstrap is complete, so this is a reliable indicator.
    /// </summary>
    public void CheckSchedulesLoaded(ObservableHashSet<ScheduleListItemViewModel>? schedules)
    {
        // If schedules are loaded, bootstrap must be complete - enable button
        if (schedules != null && schedules.Count > 0 && !IsBootstrapReady)
        {
            IsBootstrapReady = true;
            bootstrapPollingCancellation?.Cancel();
            logger.Information("Schedules loaded - Bootstrap ready, Add button enabled");
        }
        
        // Also check bootstrap status
        UpdateBootstrapReadyState();
    }

    public void Dispose()
    {
        bootstrapPollingCancellation?.Cancel();
        bootstrapPollingCancellation?.Dispose();
    }
}
