#nullable enable
using System.Linq;
using System.Net.Http;
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
    public const string DefaultFetchErrorMessage = "Please check your internet connection.";

    /// <summary>
    /// Handles the standard modal appearing workflow.
    /// ViewModel defaults IsBusy = true (XAML binding shows overlay immediately).
    /// We set IsBusy = false only after: data is loaded AND CollectionView has rendered items.
    /// If fetch fails, calls onFetchFailed callback to close modal and show toast.
    /// NOTE: There is no hard timeout - the modal stays open until fetch completes or fails.
    /// </summary>
    /// <param name="viewModel">The ViewModel with IsBusy property</param>
    /// <param name="busyOverlay">The busy overlay element (unused, kept for API compatibility)</param>
    /// <param name="collectionView">The CollectionView to monitor and scroll</param>
    /// <param name="getSelectedItem">Function to get the selected item (called AFTER refresh to get fresh reference)</param>
    /// <param name="refreshAction">Optional async action to refresh/load data</param>
    /// <param name="onFetchFailed">Callback when fetch fails - should close modal and show toast</param>
    /// <param name="getItemCountFromViewModel">Optional: return list count from ViewModel; used when CollectionView binding is delayed (e.g. reopen modal on Windows).</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success, failure, or cancellation</returns>
    public static async Task<ModalAppearingResult> HandleModalAppearingAsync(
        IListViewModel? viewModel,
        View? busyOverlay,
        MauiCollectionView? collectionView,
        Func<object?>? getSelectedItem,
        Func<Task>? refreshAction = null,
        Func<string, Task>? onFetchFailed = null,
        Func<IListViewModel, int>? getItemCountFromViewModel = null,
        CancellationToken cancellationToken = default)
    {
        if (viewModel == null) return ModalAppearingResult.Success;
        try
        {
            // Ensure spinner is showing
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                viewModel.IsBusy = true;
            });

            // Hide CollectionView during loading/scrolling (prevents visual jump)
            // Skip on Windows - causes issues when handler isn't ready
            if (collectionView != null && DeviceInfo.Platform != DevicePlatform.WinUI)
            {
                await MainThread.InvokeOnMainThreadAsync(() => collectionView.Opacity = 0);
            }

            // Load data - this is where network failures can occur
            if (refreshAction != null)
            {
                try
                {
                    await refreshAction();
                }
                catch (Exception ex) when (IsFetchFailure(ex))
                {
                    Log.Warning(ex, "Fetch failed during modal appearing");
                    if (onFetchFailed != null)
                    {
                        var msg = GetFetchErrorMessage(ex);
                        await onFetchFailed(msg);
                    }
                    return ModalAppearingResult.FetchFailed;
                }

                // Wait for ViewModel to finish (IsBusy = false) so ObservableCollection updates are applied and binding can update (matches Bible section modal behavior).
                await CollectionViewHelper.WaitForNotBusyAsync(() => viewModel.IsBusy, cancellationToken: cancellationToken);
                await Task.Yield();
                // Yield to UI thread so binding/layout can propagate (avoids "No items loaded" on Windows when pre-harvested).
                await MainThread.InvokeOnMainThreadAsync(() => { });
                await Task.Delay(100, cancellationToken);
            }

            // Wait for CollectionView to have items in its ItemsSource
            if (collectionView != null)
            {
                var hasItems = await WaitForItemsInSourceAsync(collectionView, cancellationToken: cancellationToken);
                if (!hasItems && getItemCountFromViewModel != null && viewModel != null && getItemCountFromViewModel(viewModel) > 0)
                {
                    await Task.Delay(500, cancellationToken);
                    hasItems = await WaitForItemsInSourceAsync(collectionView, maxWaitMs: 3000, cancellationToken: cancellationToken);
                }
                if (!hasItems)
                {
                    Log.Warning("No items loaded into CollectionView after refresh");
                    if (onFetchFailed != null)
                    {
                        await onFetchFailed(DefaultFetchErrorMessage);
                    }
                    return ModalAppearingResult.FetchFailed;
                }
            }

            if (collectionView != null)
            {
                await WaitForItemsRenderedAsync(collectionView, cancellationToken);
            }

            var selectedItem = getSelectedItem?.Invoke();
            if (selectedItem != null && collectionView != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(
                    collectionView,
                    selectedItem,
                    animated: false,
                    cancellationToken: cancellationToken);
                await Task.Delay(PostScrollDelayMs, cancellationToken);
            }

            // Reveal list and hide spinner
            var vm = viewModel;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (collectionView != null && DeviceInfo.Platform != DevicePlatform.WinUI)
                    collectionView.Opacity = 1;

                vm.IsBusy = false;
            });

            return ModalAppearingResult.Success;
        }
        catch (OperationCanceledException)
        {
            // User cancelled (e.g., tapped an item) - reveal immediately
            await CleanupOnCancelOrError(viewModel, busyOverlay, collectionView);
            return ModalAppearingResult.Cancelled;
        }
        catch (Exception ex) when (IsFetchFailure(ex))
        {
            Log.Warning(ex, "Fetch failed during modal appearing");
            if (onFetchFailed != null)
            {
                var msg = GetFetchErrorMessage(ex);
                await onFetchFailed(msg);
            }
            return ModalAppearingResult.FetchFailed;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error in ModalScrollHelper.HandleModalAppearingAsync");
            await CleanupOnCancelOrError(viewModel, busyOverlay, collectionView);
            return ModalAppearingResult.Success; // Don't close modal for non-fetch errors
        }
    }

    /// <summary>
    /// Overload for ViewModels that don't implement IListViewModel.
    /// Uses a custom IsBusy getter function.
    /// NOTE: There is no hard timeout - the modal stays open until fetch completes or fails.
    /// </summary>
    public static async Task<ModalAppearingResult> HandleModalAppearingAsync(
        Func<bool> isBusyGetter,
        View? busyOverlay,
        MauiCollectionView? collectionView,
        Func<object?>? getSelectedItem,
        Func<Task>? refreshAction = null,
        Func<string, Task>? onFetchFailed = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Hide CollectionView during loading/scrolling
            if (collectionView != null && DeviceInfo.Platform != DevicePlatform.WinUI)
            {
                await MainThread.InvokeOnMainThreadAsync(() => collectionView.Opacity = 0);
            }

            // Load data - this is where network failures can occur
            if (refreshAction != null)
            {
                try
                {
                    await refreshAction();
                }
                catch (Exception ex) when (IsFetchFailure(ex))
                {
                    Log.Warning(ex, "Fetch failed during modal appearing");
                    if (onFetchFailed != null)
                    {
                        var msg = GetFetchErrorMessage(ex);
                        await onFetchFailed(msg);
                    }
                    return ModalAppearingResult.FetchFailed;
                }
            }

            await CollectionViewHelper.WaitForNotBusyAsync(isBusyGetter, cancellationToken: cancellationToken);

            if (collectionView != null)
            {
                var hasItems = await WaitForItemsInSourceAsync(collectionView, cancellationToken: cancellationToken);
                if (!hasItems)
                {
                    Log.Warning("No items loaded into CollectionView after refresh");
                    if (onFetchFailed != null)
                    {
                        await onFetchFailed(DefaultFetchErrorMessage);
                    }
                    return ModalAppearingResult.FetchFailed;
                }
            }

            if (collectionView != null)
            {
                await WaitForItemsRenderedAsync(collectionView, cancellationToken);
            }

            var selectedItem = getSelectedItem?.Invoke();
            if (selectedItem != null && collectionView != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(
                    collectionView,
                    selectedItem,
                    animated: false,
                    cancellationToken: cancellationToken);
                await Task.Delay(PostScrollDelayMs, cancellationToken);
            }

            // Reveal
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (collectionView != null && DeviceInfo.Platform != DevicePlatform.WinUI)
                    collectionView.Opacity = 1;

                if (busyOverlay != null)
                {
                    busyOverlay.IsVisible = false;
                    busyOverlay.InputTransparent = true;
                }
            });

            return ModalAppearingResult.Success;
        }
        catch (OperationCanceledException)
        {
            ForceHideBusyOverlay(busyOverlay);
            RevealCollectionView(collectionView);
            return ModalAppearingResult.Cancelled;
        }
        catch (Exception ex) when (IsFetchFailure(ex))
        {
            Log.Warning(ex, "Fetch failed during modal appearing");
            if (onFetchFailed != null)
            {
                var msg = GetFetchErrorMessage(ex);
                await onFetchFailed(msg);
            }
            return ModalAppearingResult.FetchFailed;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error in ModalScrollHelper.HandleModalAppearingAsync");
            ForceHideBusyOverlay(busyOverlay);
            RevealCollectionView(collectionView);
            return ModalAppearingResult.Success; // Don't close modal for non-fetch errors
        }
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
                return "The server is temporarily unavailable. Please try again later.";
            if (code == 404)
                return "Content not found. Please try again later.";
            return "Something went wrong. Please try again later.";
        }

        if (ex is TaskCanceledException || ex is TimeoutException)
        {
            return "The connection took too long. Please check your internet connection and try again.";
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
    private static async Task CleanupOnCancelOrError(IListViewModel? viewModel, View? busyOverlay, MauiCollectionView? collectionView)
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
    /// Reveals the CollectionView by setting opacity to 1.
    /// </summary>
    private static void RevealCollectionView(MauiCollectionView? collectionView)
    {
        if (collectionView == null || DeviceInfo.Platform == DevicePlatform.WinUI)
            return;

        MainThread.BeginInvokeOnMainThread(() => collectionView.Opacity = 1);
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
            catch { }
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
            Log.Warning(ex, "Error during modal cancellation token disposal");
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
