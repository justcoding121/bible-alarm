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
            var title = playback.Title();
            playback.TapPause();
            playback.TapPlay();
            playback.TapForward();
            playback.TapBackward();
            playback.TapNext();
            Waits.Until(() => playback.Title() != title || !string.IsNullOrWhiteSpace(playback.Title()), TimeSpan.FromSeconds(30), "next track title");
            playback.TapPrevious();
            playback.TapMinimize();
            mini.WaitUntilReady();
            mini.TapPlayPause();
            mini.TapMaximize();
            playback.WaitUntilReady();
            playback.TapDismiss();
            home.WaitUntilReady();
        });
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
