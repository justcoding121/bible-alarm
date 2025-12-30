#nullable enable
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.ViewModels.Bible;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.Views;
using Bible.Alarm.Views.Bible;
using Bible.Alarm.Views.General;
using Bible.Alarm.Views.Music;
using Bible.Alarm.Views.Schedule;
using Bible.Alarm.Views.Shared;
using Polly;
using Polly.Retry;
using Serilog;

namespace Bible.Alarm.Services.UI;

public sealed class NavigationService(
    IServiceProvider serviceProvider,
    ILogger logger)
    : INavigationService, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();

    // Cached navigation instance to avoid retries on every call
    private INavigation? cachedNavigation;

    private bool isDisposed;

    // Helper method to create navigation retry policy
    private RetryPolicy CreateNavigationRetryPolicy() => Policy
        .Handle<InvalidOperationException>()
        .WaitAndRetry(
            retryCount: int.MaxValue,
            sleepDurationProvider: _ => TimeSpan.FromMilliseconds(200),
            onRetry: (exception, timeSpan, retryCount, _) =>
            {
                logger?.Debug($"Navigation not available yet, retrying in {timeSpan.TotalMilliseconds}ms (attempt {retryCount}). Error: {exception.Message}");
            });


    private INavigation GetNavigation(bool shouldRetry = true)
    {
        if (cachedNavigation is not null)
        {
            return cachedNavigation;
        }

        if (!shouldRetry)
        {
            // Try once without retry - throw immediately if navigation is not available
            return NavigationFinder();
        }

        var retryPolicy = CreateNavigationRetryPolicy();

        var retryResponse = retryPolicy.ExecuteAndCapture(() => NavigationFinder());

        return retryResponse.Result;
    }

    private INavigation NavigationFinder()
    {
        // If not in DI or GetService returned null, get it directly from the current application window
        var app = Application.Current;
        if (app is null)
        {
            var errorMsg = "Application.Current is null. Cannot get INavigation.";
            logger?.Error(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        logger?.Debug($"Application.Current found. Windows count: {app.Windows.Count}");

        // Try to get navigation from windows
        if (app.Windows.Count > 0)
        {
            var window = app.Windows[0];
            logger?.Debug($"Window found. Page type: {window?.Page?.GetType().Name ?? "null"}");

            // Check if window.Page is NavigationPage
            if (window?.Page is NavigationPage navPage)
            {
                logger?.Debug("Found NavigationPage in window.Page");
                cachedNavigation = navPage.Navigation;
                return cachedNavigation;
            }
        }
        else
        {
            logger?.Warning("Application.Current.Windows.Count is 0 - window may not be initialized yet");
        }

        var mainPageType = app.Windows.Count > 0 ? app.Windows[0].Page?.GetType().Name ?? "null" : "null (no windows)";
        var finalErrorMsg = $"INavigation is not available. Application.Current.Windows.Count={app.Windows.Count}, MainPage type={mainPageType}";
        logger?.Error(finalErrorMsg);
        throw new InvalidOperationException(finalErrorMsg);
    }

    /// <summary>
    /// Clears the cached navigation. Call this when the app is disposed or navigation becomes invalid.
    /// </summary>
    public void ClearCache()
    {
        cachedNavigation = null;
        logger?.Debug("Navigation cache cleared");
    }

    public async Task NavigateToHomeAsync()
    {
#if DEBUG
        var navStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
        logger.Information("[BOOTSTRAP] NavigateToHomeAsync starting");
#endif

        var navigation = GetNavigation();

        if (IsAlreadyOnHomePage(navigation))
        {
#if DEBUG
            var navElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - navStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            logger.Information("[BOOTSTRAP] NavigateToHomeAsync completed (already on home) in {ElapsedMs:F2}ms", navElapsed);
#endif
            return;
        }

        var existingHome = FindExistingHomeInStack(navigation);
        if (existingHome != null)
        {
            await PopToExistingHomeAsync(navigation, existingHome);
#if DEBUG
            var navElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - navStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            logger.Information("[BOOTSTRAP] NavigateToHomeAsync completed (existing home) in {ElapsedMs:F2}ms", navElapsed);
#endif
        }
        else
        {
            await PopToRootAndPushNewHomeAsync(navigation);
#if DEBUG
            var navTotalElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - navStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            logger.Information("[BOOTSTRAP] NavigateToHomeAsync completed (new home) in {ElapsedMs:F2}ms", navTotalElapsed);
#endif
        }
    }

    private static bool IsAlreadyOnHomePage(INavigation navigation)
    {
        return navigation.NavigationStack.Count > 0 &&
               navigation.NavigationStack.LastOrDefault() is Home;
    }

    private static Home? FindExistingHomeInStack(INavigation navigation)
    {
        for (int i = navigation.NavigationStack.Count - 1; i >= 0; i--)
        {
            if (navigation.NavigationStack[i] is Home home)
            {
                return home;
            }
        }
        return null;
    }

    private async Task PopToExistingHomeAsync(INavigation navigation, Home existingHome)
    {
        // Home exists in stack - pop all pages until we reach Home (root)
        while (navigation.NavigationStack.Count > 1 && navigation.NavigationStack.LastOrDefault() != existingHome)
        {
            var page = navigation.NavigationStack.LastOrDefault();
            await navigation.PopAsync(animated: true);
            if (page != existingHome && page is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private async Task PopToRootAndPushNewHomeAsync(INavigation navigation)
    {
        // Home doesn't exist - pop all pages to root, then push new Home
#if DEBUG
        var popStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        await PopAllPagesToRootAsync(navigation);
#if DEBUG
        var popElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - popStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        logger.Information("[BOOTSTRAP] PopAllPagesToRootAsync completed in {ElapsedMs:F2}ms", popElapsed);
#endif

#if DEBUG
        var homeCreateStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        var homePage = serviceProvider.GetRequiredService<Home>();
#if DEBUG
        var homeCreateElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - homeCreateStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        logger.Information("[BOOTSTRAP] Home page service resolution completed in {ElapsedMs:F2}ms", homeCreateElapsed);
#endif

        ConfigureHomePageNavigation(homePage);

#if DEBUG
        var pushStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        await navigation.PushAsync(homePage, animated: false);
#if DEBUG
        var pushElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - pushStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        logger.Information("[BOOTSTRAP] Home page PushAsync completed in {ElapsedMs:F2}ms", pushElapsed);
#endif
    }

    private async Task PopAllPagesToRootAsync(INavigation navigation)
    {
        while (navigation.NavigationStack.Count > 1)
        {
            var page = navigation.NavigationStack.LastOrDefault();
            await navigation.PopAsync(animated: true);
            if (page is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private static void ConfigureHomePageNavigation(Home homePage)
    {
        NavigationPage.SetHasBackButton(homePage, false);
        NavigationPage.SetHasNavigationBar(homePage, false);
    }

    /// <summary>
    /// Gets the current Home page from the navigation stack, if available.
    /// </summary>
    public Home? GetCurrentHomePage()
    {
        try
        {
            var navigation = GetNavigation(shouldRetry: false);
            return navigation.NavigationStack.LastOrDefault() as Home;
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
        if (homePage == null)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            // If playback is active, hide Home page to prevent flash before modal appears
            // Home will be shown when modal appears or playback stops
            homePage.Opacity = isPlaybackActive ? 0.0 : 1.0;
        });
    }

    public async Task NavigateToScheduleAsync()
    {
#if DEBUG
        var startTime = DateTime.UtcNow;
        logger.Information("[PERF] NavigateToScheduleAsync: Starting navigation at {StartTime}", startTime);

        var pageStartTime = DateTime.UtcNow;
#endif
        var page = serviceProvider.GetRequiredService<Schedule>();
#if DEBUG
        var pageElapsed = (DateTime.UtcNow - pageStartTime).TotalMilliseconds;
        logger.Information("[PERF] NavigateToScheduleAsync: Page service resolution took {ElapsedMs}ms", pageElapsed);

        var navStartTime = DateTime.UtcNow;
#endif
        var navigation = GetNavigation();
#if DEBUG
        var navElapsed = (DateTime.UtcNow - navStartTime).TotalMilliseconds;
        logger.Information("[PERF] NavigateToScheduleAsync: GetNavigationAsync took {ElapsedMs}ms", navElapsed);
#endif

        // Set navigation bar setting
        NavigationPage.SetHasNavigationBar(page, false);

        // Push the page without animation for instant navigation
#if DEBUG
        var pushStartTime = DateTime.UtcNow;
#endif
        await navigation.PushAsync(page, animated: false);
#if DEBUG
        var pushElapsed = (DateTime.UtcNow - pushStartTime).TotalMilliseconds;
        var totalElapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        logger.Information("[PERF] NavigateToScheduleAsync: PushAsync took {ElapsedMs}ms, Total navigation took {TotalMs}ms", pushElapsed, totalElapsed);
#endif
    }

    public async Task OpenMusicSelectionModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        var modal = serviceProvider.GetRequiredService<MusicSelectionModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenSongBookSelectionModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        var modal = serviceProvider.GetRequiredService<SongBookSelectionModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenTrackSelectionModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        var modal = serviceProvider.GetRequiredService<TrackSelectionModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenBibleSelectionModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        var modal = serviceProvider.GetRequiredService<BibleSelectionModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenBookSelectionModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        var modal = serviceProvider.GetRequiredService<BookSelectionModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenChapterSelectionModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        var modal = serviceProvider.GetRequiredService<ChapterSelectionModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenNumberOfChaptersModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        var modal = serviceProvider.GetRequiredService<NumberOfChaptersModal>();
        modal.BindingContext = bindingContext;
        // Disable animation for instant appearance
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenLanguageModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();

        ContentPage modal = bindingContext switch
        {
            // Use the appropriate modal based on the ViewModel type for compiled bindings
            BibleSelectionViewModel => serviceProvider.GetRequiredService<BibleLanguageModal>(),
            SongBookSelectionViewModel => serviceProvider.GetRequiredService<MusicLanguageModal>(),
            _ => throw new ArgumentException($"Unsupported ViewModel type: {bindingContext?.GetType().Name}",
                nameof(bindingContext))
        };

        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenAlarmModalAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                var navigation = GetNavigation();
                if (IsAlarmModalAlreadyShown(navigation))
                {
                    return;
                }

                var modal = serviceProvider.GetRequiredService<AlarmModal>();
                ConfigureAlarmModal(modal);
                await navigation.PushModalAsync(modal, animated: false);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error opening AlarmModal");
            }
        });
    }

    private static bool IsAlarmModalAlreadyShown(INavigation navigation)
    {
        var existingModal = navigation.ModalStack.LastOrDefault();
        return existingModal?.GetType() == typeof(AlarmModal) ||
               (existingModal is NavigationPage navPage && navPage.CurrentPage is AlarmModal);
    }

    private static void ConfigureAlarmModal(AlarmModal modal)
    {
        NavigationPage.SetHasNavigationBar(modal, false);
    }


    public async Task OpenBatteryOptimizationModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        var modal = serviceProvider.GetRequiredService<BatteryOptimizationExclusionModal>();
        modal.BindingContext = bindingContext;
        // Disable animation for instant appearance
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task PopModalAsync()
    {
        var navigation = GetNavigation();
        if (navigation.ModalStack.Count > 0)
        {
            var modal = navigation.ModalStack.LastOrDefault();
            // Keep animation enabled for modal dismissal
            var page = await navigation.PopModalAsync(animated: true);

            // Dispose the modal - handle both direct modals and wrapped modals
            if (page is IDisposable disposablePage)
            {
                disposablePage.Dispose();
            }
        }
    }

    public async Task PopAsync()
    {
        var navigation = GetNavigation();
        if (navigation.NavigationStack.Count > 1)
        {
            var page = navigation.NavigationStack.LastOrDefault();
            // Keep animation enabled for navigation back
            await navigation.PopAsync(animated: true);
            if (page is IDisposable disposable)
            {
                disposable.Dispose();
            }
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

