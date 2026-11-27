#nullable enable

using OpenQA.Selenium.Appium;

namespace Bible.Alarm.UITests.Visual;

public sealed class NullVisualCheck : IVisualCheck
{
    public Task CheckAsync(AppiumDriver driver, string name, IReadOnlyList<string> maskAutomationIds)
    {
        _ = driver;
        _ = name;
        _ = maskAutomationIds;
        Console.WriteLine("Visual checks are off. Skipping snapshot.");
        return Task.CompletedTask;
    }
}
