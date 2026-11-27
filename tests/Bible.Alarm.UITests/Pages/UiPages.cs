#nullable enable

using System.Text.RegularExpressions;
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

    public string FirstTime() => fixture.WaitForAccessibilityId("ScheduleTimeText").Text ?? string.Empty;

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

    public void SetTime(int hour, int minute)
    {
        fixture.Tap(fixture.WaitForAccessibilityId("AlarmTimePicker"));
        var driver = fixture.Driver as AndroidDriver
            ?? throw new InvalidOperationException("Time entry is implemented for the Android driver.");
        Waits.Until(
            () => driver.FindElements(By.Id("android:id/radial_picker")).Any(element => element.Displayed),
            TimeSpan.FromSeconds(10),
            "time dialog");
        TapClockValue(driver, hour.ToString());
        var minuteLabels = minute == 0
            ? new[] { "0", "00" }
            : new[] { minute.ToString("00"), minute.ToString() };
        if (!ClockValueShown(driver, minuteLabels))
        {
            var minuteHeader = driver.FindElements(By.Id("android:id/minutes")).FirstOrDefault(element => element.Displayed);
            if (minuteHeader != null)
            {
                fixture.Tap(minuteHeader);
            }
        }

        Waits.Until(() => ClockValueShown(driver, minuteLabels), TimeSpan.FromSeconds(5), "minute dial");
        TapClockValue(driver, minuteLabels.First(label => ClockValueShown(driver, [label])));
        driver.FindElements(By.Id("android:id/button1")).First(element => element.Displayed).Click();
        Waits.Until(
            () => !driver.FindElements(By.Id("android:id/radial_picker")).Any(element => element.Displayed),
            TimeSpan.FromSeconds(5),
            "time dialog closed");
    }

    private static bool ClockValueShown(AndroidDriver driver, IEnumerable<string> values)
        => values.Any(value => driver.FindElements(MobileBy.AccessibilityId(value)).Any(element => element.Displayed));

    private void TapClockValue(AndroidDriver driver, string value)
    {
        var target = driver.FindElements(MobileBy.AccessibilityId(value)).First(element => element.Displayed);
        fixture.Tap(target);
    }

    public void DismissTimeDialog()
    {
        if (fixture.Driver is not AndroidDriver driver)
        {
            return;
        }

        var cancel = driver.FindElements(By.Id("android:id/button2")).FirstOrDefault(element => element.Displayed);
        cancel?.Click();
    }

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

    public string RowLabel(string accessibilityId)
    {
        var row = fixture.WaitForAccessibilityId(accessibilityId);
        var driver = fixture.Driver ?? throw new InvalidOperationException("Driver not initialised; did InitializeAsync run?");
        var label = driver.FindElements(MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.TextView\")"))
            .Where(element => element.Displayed && !string.IsNullOrWhiteSpace(element.Text))
            .Select(element => (Text: element.Text!.Trim(), Distance: Math.Abs(element.Location.Y - row.Location.Y)))
            .Where(item => item.Text.Length > 2 && item.Distance < 80)
            .OrderBy(item => item.Distance)
            .FirstOrDefault();
        return label.Text ?? string.Empty;
    }

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

    public string TapDifferentVisibleText(string current)
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Driver not initialised; did InitializeAsync run?");
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var list = fixture.WaitForAccessibilityId("SelectionList");
            var chosen = Regex.Matches(driver.PageSource, "text=\"([^\"]+)\"")
                .Select(match => match.Groups[1].Value)
                .FirstOrDefault(text => IsOtherListLabel(text, current));
            if (!string.IsNullOrEmpty(chosen))
            {
                var match = driver.FindElements(MobileBy.AndroidUIAutomator("new UiSelector().text(\"" + chosen + "\")"))
                    .FirstOrDefault(element =>
                        element.Displayed
                        && element.Location.Y > list.Location.Y
                        && element.Location.Y < list.Location.Y + list.Size.Height);
                if (match != null)
                {
                    fixture.Tap(match);
                    return chosen;
                }
            }

            SwipeList(driver, list);
        }

        throw new TimeoutException("Could not find another list item.");
    }

    public void TapExactInList(string text)
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Driver not initialised; did InitializeAsync run?");
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var list = fixture.WaitForAccessibilityId("SelectionList");
            var match = driver.FindElements(MobileBy.AndroidUIAutomator("new UiSelector().text(\"" + text + "\")"))
                .FirstOrDefault(element =>
                    element.Displayed
                    && element.Location.Y > list.Location.Y
                    && element.Location.Y < list.Location.Y + list.Size.Height);
            if (match != null)
            {
                fixture.Tap(match);
                return;
            }

            SwipeList(driver, list);
        }

        throw new TimeoutException($"Could not find \"{text}\".");
    }

    private static bool IsOtherListLabel(string? text, string current)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        return trimmed.Length > 2
            && !string.Equals(trimmed, current, StringComparison.Ordinal)
            && !trimmed.StartsWith("Select ", StringComparison.Ordinal);
    }

    private static void SwipeList(AppiumDriver driver, IWebElement list)
    {
        var location = list.Location;
        var size = list.Size;
        driver.ExecuteScript("mobile: swipeGesture", new Dictionary<string, object>
        {
            ["left"] = location.X + 24,
            ["top"] = location.Y + 48,
            ["width"] = Math.Max(200, size.Width - 48),
            ["height"] = Math.Max(240, size.Height - 96),
            ["direction"] = "up",
            ["percent"] = 0.55
        });
    }

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
