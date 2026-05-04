#nullable enable

using Bible.Alarm.UITests.Fixtures;

namespace Bible.Alarm.UITests.Smoke;

/// <summary>
/// iOS UI smoke test: the production MAUI app launches on the simulator and renders the home
/// page. Filtered by <c>[Trait("UI","iOS")]</c>.
/// </summary>
[Trait("UI", "iOS")]
public sealed class IosHomePageUITests : IClassFixture<IosAppiumFixture>
{
    private readonly IosAppiumFixture _fixture;

    public IosHomePageUITests(IosAppiumFixture fixture) => _fixture = fixture;

    /// <summary>
    /// On iOS, MAUI maps <c>AutomationId="HomePage"</c> to <c>accessibilityIdentifier</c> on the
    /// underlying UIView; XCUITest queries that via accessibility id.
    /// </summary>
    [Fact]
    public void HomePage_loads_after_app_launch()
    {
        var homeRoot = _fixture.WaitForAccessibilityId("HomePage");
        Assert.True(homeRoot.Displayed, "HomePage root grid should be visible after cold start.");
    }
}
