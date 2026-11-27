#nullable enable

using Bible.Alarm.UITests.Fixtures;
using Bible.Alarm.UITests.Infrastructure;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;

namespace Bible.Alarm.UITests.Pages;

public sealed class HomePage
{
    private readonly AppiumFixtureBase fixture;

    public HomePage(AppiumFixtureBase fixture) => this.fixture = fixture;

    public void WaitUntilReady() => fixture.WaitForAccessibilityId("HomePage", TimeSpan.FromSeconds(60));

    public void TapAdd()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Driver not initialised; did InitializeAsync run?");
        // WinUI and Android each declare AddScheduleButton. The Android control is the later one.
        var button = driver.FindElements(By.Id("AddScheduleButton"))
            .LastOrDefault(element => element.Displayed && element.Size.Height > 0);
        if (button == null)
        {
            fixture.Tap(fixture.WaitForAccessibilityId("AddScheduleButton"));
            return;
        }

        fixture.Tap(button);
    }

    public void TapFirstPlay() => fixture.WaitForAccessibilityId("ScheduleListPlayButton").Click();

    public void TapFirstCard() => fixture.WaitForAccessibilityId("ScheduleCard").Click();

    public string FirstName() => fixture.WaitForAccessibilityId("ScheduleName").Text ?? string.Empty;

    public IReadOnlyList<string> Names()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Driver not initialised; did InitializeAsync run?");
        return driver.FindElements(By.Id("ScheduleName"))
            .Select(element => element.Text ?? string.Empty)
            .ToList();
    }

    public void TapCardNamed(string text)
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Driver not initialised; did InitializeAsync run?");
        var match = driver.FindElements(By.Id("ScheduleName"))
            .First(element => element.Text?.Contains(text, StringComparison.OrdinalIgnoreCase) == true);
        fixture.Tap(match);
    }

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
        entry.Click();
        entry.Clear();
        entry.SendKeys(name);
    }

    public void TapDay(string id) => fixture.WaitForAccessibilityId(id).Click();

    public void TapCategory() => fixture.WaitForAccessibilityId("CategoryRow").Click();

    public void TapSave() => fixture.WaitForAccessibilityId("SaveButton").Click();

    public void TapCancel() => fixture.WaitForAccessibilityId("CancelButton").Click();

    public void TapDelete()
    {
        if (fixture.Driver is AndroidDriver driver)
        {
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var shown = driver.FindElements(By.Id("DeleteScheduleButton"))
                    .FirstOrDefault(element => element.Displayed);
                if (shown != null)
                {
                    fixture.Tap(shown);
                    return;
                }

                // The delete control is below the schedule form, and the MAUI ScrollView is not always marked scrollable.
                driver.ExecuteScript("mobile: swipeGesture", new Dictionary<string, object>
                {
                    ["left"] = 100,
                    ["top"] = 500,
                    ["width"] = 800,
                    ["height"] = 1200,
                    ["direction"] = "up",
                    ["percent"] = 0.7
                });
            }
        }

        fixture.Tap(fixture.WaitForAccessibilityId("DeleteScheduleButton"));
    }

    public void TapIndefinite() => TapRow("IndefiniteRow");

    public void TapMusic() => TapRow("MusicEnabledRow");

    public void TapRow(string accessibilityId)
    {
        ScrollTo(accessibilityId);
        fixture.Tap(fixture.WaitForAccessibilityId(accessibilityId));
    }

    public void ScrollTo(string accessibilityId)
    {
        if (fixture.IsShown(accessibilityId))
        {
            return;
        }

        if (fixture.Driver is not AndroidDriver driver)
        {
            fixture.WaitForAccessibilityId(accessibilityId);
            return;
        }

        for (var attempt = 0; attempt < 6; attempt++)
        {
            driver.ExecuteScript("mobile: swipeGesture", new Dictionary<string, object>
            {
                ["left"] = 100,
                ["top"] = 500,
                ["width"] = 800,
                ["height"] = 1200,
                ["direction"] = "up",
                ["percent"] = 0.55
            });
            if (fixture.IsShown(accessibilityId))
            {
                return;
            }
        }

        fixture.WaitForAccessibilityId(accessibilityId);
    }
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

    public void RevealText(string text) => FindExactText(text);

    public void TapText(string text) => fixture.Tap(FindExactText(text));

    private IWebElement FindExactText(string text)
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Driver not initialised; did InitializeAsync run?");
        for (var attempt = 0; attempt < 14; attempt++)
        {
            var match = driver.FindElements(MobileBy.AndroidUIAutomator("new UiSelector().text(\"" + text + "\")"))
                .FirstOrDefault(element => element.Displayed && element.Location.Y > 250 && element.Location.Y < 2000);
            if (match != null)
            {
                return match;
            }

            driver.ExecuteScript("mobile: swipeGesture", new Dictionary<string, object>
            {
                ["left"] = 80,
                ["top"] = 400,
                ["width"] = 900,
                ["height"] = 1100,
                ["direction"] = "up",
                ["percent"] = 0.45
            });
        }

        throw new TimeoutException($"Could not find \"{text}\".");
    }

    public bool ShowsText(string text)
    {
        var driver = fixture.Driver;
        if (driver == null)
        {
            return false;
        }

        return driver.FindElements(MobileBy.AndroidUIAutomator(
                "new UiSelector().textContains(\"" + text + "\")"))
            .Any(element => element.Displayed);
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

    public string CurrentTime() => fixture.WaitForAccessibilityId("PlaybackCurrentTime").Text ?? string.Empty;

    public void DragProgress(double endFraction)
    {
        var slider = fixture.WaitForAccessibilityId("PlaybackProgressSlider");
        if (fixture.Driver is not AndroidDriver driver)
        {
            throw new InvalidOperationException("Slider drag is implemented for the Android driver.");
        }

        var location = slider.Location;
        var size = slider.Size;
        var y = location.Y + (size.Height / 2);
        driver.ExecuteScript("mobile: dragGesture", new Dictionary<string, object>
        {
            ["startX"] = location.X + Math.Max(8, size.Width / 10),
            ["startY"] = y,
            ["endX"] = location.X + (int)(size.Width * endFraction),
            ["endY"] = y,
            ["speed"] = 900
        });
    }

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
        fixture.WaitForAccessibilityId("HomePage", TimeSpan.FromSeconds(60));
}
