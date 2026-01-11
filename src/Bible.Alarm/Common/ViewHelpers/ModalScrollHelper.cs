#nullable enable
using Bible.Alarm.ViewModels.Interfaces;
using Serilog;
using MauiCollectionView = Microsoft.Maui.Controls.CollectionView;

namespace Bible.Alarm.Common.ViewHelpers;

/// <summary>
/// Helper class for common modal scroll-to-selected behavior.
/// Reduces duplicate code across modal pages by providing a standard pattern for:
/// - Waiting for data to load
/// - Hiding busy overlay
/// - Scrolling to selected item (hidden until positioned to avoid visual jump)
/// </summary>
public static class ModalScrollHelper
{
    /// <summary>
    /// Standard delay after waiting for data to load, before scrolling.
    /// This allows the CollectionView to render its items.
    /// </summary>
    private const int PostLoadDelayMs = 150;

    /// <summary>
    /// Delay after scroll to ensure it completes before revealing.
    /// </summary>
    private const int PostScrollDelayMs = 50;

    /// <summary>
    /// Handles the standard modal appearing workflow:
    /// 1. Hides CollectionView (prevents visible scroll jump)
    /// 2. Optionally refreshes data from state
    /// 3. Waits for IsBusy to become false
    /// 4. Scrolls to selected item (retrieved AFTER data refresh)
    /// 5. Hides spinner AND reveals CollectionView together (seamless transition)
    /// </summary>
    /// <param name="viewModel">The ViewModel with IsBusy property</param>
    /// <param name="busyOverlay">The busy overlay element to hide</param>
    /// <param name="collectionView">The CollectionView to scroll</param>
    /// <param name="getSelectedItem">Function to get the selected item (called AFTER refresh to get fresh reference)</param>
    /// <param name="refreshAction">Optional async action to refresh data before scrolling</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static async Task HandleModalAppearingAsync(
        IListViewModel? viewModel,
        View? busyOverlay,
        MauiCollectionView? collectionView,
        Func<object?>? getSelectedItem,
        Func<Task>? refreshAction = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (viewModel == null) return;

            // Yield immediately to let spinner start animating
            await Task.Yield();

            // 1. Hide CollectionView to prevent visible scroll jump
            //    Keep spinner visible during this phase
            HideCollectionView(collectionView);

            // Another yield before starting data load
            await Task.Yield();

            // 2. Refresh data if needed
            if (refreshAction != null)
            {
                await refreshAction();
            }

            // 3. Wait for IsBusy to become false
            await CollectionViewHelper.WaitForNotBusyAsync(
                () => viewModel.IsBusy,
                cancellationToken: cancellationToken);

            // 4. Keep spinner visible while we scroll (override binding)
            //    Use InvokeOnMainThreadAsync to ensure this runs synchronously
            //    before continuing, preventing the flash of empty list
            await KeepBusyOverlayVisibleAsync(busyOverlay);

            // 5. Delay for CollectionView to render (still hidden)
            await Task.Delay(PostLoadDelayMs, cancellationToken);

            // 6. Get selected item NOW (after data is loaded) and scroll
            var selectedItem = getSelectedItem?.Invoke();
            if (selectedItem != null && collectionView != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(
                    collectionView,
                    selectedItem,
                    animated: false,
                    cancellationToken: cancellationToken);

                // Small delay to ensure scroll completes
                await Task.Delay(PostScrollDelayMs, cancellationToken);
            }

            // 7. Hide spinner AND reveal CollectionView together (seamless transition)
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (busyOverlay != null)
                {
                    busyOverlay.IsVisible = false;
                    busyOverlay.InputTransparent = true;
                }
                // Only set Opacity on non-Windows (we didn't hide it there)
                if (collectionView != null && DeviceInfo.Platform != DevicePlatform.WinUI)
                {
                    collectionView.Opacity = 1;
                }
            });
        }
        catch (OperationCanceledException)
        {
            // User tapped an item - reveal immediately so their selection is visible
            ForceHideBusyOverlay(busyOverlay);
            RevealCollectionView(collectionView);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error in ModalScrollHelper.HandleModalAppearingAsync");
            ForceHideBusyOverlay(busyOverlay);
            RevealCollectionView(collectionView);
        }
    }

    /// <summary>
    /// Same as HandleModalAppearingAsync but for ViewModels that don't implement IListViewModel.
    /// Uses a custom IsBusy getter function.
    /// </summary>
    public static async Task HandleModalAppearingAsync(
        Func<bool> isBusyGetter,
        View? busyOverlay,
        MauiCollectionView? collectionView,
        Func<object?>? getSelectedItem,
        Func<Task>? refreshAction = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Yield immediately to let spinner start animating
            await Task.Yield();

            // 1. Hide CollectionView to prevent visible scroll jump
            //    Keep spinner visible during this phase
            HideCollectionView(collectionView);

            // Another yield before starting data load
            await Task.Yield();

            // 2. Refresh data if needed
            if (refreshAction != null)
            {
                await refreshAction();
            }

            // 3. Wait for IsBusy to become false
            await CollectionViewHelper.WaitForNotBusyAsync(
                isBusyGetter,
                cancellationToken: cancellationToken);

            // 4. Keep spinner visible while we scroll (override binding)
            //    Use InvokeOnMainThreadAsync to ensure this runs synchronously
            await KeepBusyOverlayVisibleAsync(busyOverlay);

            // 5. Delay for CollectionView to render (still hidden)
            await Task.Delay(PostLoadDelayMs, cancellationToken);

            // 6. Get selected item NOW (after data is loaded) and scroll
            var selectedItem = getSelectedItem?.Invoke();
            if (selectedItem != null && collectionView != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(
                    collectionView,
                    selectedItem,
                    animated: false,
                    cancellationToken: cancellationToken);

                // Small delay to ensure scroll completes
                await Task.Delay(PostScrollDelayMs, cancellationToken);
            }

            // 7. Hide spinner AND reveal CollectionView together (seamless transition)
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (busyOverlay != null)
                {
                    busyOverlay.IsVisible = false;
                    busyOverlay.InputTransparent = true;
                }
                // Only set Opacity on non-Windows (we didn't hide it there)
                if (collectionView != null && DeviceInfo.Platform != DevicePlatform.WinUI)
                {
                    collectionView.Opacity = 1;
                }
            });
        }
        catch (OperationCanceledException)
        {
            // User tapped an item - reveal immediately so their selection is visible
            ForceHideBusyOverlay(busyOverlay);
            RevealCollectionView(collectionView);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error in ModalScrollHelper.HandleModalAppearingAsync");
            ForceHideBusyOverlay(busyOverlay);
            RevealCollectionView(collectionView);
        }
    }

    /// <summary>
    /// Keeps the busy overlay visible by explicitly setting IsVisible = true.
    /// This overrides any binding that might have hidden it when IsBusy became false.
    /// Uses async to ensure the UI update completes before continuing.
    /// </summary>
    private static async Task KeepBusyOverlayVisibleAsync(View? busyOverlay)
    {
        if (busyOverlay == null) return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            busyOverlay.IsVisible = true;
            busyOverlay.InputTransparent = false;
        });
    }

    /// <summary>
    /// Hides the CollectionView by setting opacity to 0.
    /// This allows layout and scrolling to happen invisibly.
    /// Note: Skipped on Windows to avoid access violation when CollectionView handler isn't ready.
    /// </summary>
    private static void HideCollectionView(MauiCollectionView? collectionView)
    {
        if (collectionView == null) return;

        // Skip hiding on Windows - causes access violation crash when CollectionView handler isn't ready
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            collectionView.Opacity = 0;
        });
    }

    /// <summary>
    /// Reveals the CollectionView by setting opacity to 1.
    /// Called after scrolling is complete so the list appears in the correct position.
    /// Note: Skipped on Windows since we don't hide it there.
    /// </summary>
    private static void RevealCollectionView(MauiCollectionView? collectionView)
    {
        if (collectionView == null) return;

        // Skip revealing on Windows - we didn't hide it there
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            collectionView.Opacity = 1;
        });
    }

    /// <summary>
    /// Forces the busy overlay to hide immediately.
    /// </summary>
    public static void ForceHideBusyOverlay(View? busyOverlay)
    {
        if (busyOverlay == null) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            busyOverlay.IsVisible = false;
            busyOverlay.InputTransparent = true;
        });
    }

    /// <summary>
    /// Standard disposal for modal pages with cancellation token.
    /// </summary>
    public static void DisposeModal(
        CancellationTokenSource? cancellationTokenSource,
        Action? clearBindingContext = null)
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

        clearBindingContext?.Invoke();
    }
}
