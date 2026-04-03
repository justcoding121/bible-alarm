#nullable enable
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Services.UI.NavigationServiceHelpers;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.ViewModels;
using Bible.Alarm.Views;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
#if WINDOWS
using Bible.Alarm.Platforms.Windows.Helpers;
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
    private readonly NavigationStackManager stackManager = new();

    // Navigation lock to prevent concurrent page navigation operations (push/pop race conditions)
    private readonly SemaphoreSlim navigationLock = new(1, 1);

    private bool isDisposed;

    private INavigation GetNavigation(bool shouldRetry = true) => navigationManager.GetNavigation(shouldRetry);

    /// <summary>
    /// Runs the given async work on the UI thread. On Windows use the current window's root page Dispatcher so WinUI uses the correct thread; otherwise MainThread.
    /// When already on the UI thread we run work directly to avoid deadlock (dispatch-then-await would wait for our own queued work).
    /// </summary>
    private async Task InvokeOnUiThreadAsync(Func<Task> work)
    {
#if WINDOWS
        var app = Application.Current;
        Microsoft.Maui.Dispatching.IDispatcher? winDispatcher = null;
        if (app?.Windows.Count > 0 && app.Windows[0].Page is NavigationPage navPage)
            winDispatcher = navPage.Dispatcher;
        else if (app?.Dispatcher != null)
            winDispatcher = app.Dispatcher;
        if (winDispatcher != null)
        {
            if (winDispatcher.IsDispatchRequired)
                await winDispatcher.DispatchAsync(work);
            else
                await work();
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
            logger.Error(ex, "NavigateToScheduleAsync failed");
            throw;
        }
    }

    public async Task NavigateToScheduleAsync(int scheduleId, bool isEnabled)
    {
#if DEBUG
        var overallStartTime = DateTime.UtcNow;
        logger.Information("[PERF] NavigateToScheduleAsync: Start at {StartTime}", overallStartTime);
#endif

        Views.Schedule.Schedule? page = null;
        try
        {
            // Push the lightweight shell (spinner only) immediately — ViewModel resolved after push.
            await ConcurrencyHelper.ExecuteAsync(navigationLock, async () =>
            {
#if DEBUG
                var lockAcquiredTime = DateTime.UtcNow;
                logger.Information("[PERF] NavigateToScheduleAsync: Lock acquired in {ElapsedMs}ms",
                    (lockAcquiredTime - overallStartTime).TotalMilliseconds);
#endif

                ScheduleNavigationContext.ScheduleIdToLoad = scheduleId;
                ScheduleNavigationContext.IsEnabledToLoad = isEnabled;
                dispatcher.Dispatch(new ResetContainerReadinessAction());

                await InvokeOnUiThreadAsync(async () =>
                {
#if DEBUG
                    var beforeResolveTime = DateTime.UtcNow;
                    logger.Information("[PERF] NavigateToScheduleAsync: Before shell page resolve at {Time}", beforeResolveTime);
#endif

                    page = serviceProvider.GetRequiredService<Views.Schedule.Schedule>();

#if DEBUG
                    var afterResolveTime = DateTime.UtcNow;
                    logger.Information("[PERF] NavigateToScheduleAsync: Shell page resolved in {ElapsedMs}ms",
                        (afterResolveTime - beforeResolveTime).TotalMilliseconds);
                    var beforePushTime = DateTime.UtcNow;
                    logger.Information("[PERF] NavigateToScheduleAsync: Before push at {Time}", beforePushTime);
#endif

                    var navigation = GetNavigation();
                    NavigationPage.SetHasNavigationBar(page, false);
                    await navigation.PushAsync(page, animated: false);
                    WindowSetupService.UpdateNavigationBarColors();

#if DEBUG
                    var afterPushTime = DateTime.UtcNow;
                    logger.Information("[PERF] NavigateToScheduleAsync: Push completed in {ElapsedMs}ms, total so far: {TotalMs}ms",
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
                logger.Information("[PERF] NavigateToScheduleAsync: Resolving ViewModel at {Time}", beforeVmTime);
#endif
                var viewModel = await Task.Run(() => serviceProvider.GetRequiredService<ScheduleViewModel>());
#if DEBUG
                logger.Information("[PERF] NavigateToScheduleAsync: ViewModel resolved in {ElapsedMs}ms",
                    (DateTime.UtcNow - beforeVmTime).TotalMilliseconds);
#endif
                await InvokeOnUiThreadAsync(async () => await page.InitializeViewModelAsync(viewModel));
#if DEBUG
                logger.Information("[PERF] NavigateToScheduleAsync: InitializeViewModelAsync complete, total: {TotalMs}ms",
                    (DateTime.UtcNow - overallStartTime).TotalMilliseconds);
#endif
            }
        }
        catch (Exception ex)
        {
#if WINDOWS
            WindowsBootstrapLogger.WriteException(ex);
#endif
            logger.Error(ex, "NavigateToScheduleAsync(scheduleId, isEnabled) failed");
            throw;
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
        var navigation = GetNavigation();
        await modalHandler.OpenPlaybackModalAsync(navigation, animated);
    }

    public bool IsPlaybackModalOnScreen()
    {
        try
        {
            var navigation = GetNavigation(shouldRetry: false);
            return ModalNavigationHandler.IsPlaybackModalAlreadyShown(navigation);
        }
        catch
        {
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
                await stackManager.PopAsync(navigation);
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
                await stackManager.PopAsync(navigation);
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
                var playbackPage = navigation.NavigationStack
                    .LastOrDefault(p => p?.GetType() == typeof(Views.General.PlaybackModal));

                if (playbackPage != null)
                {
                    if (animated)
                    {
                        var targetY = playbackPage.Height > 0 ? playbackPage.Height : 2000;
                        await playbackPage.TranslateToAsync(0, targetY, PlaybackModalAnimationDurationMs, Easing.CubicIn);
                    }
                    else
                    {
                        // Hide before removal so that the BindingContext=null in Dispose() cannot
                        // flash a partially-reset frame (e.g. minimize button reappearing briefly).
                        playbackPage.IsVisible = false;
                    }

                    navigation.RemovePage(playbackPage);

                    if (playbackPage is IDisposable disposable)
                    {
                        try { disposable.Dispose(); }
                        catch (Exception ex) { logger?.Warning(ex, "Error disposing PlaybackModal (non-fatal)"); }
                    }

                    WindowSetupService.UpdateNavigationBarColors();
                }
            });
        });
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

    /// <summary>
    /// Pushes a fresh page instance, then clears all other pages from the stack, leaving only the newly pushed page.
    /// </summary>
    private async Task PushFreshPageAsync<T>(T page, bool hasNavigationBar = true) where T : Page
    {
        var navigation = GetNavigation();

        // Set navigation bar settings
        NavigationPage.SetHasNavigationBar(page, hasNavigationBar);
        // Enable back button when navigation bar is enabled
        NavigationPage.SetHasBackButton(page, hasNavigationBar);

        // Push the fresh page first - keep animation enabled
        await navigation.PushAsync(page, animated: true);
        WindowSetupService.UpdateNavigationBarColors();
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
            logger?.Warning(ex, "Error during cancellation token source disposal");
        }

        // Dispose navigation lock
        try
        {
            navigationLock?.Dispose();
        }
        catch (Exception ex)
        {
            logger?.Warning(ex, "Error during navigation lock disposal");
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

            logger?.Information("NavigationService.PopAllModalsAndPages - Finished disposing modals and pages. Modal count: {ModalCount}, Page count: {PageCount}",
                modalStack.Count, navigationStack.Count);
        }
        catch (Exception ex)
        {
            logger?.Warning(ex, "NavigationService.PopAllModalsAndPages - Error during modal/page cleanup");
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
            logger?.Debug(ex, "NavigationService.PopAllModalsAndPages - Could not get navigation, fragments may be destroyed");
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
            logger?.Debug(ex, "NavigationService.PopAllModalsAndPages - Could not access ModalStack, fragments may be destroyed");
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
            logger?.Debug(ex, "NavigationService.PopAllModalsAndPages - Could not access NavigationStack, fragments may be destroyed");
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
                    logger?.Debug("NavigationService.PopAllModalsAndPages - Disposed {PageType}: {PageTypeName}", pageType, page.GetType().Name);
                }
            }
            catch (Exception ex)
            {
                logger?.Warning(ex, "NavigationService.PopAllModalsAndPages - Error disposing {PageType}: {PageTypeName}", pageType, page.GetType().Name);
            }

#if IOS
            try
            {
                NavigationStackManager.CleanupIOSNativeViews(page);
            }
            catch (Exception ex)
            {
                logger?.Debug(ex, "NavigationService.PopAllModalsAndPages - Error cleaning up iOS native views for {PageType}: {PageTypeName} (non-fatal)", pageType, page.GetType().Name);
            }
#endif
        }
    }
}

