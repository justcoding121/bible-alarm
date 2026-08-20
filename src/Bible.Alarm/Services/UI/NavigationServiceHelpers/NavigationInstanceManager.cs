#nullable enable
using Polly;
using Polly.Retry;
using Serilog;

namespace Bible.Alarm.Services.UI.NavigationServiceHelpers;

/// <summary>
/// Handles navigation instance management including caching, retry policy, and finding.
/// </summary>
public sealed class NavigationInstanceManager(ILogger logger)
{
    // Cached navigation instance to avoid retries on every call
    private INavigation? cachedNavigation;

    /// <summary>
    /// Helper method to create navigation retry policy.
    /// Limit retries to prevent infinite loops when navigation is unavailable.
    /// </summary>
    private RetryPolicy CreateNavigationRetryPolicy() => Policy
        .Handle<InvalidOperationException>()
        .WaitAndRetry(
            retryCount: 10, // Limit to 10 retries (2 seconds total) to prevent infinite loops
            sleepDurationProvider: _ => TimeSpan.FromMilliseconds(200),
            onRetry: (exception, timeSpan, retryCount, _) =>
            {
                logger.Debug(
                    "Navigation not available yet, retrying in {DelayMs}ms (attempt {RetryCount}/10). Error: {Message}",
                    timeSpan.TotalMilliseconds,
                    retryCount,
                    exception.Message);
            });

    /// <summary>
    /// Gets the navigation instance with optional retry.
    /// </summary>
    public INavigation GetNavigation(bool shouldRetry = true)
    {
        if (cachedNavigation is not null)
        {
            // Verify cached navigation is still valid before returning it
            try
            {
                // Access a property to verify the navigation is still valid
                _ = cachedNavigation.NavigationStack;
                return cachedNavigation;
            }
            catch (Exception ex)
            {
                // Cached navigation is invalid, clear it and try to get a new one
                logger.Debug(ex, "Cached navigation is invalid, clearing cache");
                cachedNavigation = null;
            }
        }

        if (!shouldRetry)
        {
            // Try once without retry - throw immediately if navigation is not available
            var navigation = NavigationFinder();
            cachedNavigation = navigation;
            return navigation;
        }

        var retryPolicy = CreateNavigationRetryPolicy();

        var retryResponse = retryPolicy.ExecuteAndCapture(() => NavigationFinder());

        if (retryResponse.Outcome == OutcomeType.Failure)
        {
            // If retries failed, clear cache and throw the exception
            cachedNavigation = null;
            throw retryResponse.FinalException;
        }

        cachedNavigation = retryResponse.Result;
        return cachedNavigation;
    }

    private INavigation NavigationFinder()
    {
        // If not in DI or GetService returned null, get it directly from the current application window
        var app = Application.Current;
        if (app is null)
        {
            var errorMsg = "Application.Current is null. Cannot get INavigation.";
            logger.Error(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        logger.Debug("Application.Current found. Windows count: {WindowsCount}", app.Windows.Count);

        // Try to get navigation from windows
        if (app.Windows.Count > 0)
        {
            var window = app.Windows[0];
            logger.Debug("Window found. Page type: {PageType}", window?.Page?.GetType().Name ?? "null");

            if (window?.Page is NavigationPage navPage)
            {
                logger.Debug("Found NavigationPage in window.Page");
                var navigation = navPage.Navigation;

                // Verify navigation is accessible before caching
                try
                {
                    _ = navigation.NavigationStack;
                    return navigation;
                }
                catch (Exception ex)
                {
                    logger.Debug(ex, "Navigation is not accessible yet, will retry");
                    throw new InvalidOperationException("Navigation is not accessible yet", ex);
                }
            }
        }
        else
        {
            logger.Warning("Application.Current.Windows.Count is 0 - window may not be initialized yet");
        }

        var mainPageType = app.Windows.Count > 0 ? app.Windows[0].Page?.GetType().Name ?? "null" : "null (no windows)";
        var finalErrorMsg =
            $"INavigation is not available. Application.Current.Windows.Count={app.Windows.Count}, MainPage type={mainPageType}";
        logger.Error(
            "INavigation is not available. Application.Current.Windows.Count={WindowsCount}, MainPage type={MainPageType}",
            app.Windows.Count,
            mainPageType);
        throw new InvalidOperationException(finalErrorMsg);
    }

    /// <summary>
    /// Clears the cached navigation. Call this when the app is disposed or navigation becomes invalid.
    /// </summary>
    public void ClearCache()
    {
        cachedNavigation = null;
        logger.Debug("Navigation cache cleared");
    }
}
