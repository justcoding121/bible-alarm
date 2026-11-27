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
public class AndroidAppiumFixture : AppiumFixtureBase
{
    public const string DefaultPackageName = "com.jthomas.info.Bible.Alarm";
    public const string DefaultMainActivity = "crc644dd66dffaf5cb9c0.MainActivity";

    protected virtual bool GrantPermissions =>
        !string.Equals(Environment.GetEnvironmentVariable("UI_DENY_PERMISSIONS"), "1", StringComparison.Ordinal);

    protected virtual bool ResetBeforeSession =>
        !string.Equals(Environment.GetEnvironmentVariable("UI_RESET"), "0", StringComparison.Ordinal);

    public override Task InitializeAsync()
    {
        Bible.Alarm.UITests.Infrastructure.DeviceControl.EnableAndroidWindowAnimations();

        if (ResetBeforeSession)
        {
            Bible.Alarm.UITests.Infrastructure.DeviceControl.TryClearAndroidPackage(
                Environment.GetEnvironmentVariable("BIBLE_ALARM_APP_PACKAGE") ?? DefaultPackageName);
        }

        return base.InitializeAsync();
    }

    protected override string CollectDiagnostics()
    {
        var packageName = Environment.GetEnvironmentVariable("BIBLE_ALARM_APP_PACKAGE") ?? DefaultPackageName;
        return Bible.Alarm.UITests.Infrastructure.DeviceControl.TryAndroidDiagnostics(packageName);
    }

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
            var packageName = Environment.GetEnvironmentVariable("BIBLE_ALARM_APP_PACKAGE") ?? DefaultPackageName;
            options.AddAdditionalAppiumOption("appPackage", packageName);
            options.AddAdditionalAppiumOption("appActivity", ResolveMainActivity(packageName));
            // Don't reinstall on each session if the APK is already on the device — saves ~30s.
            options.AddAdditionalAppiumOption("noReset", true);
        }

        // MAUI cold-start on a fresh emulator is significantly slower than a real device.
        options.AddAdditionalAppiumOption("appWaitDuration", 60_000);
        options.AddAdditionalAppiumOption("newCommandTimeout", 180);
        options.AddAdditionalAppiumOption("autoGrantPermissions", GrantPermissions);
        // The startup spinner keeps the window from going idle. A non-zero idle wait then fails every find.
        options.AddAdditionalAppiumOption("settings[waitForIdleTimeout]", 0);

        var deviceUdid = Environment.GetEnvironmentVariable("ANDROID_UDID");
        if (!string.IsNullOrWhiteSpace(deviceUdid))
        {
            options.AddAdditionalAppiumOption("udid", deviceUdid);
        }

        return options;
    }

    protected override AppiumDriver CreateDriver(Uri serverUrl, AppiumOptions options)
        => new AndroidDriver(serverUrl, options);

    private static string ResolveMainActivity(string packageName)
    {
        var configured = Environment.GetEnvironmentVariable("BIBLE_ALARM_APP_ACTIVITY");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        // The MAUI activity class name includes a hash that changes between builds.
        var output = Bible.Alarm.UITests.Infrastructure.DeviceControl.AdbOrEmpty(
            $"shell cmd package resolve-activity --brief -c android.intent.category.LAUNCHER {packageName}");
        foreach (var line in output.Split('\r', '\n'))
        {
            var slash = line.LastIndexOf('/');
            if (slash >= 0 && slash < line.Length - 1)
            {
                return line[(slash + 1)..].Trim();
            }
        }

        return DefaultMainActivity;
    }
}
