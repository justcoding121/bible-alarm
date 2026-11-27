#nullable enable

using OpenQA.Selenium.Appium;

namespace Bible.Alarm.UITests.Visual;

public interface IVisualCheck
{
    Task CheckAsync(AppiumDriver driver, string name, IReadOnlyList<string> maskAutomationIds);
}
