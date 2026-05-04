#nullable enable

using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace Bible.Alarm.UITests.Fixtures;

/// <summary>
/// Drives the production Bible Alarm MSIX through Appium 2 + the <c>windows</c> driver, which
/// wraps WinAppDriver under the hood. WinAppDriver is archived/legacy but still the only stable
/// option for UWP/MSIX UI automation today, hence the explicit dependency.
/// </summary>
/// <remarks>
/// <para>The default <c>app</c> is the AppUserModelId of a side-loaded MAUI MSIX:</para>
/// <c>7610JehonathanThomas.BibleAlarm_<i>publisherHash</i>!App</c>
/// <para>
/// The publisher hash differs per machine (it is derived from the dev cert), so this fixture
/// reads the AUMID from the <c>BIBLE_ALARM_AUMID</c> env var. To discover it locally, run:
/// </para>
/// <code>(Get-StartApps | Where-Object Name -eq 'Bible Alarm').AppId</code>
/// <para>
/// Set the result as <c>BIBLE_ALARM_AUMID</c> before invoking <c>dotnet test</c>, or provide
/// it inline in <c>tests/run-ui-tests.ps1 -Aumid ...</c>.
/// </para>
/// </remarks>
public sealed class WindowsAppiumFixture : AppiumFixtureBase
{
    public const string DefaultPackageFamily = "7610JehonathanThomas.BibleAlarm";

    protected override AppiumOptions BuildOptions()
    {
        var aumid = Environment.GetEnvironmentVariable("BIBLE_ALARM_AUMID")
            ?? throw new InvalidOperationException(
                $"BIBLE_ALARM_AUMID env var is not set. Resolve it once with " +
                $"(Get-StartApps | ? Name -eq 'Bible Alarm').AppId — it should look like " +
                $"'{DefaultPackageFamily}_<hash>!App'. tests/UITESTS.md walks through this.");

        var options = new AppiumOptions
        {
            AutomationName = "Windows",
            PlatformName = "Windows",
            App = aumid
        };

        // deviceName is required by WinAppDriver; "WindowsPC" is the canonical placeholder.
        options.AddAdditionalAppiumOption("deviceName", "WindowsPC");
        // Long Appium-side default is 60s; bump to 90s because MAUI Windows cold-start with
        // Syncfusion + EF Core takes ~25s on warm boxes and far more on a cold runner.
        options.AddAdditionalAppiumOption("ms:waitForAppLaunch", 90);
        return options;
    }

    protected override AppiumDriver CreateDriver(Uri serverUrl, AppiumOptions options)
        => new WindowsDriver(serverUrl, options);
}
