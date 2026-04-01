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
    public async Task NavigateToHomeAsync(INavigation navigation, bool animated = true)
    {
#if DEBUG
        var navStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
        Logger.Information("[BOOTSTRAP] NavigateToHomeAsync starting (animated={Animated})", animated);
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
            await PopToExistingHomeAsync(navigation, existingHome, animated);
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

    private async Task PopToExistingHomeAsync(INavigation navigation, Home existingHome, bool animated = true)
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
                try
                {
                    await navigation.PopAsync(animated: animated);
                }
                catch (Exception ex) when (NavigationStackManager.IsAndroidNavControllerError(ex))
                {
                    Logger.Warning(ex, "PopToExistingHomeAsync: NavController back stack out of sync, aborting remaining pops");
                    break;
                }

                if (page is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        // NOTE: Do NOT change opacity when navigating back to an existing Home page.
        // The Home page's opacity is managed by PlaybackModalService based on playback state.
        // Changing it here causes bugs:
        // - If playback is active: opacity = 0 makes Home blank when user returns from Schedule page
        // - The PlaybackModalService will show/hide Home appropriately when playback starts/stops
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
        // Skip index 0 (root)
        for (int i = navigation.NavigationStack.Count - 1; i >= 1; i--)
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
                try
                {
                    await navigation.PopAsync(animated: true);
                }
                catch (Exception ex) when (NavigationStackManager.IsAndroidNavControllerError(ex))
                {
                    Logger.Warning(ex, "PopAllPagesToRootAsync: NavController back stack out of sync, aborting remaining pops");
                    break;
                }

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


}
