#nullable enable

using Bible.Alarm.UITests.Fixtures;
using Bible.Alarm.UITests.Infrastructure;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace Bible.Alarm.UITests.Pages;

public sealed class HomePage
{
    private readonly AppiumFixtureBase fixture;

    public HomePage(AppiumFixtureBase fixture) => this.fixture = fixture;

    public void WaitUntilReady() => fixture.WaitForAccessibilityId("HomePage", TimeSpan.FromSeconds(60));

    public void TapAdd() => fixture.WaitForAccessibilityId("AddScheduleButton").Click();

    public void TapFirstPlay() => fixture.WaitForAccessibilityId("ScheduleListPlayButton").Click();

    public void TapFirstCard() => fixture.WaitForAccessibilityId("ScheduleCard").Click();

    public string FirstName() => fixture.WaitForAccessibilityId("ScheduleName").Text ?? string.Empty;

    public void ToggleFirst() => fixture.WaitForAccessibilityId("ScheduleEnableSwitch").Click();
}

public sealed class SchedulePage
{
    private readonly AppiumFixtureBase fixture;

    public SchedulePage(AppiumFixtureBase fixture) => this.fixture = fixture;

    public void WaitUntilReady() => fixture.WaitForAccessibilityId("SchedulePage", TimeSpan.FromSeconds(45));

    public void SetName(string name)
    {
        var entry = fixture.WaitForAccessibilityId("ScheduleNameEntry");
        entry.Clear();
        entry.SendKeys(name);
    }

    public void TapDay(string id) => fixture.WaitForAccessibilityId(id).Click();

    public void TapCategory() => fixture.WaitForAccessibilityId("CategoryRow").Click();

    public void TapSave() => fixture.WaitForAccessibilityId("SaveButton").Click();

    public void TapCancel() => fixture.WaitForAccessibilityId("CancelButton").Click();

    public void TapDelete() => fixture.WaitForAccessibilityId("DeleteScheduleButton").Click();

    public void TapIndefinite() => fixture.WaitForAccessibilityId("IndefiniteRow").Click();

    public void TapMusic() => fixture.WaitForAccessibilityId("MusicEnabledRow").Click();
}

public sealed class SelectionModal
{
    private readonly AppiumFixtureBase fixture;

    public SelectionModal(AppiumFixtureBase fixture) => this.fixture = fixture;

    public void WaitUntilReady() => fixture.WaitForAccessibilityId("SelectionList");

    public void Close() => fixture.WaitForAccessibilityId("ModalCloseButton").Click();

    public void TypeLanguageSearch(string text)
    {
        var search = fixture.WaitForAccessibilityId("LanguageSearchBar");
        search.Clear();
        search.SendKeys(text);
    }
}

public sealed class PlaybackPage
{
    private readonly AppiumFixtureBase fixture;

    public PlaybackPage(AppiumFixtureBase fixture) => this.fixture = fixture;

    public void WaitUntilReady() => fixture.WaitForAccessibilityId("PlaybackModal", TimeSpan.FromSeconds(90));

    public string Title() => fixture.WaitForAccessibilityId("PlaybackTitle", TimeSpan.FromSeconds(45)).Text ?? string.Empty;

    public void TapPlay() => ClickIfPresent("PlaybackPlayButton");

    public void TapPause() => ClickIfPresent("PlaybackPauseButton");

    public void TapNext() => fixture.WaitForAccessibilityId("PlaybackNextButton").Click();

    public void TapPrevious() => fixture.WaitForAccessibilityId("PlaybackPreviousButton").Click();

    public void TapForward() => fixture.WaitForAccessibilityId("PlaybackForwardButton").Click();

    public void TapBackward() => fixture.WaitForAccessibilityId("PlaybackBackwardButton").Click();

    public void TapDismiss() => fixture.WaitForAccessibilityId("AlarmDismissButton").Click();

    public void TapMinimize() => fixture.WaitForAccessibilityId("PlaybackMinimizeButton").Click();

    private void ClickIfPresent(string id)
    {
        try
        {
            fixture.WaitForAccessibilityId(id, TimeSpan.FromSeconds(8)).Click();
        }
        catch (WebDriverTimeoutException)
        {
            // The other of play/pause is showing.
        }
    }
}

public sealed class MiniBarPage
{
    private readonly AppiumFixtureBase fixture;

    public MiniBarPage(AppiumFixtureBase fixture) => this.fixture = fixture;

    public void WaitUntilReady() => fixture.WaitForAccessibilityId("MiniBar");

    public void TapPlayPause() => fixture.WaitForAccessibilityId("MiniBarPlayPause").Click();

    public void TapStop() => fixture.WaitForAccessibilityId("MiniBarStop").Click();

    public void TapMaximize() => fixture.WaitForAccessibilityId("MiniBarMaximize").Click();
}

public static class UiFlow
{
    public static readonly string[] DynamicMasks =
    [
        "PlaybackCurrentTime",
        "PlaybackEndTime",
        "PlaybackProgressSlider",
        "MiniBarProgress",
        "ScheduleTimeText"
    ];

    public static void UntilHome(AppiumFixtureBase fixture) =>
        Waits.Until(() =>
        {
            try
            {
                return fixture.Driver?.FindElement(MobileBy.AccessibilityId("HomePage")).Displayed == true;
            }
            catch (NoSuchElementException)
            {
                return false;
            }
        }, TimeSpan.FromSeconds(60), "home page");
}
