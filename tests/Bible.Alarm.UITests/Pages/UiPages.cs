#nullable enable

using System.Text.RegularExpressions;
using Bible.Alarm.UITests.Fixtures;
using Bible.Alarm.UITests.Infrastructure;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using SkiaSharp;

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

    public string TimeForCard(string namePart)
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Driver not initialised; did InitializeAsync run?");
        var name = driver.FindElements(By.Id("ScheduleName"))
            .First(element => element.Text?.Contains(namePart, StringComparison.OrdinalIgnoreCase) == true);
        var time = driver.FindElements(By.Id("ScheduleTimeText"))
            .Where(element => element.Displayed && !string.IsNullOrWhiteSpace(element.Text))
            .Select(element => (Text: element.Text!.Trim(), Distance: Math.Abs(element.Location.Y - name.Location.Y)))
            .OrderBy(item => item.Distance)
            .FirstOrDefault();
        return time.Text ?? string.Empty;
    }

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

    public bool DayLooksDifferent(string dayId, string otherDayId)
    {
        var day = DayColor(dayId);
        var other = DayColor(otherDayId);
        return Math.Abs(day.Red - other.Red) + Math.Abs(day.Green - other.Green) + Math.Abs(day.Blue - other.Blue) > 80;
    }

    private SKColor DayColor(string dayId)
    {
        var element = fixture.WaitForAccessibilityId(dayId) as AppiumElement
            ?? throw new InvalidOperationException("Day button is not an Appium element.");
        using var bitmap = SKBitmap.Decode(element.GetScreenshot().AsByteArray);
        if (bitmap == null || bitmap.Width < 3 || bitmap.Height < 3)
        {
            throw new InvalidOperationException("Day button screenshot was empty.");
        }

        var counts = new Dictionary<(byte R, byte G, byte B), int>();
        for (var y = 0; y < bitmap.Height; y += 2)
        {
            for (var x = 0; x < bitmap.Width; x += 2)
            {
                var pixel = bitmap.GetPixel(x, y);
                var bucket = ((byte)(pixel.Red / 16 * 16), (byte)(pixel.Green / 16 * 16), (byte)(pixel.Blue / 16 * 16));
                counts[bucket] = counts.GetValueOrDefault(bucket) + 1;
            }
        }

        var dominant = counts.OrderByDescending(pair => pair.Value).First().Key;
        return new SKColor(dominant.R, dominant.G, dominant.B);
    }

    public SKColor RowEndColor(string accessibilityId)
    {
        var element = fixture.WaitForAccessibilityId(accessibilityId) as AppiumElement
            ?? throw new InvalidOperationException("Row is not an Appium element.");
        using var bitmap = SKBitmap.Decode(element.GetScreenshot().AsByteArray);
        if (bitmap == null || bitmap.Width < 8 || bitmap.Height < 3)
        {
            throw new InvalidOperationException("Row screenshot was empty.");
        }

        var background = bitmap.GetPixel(bitmap.Width / 3, bitmap.Height / 2);
        SKColor best = background;
        var bestDistance = 0;
        var startX = Math.Max(0, bitmap.Width - 80);
        for (var y = 0; y < bitmap.Height; y += 2)
        {
            for (var x = startX; x < bitmap.Width; x += 2)
            {
                var pixel = bitmap.GetPixel(x, y);
                var distance = Math.Abs(pixel.Red - background.Red)
                    + Math.Abs(pixel.Green - background.Green)
                    + Math.Abs(pixel.Blue - background.Blue);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = pixel;
                }
            }
        }

        return new SKColor(
            (byte)(best.Red / 16 * 16),
            (byte)(best.Green / 16 * 16),
            (byte)(best.Blue / 16 * 16));
    }

    public bool RowEndDiffers(string accessibilityId, SKColor baseline)
    {
        var color = RowEndColor(accessibilityId);
        return Math.Abs(color.Red - baseline.Red) + Math.Abs(color.Green - baseline.Green) + Math.Abs(color.Blue - baseline.Blue) > 80;
    }

    public void TapCategory() => fixture.WaitForAccessibilityId("CategoryRow").Click();

    public void TapSave() => fixture.WaitForAccessibilityId("SaveButton").Click();

    public void SetTime(int hour, int minute, bool afternoon = false)
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
        if (afternoon)
        {
            var evening = driver.FindElements(By.Id("android:id/pm_label")).First(element => element.Displayed);
            fixture.Tap(evening);
        }

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

    public void SetExactTime(DateTime time)
    {
        var hour12 = time.Hour % 12;
        if (hour12 == 0)
        {
            hour12 = 12;
        }

        fixture.Tap(fixture.WaitForAccessibilityId("AlarmTimePicker"));
        var driver = fixture.Driver as AndroidDriver
            ?? throw new InvalidOperationException("Time entry is implemented for the Android driver.");
        Waits.Until(
            () => driver.FindElements(By.Id("android:id/toggle_mode")).Any(element => element.Displayed),
            TimeSpan.FromSeconds(10),
            "time dialog");
        var toggle = driver.FindElements(By.Id("android:id/toggle_mode")).First(element => element.Displayed);
        var description = toggle.GetAttribute("content-desc") ?? string.Empty;
        if (description.Contains("text", StringComparison.OrdinalIgnoreCase))
        {
            fixture.Tap(toggle);
        }

        Waits.Until(
            () => driver.FindElements(By.Id("android:id/input_hour")).Any(element => element.Displayed),
            TimeSpan.FromSeconds(10),
            "time text fields");
        TypeTimeField(driver, "android:id/input_hour", hour12.ToString());
        TypeTimeField(driver, "android:id/input_minute", time.Minute.ToString("00"));
        var meridianId = time.Hour >= 12 ? "android:id/pm_label" : "android:id/am_label";
        var meridian = driver.FindElements(By.Id(meridianId)).FirstOrDefault(element => element.Displayed);
        if (meridian != null)
        {
            fixture.Tap(meridian);
        }

        try
        {
            driver.HideKeyboard();
        }
        catch (Exception)
        {
            // The time dialog can confirm without a keyboard.
        }

        fixture.Tap(driver.FindElements(By.Id("android:id/button1")).First(element => element.Displayed));
        Waits.Until(
            () => !driver.FindElements(By.Id("android:id/input_hour")).Any(element => element.Displayed),
            TimeSpan.FromSeconds(5),
            "time dialog closed");
        var shown = fixture.WaitForAccessibilityId("AlarmTimePicker").Text ?? string.Empty;
        var expected = time.ToString("h:mm");
        if (!shown.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Time picker still shows '{shown}', expected {expected}.");
        }
    }

    private static void TypeTimeField(AndroidDriver driver, string id, string text)
    {
        var field = driver.FindElements(By.Id(id)).First(element => element.Displayed);
        driver.ExecuteScript("mobile: replaceElementValue", new Dictionary<string, object>
        {
            ["elementId"] = ((AppiumElement)field).Id,
            ["text"] = text
        });
    }

    public void DismissTimeDialog()
    {
        if (fixture.Driver is not AndroidDriver driver)
        {
            return;
        }

        var cancel = driver.FindElements(By.Id("android:id/button2")).FirstOrDefault(element => element.Displayed);
        if (cancel != null)
        {
            fixture.Tap(cancel);
        }
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

    public string RowValue(string accessibilityId)
    {
        var row = fixture.WaitForAccessibilityId(accessibilityId);
        var driver = fixture.Driver ?? throw new InvalidOperationException("Driver not initialised; did InitializeAsync run?");
        var label = driver.FindElements(MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.TextView\")"))
            .Where(element => element.Displayed && !string.IsNullOrWhiteSpace(element.Text))
            .Select(element => (Text: element.Text!.Trim(), Distance: Math.Abs(element.Location.Y - row.Location.Y), X: element.Location.X))
            .Where(item => item.Distance < 80 && item.X >= row.Location.X + (row.Size.Width / 2))
            .OrderByDescending(item => item.X)
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
            var labels = Regex.Matches(driver.PageSource, "text=\"([^\"]+)\"")
                .Select(match => match.Groups[1].Value)
                .Where(text => IsOtherListLabel(text, current))
                .Distinct(StringComparer.Ordinal);
            foreach (var chosen in labels)
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
        for (var attempt = 0; attempt < 16; attempt++)
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

            SwipeList(driver, list, attempt < 8 ? "up" : "down");
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

    private static void SwipeList(AppiumDriver driver, IWebElement list, string direction = "up")
    {
        var location = list.Location;
        var size = list.Size;
        driver.ExecuteScript("mobile: swipeGesture", new Dictionary<string, object>
        {
            ["left"] = location.X + 24,
            ["top"] = location.Y + 48,
            ["width"] = Math.Max(200, size.Width - 48),
            ["height"] = Math.Max(240, size.Height - 96),
            ["direction"] = direction,
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
