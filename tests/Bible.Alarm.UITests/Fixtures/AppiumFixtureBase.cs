#nullable enable

using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;

namespace Bible.Alarm.UITests.Fixtures;

/// <summary>
/// Shared Appium-session lifecycle for one platform. Subclasses build the platform-specific
/// <see cref="AppiumOptions"/> in <see cref="BuildOptions"/> and the fixture base owns
/// connection, screenshot dump on failure, and teardown.
/// </summary>
/// <remarks>
/// We deliberately use xunit's <see cref="IAsyncLifetime"/> per *class* (not per *test*) so the
/// app launches once and the smoke tests inside the class share one session. Multi-test fixtures
/// can override <see cref="ResetBetweenTests"/> to true if they need clean state per [Fact].
/// </remarks>
public abstract class AppiumFixtureBase : IAsyncLifetime
{
    private static readonly TimeSpan DefaultElementTimeout = TimeSpan.FromSeconds(30);

    public AppiumDriver? Driver { get; private set; }

    /// <summary>The xunit class this fixture serves; used to name screenshots on failure.</summary>
    private string ContextName => GetType().Name;

    /// <summary>
    /// Default <c>http://127.0.0.1:4723</c>. Override in CI by setting <c>APPIUM_URL</c> before
    /// invoking <c>dotnet test</c>.
    /// </summary>
    public static Uri AppiumServerUrl =>
        new(Environment.GetEnvironmentVariable("APPIUM_URL") ?? "http://127.0.0.1:4723/");

    /// <summary>Path to write screenshots on test failure (<c>UI_SCREENSHOT_DIR</c> or <c>./TestResults/ui</c>).</summary>
    public static string ScreenshotDirectory =>
        Environment.GetEnvironmentVariable("UI_SCREENSHOT_DIR") ?? Path.Combine("TestResults", "ui");

    /// <summary>Builds the platform-specific session options (capabilities, app path, automation name, etc.).</summary>
    protected abstract AppiumOptions BuildOptions();

    /// <summary>Constructs the platform-specific driver instance. Default overrides are sufficient for most cases.</summary>
    protected abstract AppiumDriver CreateDriver(Uri serverUrl, AppiumOptions options);

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(ScreenshotDirectory);
        var options = BuildOptions();
        Driver = CreateDriver(AppiumServerUrl, options);
        // Implicit waits play poorly with WaitFor* helpers; keep at 0 and use explicit waits everywhere.
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        try { Driver?.Quit(); }
        catch { /* best-effort teardown; the test outcome is the source of truth */ }
        Driver?.Dispose();
        Driver = null;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Polls for an element with the given accessibility id (== MAUI <c>AutomationId</c>) and
    /// returns it once visible. Throws <see cref="WebDriverTimeoutException"/> if the element
    /// never appears, after dumping a screenshot to <see cref="ScreenshotDirectory"/>.
    /// </summary>
    public IWebElement WaitForAccessibilityId(string accessibilityId, TimeSpan? timeout = null)
    {
        var driver = Driver ?? throw new InvalidOperationException("Driver not initialised; did InitializeAsync run?");
        var wait = new WebDriverWait(driver, timeout ?? DefaultElementTimeout)
        {
            PollingInterval = TimeSpan.FromMilliseconds(500)
        };
        wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));

        try
        {
            return wait.Until(d =>
            {
                // MobileBy.AccessibilityId / Appium "accessibility id" maps to:
                //   - Android: content-description -> set by MAUI from AutomationId
                //   - iOS:     accessibilityIdentifier -> set by MAUI from AutomationId
                //   - Windows: AutomationProperties.AutomationId -> set by MAUI from AutomationId
                var element = d.FindElement(MobileBy.AccessibilityId(accessibilityId));
                return element.Displayed ? element : null!;
            });
        }
        catch (WebDriverTimeoutException)
        {
            DumpScreenshot($"{ContextName}-{accessibilityId}-not-found");
            throw;
        }
    }

    /// <summary>Dumps the current viewport to <see cref="ScreenshotDirectory"/>; safe to call from cleanup.</summary>
    public void DumpScreenshot(string label)
    {
        if (Driver is not ITakesScreenshot shot)
        {
            return;
        }

        try
        {
            var path = Path.Combine(ScreenshotDirectory, $"{label}.png");
            shot.GetScreenshot().SaveAsFile(path);
        }
        catch
        {
            // Screenshot failure is never the cause of a real test failure; swallow.
        }
    }
}
