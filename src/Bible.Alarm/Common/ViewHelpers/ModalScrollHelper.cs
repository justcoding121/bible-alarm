#nullable enable
using System.Linq;
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
    /// Android needs more time for rendering, especially with many items.
    /// </summary>
    private static int PostLoadDelayMs => DeviceInfo.Platform == DevicePlatform.Android ? 400 : 150;

    /// <summary>
    /// Delay after scroll to ensure it completes before revealing.
    /// </summary>
    private const int PostScrollDelayMs = 50;

    /// <summary>
    /// Handles the standard modal appearing workflow.
    /// ViewModel defaults IsBusy = true (XAML binding shows overlay immediately).
    /// We set IsBusy = false only after: data is loaded, set to list, and list is rendered (Android).
    /// 1. Hide CollectionView (prevents visible scroll jump)
    /// 2. Refresh data
    /// 3. Delay for list to receive data, scroll to selected item
    /// 4. Wait for CollectionView items to be rendered (Android)
    /// 5. Set IsBusy = false and reveal CollectionView
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

            // ViewModel defaults IsBusy = true; binding shows overlay immediately.
            HideCollectionView(collectionView);
            await Task.Yield();

            if (refreshAction != null)
                await refreshAction();

            // Delay for list to receive data, then scroll
            await Task.Delay(PostLoadDelayMs, cancellationToken);

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

            // Wait for CollectionView items to be rendered (Android) before hiding overlay
            await WaitForCollectionViewItemsRenderedAsync(collectionView, cancellationToken);

            // Set IsBusy = false only after data is in list and rendered
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    dynamic dynamicViewModel = viewModel;
                    dynamicViewModel.IsBusy = false;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "ModalScrollHelper: Failed to set IsBusy to false");
                }
                if (collectionView != null && DeviceInfo.Platform != DevicePlatform.WinUI)
                    collectionView.Opacity = 1;
            });
        }
        catch (OperationCanceledException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    if (viewModel != null)
                    {
                        dynamic dynamicViewModel = viewModel;
                        if ((bool)dynamicViewModel.IsBusy)
                            dynamicViewModel.IsBusy = false;
                    }
                }
                catch
                {
                    ForceHideBusyOverlay(busyOverlay);
                }
            });
            RevealCollectionView(collectionView);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error in ModalScrollHelper.HandleModalAppearingAsync");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    if (viewModel != null)
                    {
                        dynamic dynamicViewModel = viewModel;
                        if ((bool)dynamicViewModel.IsBusy)
                            dynamicViewModel.IsBusy = false;
                    }
                }
                catch
                {
                    ForceHideBusyOverlay(busyOverlay);
                }
            });
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
            // 0. For the Func<bool> overload, we can't set IsBusy directly
            //    We rely on the refreshAction to set IsBusy properly
            //    Don't set IsVisible directly - let the binding handle it

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

            // 4. For Func<bool> overload, we can't directly manage IsBusy
            //    So we rely on the refreshAction to manage it properly
            //    Don't set IsVisible directly - let the binding handle it based on IsBusy

            // 5. Delay for CollectionView to render (still hidden)
            //    Android needs more time, especially with many items
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

            // 7. Wait for CollectionView items to be rendered (especially important on Android)
            //    This ensures items are actually visible before hiding the overlay
            await WaitForCollectionViewItemsRenderedAsync(collectionView, cancellationToken);

            // 8. Hide spinner AND reveal CollectionView together (seamless transition)
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
    /// Waits for CollectionView items to be rendered before hiding the overlay.
    /// This is especially important on Android where rendering can be delayed.
    /// Checks that the CollectionView has items and that they're actually rendered.
    /// </summary>
    private static async Task WaitForCollectionViewItemsRenderedAsync(MauiCollectionView? collectionView, CancellationToken cancellationToken)
    {
        if (collectionView == null) return;

        // On Android, wait a bit longer to ensure items are rendered
        // This prevents the flash of empty list after the busy overlay closes
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            const int maxAttempts = 5;
            const int delayMs = 100;

            for (int i = 0; i < maxAttempts; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var hasItems = await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (collectionView.ItemsSource == null)
                        return false;

                    // Check if ItemsSource has items
                    if (collectionView.ItemsSource is System.Collections.ICollection collection)
                    {
                        return collection.Count > 0;
                    }

                    if (collectionView.ItemsSource is System.Collections.IEnumerable enumerable)
                    {
                        return enumerable.Cast<object>().Any();
                    }

                    return false;
                });

                if (hasItems)
                {
                    // Items exist, give Android a bit more time to render them
                    await Task.Delay(delayMs, cancellationToken);
                    return;
                }

                // Wait before checking again
                await Task.Delay(delayMs, cancellationToken);
            }

            // If we get here, items might not be ready, but we've waited long enough
            // Log a warning but continue to avoid indefinite waiting
            Log.Debug("WaitForCollectionViewItemsRenderedAsync: CollectionView items may not be fully rendered after {MaxAttempts} attempts", maxAttempts);
        }
        else
        {
            // On other platforms, a shorter delay is usually sufficient
            await Task.Delay(50, cancellationToken);
        }
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
    /// Only use this in error cases where we can't set IsBusy.
    /// In normal flow, set IsBusy = false and let the binding handle it.
    /// </summary>
    public static void ForceHideBusyOverlay(View? busyOverlay)
    {
        if (busyOverlay == null) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Only set IsVisible directly as a last resort - this bypasses the binding
            // which can cause issues, but is necessary in error cases
            try
            {
                busyOverlay.IsVisible = false;
                busyOverlay.InputTransparent = true;
            }
            catch
            {
                // If setting fails, ignore - overlay might already be disposed
            }
        });
    }

    /// <summary>
    /// Standard disposal for modal pages with cancellation token.
    /// Also ensures IsBusy is set to false on the view model (when not null) to prevent infinite loops.
    /// </summary>
    /// <param name="viewModel">Any object with IsBusy property (e.g. IListViewModel); cleared via dynamic.</param>
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

        if (viewModel != null)
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        dynamic dynamicViewModel = viewModel;
                        if ((bool)dynamicViewModel.IsBusy)
                            dynamicViewModel.IsBusy = false;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "ModalScrollHelper.DisposeModal: Failed to set IsBusy to false");
                    }
                });
            }
            catch { /* ignore */ }
        }

        clearBindingContext?.Invoke();
    }
}
