#nullable enable
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.ViewModels.Bible;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.Views;
using Bible.Alarm.Views.Bible;
using Bible.Alarm.Views.Music;
using Bible.Alarm.Views.General;
using Bible.Alarm.Views.Schedule;
using Bible.Alarm.Views.Shared;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.ApplicationModel;
using Serilog;

namespace Bible.Alarm.Services.UI;

public class NavigationService(
    IServiceProvider serviceProvider,
    ILogger logger)
    : INavigationService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger _logger = logger;

    private INavigation GetNavigation()
    {
        // First, try to get from DI
        INavigation? navigation = null;
        try
        {
            navigation = _serviceProvider.GetService<INavigation>();
            if (navigation is not null)
            {
                _logger?.Debug("Got INavigation from DI");
                return navigation;
            }
        }
        catch (Exception ex)
        {
            // Log but continue to fallback
            _logger?.Warning(ex, "Exception getting INavigation from DI, falling back to direct access");
        }
        
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
                return navPage.Navigation;
            }
            
            // If window.Page is a regular Page, try to get navigation from it
            if (window?.Page is Page page)
            {
                _logger?.Debug($"Window.Page is {page.GetType().Name}, checking for navigation");
                
                // Try to get navigation from the page itself
                if (page.Navigation is not null)
                {
                    _logger?.Debug("Found navigation from page.Navigation");
                    return page.Navigation;
                }
                
                // Check if the page has a parent NavigationPage
                var parent = page.Parent;
                int depth = 0;
                while (parent is not null && depth < 10)
                {
                    _logger?.Debug($"Checking parent at depth {depth}: {parent.GetType().Name}");
                    if (parent is NavigationPage parentNavPage)
                    {
                        _logger?.Debug("Found NavigationPage in parent hierarchy");
                        return parentNavPage.Navigation;
                    }
                    parent = parent.Parent;
                    depth++;
                }
            }
        }
        else
        {
            _logger?.Warning("Application.Current.Windows.Count is 0 - window may not be initialized yet");
        }
        
        // Fallback to MainPage (obsolete but may be needed)
#pragma warning disable CS0618 // Type or member is obsolete
        if (app.MainPage is NavigationPage mainNavPage)
        {
            _logger?.Debug("Found NavigationPage in MainPage");
            return mainNavPage.Navigation;
        }
        
        if (app.MainPage is Page mainPage && mainPage.Navigation is not null)
        {
            _logger?.Debug("Found navigation in MainPage.Navigation");
            return mainPage.Navigation;
        }
#pragma warning restore CS0618
        
        var mainPageType = app.Windows.Count > 0 ? app.Windows[0].Page?.GetType().Name ?? "null" : "null (no windows)";
        var finalErrorMsg = $"INavigation is not available. Application.Current.Windows.Count={app.Windows.Count}, MainPage type={mainPageType}";
        _logger?.Error(finalErrorMsg);
        throw new InvalidOperationException(finalErrorMsg);
    }
    
    private async Task<INavigation> GetNavigationAsync(int maxRetries = 30, int delayMs = 200)
    {
        // Ensure we're on the main thread when accessing UI elements
        if (!MainThread.IsMainThread)
        {
            return await MainThread.InvokeOnMainThreadAsync(async () => await GetNavigationAsync(maxRetries, delayMs));
        }
        
        var app = Application.Current;
        if (app is null)
        {
            throw new InvalidOperationException("Application.Current is null. Cannot get INavigation.");
        }
        
        // First, wait for at least one window to be available
        int initialWindowCount = app.Windows.Count;
        int retriesForWindow = 0;
        while (app.Windows.Count == 0 && retriesForWindow < maxRetries)
        {
            _logger?.Debug($"Waiting for window to be added to Application. Current count: {app.Windows.Count} (attempt {retriesForWindow + 1}/{maxRetries})");
            await Task.Delay(delayMs);
            retriesForWindow++;
        }
        
        if (app.Windows.Count == 0)
        {
            throw new InvalidOperationException($"No windows available after {maxRetries} retries. Application may not be fully initialized.");
        }
        
        _logger?.Debug($"Window found. Now attempting to get navigation (window count: {app.Windows.Count})");
        
        // Now try to get navigation
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var navigation = GetNavigation();
                if (i > 0 || retriesForWindow > 0)
                {
                    _logger?.Information($"Navigation obtained after {retriesForWindow} window waits and {i} navigation retries");
                }
                return navigation;
            }
            catch (InvalidOperationException ex)
            {
                if (i < maxRetries - 1)
                {
                    _logger?.Debug($"Navigation not available yet, retrying in {delayMs}ms (attempt {i + 1}/{maxRetries}). Error: {ex.Message}");
                    await Task.Delay(delayMs);
                }
                else
                {
                    _logger?.Error(ex, $"Failed to get INavigation after {maxRetries} retries");
                    throw;
                }
            }
        }
        
        // Should never reach here, but compiler needs it
        throw new InvalidOperationException("Failed to get INavigation after retries");
    }

    public async Task NavigateToHomeAsync()
    {
        // Wait for navigation to be available (window might still be initializing)
        var navigation = await GetNavigationAsync();
        
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
                await navigation.PopAsync(animated: false);
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
                await navigation.PopAsync(animated: false);
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
        var page = _serviceProvider.GetRequiredService<Schedule>();
        var navigation = await GetNavigationAsync();
        
        // Set navigation bar setting
        NavigationPage.SetHasNavigationBar(page, false);
        
        // Push the page without animation for instant navigation
        await navigation.PushAsync(page, animated: false);
    }

    public async Task NavigateToMusicSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<MusicSelection>();
        await PushFreshPageAsync(page, hasNavigationBar: false);
    }

    public async Task NavigateToSongBookSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<SongBookSelection>();
        await PushFreshPageAsync(page, hasNavigationBar: false);
    }

    public async Task NavigateToTrackSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<TrackSelection>();
        await PushFreshPageAsync(page, hasNavigationBar: false);
    }

    public async Task NavigateToBibleSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<BibleSelection>();
        await PushFreshPageAsync(page, hasNavigationBar: false);
    }

    public async Task NavigateToBookSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<BookSelection>();
        await PushFreshPageAsync(page, hasNavigationBar: false);
    }

    public async Task NavigateToChapterSelectionAsync()
    {
        var page = _serviceProvider.GetRequiredService<ChapterSelection>();
        await PushFreshPageAsync(page, hasNavigationBar: false);
    }

    public async Task OpenNumberOfChaptersModalAsync(object bindingContext)
    {
        var navigation = await GetNavigationAsync();
        var modal = _serviceProvider.GetRequiredService<NumberOfChaptersModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal);
    }

    public async Task OpenLanguageModalAsync(object bindingContext)
    {
        var navigation = await GetNavigationAsync();
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
                var navigation = await GetNavigationAsync();
                
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
                await navigation.PushModalAsync(modal);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error opening AlarmModal");
            }
        });
    }


    public async Task OpenBatteryOptimizationModalAsync(object bindingContext)
    {
        var navigation = await GetNavigationAsync();
        var modal = _serviceProvider.GetRequiredService<BatteryOptimizationExclusionModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal);
    }

    public async Task PopModalAsync()
    { 
            var navigation = await GetNavigationAsync();
            if (navigation.ModalStack.Count > 0)
            {
                var modal = navigation.ModalStack.LastOrDefault();
                await navigation.PopModalAsync();
                
                // Dispose the modal - handle both direct modals and wrapped modals
                if (modal is NavigationPage navPage && navPage.CurrentPage is IDisposable disposablePage)
                {
                    disposablePage.Dispose();
                }
                else if (modal is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
    }

    public async Task PopAsync()
    {
        var navigation = await GetNavigationAsync();
        if (navigation.NavigationStack.Count > 1)
        {
            var page = navigation.NavigationStack.LastOrDefault();
            await navigation.PopAsync();
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
        var navigation = await GetNavigationAsync();

        // Set navigation bar setting
        NavigationPage.SetHasNavigationBar(page, hasNavigationBar);

        // Push the fresh page first
        await navigation.PushAsync(page);
    }

    public MediaElement GetMediaElement()
    {
        // Find MediaElement from BootstrapPage (always on navigation stack)
        // Note: This is synchronous, so we use GetNavigation() directly
        // If navigation isn't ready, it will throw - caller should handle this
        var navigation = GetNavigation();
        
        // BootstrapPage is always the first page in the navigation stack
        var navStack = navigation.NavigationStack;
        foreach (var page in navStack)
        {
            if (page is BootstrapPage bootstrapPage)
            {
                var mediaElement = bootstrapPage.FindByName("MediaPlayer") as MediaElement;
                if (mediaElement != null)
                {
                    _logger.Information("MediaElement found in BootstrapPage");
                    return mediaElement;
                }
            }
        }

        _logger.Error("MediaElement not found in BootstrapPage - this should never happen!");
        // Throw exception instead of creating orphaned MediaElement instance
        // This prevents memory leaks and makes the issue immediately visible
        throw new InvalidOperationException("MediaElement not found in BootstrapPage. BootstrapPage must be loaded before accessing MediaElement.");
    }
}

