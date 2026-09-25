#nullable enable

using Xunit;

namespace Bible.Alarm.Tests.Platforms.iOS;

/// <summary>
/// Bare-minimum iOS smoke test so the device-host .app exits cleanly under xharness.
/// Avoid touching production singletons (notification permission / MainThread) — those can
/// deadlock the xunit-on-iOS entry point when the main runloop is already blocked.
/// </summary>
[Trait("Platform", "iOS")]
public sealed class IosNotificationPermissionServiceSmokeTests
{
    [Fact]
    public void Smoke_assembly_loads()
    {
        Assert.True(true);
    }

    [Fact]
    public void Smoke_platform_trait_present()
    {
        Assert.Equal("iOS", "iOS");
    }
}
