#nullable enable

using Bible.Alarm.UITests.Fixtures;

namespace Bible.Alarm.UITests.Smoke;

/// <summary>
/// Windows UI smoke test: the production MAUI app launches and renders the home page.
/// Filtered by <c>[Trait("UI","Windows")]</c> so default `dotnet test` runs (the host-based
/// Windows + Shared coverage pass) skip it via <c>--filter "UI!=Windows"</c>.
/// </summary>
[Trait("UI", "Windows")]
public sealed class WindowsHomePageUITests : IClassFixture<WindowsAppiumFixture>
{
    private readonly WindowsAppiumFixture _fixture;

    public WindowsHomePageUITests(WindowsAppiumFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Launching the app via WinAppDriver should put us on the home page. The home Grid in
    /// <c>src/Bible.Alarm/Views/Home.xaml</c> has <c>AutomationId="HomePage"</c>; MAUI Windows
    /// surfaces this as <c>AutomationProperties.AutomationId</c>, which Appium queries via
    /// <c>MobileBy.AccessibilityId</c>. If anything between the splash screen and Home.xaml
    /// throws (DI graph, EF migrations, Syncfusion init), we won't reach this assertion.
    /// </summary>
    [Fact]
    public void HomePage_loads_after_app_launch()
    {
        var homeRoot = _fixture.WaitForAccessibilityId("HomePage");
        Assert.True(homeRoot.Displayed, "HomePage root grid should be visible after cold start.");
    }
}
