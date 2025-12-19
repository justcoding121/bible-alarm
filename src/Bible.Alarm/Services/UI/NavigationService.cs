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

public class NavigationService(
    IServiceProvider serviceProvider,
    ILogger logger)
    : INavigationService, IDisposable
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger _logger = logger;
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

    // Cached navigation instance to avoid retries on every call
    private INavigation? _cachedNavigation;

    private bool _isDisposed;

    // Helper method to create navigation retry policy
    private RetryPolicy CreateNavigationRetryPolicy() => Policy
        .Handle<InvalidOperationException>()
        .WaitAndRetry(
            retryCount: int.MaxValue,
            sleepDurationProvider: _ => TimeSpan.FromMilliseconds(200),
            onRetry: (exception, timeSpan, retryCount, context) =>
            {
                _logger?.Debug($"Navigation not available yet, retrying in {timeSpan.TotalMilliseconds}ms (attempt {retryCount}). Error: {exception.Message}");
            });


    private INavigation GetNavigation(bool shouldRetry = true)
    {
        if (_cachedNavigation is not null)
        {
            return _cachedNavigation;
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
            _logger?.Error(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        _logger?.Debug($"Application.Current found. Windows count: {app.Windows.Count}");

        // Try to get navigation from windows
        if (app.Windows.Count > 0)
        {
            var window = app.Windows[0];
            _logger?.Debug($"Window found. Page type: {window?.Page?.GetType().Name ?? "null"}");

            // Check if window.Page is NavigationPage
            if (window?.Page is NavigationPage navPage)
            {
                _logger?.Debug("Found NavigationPage in window.Page");
                _cachedNavigation = navPage.Navigation;
                return _cachedNavigation;
            }
        }
        else
        {
            _logger?.Warning("Application.Current.Windows.Count is 0 - window may not be initialized yet");
        }

        var mainPageType = app.Windows.Count > 0 ? app.Windows[0].Page?.GetType().Name ?? "null" : "null (no windows)";
        var finalErrorMsg = $"INavigation is not available. Application.Current.Windows.Count={app.Windows.Count}, MainPage type={mainPageType}";
        _logger?.Error(finalErrorMsg);
        throw new InvalidOperationException(finalErrorMsg);
    }

    /// <summary>
    /// Clears the cached navigation. Call this when the app is disposed or navigation becomes invalid.
    /// </summary>
    public void ClearCache()
    {
        _cachedNavigation = null;
        _logger?.Debug("Navigation cache cleared");
    }

    public async Task NavigateToHomeAsync()
    {
        // Wait for navigation to be available (window might still be initializing)
        var navigation = GetNavigation();

        // Check if we're already on Home page
        if (navigation.NavigationStack.Count > 0 && navigation.NavigationStack.LastOrDefault() is Home)
        {
            // Already on Home, no need to navigate
            return;
        }

        // Find Home in the stack (BootstrapPage is at index 0, Home should be at index 1)
        Home? existingHome = null;
        for (int i = navigation.NavigationStack.Count - 1; i >= 0; i--)
        {
            if (navigation.NavigationStack[i] is Home home)
            {
                existingHome = home;
                break;
            }
        }

        if (existingHome != null)
        {
            // Home exists in stack - pop all pages until we reach Home (but keep BootstrapPage)
            while (navigation.NavigationStack.Count > 1 && navigation.NavigationStack.LastOrDefault() != existingHome)
            {
                var page = navigation.NavigationStack.LastOrDefault();
                // Keep animation enabled for navigation back
                await navigation.PopAsync(animated: true);
                if (page != existingHome && page is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
        else
        {
            // Home doesn't exist - pop all pages except BootstrapPage (index 0), then push new Home
            while (navigation.NavigationStack.Count > 1)
            {
                var page = navigation.NavigationStack.LastOrDefault();
                // Keep animation enabled for navigation back
                await navigation.PopAsync(animated: true);
                if (page is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            // Create a new Home page via DI
            var homePage = _serviceProvider.GetRequiredService<Home>();

            // Set navigation bar setting
            NavigationPage.SetHasBackButton(homePage, false);
            NavigationPage.SetHasNavigationBar(homePage, false);

            // Push the new Home page without animation
            await navigation.PushAsync(homePage, animated: false);
        }
    }

    public async Task NavigateToScheduleAsync()
    {
        var startTime = DateTime.UtcNow;
        _logger.Information("[PERF] NavigateToScheduleAsync: Starting navigation at {StartTime}", startTime);

        var pageStartTime = DateTime.UtcNow;
        var page = _serviceProvider.GetRequiredService<Schedule>();
        var pageElapsed = (DateTime.UtcNow - pageStartTime).TotalMilliseconds;
        _logger.Information("[PERF] NavigateToScheduleAsync: Page service resolution took {ElapsedMs}ms", pageElapsed);

        var navStartTime = DateTime.UtcNow;
        var navigation = GetNavigation();
        var navElapsed = (DateTime.UtcNow - navStartTime).TotalMilliseconds;
        _logger.Information("[PERF] NavigateToScheduleAsync: GetNavigationAsync took {ElapsedMs}ms", navElapsed);

        // Set navigation bar setting
        NavigationPage.SetHasNavigationBar(page, false);

        // Push the page without animation for instant navigation
        var pushStartTime = DateTime.UtcNow;
        await navigation.PushAsync(page, animated: false);
        var pushElapsed = (DateTime.UtcNow - pushStartTime).TotalMilliseconds;
        var totalElapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        _logger.Information("[PERF] NavigateToScheduleAsync: PushAsync took {ElapsedMs}ms, Total navigation took {TotalMs}ms", pushElapsed, totalElapsed);
    }

    public async Task NavigateToMusicSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<MusicSelection>();
        await PushFreshPageAsync(page, hasNavigationBar: true);
    }

    public async Task NavigateToSongBookSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<SongBookSelection>();
        await PushFreshPageAsync(page, hasNavigationBar: true);
    }

    public async Task NavigateToTrackSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<TrackSelection>();
        await PushFreshPageAsync(page, hasNavigationBar: true);
    }

    public async Task NavigateToBibleSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<BibleSelection>();
        await PushFreshPageAsync(page, hasNavigationBar: true);
    }

    public async Task NavigateToBookSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<BookSelection>();
        await PushFreshPageAsync(page, hasNavigationBar: true);
    }

    public async Task NavigateToChapterSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<ChapterSelection>();
        await PushFreshPageAsync(page, hasNavigationBar: true);
    }

    public async Task OpenNumberOfChaptersModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        var modal = _serviceProvider.GetRequiredService<NumberOfChaptersModal>();
        modal.BindingContext = bindingContext;
        // Disable animation for instant appearance
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenLanguageModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        ContentPage modal;

        // Use the appropriate modal based on the ViewModel type for compiled bindings
        if (bindingContext is BibleSelectionViewModel)
        {
            modal = _serviceProvider.GetRequiredService<BibleLanguageModal>();
        }
        else if (bindingContext is SongBookSelectionViewModel)
        {
            modal = _serviceProvider.GetRequiredService<MusicLanguageModal>();
        }
        else
        {
            throw new ArgumentException($"Unsupported ViewModel type: {bindingContext?.GetType().Name}", nameof(bindingContext));
        }

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

                // Check if modal is already shown
                var existingModal = navigation.ModalStack.LastOrDefault();
                if (existingModal?.GetType() == typeof(AlarmModal) ||
                    (existingModal is NavigationPage navPage && navPage.CurrentPage is AlarmModal))
                {
                    return;
                }


                var modal = _serviceProvider.GetRequiredService<AlarmModal>();

                // Ensure modal is properly configured
                NavigationPage.SetHasNavigationBar(modal, false);

                // Push modal directly - wrapping in NavigationPage on Windows causes display issues
                // Disable animation for instant appearance
                await navigation.PushModalAsync(modal, animated: false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error opening AlarmModal");
            }
        });
    }


    public async Task OpenBatteryOptimizationModalAsync(object bindingContext)
    {
        var navigation = GetNavigation();
        var modal = _serviceProvider.GetRequiredService<BatteryOptimizationExclusionModal>();
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

    public BootstrapPage? GetBootstrapPage(bool shouldRetry = true)
    {
        try
        {
            var navigation = GetNavigation(shouldRetry);
            var navStack = navigation.NavigationStack;
            foreach (var page in navStack)
            {
                if (page is BootstrapPage bootstrapPage)
                {
                    return bootstrapPage;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error getting BootstrapPage from navigation stack");
        }

        return null;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        // Cancel and dispose cancellation token source (this will cancel any infinite Polly retries)
        try
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            _logger?.Warning(ex, "Error during cancellation token source disposal");
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
            INavigation? navigation = null;
            try
            {
                navigation = GetNavigation();
            }
            catch (Exception ex)
            {
                // Navigation might not be available if fragments are already destroyed
                _logger?.Debug(ex, "NavigationService.PopAllModalsAndPages - Could not get navigation, fragments may be destroyed");
                return;
            }

            if (navigation == null)
            {
                _logger?.Warning("NavigationService.PopAllModalsAndPages - Navigation is null");
                return;
            }

            // Dispose all modals directly without popping (fragments are already destroyed)
            List<Page> modalStack;
            try
            {
                modalStack = navigation.ModalStack.ToList(); // Create a copy to avoid modification during iteration
            }
            catch (Exception ex)
            {
                _logger?.Debug(ex, "NavigationService.PopAllModalsAndPages - Could not access ModalStack, fragments may be destroyed");
                modalStack = new List<Page>();
            }

            foreach (var page in modalStack)
            {
                try
                {
                    if (page is IDisposable disposable)
                    {
                        disposable.Dispose();
                        _logger?.Debug("NavigationService.PopAllModalsAndPages - Disposed modal: {PageType}", page.GetType().Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Warning(ex, "NavigationService.PopAllModalsAndPages - Error disposing modal: {PageType}", page.GetType().Name);
                }
            }

            // Dispose all pages in navigation stack directly without popping (fragments are already destroyed)
            List<Page> navigationStack;
            try
            {
                navigationStack = navigation.NavigationStack.ToList(); // Create a copy to avoid modification during iteration
            }
            catch (Exception ex)
            {
                _logger?.Debug(ex, "NavigationService.PopAllModalsAndPages - Could not access NavigationStack, fragments may be destroyed");
                navigationStack = new List<Page>();
            }

            foreach (var page in navigationStack)
            {
                try
                {
                    if (page is IDisposable disposable)
                    {
                        disposable.Dispose();
                        _logger?.Debug("NavigationService.PopAllModalsAndPages - Disposed page: {PageType}", page.GetType().Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Warning(ex, "NavigationService.PopAllModalsAndPages - Error disposing page: {PageType}", page.GetType().Name);
                }
            }

            _logger?.Information("NavigationService.PopAllModalsAndPages - Finished disposing modals and pages. Modal count: {ModalCount}, Page count: {PageCount}",
                modalStack.Count, navigationStack.Count);
        }
        catch (Exception ex)
        {
            _logger?.Warning(ex, "NavigationService.PopAllModalsAndPages - Error during modal/page cleanup");
        }
    }
}

