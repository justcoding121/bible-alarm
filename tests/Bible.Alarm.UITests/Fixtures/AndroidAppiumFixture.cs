#nullable enable

using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;

namespace Bible.Alarm.UITests.Fixtures;

/// <summary>
/// Drives the production Bible Alarm APK through Appium 2 + the <c>uiautomator2</c> driver
/// (the modern, supported driver for Android UI automation).
/// </summary>
/// <remarks>
/// Expects the production APK to be already installed on the connected ADB device, OR an APK
/// path supplied via <c>BIBLE_ALARM_APK</c>. Locally, <c>tests/run-ui-tests.ps1 -Platform Android</c>
/// builds + installs first; in CI the test-android job's APK artifact is reused.
/// </remarks>
public sealed class AndroidAppiumFixture : AppiumFixtureBase
{
    public const string DefaultPackageName = "com.jthomas.info.Bible.Alarm";
    public const string DefaultMainActivity = "crc644dd66dffaf5cb9c0.MainActivity";

    protected override AppiumOptions BuildOptions()
    {
        var options = new AppiumOptions
        {
            AutomationName = "UiAutomator2",
            PlatformName = "Android"
        };

        // Either install + launch from the APK on disk, or attach to the already-installed package.
        var apk = Environment.GetEnvironmentVariable("BIBLE_ALARM_APK");
        if (!string.IsNullOrWhiteSpace(apk))
        {
            options.App = apk;
        }
        else
        {
            options.AddAdditionalAppiumOption("appPackage",
                Environment.GetEnvironmentVariable("BIBLE_ALARM_APP_PACKAGE") ?? DefaultPackageName);
            options.AddAdditionalAppiumOption("appActivity",
                Environment.GetEnvironmentVariable("BIBLE_ALARM_APP_ACTIVITY") ?? DefaultMainActivity);
            // Don't reinstall on each session if the APK is already on the device — saves ~30s.
            options.AddAdditionalAppiumOption("noReset", true);
        }

        // MAUI cold-start on a fresh emulator is significantly slower than a real device.
        options.AddAdditionalAppiumOption("appWaitDuration", 60_000);
        options.AddAdditionalAppiumOption("newCommandTimeout", 120);

        var deviceUdid = Environment.GetEnvironmentVariable("ANDROID_UDID");
        if (!string.IsNullOrWhiteSpace(deviceUdid))
        {
            options.AddAdditionalAppiumOption("udid", deviceUdid);
        }

        return options;
    }

    protected override AppiumDriver CreateDriver(Uri serverUrl, AppiumOptions options)
        => new AndroidDriver(serverUrl, options);
}
