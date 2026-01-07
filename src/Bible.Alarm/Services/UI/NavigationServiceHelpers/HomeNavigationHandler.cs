#nullable enable
using Bible.Alarm.Views;
using Microsoft.Maui.Controls;
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

        MainThread.BeginInvokeOnMainThread(() =>
        {
            // If playback is active, hide Home page to prevent flash before modal appears
            // Home will be shown when modal appears or playback stops
            homePage.Opacity = isPlaybackActive ? 0.0 : 1.0;
        });
    }
}
