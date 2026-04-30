#nullable enable
using System;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Services.UI.NavigationServiceHelpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.ViewModels;
using Bible.Alarm.Views;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
#if WINDOWS
using Bible.Alarm.Platforms.Windows.Helpers;
#endif
#if ANDROID
using Bible.Alarm.Platforms.Android.Services.UI.Interfaces;
#endif

namespace Bible.Alarm.Services.UI;

public sealed class NavigationService(
    IServiceProvider serviceProvider,
    ILogger logger,
    IDispatcher dispatcher)
    : INavigationService, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();

    // Helper classes
    private readonly NavigationInstanceManager navigationManager = new(logger);
    private readonly HomeNavigationHandler homeHandler = new(logger, serviceProvider);
    private readonly ModalNavigationHandler modalHandler = new(logger, serviceProvider);

    // Navigation lock to prevent concurrent page navigation operations (push/pop race conditions)
    private readonly SemaphoreSlim navigationLock = new(1, 1);

    private bool isDisposed;

    private INavigation GetNavigation(bool shouldRetry = true) => navigationManager.GetNavigation(shouldRetry);

    /// <summary>
    /// Runs the given async work on the UI thread. On Windows use the current window's root page Dispatcher so WinUI uses the correct thread; otherwise MainThread.
    /// When already on the UI thread we run work directly to avoid deadlock (dispatch-then-await would wait for our own queued work).
    /// </summary>
    private static async Task InvokeOnUiThreadAsync(Func<Task> work)
    {
#if WINDOWS
        var app = Application.Current;
        var winDispatcher = (app?.Windows.Count > 0 && app.Windows[0].Page is NavigationPage navPage)
            ? navPage.Dispatcher
            : app?.Dispatcher;
        if (winDispatcher != null)
        {
            if (winDispatcher.IsDispatchRequired)
            {
                await winDispatcher.DispatchAsync(work);
            }
            else
            {
                await work();
            }

            return;
        }
#endif
        await MainThread.InvokeOnMainThreadAsync(work);
    }

    /// <summary>
    /// Clears the cached navigation. Call this when the app is disposed or navigation becomes invalid.
    /// </summary>
    public void ClearCache() => navigationManager.ClearCache();

    public async Task NavigateToHomeAsync(bool animated = true)
    {
        // Use lock to prevent race conditions with concurrent navigation (e.g., Cancel then Add quickly)
        await ConcurrencyHelper.ExecuteAsync(navigationLock, async () =>
        {
            var navigation = GetNavigation();
            await homeHandler.NavigateToHomeAsync(navigation, animated);
        });
    }

    /// <summary>
    /// Gets the current Home page from the navigation stack, if available.
    /// </summary>
    public Home? GetCurrentHomePage()
    {
        try
        {
            var navigation = GetNavigation(shouldRetry: false);
            return homeHandler.GetCurrentHomePage(navigation);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the top page of the navigation stack (visible when no modal is shown).
    /// </summary>
    public Page? GetCurrentPage()
    {
        try
        {
            var navigation = GetNavigation(shouldRetry: false);
            return navigation.NavigationStack.Count > 0
                ? navigation.NavigationStack[^1]
                : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task NavigateToScheduleAsync()
    {
        Views.Schedule.Schedule? page = null;
        try
        {
            // Push the lightweight shell (spinner only) immediately — no ViewModel needed yet.
            await ConcurrencyHelper.ExecuteAsync(navigationLock, async () =>
            {
                await InvokeOnUiThreadAsync(async () =>
                {
                    page = serviceProvider.GetRequiredService<Views.Schedule.Schedule>();
                    var navigation = GetNavigation();
                    NavigationPage.SetHasNavigationBar(page, false);
                    await navigation.PushAsync(page, animated: false);
                    WindowSetupService.UpdateNavigationBarColors();
                });
            });

            // Spinner is now visible. Resolve ViewModel on a background thread so it does
            // not block the UI, then hand it to the page to finish loading content.
            if (page != null)
            {
                var viewModel = await Task.Run(() => serviceProvider.GetRequiredService<ScheduleViewModel>());
                await InvokeOnUiThreadAsync(async () => await page.InitializeViewModelAsync(viewModel));
            }
        }
        catch (Exception ex)
        {
#if WINDOWS
            WindowsBootstrapLogger.WriteException(ex);
#endif
            throw new InvalidOperationException(
                AppConstants.Logging.NavigationServiceDiagnosticsLog.NavigateToScheduleAsyncFailed,
                ex);
        }
    }

    public async Task NavigateToScheduleAsync(int scheduleId, bool isEnabled)
    {
#if DEBUG
        var overallStartTime = DateTime.UtcNow;
        logger.Information(AppConstants.Logging.NavigationServiceDiagnosticsLog.PerfNavigateToScheduleAsyncStartAt, overallStartTime);
#endif

        Views.Schedule.Schedule? page = null;
        try
        {
            // Push the lightweight shell (spinner only) immediately — ViewModel resolved after push.
            await ConcurrencyHelper.ExecuteAsync(navigationLock, async () =>
            {
#if DEBUG
                var lockAcquiredTime = DateTime.UtcNow;
                logger.Information(AppConstants.Logging.NavigationServiceDiagnosticsLog.PerfNavigateToScheduleAsyncLockAcquiredInMs,
                    (lockAcquiredTime - overallStartTime).TotalMilliseconds);
#endif

                ScheduleNavigationContext.ScheduleIdToLoad = scheduleId;
                ScheduleNavigationContext.IsEnabledToLoad = isEnabled;
                dispatcher.Dispatch(new ResetContainerReadinessAction());

                await InvokeOnUiThreadAsync(async () =>
                {
#if DEBUG
                    var beforeResolveTime = DateTime.UtcNow;
                    logger.Information(AppConstants.Logging.NavigationServiceDiagnosticsLog.PerfNavigateToScheduleAsyncBeforeShellPageResolveAt, beforeResolveTime);
#endif

                    page = serviceProvider.GetRequiredService<Views.Schedule.Schedule>();

#if DEBUG
                    var afterResolveTime = DateTime.UtcNow;
                    logger.Information(AppConstants.Logging.NavigationServiceDiagnosticsLog.PerfNavigateToScheduleAsyncShellPageResolvedInMs,
                        (afterResolveTime - beforeResolveTime).TotalMilliseconds);
                    var beforePushTime = DateTime.UtcNow;
                    logger.Information(AppConstants.Logging.NavigationServiceDiagnosticsLog.PerfNavigateToScheduleAsyncBeforePushAt, beforePushTime);
#endif

                    var navigation = GetNavigation();
                    NavigationPage.SetHasNavigationBar(page, false);
                    await navigation.PushAsync(page, animated: false);
                    WindowSetupService.UpdateNavigationBarColors();

#if DEBUG
                    var afterPushTime = DateTime.UtcNow;
                    logger.Information(AppConstants.Logging.NavigationServiceDiagnosticsLog.PerfNavigateToScheduleAsyncPushCompletedTotalSoFarMs,
                        (afterPushTime - beforePushTime).TotalMilliseconds,
                        (afterPushTime - overallStartTime).TotalMilliseconds);
#endif
                });
            });

            // Spinner is now visible on screen.
            // Resolve ViewModel on a background thread (doesn't block spinner animation),
            // then hand it to the page to bind and load the heavy ScheduleContent XAML.
            if (page != null)
            {
#if DEBUG
                var beforeVmTime = DateTime.UtcNow;
                logger.Information(AppConstants.Logging.NavigationServiceDiagnosticsLog.PerfNavigateToScheduleAsyncResolvingViewModelAt, beforeVmTime);
#endif
                var viewModel = await Task.Run(() => serviceProvider.GetRequiredService<ScheduleViewModel>());
#if DEBUG
                logger.Information(AppConstants.Logging.NavigationServiceDiagnosticsLog.PerfNavigateToScheduleAsyncViewModelResolvedInMs,
                    (DateTime.UtcNow - beforeVmTime).TotalMilliseconds);
#endif
                await InvokeOnUiThreadAsync(async () => await page.InitializeViewModelAsync(viewModel));
#if DEBUG
                logger.Information(AppConstants.Logging.NavigationServiceDiagnosticsLog.PerfNavigateToScheduleAsyncInitializeViewModelCompleteTotalMs,
                    (DateTime.UtcNow - overallStartTime).TotalMilliseconds);
#endif
            }
        }
        catch (Exception ex)
        {
#if WINDOWS
            WindowsBootstrapLogger.WriteException(ex);
#endif
            throw new InvalidOperationException(
                AppConstants.Logging.NavigationServiceDiagnosticsLog.NavigateToScheduleAsyncWithScheduleIdFailed,
                ex);
        }
    }

    public async Task OpenSongPublicationSelectionModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        await modalHandler.OpenSongPublicationSelectionModalAsync(navigation, bindingContext);
    }

    public async Task OpenMusicTrackSelectionModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        await modalHandler.OpenMusicTrackSelectionModalAsync(navigation, bindingContext);
    }

    public async Task OpenBibleSelectionModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        await modalHandler.OpenBibleSelectionModalAsync(navigation, bindingContext);
    }

    public async Task OpenSectionSelectionModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        await modalHandler.OpenSectionSelectionModalAsync(navigation, bindingContext);
    }

    public async Task OpenMusicSectionSelectionModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        await modalHandler.OpenMusicSectionSelectionModalAsync(navigation, bindingContext);
    }

    public async Task OpenBiblePublicationTrackSelectionModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        await modalHandler.OpenBiblePublicationTrackSelectionModalAsync(navigation, bindingContext);
    }

    public async Task OpenNumberOfTracksModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        await modalHandler.OpenNumberOfTracksModalAsync(navigation, bindingContext);
    }

    public async Task OpenLanguageModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        await modalHandler.OpenLanguageModalAsync(navigation, bindingContext);
    }

    public async Task OpenCategoryModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        await modalHandler.OpenCategoryModalAsync(navigation, bindingContext);
    }

    public async Task OpenPlaybackModalAsync(bool animated = false)
    {
        await ConcurrencyHelper.ExecuteAsync(navigationLock, async () =>
        {
            var navigation = GetNavigation();
            await modalHandler.OpenPlaybackModalAsync(navigation, animated);
        });
    }

    public bool IsPlaybackModalOnScreen()
    {
        try
        {
            var navigation = GetNavigation(shouldRetry: false);
            return ModalNavigationHandler.IsPlaybackModalAlreadyShown(navigation);
        }
        catch (Exception ex)
        {
            logger.Debug(ex, AppConstants.Logging.NavigationServiceDiagnosticsLog.IsPlaybackModalOnScreenNavigationUnavailable);
            return false;
        }
    }

    public async Task OpenBatteryOptimizationModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        await modalHandler.OpenBatteryOptimizationModalAsync(navigation, bindingContext);
    }

    public async Task OpenNotificationPermissionModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        await modalHandler.OpenNotificationPermissionModalAsync(navigation, bindingContext);
    }

    public async Task PopModalAsync()
    {
        await ConcurrencyHelper.ExecuteAsync(navigationLock, async () =>
        {
            var navigation = GetNavigation();
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await NavigationStackManager.PopAsync(navigation);
            });
        });
    }

    public async Task PopAsync()
    {
        await ConcurrencyHelper.ExecuteAsync(navigationLock, async () =>
        {
            var navigation = GetNavigation();
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await NavigationStackManager.PopAsync(navigation);
            });
        });
    }

    private const uint PlaybackModalAnimationDurationMs = 300;

    public async Task PopPlaybackPageAsync(bool animated = false)
    {
        await ConcurrencyHelper.ExecuteAsync(navigationLock, async () =>
        {
            var navigation = GetNavigation();
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var playbackPage = FindPlaybackModalInStack(navigation);
                if (playbackPage == null)
                {
                    return;
                }

                await AnimatePlaybackModalExitIfNeededAsync(playbackPage, animated);
                playbackPage.IsVisible = false;

                await PopOrRemovePlaybackPageAsync(navigation, playbackPage);
                DisposePlaybackModalSafely(playbackPage);
                NotifyPlaybackModalClosedOnPlatforms();
            });
        });
    }

    private static Page? FindPlaybackModalInStack(INavigation navigation)
    {
        var stack = navigation.NavigationStack;
        for (var i = stack.Count - 1; i >= 0; i--)
        {
            var p = stack[i];
            if (p?.GetType() == typeof(Views.General.PlaybackModal))
            {
                return p;
            }
        }

        return null;
    }

    private static async Task AnimatePlaybackModalExitIfNeededAsync(Page playbackPage, bool animated)
    {
        if (!animated)
        {
            return;
        }

        var targetY = playbackPage.Height > 0 ? playbackPage.Height : 2000;
        await playbackPage.TranslateToAsync(0, targetY, PlaybackModalAnimationDurationMs, Easing.CubicIn);
    }

    private static async Task PopOrRemovePlaybackPageAsync(INavigation navigation, Page playbackPage)
    {
        var isTopPage = navigation.NavigationStack is { Count: > 0 } stack && stack[^1] == playbackPage;
        if (isTopPage)
        {
            await navigation.PopAsync(animated: false);
        }
        else
        {
            navigation.RemovePage(playbackPage);
        }
    }

    private void DisposePlaybackModalSafely(Page playbackPage)
    {
        if (playbackPage is IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                logger?.Warning(ex, AppConstants.Logging.NavigationServiceDiagnosticsLog.ErrorDisposingPlaybackModalNonFatal);
            }
        }

#if IOS
        try
        {
            NavigationStackManager.CleanupIOSNativeViews(playbackPage);
        }
        catch (Exception ex)
        {
            logger?.Warning(ex, AppConstants.Logging.NavigationServiceDiagnosticsLog.ErrorCleaningUpIosNativeViewsPlaybackModalNonFatal);
        }
#endif
    }

    private void NotifyPlaybackModalClosedOnPlatforms()
    {
#if ANDROID
        var barHost = serviceProvider.GetService<IAndroidMiniPlaybackBarHost>();
        barHost?.SetPlaybackModalActive(false);
#endif

        WindowSetupService.UpdateNavigationBarColors();
    }

    public void SetMiniBarVisible(bool visible)
    {
        var miniBarVm = serviceProvider.GetService<ViewModels.Shared.MiniPlaybackBarViewModel>();
        if (miniBarVm == null) return;

        if (MainThread.IsMainThread)
        {
            miniBarVm.IsVisible = visible;
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(() => miniBarVm.IsVisible = visible);
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // Cancel and dispose cancellation token source (this will cancel any infinite Polly retries)
        try
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            logger?.Warning(ex, AppConstants.Logging.DisposableLifetimeLog.ErrorDuringCancellationTokenSourceDisposal);
        }

        // Dispose navigation lock
        try
        {
            navigationLock?.Dispose();
        }
        catch (Exception ex)
        {
            logger?.Warning(ex, AppConstants.Logging.NavigationServiceDiagnosticsLog.ErrorDuringNavigationLockDisposal);
        }

        // Clear the navigation cache
        ClearCache();

        // All injected services are singletons, so don't dispose them
        // No event handlers to unsubscribe
    }

    /// <summary>
    /// Disposes all modals and pages from the navigation stack without popping them.
    /// This is used when the window is being destroyed and fragments are already disposed.
    /// Popping would fail, but we can still dispose the page objects directly to clean up ViewModels and event handlers.
    /// </summary>
    public void PopAllModalsAndPages()
    {
        try
        {
            var navigation = GetNavigationSafely();
            if (navigation == null)
            {
                return;
            }

            var modalStack = GetModalStackSafely(navigation);
            var navigationStack = GetNavigationStackSafely(navigation);

            DisposePages(modalStack, "modal");
            DisposePages(navigationStack, "page");

            logger?.Information(AppConstants.Logging.NavigationServiceDiagnosticsLog.PopAllModalsAndPagesFinishedDisposing,
                modalStack.Count, navigationStack.Count);
        }
        catch (Exception ex)
        {
            logger?.Warning(ex, AppConstants.Logging.NavigationServiceDiagnosticsLog.PopAllModalsAndPagesErrorDuringCleanup);
        }
    }

    private INavigation? GetNavigationSafely()
    {
        try
        {
            return GetNavigation();
        }
        catch (Exception ex)
        {
            logger?.Debug(ex, AppConstants.Logging.NavigationServiceDiagnosticsLog.PopAllModalsAndPagesCouldNotGetNavigation);
            return null;
        }
    }

    private List<Page> GetModalStackSafely(INavigation navigation)
    {
        try
        {
            return navigation.ModalStack.ToList();
        }
        catch (Exception ex)
        {
            logger?.Debug(ex, AppConstants.Logging.NavigationServiceDiagnosticsLog.PopAllModalsAndPagesCouldNotAccessModalStack);
            return [];
        }
    }

    private List<Page> GetNavigationStackSafely(INavigation navigation)
    {
        try
        {
            return navigation.NavigationStack.ToList();
        }
        catch (Exception ex)
        {
            logger?.Debug(ex, AppConstants.Logging.NavigationServiceDiagnosticsLog.PopAllModalsAndPagesCouldNotAccessNavigationStack);
            return [];
        }
    }

    private void DisposePages(List<Page> pages, string pageType)
    {
        foreach (var page in pages)
        {
            try
            {
                if (page is IDisposable disposable)
                {
                    disposable.Dispose();
                    logger?.Debug(AppConstants.Logging.NavigationServiceDiagnosticsLog.PopAllModalsAndPagesDisposedPage, pageType, page.GetType().Name);
                }
            }
            catch (Exception ex)
            {
                logger?.Warning(ex, AppConstants.Logging.NavigationServiceDiagnosticsLog.PopAllModalsAndPagesErrorDisposingPage, pageType, page.GetType().Name);
            }

#if IOS
            try
            {
                NavigationStackManager.CleanupIOSNativeViews(page);
            }
            catch (Exception ex)
            {
                logger?.Debug(ex, AppConstants.Logging.NavigationServiceDiagnosticsLog.PopAllModalsAndPagesErrorCleaningUpIosNativeViews, pageType, page.GetType().Name);
            }
#endif
        }
    }
}

