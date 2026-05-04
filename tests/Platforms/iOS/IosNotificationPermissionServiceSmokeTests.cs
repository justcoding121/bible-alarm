#nullable enable

using Bible.Alarm.Platforms.iOS.Services.Helpers;
using Xunit;

namespace Bible.Alarm.Tests.Platforms.iOS;

/// <summary>
/// Bare-minimum iOS smoke test so coverage for Bible.Alarm.Platforms.iOS.* is non-zero in SonarCloud
/// the first time a green iOS job runs. See the Android sibling for the rationale.
/// </summary>
[Trait("Platform", "iOS")]
public sealed class IosNotificationPermissionServiceSmokeTests
{
    [Fact]
    public void Instance_returns_singleton()
    {
        var first = IosNotificationPermissionService.Instance;
        var second = IosNotificationPermissionService.Instance;

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void InvalidateCache_does_not_throw()
    {
        // Just exercises one method on the singleton so the type's static ctor + a few branches show
        // up in the OpenCover report. Real authorization is gated by the system prompt and cannot be
        // automated reliably from a unit test.
        IosNotificationPermissionService.Instance.InvalidateCache();
    }
}
