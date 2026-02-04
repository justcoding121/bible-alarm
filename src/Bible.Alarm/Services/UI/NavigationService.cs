#nullable enable
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Services.UI.NavigationServiceHelpers;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Views;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

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
    /// Clears the cached navigation. Call this when the app is disposed or navigation becomes invalid.
    /// </summary>
    public void ClearCache() => navigationManager.ClearCache();

    public async Task NavigateToHomeAsync()
    {
        // Use lock to prevent race conditions with concurrent navigation (e.g., Cancel then Add quickly)
        await ConcurrencyHelper.ExecuteAsync(navigationLock, async () =>
        {
            var navigation = GetNavigation();
            await homeHandler.NavigateToHomeAsync(navigation);
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
    /// Sets Home page visibility based on playback state.
    /// If playback is active, hides Home to prevent visual flash before alarm modal appears.
    /// </summary>
    public void SetHomePageVisibility(bool isPlaybackActive)
    {
        var homePage = GetCurrentHomePage();
        homeHandler.SetHomePageVisibility(homePage, isPlaybackActive);
    }

    public async Task NavigateToScheduleAsync()
    {
        // Use lock to prevent race conditions with concurrent navigation (e.g., Cancel then Add quickly)
        await ConcurrencyHelper.ExecuteAsync(navigationLock, async () =>
        {
            var page = serviceProvider.GetRequiredService<Views.Schedule.Schedule>();
            var navigation = GetNavigation();

            // Set navigation bar setting
            NavigationPage.SetHasNavigationBar(page, false);

            // Push the page without animation for instant navigation
            await navigation.PushAsync(page, animated: false);
        });
    }

    public async Task NavigateToScheduleAsync(int scheduleId, bool isEnabled)
    {
#if DEBUG
        var overallStartTime = DateTime.UtcNow;
        logger.Information("[PERF] NavigateToScheduleAsync: Start at {StartTime}", overallStartTime);
#endif

        // Use lock to prevent race conditions with concurrent navigation
        await ConcurrencyHelper.ExecuteAsync(navigationLock, async () =>
        {
#if DEBUG
            var lockAcquiredTime = DateTime.UtcNow;
            logger.Information("[PERF] NavigateToScheduleAsync: Lock acquired in {ElapsedMs}ms",
                (lockAcquiredTime - overallStartTime).TotalMilliseconds);
#endif

            // Set navigation context BEFORE creating page - ScheduleStateManager will read this
            // This is simpler than Fluxor state which can be cleared by ResetScheduleStateAction
            ScheduleNavigationContext.ScheduleIdToLoad = scheduleId;
            ScheduleNavigationContext.IsEnabledToLoad = isEnabled;

            // Reset container readiness
            dispatcher.Dispatch(new ResetContainerReadinessAction());

#if DEBUG
            var beforeResolveTime = DateTime.UtcNow;
            logger.Information("[PERF] NavigateToScheduleAsync: Before page resolve at {Time}", beforeResolveTime);
#endif

            var page = serviceProvider.GetRequiredService<Views.Schedule.Schedule>();

#if DEBUG
            var afterResolveTime = DateTime.UtcNow;
            logger.Information("[PERF] NavigateToScheduleAsync: Page resolved in {ElapsedMs}ms",
                (afterResolveTime - beforeResolveTime).TotalMilliseconds);
#endif

            var navigation = GetNavigation();

            // Set navigation bar setting
            NavigationPage.SetHasNavigationBar(page, false);

#if DEBUG
            var beforePushTime = DateTime.UtcNow;
            logger.Information("[PERF] NavigateToScheduleAsync: Before push at {Time}", beforePushTime);
#endif

            // Push the page without animation for instant navigation
            await navigation.PushAsync(page, animated: false);

#if DEBUG
            var afterPushTime = DateTime.UtcNow;
            logger.Information("[PERF] NavigateToScheduleAsync: Push completed in {ElapsedMs}ms, total: {TotalMs}ms",
                (afterPushTime - beforePushTime).TotalMilliseconds,
                (afterPushTime - overallStartTime).TotalMilliseconds);
#endif
        });
    }

    public async Task OpenMusicSelectionModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        await modalHandler.OpenMusicSelectionModalAsync(navigation, bindingContext);
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

    public async Task OpenPlaybackModalAsync()
    {
        await OpenPlaybackModalAsync(revealHomeBehindModalOnLoad: true);
    }

    public async Task OpenPlaybackModalAsync(bool revealHomeBehindModalOnLoad)
    {
        var navigation = GetNavigation();
        await modalHandler.OpenPlaybackModalAsync(navigation, revealHomeBehindModalOnLoad);
    }

    public async Task OpenBatteryOptimizationModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        await modalHandler.OpenBatteryOptimizationModalAsync(navigation, bindingContext);
    }

    public async Task PopModalAsync()
    {
        await ConcurrencyHelper.ExecuteAsync(navigationLock, async () =>
        {
            var navigation = GetNavigation();
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await stackManager.PopModalAsync(navigation);
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
        }
    }
}

