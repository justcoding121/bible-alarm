#nullable enable

using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.iOS;

namespace Bible.Alarm.UITests.Fixtures;

/// <summary>
/// Drives the production Bible Alarm app on an iOS simulator through Appium 2 + the
/// <c>xcuitest</c> driver. Real-device runs additionally need a signed .ipa and a developer
/// team id; for now this fixture only targets the simulator (set <c>BIBLE_ALARM_APP_PATH</c>
/// to the .app bundle path).
/// </summary>
public sealed class IosAppiumFixture : AppiumFixtureBase
{
    public const string DefaultBundleId = "com.jthomas.info.Bible.Alarm";

    protected override AppiumOptions BuildOptions()
    {
        var options = new AppiumOptions
        {
            AutomationName = "XCUITest",
            PlatformName = "iOS"
        };

        var appPath = Environment.GetEnvironmentVariable("BIBLE_ALARM_APP_PATH");
        if (!string.IsNullOrWhiteSpace(appPath))
        {
            options.App = appPath;
        }
        else
        {
            options.AddAdditionalAppiumOption("bundleId",
                Environment.GetEnvironmentVariable("BIBLE_ALARM_BUNDLE_ID") ?? DefaultBundleId);
        }

        var udid = Environment.GetEnvironmentVariable("IOS_SIMULATOR_UDID");
        if (!string.IsNullOrWhiteSpace(udid))
        {
            options.AddAdditionalAppiumOption("udid", udid);
        }

        // MAUI iOS cold-start on a freshly booted simulator runs ~20-40s; XCUITest is patient by
        // default but we still bump it to keep the smoke test from flaking on slow runners.
        options.AddAdditionalAppiumOption("wdaLaunchTimeout", 90_000);
        options.AddAdditionalAppiumOption("appLaunchStateTimeoutSec", 90);
        options.AddAdditionalAppiumOption("newCommandTimeout", 120);

        return options;
    }

    protected override AppiumDriver CreateDriver(Uri serverUrl, AppiumOptions options)
        => new IOSDriver(serverUrl, options);
}
