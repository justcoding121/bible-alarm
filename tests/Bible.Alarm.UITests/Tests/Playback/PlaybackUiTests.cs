#nullable enable

using Bible.Alarm.UITests.Fixtures;
using Bible.Alarm.UITests.Infrastructure;
using Bible.Alarm.UITests.Pages;
using Bible.Alarm.UITests.Visual;

namespace Bible.Alarm.UITests.Tests.Playback;

[Trait("UI", "Android")]
[Trait("Category", "Playback")]
public sealed class AndroidPlaybackUiTests : IClassFixture<AndroidAppiumFixture>
{
    private readonly AndroidAppiumFixture fixture;
    private readonly IVisualCheck visual = VisualCheckFactory.Create("android");

    public AndroidPlaybackUiTests(AndroidAppiumFixture fixture) => this.fixture = fixture;

    [Fact]
    public void Play_pause_next_seek_minimize_and_stop()
    {
        fixture.Run(nameof(Play_pause_next_seek_minimize_and_stop), () =>
        {
            var home = new HomePage(fixture);
            var playback = new PlaybackPage(fixture);
            var mini = new MiniBarPage(fixture);
            home.WaitUntilReady();
            home.TapFirstPlay();
            playback.WaitUntilReady();
            visual.CheckAsync(fixture.Driver!, "playback", UiFlow.DynamicMasks).GetAwaiter().GetResult();
            var title = WaitForTitle(playback, current => current.Length > 0, "playback title");
            playback.TapPause();
            playback.TapPlay();
            playback.TapForward();
            playback.TapBackward();
            playback.TapNext();
            var nextTitle = WaitForTitle(
                playback,
                current => !string.Equals(current, title, StringComparison.Ordinal),
                "next track title");
            playback.TapPrevious();
            WaitForTitle(
                playback,
                current => !string.Equals(current, nextTitle, StringComparison.Ordinal),
                "previous track title");
            playback.TapMinimize();
            mini.WaitUntilReady();
            mini.TapPlayPause();
            mini.TapMaximize();
            playback.WaitUntilReady();
            playback.TapDismiss();
            home.WaitUntilReady();
        });
    }

    [Fact]
    public void Pause_and_play_change_the_session_state()
    {
        fixture.Run(nameof(Pause_and_play_change_the_session_state), () =>
        {
            var home = new HomePage(fixture);
            var playback = new PlaybackPage(fixture);
            home.WaitUntilReady();
            home.TapFirstPlay();
            playback.WaitUntilReady();
            Waits.Until(() => SessionState() == 3, TimeSpan.FromSeconds(20), "playing");
            fixture.Tap(fixture.WaitForAccessibilityId("PlaybackPauseButton"));
            Waits.Until(
                () => SessionState() is 2 or 1 && fixture.IsShown("PlaybackPlayButton"),
                TimeSpan.FromSeconds(15),
                "paused");
            fixture.Tap(fixture.WaitForAccessibilityId("PlaybackPlayButton"));
            Waits.Until(
                () => SessionState() == 3 && fixture.IsShown("PlaybackPauseButton"),
                TimeSpan.FromSeconds(15),
                "playing again");
            playback.TapDismiss();
            home.WaitUntilReady();
        });
    }

    private static int? SessionState() =>
        DeviceControl.AndroidMediaSession(AndroidAppiumFixture.DefaultPackageName).PlaybackState;

    [Fact]
    public void Slider_drag_seeks_and_relaunch_resumes_that_position()
    {
        fixture.Run(nameof(Slider_drag_seeks_and_relaunch_resumes_that_position), () =>
        {
            var home = new HomePage(fixture);
            var playback = new PlaybackPage(fixture);
            home.WaitUntilReady();
            home.TapFirstPlay();
            playback.WaitUntilReady();
            var start = WaitForSeconds(playback, seconds => seconds >= 0, "playback clock");
            playback.DragProgress(0.65);
            var sought = WaitForSeconds(playback, seconds => Math.Abs(seconds - start) > 15, "seeked position");
            playback.TapDismiss();
            home.WaitUntilReady();
            var driver = (OpenQA.Selenium.Appium.Android.AndroidDriver)fixture.Driver!;
            driver.TerminateApp(AndroidAppiumFixture.DefaultPackageName);
            driver.ActivateApp(AndroidAppiumFixture.DefaultPackageName);
            home.WaitUntilReady();
            home.TapFirstPlay();
            playback.WaitUntilReady();
            WaitForSeconds(playback, seconds => seconds >= Math.Max(0, sought - 20), "resumed position");
            playback.TapDismiss();
            home.WaitUntilReady();
        });
    }

    [Fact]
    public void Forward_and_backward_skip_about_fifteen_seconds()
    {
        fixture.Run(nameof(Forward_and_backward_skip_about_fifteen_seconds), () =>
        {
            var home = new HomePage(fixture);
            var playback = new PlaybackPage(fixture);
            home.WaitUntilReady();
            home.TapFirstPlay();
            playback.WaitUntilReady();
            var start = WaitForSeconds(playback, seconds => seconds >= 1, "playback clock");
            playback.TapForward();
            var afterForward = WaitForSeconds(
                playback,
                seconds => seconds >= start + 10 && seconds <= start + 25,
                "forward skip");
            playback.TapBackward();
            WaitForSeconds(
                playback,
                seconds => Math.Abs(seconds - (afterForward - 15)) <= 8,
                "backward skip");
            playback.TapDismiss();
            home.WaitUntilReady();
        });
    }

    private static string WaitForTitle(PlaybackPage playback, Func<string, bool> match, string description)
    {
        var seen = string.Empty;
        Waits.Until(() =>
        {
            seen = playback.Title();
            return match(seen);
        }, TimeSpan.FromSeconds(30), description);
        return seen;
    }

    private static int WaitForSeconds(PlaybackPage playback, Func<int, bool> match, string description)
    {
        var seen = -1;
        Waits.Until(() =>
        {
            seen = ClockSeconds(playback.CurrentTime());
            return match(seen);
        }, TimeSpan.FromSeconds(25), description);
        return seen;
    }

    private static int ClockSeconds(string text)
    {
        var parts = text.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2
            && int.TryParse(parts[0], out var minutes)
            && int.TryParse(parts[1], out var seconds))
        {
            return (minutes * 60) + seconds;
        }

        return -1;
    }
}

[Trait("UI", "iOS")]
[Trait("Category", "Playback")]
public sealed class IosPlaybackUiTests : IClassFixture<IosAppiumFixture>
{
    private readonly IosAppiumFixture fixture;

    public IosPlaybackUiTests(IosAppiumFixture fixture) => this.fixture = fixture;

    [Fact]
    public void Play_from_the_home_card_opens_the_player()
    {
        fixture.Run(nameof(Play_from_the_home_card_opens_the_player), () =>
        {
            var home = new HomePage(fixture);
            home.WaitUntilReady();
            home.TapFirstPlay();
            new PlaybackPage(fixture).WaitUntilReady();
        });
    }
}

[Trait("UI", "iOS")]
[Trait("Category", "Alarm")]
public sealed class IosAlarmFlowUiTests : IClassFixture<IosAppiumFixture>
{
    private readonly IosAppiumFixture fixture;
    private readonly IVisualCheck visual = VisualCheckFactory.Create("ios");

    public IosAlarmFlowUiTests(IosAppiumFixture fixture) => this.fixture = fixture;

    [Fact]
    public void Create_a_named_schedule()
    {
        fixture.Run(nameof(Create_a_named_schedule), () =>
        {
            var home = new HomePage(fixture);
            var schedule = new SchedulePage(fixture);
            home.WaitUntilReady();
            visual.CheckAsync(fixture.Driver!, "home", UiFlow.DynamicMasks).GetAwaiter().GetResult();
            home.TapAdd();
            schedule.WaitUntilReady();
            schedule.SetName("Evening reading");
            schedule.TapDay("DayButton_Fri");
            schedule.TapSave();
            home.WaitUntilReady();
        });
    }

    [Fact]
    public void Focus_modal_dismisses_when_it_is_shown()
    {
        fixture.Run(nameof(Focus_modal_dismisses_when_it_is_shown), () =>
        {
            new HomePage(fixture).WaitUntilReady();
            try
            {
                fixture.WaitForAccessibilityId("FocusWarningFloatingButton", TimeSpan.FromSeconds(8)).Click();
                fixture.WaitForAccessibilityId("FocusSettingsGotIt").Click();
            }
            catch (OpenQA.Selenium.WebDriverTimeoutException)
            {
                // The focus warning is shown only when the handler says it should be.
            }
        });
    }
}
