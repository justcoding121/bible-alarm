#nullable enable

using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.DataStructures;
using Serilog;

namespace Bible.Alarm.ViewModels.HomeViewModelHelpers;

/// <summary>
/// Manages bootstrap ready state tracking for HomeViewModel.
/// Uses async waiting via BootstrapHelper.WaitForBootstrapAsync instead of polling.
/// </summary>
public class BootstrapReadyManager : IDisposable
{
    private readonly ILogger logger;
    private bool disposed;
    private bool isBootstrapReady;
    private CancellationTokenSource? waitCancellation;

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
                logger.Debug(AppConstants.Logging.BootstrapReadyManagerDiagnosticsLog.IsBootstrapReadyChangedTo, value);
            }
        }
    }

    /// <summary>
    /// Starts tracking bootstrap completion status.
    /// Checks immediately and then waits asynchronously for bootstrap to complete.
    /// </summary>
    public void StartTracking()
    {
        // Check immediately first
        if (BootstrapHelper.IsBootstrapCompleted())
        {
            IsBootstrapReady = true;
            logger.Debug(AppConstants.Logging.BootstrapReadyManagerDiagnosticsLog.BootstrapAlreadyCompleteNoWaitingNeeded);
            return;
        }

        // Use async waiting instead of polling for better efficiency
        waitCancellation = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                // Wait for bootstrap to complete using the existing async mechanism
                // WaitForBootstrapAsync never throws (handles timeout/error internally)
                await BootstrapHelper.WaitForBootstrapAsync();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!IsBootstrapReady)
                    {
                        IsBootstrapReady = true;
                        logger.Information(AppConstants.Logging.BootstrapReadyManagerDiagnosticsLog.BootstrapReadyAddButtonEnabled);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested (Dispose called)
            }
            catch (Exception ex)
            {
                logger.Error(ex, AppConstants.Logging.BootstrapReadyManagerDiagnosticsLog.ErrorWaitingForBootstrapCompletion);
                // Enable button anyway on error
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!IsBootstrapReady)
                    {
                        IsBootstrapReady = true;
                    }
                });
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
            logger.Debug(AppConstants.Logging.BootstrapReadyManagerDiagnosticsLog.UpdateBootstrapReadyStateBootstrapCompletedAndReady, isComplete, IsBootstrapReady);

            if (isComplete && !IsBootstrapReady)
            {
                IsBootstrapReady = true;
                // Stop waiting once bootstrap is complete
                waitCancellation?.Cancel();
                logger.Information(AppConstants.Logging.BootstrapReadyManagerDiagnosticsLog.BootstrapReadyAddButtonEnabled);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.BootstrapReadyManagerDiagnosticsLog.ErrorCheckingBootstrapStatus);
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
            waitCancellation?.Cancel();
            logger.Information(AppConstants.Logging.BootstrapReadyManagerDiagnosticsLog.SchedulesLoadedBootstrapReadyAddButtonEnabled);
        }

        // Also check bootstrap status
        UpdateBootstrapReadyState();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposed || !disposing)
        {
            return;
        }

        waitCancellation?.Cancel();
        waitCancellation?.Dispose();
        waitCancellation = null;
        disposed = true;
    }
}
