#nullable enable
using Bible.Alarm.Views;
using Microsoft.Maui.ApplicationModel;
using Serilog;

namespace Bible.Alarm.Services.UI.NavigationServiceHelpers;

/// <summary>
/// Handles home page navigation logic.
/// </summary>
public sealed class HomeNavigationHandler(ILogger logger, IServiceProvider serviceProvider)
{
    // Ensure logger is always considered used (not just in DEBUG blocks)
    private ILogger Logger => logger;
    /// <summary>
    /// Navigates to the home page, reusing existing if available.
    /// </summary>
    public async Task NavigateToHomeAsync(INavigation navigation)
    {
#if DEBUG
        var navStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
        Logger.Information("[BOOTSTRAP] NavigateToHomeAsync starting");
#endif

        if (IsAlreadyOnHomePage(navigation))
        {
#if DEBUG
            var navElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - navStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Logger.Information("[BOOTSTRAP] NavigateToHomeAsync completed (already on home) in {ElapsedMs:F2}ms", navElapsed);
#endif
            return;
        }

        var existingHome = FindExistingHomeInStack(navigation);
        if (existingHome != null)
        {
            await PopToExistingHomeAsync(navigation, existingHome);
#if DEBUG
            var navElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - navStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Logger.Information("[BOOTSTRAP] NavigateToHomeAsync completed (existing home) in {ElapsedMs:F2}ms", navElapsed);
#endif
        }
        else
        {
            await PopToRootAndPushNewHomeAsync(navigation);
#if DEBUG
            var navTotalElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - navStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Logger.Information("[BOOTSTRAP] NavigateToHomeAsync completed (new home) in {ElapsedMs:F2}ms", navTotalElapsed);
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
        // Capture the pages to pop BEFORE starting - this prevents race conditions
        // where a new page is pushed while we're popping and we accidentally pop the new page too
        var pagesToPop = new List<Page>();
        for (int i = navigation.NavigationStack.Count - 1; i >= 0; i--)
        {
            var page = navigation.NavigationStack[i];
            if (page == existingHome)
            {
                break; // Stop when we reach Home
            }
            pagesToPop.Add(page);
        }

        // Pop only the pages that were on the stack when navigation started
        foreach (var page in pagesToPop)
        {
            // Verify the page is still on top of the stack before popping
            // This handles the edge case where another navigation already happened
            if (navigation.NavigationStack.LastOrDefault() == page)
            {
                await navigation.PopAsync(animated: true);
                if (page is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        // NOTE: Do NOT change opacity when navigating back to an existing Home page.
        // The Home page's opacity is managed by AlarmModalService based on playback state.
        // Changing it here causes bugs:
        // - If playback is active: opacity = 0 makes Home blank when user returns from Schedule page
        // - The AlarmModalService will show/hide Home appropriately when playback starts/stops
        Logger.Information("Navigated back to existing home page (opacity unchanged)");
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
        Logger.Information("[BOOTSTRAP] PopAllPagesToRootAsync completed in {ElapsedMs:F2}ms", popElapsed);
#endif

#if DEBUG
        var homeCreateStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        var homePage = serviceProvider.GetRequiredService<Home>();
#if DEBUG
        var homeCreateElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - homeCreateStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        Logger.Information("[BOOTSTRAP] Home page service resolution completed in {ElapsedMs:F2}ms", homeCreateElapsed);
#endif

        ConfigureHomePageNavigation(homePage);

        // Check if playback is active and hide home page immediately if so
        // This prevents visual flash when app starts cold from Android Auto while playing
        // We check MediaSession directly as PlaybackState might not be initialized yet
        if (ShouldHideHomePageOnStart())
        {
            homePage.Opacity = 0.0;
            Logger.Information("Home page opacity set to 0.0 immediately after creation (playback active)");
        }

#if DEBUG
        var pushStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        await navigation.PushAsync(homePage, animated: false);
#if DEBUG
        var pushElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - pushStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        Logger.Information("[BOOTSTRAP] Home page PushAsync completed in {ElapsedMs:F2}ms", pushElapsed);
#endif
    }

    private async Task PopAllPagesToRootAsync(INavigation navigation)
    {
        // Capture the pages to pop BEFORE starting - this prevents race conditions
        // where a new page is pushed while we're popping and we accidentally pop the new page too
        var pagesToPop = new List<Page>();
        for (int i = navigation.NavigationStack.Count - 1; i >= 1; i--) // Skip index 0 (root)
        {
            pagesToPop.Add(navigation.NavigationStack[i]);
        }

        // Pop only the pages that were on the stack when navigation started
        foreach (var page in pagesToPop)
        {
            // Verify the page is still on top of the stack before popping
            // This handles the edge case where another navigation already happened
            if (navigation.NavigationStack.LastOrDefault() == page)
            {
                await navigation.PopAsync(animated: true);
                if (page is IDisposable disposable)
                {
                    disposable.Dispose();
                }
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
    public Home? GetCurrentHomePage(INavigation navigation)
    {
        try
        {
            return navigation.NavigationStack.LastOrDefault() as Home;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Error getting current Home page from navigation stack");
            return null;
        }
    }

    /// <summary>
    /// Sets Home page visibility based on playback state.
    /// If playback is active, hides Home to prevent visual flash before alarm modal appears.
    /// </summary>
    public void SetHomePageVisibility(Home? homePage, bool isPlaybackActive)
    {
        if (homePage == null)
        {
            return;
        }

        // Set opacity immediately if already on main thread, otherwise invoke on main thread
        // This prevents visual flash when hiding the home page during cold start from Android Auto
        if (MainThread.IsMainThread)
        {
            // If playback is active, hide Home page to prevent flash before modal appears
            // Home will be shown when modal appears or playback stops
            homePage.Opacity = isPlaybackActive ? 0.0 : 1.0;
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // If playback is active, hide Home page to prevent flash before modal appears
                // Home will be shown when modal appears or playback stops
                homePage.Opacity = isPlaybackActive ? 0.0 : 1.0;
            });
        }
    }

    /// <summary>
    /// Checks if home page should be hidden on start (when playback is active).
    /// Checks MediaSession directly as PlaybackState Fluxor store might not be initialized yet during cold start.
    /// </summary>
    private bool ShouldHideHomePageOnStart()
    {
#if ANDROID
        try
        {
            var mediaSession = Platforms.Android.Services.Media.MediaSessionHelper.Create();
            var playbackState = mediaSession?.Controller?.PlaybackState;
            
            if (playbackState != null)
            {
                // Check if playback state indicates active playback (Playing, Buffering, or Paused)
                var isActive = playbackState.State is 
                    Android.Support.V4.Media.Session.PlaybackStateCompat.StatePlaying or
                    Android.Support.V4.Media.Session.PlaybackStateCompat.StateBuffering or
                    Android.Support.V4.Media.Session.PlaybackStateCompat.StatePaused;
                
                return isActive;
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to check MediaSession playback state in ShouldHideHomePageOnStart");
        }
#endif
        return false;
    }
}
