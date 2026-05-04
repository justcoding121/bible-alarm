#nullable enable

using Bible.Alarm.UITests.Fixtures;

namespace Bible.Alarm.UITests.Smoke;

/// <summary>
/// Android UI smoke test: the production MAUI APK launches on the connected emulator/device
/// and renders the home page. Filtered by <c>[Trait("UI","Android")]</c>.
/// </summary>
[Trait("UI", "Android")]
public sealed class AndroidHomePageUITests : IClassFixture<AndroidAppiumFixture>
{
    private readonly AndroidAppiumFixture _fixture;

    public AndroidHomePageUITests(AndroidAppiumFixture fixture) => _fixture = fixture;

    /// <summary>
    /// On Android, MAUI maps <c>AutomationId="HomePage"</c> to the underlying View's
    /// <c>contentDescription</c>; UiAutomator2 in turn exposes that as accessibility id.
    /// </summary>
    [Fact]
    public void HomePage_loads_after_app_launch()
    {
        var homeRoot = _fixture.WaitForAccessibilityId("HomePage");
        Assert.True(homeRoot.Displayed, "HomePage root grid should be visible after cold start.");
    }
}
