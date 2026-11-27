#nullable enable

using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using SkiaSharp;

namespace Bible.Alarm.UITests.Visual;

/// <summary>
/// Pixel diff against a per-device baseline. A missing baseline is logged and skipped so a new device profile does not fail the run.
/// Set UPDATE_BASELINES=1 to record. VISUAL_PROVIDER can later select another implementation of <see cref="IVisualCheck"/>.
/// </summary>
public sealed class LocalBaselineVisualCheck : IVisualCheck
{
    private const int ChannelTolerance = 16;
    private const double DiffRatioLimit = 0.005;
    private readonly string platform;

    public LocalBaselineVisualCheck(string platform) => this.platform = platform;

    public Task CheckAsync(AppiumDriver driver, string name, IReadOnlyList<string> maskAutomationIds)
    {
        if (driver is not ITakesScreenshot shot)
        {
            Console.WriteLine($"Visual baseline skipped, driver cannot screenshot: {name}");
            return Task.CompletedTask;
        }

        using var image = SKBitmap.Decode(shot.GetScreenshot().AsByteArray);
        CropSystemBars(image);
        Mask(driver, image, maskAutomationIds);

        var profile = Environment.GetEnvironmentVariable("UI_DEVICE_PROFILE") ?? "default";
        var theme = Environment.GetEnvironmentVariable("UI_THEME") ?? "light";
        var directory = BaselineDirectory(profile, theme);
        Directory.CreateDirectory(directory);
        var baselinePath = Path.Combine(directory, name + ".png");
        var actualDirectory = Path.Combine(Bible.Alarm.UITests.Fixtures.AppiumFixtureBase.ScreenshotDirectory, "visual");
        Directory.CreateDirectory(actualDirectory);
        var actualPath = Path.Combine(actualDirectory, $"{platform}-{name}-actual.png");
        SavePng(image, actualPath);

        if (string.Equals(Environment.GetEnvironmentVariable("UPDATE_BASELINES"), "1", StringComparison.Ordinal))
        {
            SavePng(image, baselinePath);
            Console.WriteLine($"Visual baseline recorded: {baselinePath}");
            return Task.CompletedTask;
        }

        if (!File.Exists(baselinePath))
        {
            var candidate = Path.Combine(actualDirectory, $"{platform}-{name}-candidate.png");
            File.Copy(actualPath, candidate, overwrite: true);
            Console.WriteLine($"Visual baseline missing, skipping: {name} ({baselinePath})");
            return Task.CompletedTask;
        }

        using var baseline = SKBitmap.Decode(baselinePath);
        var width = Math.Min(image.Width, baseline.Width);
        var height = Math.Min(image.Height, baseline.Height);
        var different = 0;
        var total = Math.Max(1, width * height);
        using var diff = image.Copy();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var left = image.GetPixel(x, y);
                var right = baseline.GetPixel(x, y);
                if (Math.Abs(left.Red - right.Red) > ChannelTolerance ||
                    Math.Abs(left.Green - right.Green) > ChannelTolerance ||
                    Math.Abs(left.Blue - right.Blue) > ChannelTolerance)
                {
                    different++;
                    diff.SetPixel(x, y, new SKColor(255, 0, 0));
                }
            }
        }

        var ratio = different / (double)total;
        if (ratio > DiffRatioLimit)
        {
            var diffPath = Path.Combine(actualDirectory, $"{platform}-{name}-diff.png");
            SavePng(diff, diffPath);
            throw new InvalidOperationException(
                $"Visual diff {ratio:P2} exceeds {DiffRatioLimit:P2} for {name}. Diff: {diffPath}");
        }

        return Task.CompletedTask;
    }

    private static void SavePng(SKBitmap image, string path)
    {
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Open(path, FileMode.Create);
        data.SaveTo(stream);
    }

    private static void CropSystemBars(SKBitmap image)
    {
        var top = (int)(image.Height * 0.04);
        var bottom = (int)(image.Height * 0.06);
        var height = image.Height - top - bottom;
        if (height <= 20)
        {
            return;
        }

        using var subset = new SKBitmap(image.Width, height);
        if (!image.ExtractSubset(subset, new SKRectI(0, top, image.Width, top + height)))
        {
            return;
        }

        image.Reset();
        if (!image.TryAllocPixels(subset.Info))
        {
            return;
        }

        subset.CopyTo(image);
    }

    private static void Mask(AppiumDriver driver, SKBitmap image, IReadOnlyList<string> automationIds)
    {
        foreach (var id in automationIds)
        {
            try
            {
                var element = driver.FindElement(MobileBy.AccessibilityId(id));
                var location = element.Location;
                var size = element.Size;
                var x = Math.Clamp(location.X, 0, image.Width - 1);
                var y = Math.Clamp(location.Y, 0, image.Height - 1);
                var width = Math.Clamp(size.Width, 1, image.Width - x);
                var height = Math.Clamp(size.Height, 1, image.Height - y);
                using var canvas = new SKCanvas(image);
                canvas.DrawRect(new SKRect(x, y, x + width, y + height), new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Visual mask skipped for {id}: {ex.Message}");
            }
        }
    }

    private string BaselineDirectory(string profile, string theme)
    {
        var root = Environment.GetEnvironmentVariable("UI_BASELINE_DIR");
        if (!string.IsNullOrWhiteSpace(root))
        {
            return Path.Combine(root, platform, profile, theme);
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Bible.Alarm.sln")))
            {
                return Path.Combine(current.FullName, "tests", "Bible.Alarm.UITests", "Baselines", platform, profile, theme);
            }

            current = current.Parent;
        }

        return Path.Combine(Bible.Alarm.UITests.Fixtures.AppiumFixtureBase.ScreenshotDirectory, "Baselines", platform, profile, theme);
    }
}
