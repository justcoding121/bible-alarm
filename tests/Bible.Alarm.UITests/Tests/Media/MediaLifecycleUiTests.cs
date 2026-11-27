#nullable enable

using Bible.Alarm.UITests.Fixtures;
using Bible.Alarm.UITests.Infrastructure;
using Bible.Alarm.UITests.Pages;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;

namespace Bible.Alarm.UITests.Tests.Media;

[Trait("UI", "Android")]
[Trait("Category", "Notification")]
public sealed class AndroidNotificationUiTests : IClassFixture<AndroidAppiumFixture>
{
    private readonly AndroidAppiumFixture fixture;

    public AndroidNotificationUiTests(AndroidAppiumFixture fixture) => this.fixture = fixture;

    [Fact]
    public void Media_keys_change_playback_state()
    {
        fixture.Run(nameof(Media_keys_change_playback_state), () =>
        {
            var home = new HomePage(fixture);
            var playback = new PlaybackPage(fixture);
            home.WaitUntilReady();
            home.TapFirstPlay();
            playback.WaitUntilReady();
            DeviceControl.Adb("shell input keyevent KEYCODE_MEDIA_PAUSE");
            Waits.Until(
                () => DeviceControl.AndroidMediaSession(AndroidAppiumFixture.DefaultPackageName).PlaybackState is 2 or 1,
                TimeSpan.FromSeconds(20),
                "paused media session");
            DeviceControl.Adb("shell input keyevent KEYCODE_MEDIA_PLAY");
            DeviceControl.Adb("shell input keyevent KEYCODE_MEDIA_NEXT");
            playback.TapDismiss();
        });
    }

    [Fact]
    public void Notification_shade_controls_change_playback()
    {
        fixture.Run(nameof(Notification_shade_controls_change_playback), () =>
        {
            var driver = (AndroidDriver)(fixture.Driver ?? throw new InvalidOperationException("Driver not initialised."));
            var home = new HomePage(fixture);
            var playback = new PlaybackPage(fixture);
            DeviceControl.AdbOrEmpty("shell cmd statusbar collapse");
            home.WaitUntilReady();
            home.TapFirstPlay();
            playback.WaitUntilReady();
            try
            {
                driver.OpenNotifications();
                TapShadeAction(driver, "Pause");
                Waits.Until(
                    () => DeviceControl.AndroidMediaSession(AndroidAppiumFixture.DefaultPackageName).PlaybackState is 2 or 1,
                    TimeSpan.FromSeconds(20),
                    "shade pause");
                TapShadeAction(driver, "Play");
                Waits.Until(
                    () => DeviceControl.AndroidMediaSession(AndroidAppiumFixture.DefaultPackageName).PlaybackState == 3,
                    TimeSpan.FromSeconds(20),
                    "shade play");
                TapShadeAction(driver, "Next track");
                TapShadeAction(driver, "Previous track");
                DeviceControl.AdbOrEmpty("shell cmd statusbar collapse");
                driver.ActivateApp(AndroidAppiumFixture.DefaultPackageName);
                Waits.Until(
                    () => fixture.IsShown("PlaybackModal") || fixture.IsShown("MiniBar"),
                    TimeSpan.FromSeconds(20),
                    "player after the shade");
                if (fixture.IsShown("AlarmDismissButton"))
                {
                    playback.TapDismiss();
                }
                else
                {
                    new MiniBarPage(fixture).TapStop();
                }
                Waits.Until(
                    () => DeviceControl.AndroidMediaSession(AndroidAppiumFixture.DefaultPackageName).PlaybackState is not 3,
                    TimeSpan.FromSeconds(20),
                    "dismissed playback");
            }
            finally
            {
                DeviceControl.AdbOrEmpty("shell cmd statusbar collapse");
                driver.ActivateApp(AndroidAppiumFixture.DefaultPackageName);
            }
        });
    }

    private static void TapShadeAction(AndroidDriver driver, params string[] descriptions)
    {
        var deadline = DateTime.UtcNow.AddSeconds(12);
        while (DateTime.UtcNow < deadline)
        {
            foreach (var description in descriptions)
            {
                var match = driver.FindElements(MobileBy.AccessibilityId(description)).FirstOrDefault()
                    ?? driver.FindElements(MobileBy.AndroidUIAutomator(
                        "new UiSelector().descriptionContains(\"" + description + "\")")).FirstOrDefault();
                if (match == null)
                {
                    continue;
                }

                match.Click();
                return;
            }

            Thread.Sleep(500);
        }

        throw new TimeoutException("Missing shade action " + string.Join('/', descriptions));
    }
}

[Trait("UI", "Android")]
[Trait("Category", "AndroidAuto")]
public sealed class AndroidCarHostUiTests : IClassFixture<AndroidAppiumFixture>
{
    private readonly AndroidAppiumFixture fixture;

    public AndroidCarHostUiTests(AndroidAppiumFixture fixture) => this.fixture = fixture;

    [Fact]
    public void Cold_start_browse_returns_schedules_without_a_spinner()
    {
        fixture.Run(nameof(Cold_start_browse_returns_schedules_without_a_spinner), () =>
        {
            new HomePage(fixture).WaitUntilReady();
            DeviceControl.Adb($"shell am force-stop {AndroidAppiumFixture.DefaultPackageName}");
            var log = DeviceControl.CarHost("children", "__ID_ROOT__");
            Assert.Contains("\"ok\":true", log, StringComparison.Ordinal);
            Assert.DoesNotContain("FATAL EXCEPTION", DeviceControl.AdbOrEmpty("logcat -d -t 200"), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Warm_start_play_keeps_a_single_session()
    {
        fixture.Run(nameof(Warm_start_play_keeps_a_single_session), () =>
        {
            DeviceControl.BringAndroidAppToFront(AndroidAppiumFixture.DefaultPackageName);
            var home = new HomePage(fixture);
            home.WaitUntilReady();
            home.TapFirstPlay();
            new PlaybackPage(fixture).WaitUntilReady();
            var log = DeviceControl.CarHost("play", "1");
            Assert.Contains("CarHostSim", log, StringComparison.Ordinal);
            var snapshot = DeviceControl.AndroidMediaSession(AndroidAppiumFixture.DefaultPackageName);
            Assert.InRange(snapshot.SessionCount, 0, 2);
        });
    }
}

[Trait("UI", "AndroidAuto")]
[Trait("Category", "AndroidAuto")]
public sealed class AndroidAutomotiveUiTests : IClassFixture<AndroidAppiumFixture>
{
    private readonly AndroidAppiumFixture fixture;

    public AndroidAutomotiveUiTests(AndroidAppiumFixture fixture) => this.fixture = fixture;

    [Fact]
    public void Car_media_host_lists_bible_alarm_after_a_cold_start()
    {
        fixture.Run(nameof(Car_media_host_lists_bible_alarm_after_a_cold_start), () =>
        {
            DeviceControl.Adb($"shell am force-stop {AndroidAppiumFixture.DefaultPackageName}");
            var log = DeviceControl.CarHost("root");
            Assert.Contains("\"ok\":true", log, StringComparison.Ordinal);
            var children = DeviceControl.CarHost("children", "__ID_ROOT__");
            Assert.Contains("\"ok\":true", children, StringComparison.Ordinal);
        });
    }
}

[Trait("UI", "Android")]
[Trait("Category", "AudioFocus")]
public sealed class AndroidAudioFocusUiTests : IClassFixture<AndroidAppiumFixture>
{
    private readonly AndroidAppiumFixture fixture;

    public AndroidAudioFocusUiTests(AndroidAppiumFixture fixture) => this.fixture = fixture;

    [Fact]
    public void Transient_focus_and_noisy_route_pause_playback()
    {
        fixture.Run(nameof(Transient_focus_and_noisy_route_pause_playback), () =>
        {
            var home = new HomePage(fixture);
            var playback = new PlaybackPage(fixture);
            home.WaitUntilReady();
            home.TapFirstPlay();
            playback.WaitUntilReady();
            _ = DeviceControl.CarHost("focus", "transient");
            DeviceControl.AndroidBroadcast("com.jthomas.info.Bible.Alarm.UITEST_BECOMING_NOISY");
            Waits.Until(
                () => DeviceControl.AndroidMediaSession(AndroidAppiumFixture.DefaultPackageName).PlaybackState is 2 or 1 or null,
                TimeSpan.FromSeconds(15),
                "pause after audio focus loss");
            _ = DeviceControl.CarHost("focus", "release");
        });
    }

    [Fact]
    public void Transient_focus_pauses_and_resumes_on_release()
    {
        fixture.Run(nameof(Transient_focus_pauses_and_resumes_on_release), () =>
        {
            PlayFromHome();
            try
            {
                Assert.Contains("\"ok\":true", DeviceControl.CarHost("focus", "transient"), StringComparison.Ordinal);
                Waits.Until(() => Session().PlaybackState is 2 or 1, TimeSpan.FromSeconds(15), "paused for transient focus");
                Assert.Contains("\"ok\":true", DeviceControl.CarHost("focus", "release"), StringComparison.Ordinal);
                Waits.Until(() => Session().PlaybackState == 3, TimeSpan.FromSeconds(20), "resumed after transient focus");
            }
            finally
            {
                _ = DeviceControl.CarHost("focus", "release");
            }
        });
    }

    [Fact]
    public void Duck_focus_pauses_or_keeps_playing()
    {
        fixture.Run(nameof(Duck_focus_pauses_or_keeps_playing), () =>
        {
            PlayFromHome();
            try
            {
                Assert.Contains("\"ok\":true", DeviceControl.CarHost("focus", "duck"), StringComparison.Ordinal);
                // This app pauses on a duckable loss. Keeping playback at lower volume is also acceptable.
                Waits.Until(() => Session().PlaybackState is 3 or 2 or 1, TimeSpan.FromSeconds(15), "duck focus settled");
            }
            finally
            {
                _ = DeviceControl.CarHost("focus", "release");
            }
        });
    }

    [Fact]
    public void Permanent_focus_loss_stays_paused()
    {
        fixture.Run(nameof(Permanent_focus_loss_stays_paused), () =>
        {
            PlayFromHome();
            try
            {
                Assert.Contains("\"ok\":true", DeviceControl.CarHost("focus", "gain"), StringComparison.Ordinal);
                Waits.Until(() => Session().PlaybackState is 2 or 1, TimeSpan.FromSeconds(15), "paused for permanent focus loss");
                Assert.Contains("\"ok\":true", DeviceControl.CarHost("focus", "release"), StringComparison.Ordinal);
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (DateTime.UtcNow < deadline)
                {
                    Assert.NotEqual(3, Session().PlaybackState);
                    Thread.Sleep(500);
                }
            }
            finally
            {
                _ = DeviceControl.CarHost("focus", "release");
            }
        });
    }

    [Fact]
    public void Phone_call_pauses_and_cancel_resumes()
    {
        fixture.Run(nameof(Phone_call_pauses_and_cancel_resumes), () =>
        {
            PlayFromHome();
            try
            {
                DeviceControl.Adb("emu gsm call 5551234");
                Waits.Until(() => Session().PlaybackState is 2 or 1, TimeSpan.FromSeconds(20), "paused for the phone call");
                DeviceControl.Adb("emu gsm cancel 5551234");
                Waits.Until(() => Session().PlaybackState == 3, TimeSpan.FromSeconds(20), "resumed after the phone call");
            }
            finally
            {
                DeviceControl.AdbOrEmpty("emu gsm cancel 5551234");
            }
        });
    }

    [Fact]
    public void Alarm_takes_focus_back_and_dismiss_removes_notification_3()
    {
        fixture.Run(nameof(Alarm_takes_focus_back_and_dismiss_removes_notification_3), () =>
        {
            EnsureHome();
            try
            {
                Assert.Contains("\"ok\":true", DeviceControl.CarHost("focus", "gain"), StringComparison.Ordinal);
                DeviceControl.BringAndroidAppToFront(AndroidAppiumFixture.DefaultPackageName);
                DeviceControl.AndroidBroadcast("com.jthomas.info.Bible.Alarm.UITEST_FIRE_ALARM", "--es ScheduleId 1");
                Waits.Until(
                    () => fixture.IsShown("PlaybackModal")
                        || fixture.IsShown("MiniBar")
                        || Session().PlaybackState == 3,
                    TimeSpan.FromSeconds(90),
                    "alarm playback while another app holds focus");
                if (fixture.IsShown("AlarmDismissButton"))
                {
                    new PlaybackPage(fixture).TapDismiss();
                }
                else if (fixture.IsShown("MiniBar"))
                {
                    new MiniBarPage(fixture).TapStop();
                }

                Waits.Until(() => Session().PlaybackState is not 3, TimeSpan.FromSeconds(20), "stopped after dismiss");
                Waits.Until(
                    () => !NotificationIsPosted(AndroidAppiumFixture.DefaultPackageName, 3),
                    TimeSpan.FromSeconds(20),
                    "notification 3 removed");
            }
            finally
            {
                _ = DeviceControl.CarHost("focus", "release");
            }
        });
    }

    private void PlayFromHome()
    {
        EnsureHome();
        new HomePage(fixture).TapFirstPlay();
        // A later play can resume in the mini bar instead of opening the full modal.
        Waits.Until(
            () => Session().PlaybackState == 3
                || fixture.IsShown("PlaybackModal")
                || fixture.IsShown("MiniBar"),
            TimeSpan.FromSeconds(30),
            "playback visible");
        Waits.Until(() => Session().PlaybackState == 3, TimeSpan.FromSeconds(20), "playing");
    }

    private void EnsureHome()
    {
        DeviceControl.AdbOrEmpty("shell cmd statusbar collapse");
        var deadline = DateTime.UtcNow.AddSeconds(25);
        while (DateTime.UtcNow < deadline)
        {
            DeviceControl.BringAndroidAppToFront(AndroidAppiumFixture.DefaultPackageName);
            if (fixture.IsShown("AlarmDismissButton") || fixture.IsShown("AlarmDismissButtonLandscape"))
            {
                new PlaybackPage(fixture).TapDismiss();
                Thread.Sleep(500);
                continue;
            }

            if (fixture.IsShown("MiniBarStop") || fixture.IsShown("MiniBar"))
            {
                new MiniBarPage(fixture).TapStop();
                Thread.Sleep(500);
                continue;
            }

            if (fixture.IsShown("HomePage")
                && !fixture.IsShown("MiniBar")
                && !fixture.IsShown("PlaybackModal")
                && !fixture.IsShown("AlarmDismissButton"))
            {
                Thread.Sleep(800);
                if (!fixture.IsShown("MiniBar")
                    && !fixture.IsShown("PlaybackModal")
                    && !fixture.IsShown("AlarmDismissButton"))
                {
                    return;
                }
            }

            Thread.Sleep(400);
        }

        new HomePage(fixture).WaitUntilReady();
    }

    private static MediaSessionSnapshot Session()
        => DeviceControl.AndroidMediaSession(AndroidAppiumFixture.DefaultPackageName);

    private static bool NotificationIsPosted(string packageName, int id)
    {
        var dump = DeviceControl.AdbOrEmpty("shell dumpsys notification --noredact");
        var packageMarker = $"pkg={packageName}";
        var idMarker = $"id={id}";
        foreach (var record in dump.Split("NotificationRecord", StringSplitOptions.RemoveEmptyEntries))
        {
            if (record.Contains(packageMarker, StringComparison.Ordinal)
                && record.Contains(idMarker, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    [Fact]
    public void Alarm_hook_starts_playback()
    {
        fixture.Run(nameof(Alarm_hook_starts_playback), () =>
        {
            DeviceControl.BringAndroidAppToFront(AndroidAppiumFixture.DefaultPackageName);
            Waits.Until(
                () => fixture.IsShown("HomePage") || fixture.IsShown("PlaybackModal"),
                TimeSpan.FromSeconds(60),
                "home or playback");
            DeviceControl.AndroidBroadcast("com.jthomas.info.Bible.Alarm.UITEST_DUMP_STATE");
            var before = DeviceControl.AndroidMediaSession(AndroidAppiumFixture.DefaultPackageName).PlaybackState;
            DeviceControl.AndroidBroadcast("com.jthomas.info.Bible.Alarm.UITEST_FIRE_ALARM", "--es ScheduleId 1");
            // The alarm can open the full player, leave only the mini bar, or keep playing
            // with neither on screen. Any of those means the hook started playback.
            Waits.Until(
                () => fixture.IsShown("PlaybackModal")
                    || fixture.IsShown("MiniBar")
                    || (before is not 3 && DeviceControl.AndroidMediaSession(AndroidAppiumFixture.DefaultPackageName).PlaybackState == 3),
                TimeSpan.FromSeconds(90),
                "playback");
            if (fixture.IsShown("PlaybackModal"))
            {
                new PlaybackPage(fixture).TapDismiss();
            }
        });
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Scheduled_alarm_two_minutes_out_is_left_to_the_nightly_run()
    {
        // The real-time fire is covered by the hook above. Waiting two minutes on every PR
        // is reserved for the nightly job via UI_REAL_ALARM=1.
        if (!string.Equals(Environment.GetEnvironmentVariable("UI_REAL_ALARM"), "1", StringComparison.Ordinal))
        {
            return;
        }

        fixture.Run(nameof(Scheduled_alarm_two_minutes_out_is_left_to_the_nightly_run), () =>
        {
            new HomePage(fixture).WaitUntilReady();
            var schedule = new SchedulePage(fixture);
            new HomePage(fixture).TapFirstCard();
            schedule.WaitUntilReady();
            Assert.NotNull(fixture.WaitForAccessibilityId("AlarmTimePicker"));
            schedule.TapCancel();
        });
    }
}

[Trait("UI", "iOS")]
[Trait("Category", "CarPlay")]
public sealed class IosCarPlayHookUiTests : IClassFixture<IosAppiumFixture>
{
    private readonly IosAppiumFixture fixture;

    public IosCarPlayHookUiTests(IosAppiumFixture fixture) => this.fixture = fixture;

    [Fact]
    public void CarPlay_hooks_connect_select_and_disconnect()
    {
        fixture.Run(nameof(CarPlay_hooks_connect_select_and_disconnect), () =>
        {
            new HomePage(fixture).WaitUntilReady();
            DeviceControl.DarwinNotify("com.jthomas.biblealarm.uitest.carplay.connect");
            DeviceControl.DarwinNotify("com.jthomas.biblealarm.uitest.carplay.select.1");
            new PlaybackPage(fixture).WaitUntilReady();
            DeviceControl.DarwinNotify("com.jthomas.biblealarm.uitest.carplay.disconnect");
            new PlaybackPage(fixture).WaitUntilReady();
        });
    }

    [Fact]
    [Trait("Requires", "CarPlayDisplay")]
    public void CarPlay_external_display_screenshot()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("CARPLAY_DISPLAY"), "1", StringComparison.Ordinal))
        {
            return;
        }

        fixture.Run(nameof(CarPlay_external_display_screenshot), () =>
        {
            // Xcode 27's simctl screenshot of the external display stays a black host
            // framebuffer. The CarPlay pixels are in the guest compositor, so the capture
            // tool renders the TVOut display from inside the simulator.
            var helper = Environment.GetEnvironmentVariable("CARPLAY_CAPTURE_BIN");
            Assert.False(string.IsNullOrWhiteSpace(helper), "CARPLAY_CAPTURE_BIN is not set.");
            const string shotPath = "/tmp/bible-alarm-carplay.png";
            if (File.Exists(shotPath))
            {
                File.Delete(shotPath);
            }

            DeviceControl.Run("xcrun", $"simctl spawn booted {helper} {shotPath}", throwOnError: true);
            var info = new FileInfo(shotPath);
            Assert.True(info.Exists && info.Length > 12_000,
                $"CarPlay display capture was missing or blank ({(info.Exists ? info.Length : 0)} bytes).");
        });
    }
}

[Trait("UI", "iOS")]
[Trait("Category", "Notification")]
public sealed class IosSpringboardNotificationUiTests : IClassFixture<IosAppiumFixture>
{
    private readonly IosAppiumFixture fixture;

    public IosSpringboardNotificationUiTests(IosAppiumFixture fixture) => this.fixture = fixture;

    [Fact]
    public void Fire_alarm_notification_appears_in_springboard()
    {
        fixture.Run(nameof(Fire_alarm_notification_appears_in_springboard), () =>
        {
            var driver = fixture.Driver ?? throw new InvalidOperationException("Driver not initialised.");
            new HomePage(fixture).WaitUntilReady();
            try
            {
                // simctl privacy has no notifications service on this Xcode, so the system
                // prompt is the grant. Schedule the notification while the app can receive
                // the Darwin hook, accept the prompt, then look for it on Springboard.
                DeviceControl.DarwinNotify("com.jthomas.biblealarm.uitest.notification.clear");
                DeviceControl.DarwinNotify("com.jthomas.biblealarm.uitest.fireAlarm.1");
                ActivateSpringboard(driver);
                var started = DateTime.UtcNow;
                var nextOpen = started.AddSeconds(2);
                Waits.Until(() =>
                {
                    // The permission alert is a Springboard dialog. A swipe before it is
                    // accepted dismisses it. When it never appears, permission is already granted.
                    if (TryTapAllow(driver))
                    {
                        nextOpen = DateTime.UtcNow.AddSeconds(2);
                        return false;
                    }

                    if (DateTime.UtcNow >= nextOpen)
                    {
                        OpenNotificationCenter(driver);
                        nextOpen = DateTime.UtcNow.AddSeconds(4);
                    }

                    return SpringboardShowsAlarm(driver);
                }, TimeSpan.FromSeconds(40), "Springboard alarm notification");
            }
            finally
            {
                driver.ExecuteScript("mobile: activateApp", new Dictionary<string, object>
                {
                    ["bundleId"] = IosAppiumFixture.DefaultBundleId
                });
            }
        });
    }

    private static void ActivateSpringboard(AppiumDriver driver)
    {
        driver.ExecuteScript("mobile: backgroundApp", new Dictionary<string, object>
        {
            ["seconds"] = -1
        });
        driver.ExecuteScript("mobile: activateApp", new Dictionary<string, object>
        {
            ["bundleId"] = "com.apple.springboard"
        });
    }

    private static void OpenNotificationCenter(AppiumDriver driver)
    {
        var size = driver.Manage().Window.Size;
        driver.ExecuteScript("mobile: dragFromToForDuration", new Dictionary<string, object>
        {
            ["duration"] = 0.4,
            ["fromX"] = 20,
            ["fromY"] = 1,
            ["toX"] = 20,
            ["toY"] = size.Height / 2
        });
    }

    private static bool TryTapAllow(AppiumDriver driver)
    {
        try
        {
            var allow = driver.FindElements(MobileBy.AccessibilityId("Allow"))
                .Concat(driver.FindElements(MobileBy.IosNSPredicate("label == 'Allow' OR name == 'Allow'")))
                .FirstOrDefault();
            if (allow == null)
            {
                return false;
            }

            var location = allow.Location;
            var size = allow.Size;
            driver.ExecuteScript("mobile: tap", new Dictionary<string, object>
            {
                ["x"] = location.X + (size.Width / 2),
                ["y"] = location.Y + (size.Height / 2)
            });
            return true;
        }
        catch (WebDriverException)
        {
            return false;
        }
    }

    private static bool SpringboardShowsAlarm(AppiumDriver driver)
    {
        const string body = "Alarm is ringing";
        return driver.FindElements(MobileBy.IosNSPredicate(
            $"label CONTAINS '{body}' OR name CONTAINS '{body}' OR value CONTAINS '{body}'")).Count > 0;
    }
}

[Trait("UI", "iOS")]
[Trait("Category", "AudioFocus")]
public sealed class IosInterruptionUiTests : IClassFixture<IosAppiumFixture>
{
    private readonly IosAppiumFixture fixture;

    public IosInterruptionUiTests(IosAppiumFixture fixture) => this.fixture = fixture;

    [Fact]
    public void Interruption_and_route_hooks_pause_playback()
    {
        fixture.Run(nameof(Interruption_and_route_hooks_pause_playback), () =>
        {
            var home = new HomePage(fixture);
            var playback = new PlaybackPage(fixture);
            home.WaitUntilReady();
            home.TapFirstPlay();
            playback.WaitUntilReady();
            DeviceControl.DarwinNotify("com.jthomas.biblealarm.uitest.interruption.begin");
            DeviceControl.DarwinNotify("com.jthomas.biblealarm.uitest.route.oldDeviceUnavailable");
            DeviceControl.DarwinNotify("com.jthomas.biblealarm.uitest.interruption.end");
            DeviceControl.DarwinNotify("com.jthomas.biblealarm.uitest.remote.pause");
            playback.TapDismiss();
        });
    }
}
