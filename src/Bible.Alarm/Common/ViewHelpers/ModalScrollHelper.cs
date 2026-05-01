#nullable enable
using System.Linq;
using System.Net.Http;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.ViewModels.Interfaces;
using Serilog;
using MauiCollectionView = Microsoft.Maui.Controls.CollectionView;

namespace Bible.Alarm.Common.ViewHelpers;

/// <summary>
/// Result of a modal appearing operation.
/// </summary>
public enum ModalAppearingResult
{
    /// <summary>Modal loaded successfully with items.</summary>
    Success,
    /// <summary>Fetch failed due to network or other error.</summary>
    FetchFailed,
    /// <summary>User cancelled the operation (e.g., tapped an item).</summary>
    Cancelled
}

/// <summary>
/// Inputs for modal list appearing when the page ViewModel implements <see cref="IListViewModel"/>.
/// </summary>
public sealed record ListModalAppearOptions(
    View? BusyOverlay,
    MauiCollectionView? CollectionView,
    Func<object?>? GetSelectedItem = null,
    Func<Task>? RefreshAction = null,
    Func<string, Task>? OnFetchFailed = null,
    Func<IListViewModel, int>? GetItemCountFromViewModel = null,
    CancellationToken CancellationToken = default);

/// <summary>
/// Helper class for common modal scroll-to-selected behavior.
/// Provides a standard pattern for:
/// - Loading data with spinner visible
/// - Waiting for CollectionView to render items (event-driven, not delay-based)
/// - Scrolling to selected item
/// - Hiding spinner only after items are visually rendered
/// - Handling fetch failures gracefully
/// </summary>
public static class ModalScrollHelper
{
    /// <summary>
    /// Small delay after scroll to ensure it completes.
    /// </summary>
    private const int PostScrollDelayMs = 50;

    /// <summary>
    /// Maximum time to wait for items to render before giving up.
    /// </summary>
    private const int MaxRenderWaitMs = 10000;

    /// <summary>
    /// Default error message for fetch failures.
    /// </summary>
    public const string DefaultFetchErrorMessage = AppConstants.ToastMessages.PleaseCheckInternetConnection;

    /// <summary>
    /// Handles the standard modal appearing workflow.
    /// ViewModel defaults IsBusy = true (XAML binding shows overlay immediately).
    /// We set IsBusy = false only after: data is loaded AND CollectionView has rendered items.
    /// If fetch fails, calls onFetchFailed callback to close modal and show toast.
    /// NOTE: There is no hard timeout - the modal stays open until fetch completes or fails.
    /// </summary>
    /// <param name="viewModel">Host list ViewModel implementing <see cref="IListViewModel"/>.</param>
    /// <param name="options">Busy overlay reference (unused; kept for call-site clarity), CollectionView and workflow callbacks.</param>
    /// <returns>Result indicating success, failure, or cancellation</returns>
    public static async Task<ModalAppearingResult> HandleModalAppearingAsync(
        IListViewModel? viewModel,
        ListModalAppearOptions options)
    {
        if (viewModel == null) return ModalAppearingResult.Success;

        try
        {
            await EnsureBusyShowingAndHideCollectionOpacityAsync(viewModel, options.CollectionView);

            var refreshAbort = await RunOptionalRefreshForListModalAsync(options.RefreshAction, options.OnFetchFailed, options.CancellationToken);
            if (refreshAbort != null)
                return refreshAbort.Value;

            if (options.CollectionView is { } cv)
            {
                var missingItemsAbort = await RequireItemsOrFailListModalAsync(
                    viewModel, cv, options.GetItemCountFromViewModel, options.OnFetchFailed, options.CancellationToken);
                if (missingItemsAbort != null)
                    return missingItemsAbort.Value;

                await WaitForItemsRenderedAsync(cv, options.CancellationToken);
            }

            await ScrollSelectedIntoViewAsync(options.CollectionView, options.GetSelectedItem, options.CancellationToken);

            await RevealListAndSetBusyAsync(viewModel, options.CollectionView);

            return ModalAppearingResult.Success;
        }
        catch (OperationCanceledException)
        {
            await CleanupOnCancelOrError(viewModel, options.CollectionView);
            return ModalAppearingResult.Cancelled;
        }
        catch (Exception ex) when (IsFetchFailure(ex))
        {
            Log.Warning(ex, AppConstants.Logging.ProcessDiagnosticsLog.FetchFailedDuringModalAppearing);
            if (options.OnFetchFailed != null)
            {
                var msg = GetFetchErrorMessage(ex);
                await options.OnFetchFailed(msg);
            }
            return ModalAppearingResult.FetchFailed;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, AppConstants.Logging.ModalUiDiagnosticsLog.HandleModalAppearingAsyncError);
            await CleanupOnCancelOrError(viewModel, options.CollectionView);
            return ModalAppearingResult.Success; // Don't close modal for non-fetch errors
        }
    }

    private static async Task EnsureBusyShowingAndHideCollectionOpacityAsync(IListViewModel viewModel, MauiCollectionView? collectionView)
    {
        await MainThread.InvokeOnMainThreadAsync(() => { viewModel.IsBusy = true; });

        if (collectionView != null && DeviceInfo.Platform != DevicePlatform.WinUI)
            await MainThread.InvokeOnMainThreadAsync(() => collectionView.Opacity = 0);
    }

    private static async Task<ModalAppearingResult?> RunOptionalRefreshForListModalAsync(
        Func<Task>? refreshAction, Func<string, Task>? onFetchFailed, CancellationToken cancellationToken)
    {
        if (refreshAction == null)
            return null;

        try
        {
            await refreshAction();
        }
        catch (Exception ex) when (IsFetchFailure(ex))
        {
            Log.Warning(ex, AppConstants.Logging.ProcessDiagnosticsLog.FetchFailedDuringModalAppearing);
            if (onFetchFailed != null)
            {
                var msg = GetFetchErrorMessage(ex);
                await onFetchFailed(msg);
            }
            return ModalAppearingResult.FetchFailed;
        }

        await Task.Yield();
        await MainThread.InvokeOnMainThreadAsync(() => { });
        await Task.Delay(100, cancellationToken);
        return null;
    }

    private static async Task<ModalAppearingResult?> RequireItemsOrFailListModalAsync(
        IListViewModel viewModel,
        MauiCollectionView collectionView,
        Func<IListViewModel, int>? getItemCountFromViewModel,
        Func<string, Task>? onFetchFailed,
        CancellationToken cancellationToken)
    {
        var hasItems = await WaitForItemsInSourceAsync(collectionView, cancellationToken: cancellationToken);
        var viewModelItemCountForRetry = 0;
        if (!hasItems && getItemCountFromViewModel != null)
        {
            viewModelItemCountForRetry = await MainThread.InvokeOnMainThreadAsync(() => getItemCountFromViewModel(viewModel));
        }

        if (!hasItems && viewModelItemCountForRetry > 0)
        {
            await Task.Delay(500, cancellationToken);
            hasItems = await WaitForItemsInSourceAsync(collectionView, maxWaitMs: 3000, cancellationToken: cancellationToken);
        }

        if (hasItems)
            return null;

        var viewModelItemCount = viewModelItemCountForRetry;
        if (viewModelItemCount == 0 && getItemCountFromViewModel != null)
        {
            viewModelItemCount = await MainThread.InvokeOnMainThreadAsync(() => getItemCountFromViewModel(viewModel));
        }

        if (viewModelItemCount <= 0)
        {
            Log.Warning(AppConstants.Logging.ModalUiDiagnosticsLog.NoItemsLoadedIntoCollectionViewAfterRefresh);
            if (onFetchFailed != null)
                await onFetchFailed(DefaultFetchErrorMessage);
            return ModalAppearingResult.FetchFailed;
        }

        return null;
    }

    private static async Task ScrollSelectedIntoViewAsync(
        MauiCollectionView? collectionView, Func<object?>? getSelectedItem, CancellationToken cancellationToken)
    {
        var selectedItem = getSelectedItem?.Invoke();
        if (selectedItem == null || collectionView == null)
            return;

        await CollectionViewHelper.ScrollToWhenReadyAsync(collectionView, selectedItem, animated: false, cancellationToken: cancellationToken);
        await Task.Delay(PostScrollDelayMs, cancellationToken);
    }

    private static Task RevealListAndSetBusyAsync(IListViewModel viewModel, MauiCollectionView? collectionView)
    {
        var vmRef = viewModel;
        var cvRef = collectionView;

        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (cvRef != null && DeviceInfo.Platform != DevicePlatform.WinUI)
                cvRef.Opacity = 1;
            vmRef.IsBusy = false;
        });
    }

    /// <summary>
    /// Determines if an exception represents a fetch/network failure.
    /// </summary>
    public static bool IsFetchFailure(Exception ex)
    {
        return ex is HttpRequestException
            || ex is TaskCanceledException { InnerException: TimeoutException }
            || ex is TimeoutException
            || ex is System.Net.WebException
            || ex is System.Net.Sockets.SocketException
            || (ex.InnerException != null && IsFetchFailure(ex.InnerException));
    }

    /// <summary>
    /// Gets a user-friendly error message for a fetch failure.
    /// </summary>
    public static string GetFetchErrorMessage(Exception ex)
    {
        if (ex is HttpRequestException httpEx && httpEx.StatusCode.HasValue)
        {
            var code = (int)httpEx.StatusCode;
            if (code >= 500)
                return "The server is temporarily unavailable, please try again later";
            if (code == 404)
                return "Content not found, please try again later";
            return "Something went wrong, please try again later";
        }

        if (ex is TaskCanceledException || ex is TimeoutException)
        {
            return "The connection took too long, please check your internet connection";
        }

        return DefaultFetchErrorMessage;
    }

    /// <summary>
    /// Waits for the CollectionView's ItemsSource to contain items.
    /// Returns true if items were found, false if timeout occurred.
    /// </summary>
    private static async Task<bool> WaitForItemsInSourceAsync(MauiCollectionView collectionView, int maxWaitMs = -1, CancellationToken cancellationToken = default)
    {
        var timeout = maxWaitMs >= 0 ? maxWaitMs : MaxRenderWaitMs;
        var startTime = Environment.TickCount;

        while (Environment.TickCount - startTime < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var hasItems = await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (collectionView.ItemsSource == null)
                    return false;

                if (collectionView.ItemsSource is System.Collections.ICollection collection)
                    return collection.Count > 0;

                if (collectionView.ItemsSource is System.Collections.IEnumerable enumerable)
                    return enumerable.Cast<object>().Any();

                return false;
            });

            if (hasItems)
                return true;

            await Task.Delay(50, cancellationToken);
        }

        return false;
    }

    /// <summary>
    /// Waits for CollectionView to actually render items.
    /// Uses SizeChanged event and verifies the view has positive dimensions.
    /// </summary>
    private static async Task WaitForItemsRenderedAsync(MauiCollectionView collectionView, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();
        var startTime = Environment.TickCount;

        // Register cancellation
        using var registration = cancellationToken.Register(() => tcs.TrySetCanceled());

        EventHandler? sizeChangedHandler = null;

        sizeChangedHandler = (sender, args) =>
        {
            // Check if items are actually ready
            var isReady = collectionView.Height > 0 && 
                          collectionView.Handler != null &&
                          collectionView.ItemsSource is System.Collections.ICollection col && 
                          col.Count > 0;

            if (isReady)
            {
                collectionView.SizeChanged -= sizeChangedHandler;
                tcs.TrySetResult(true);
            }
        };

        // Subscribe to SizeChanged
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            collectionView.SizeChanged += sizeChangedHandler;
        });

        // Immediate check in case layout already happened
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            sizeChangedHandler(collectionView, EventArgs.Empty);
        });

        // If not yet complete, poll periodically as fallback
        var pollTask = Task.Run(async () =>
        {
            while (!tcs.Task.IsCompleted && Environment.TickCount - startTime < MaxRenderWaitMs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(100, cancellationToken);

                var isReady = await MainThread.InvokeOnMainThreadAsync(() =>
                    collectionView.Height > 0 && 
                    collectionView.Handler != null &&
                    collectionView.ItemsSource is System.Collections.ICollection col && 
                    col.Count > 0);

                if (isReady)
                {
                    tcs.TrySetResult(true);
                    break;
                }
            }
        }, cancellationToken);

        // Set up a timeout
        var timeoutTask = Task.Delay(MaxRenderWaitMs, cancellationToken);
        await Task.WhenAny(tcs.Task, timeoutTask, pollTask);

        // Cleanup handler
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            collectionView.SizeChanged -= sizeChangedHandler;
        });

        if (!tcs.Task.IsCompleted)
        {
            // Don't throw - just continue. Better to show potentially empty list than hang forever.
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Cleanup helper for cancellation or error scenarios.
    /// </summary>
    private static async Task CleanupOnCancelOrError(IListViewModel? viewModel, MauiCollectionView? collectionView)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            // Reveal list
            if (collectionView != null && DeviceInfo.Platform != DevicePlatform.WinUI)
                collectionView.Opacity = 1;

            // Always hide spinner on cancellation/error, regardless of current state
            // This ensures overlay closes even if cancellation happens before items render
            if (viewModel != null)
                viewModel.IsBusy = false;
        });
    }

    /// <summary>
    /// Forces the busy overlay to hide immediately.
    /// Only use in error cases.
    /// </summary>
    public static void ForceHideBusyOverlay(View? busyOverlay)
    {
        if (busyOverlay == null) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                busyOverlay.IsVisible = false;
                busyOverlay.InputTransparent = true;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex, "ModalScrollHelper: could not force-hide busy overlay");
            }
        });
    }

    /// <summary>
    /// Standard disposal for modal pages with cancellation token.
    /// Also ensures IsBusy is set to false on the view model to prevent stuck spinner.
    /// </summary>
    public static void DisposeModal(
        CancellationTokenSource? cancellationTokenSource,
        Action? clearBindingContext = null,
        object? viewModel = null)
    {
        try
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, AppConstants.Logging.ModalUiDiagnosticsLog.ModalCancellationTokenDisposalWarning);
        }

        if (viewModel is IListViewModel listViewModel)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (listViewModel.IsBusy)
                    listViewModel.IsBusy = false;
            });
        }

        clearBindingContext?.Invoke();
    }
}
