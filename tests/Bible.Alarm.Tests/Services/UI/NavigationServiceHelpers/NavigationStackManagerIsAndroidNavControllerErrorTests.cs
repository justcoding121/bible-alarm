#nullable enable

using Bible.Alarm.Services.UI.NavigationServiceHelpers;

namespace Bible.Alarm.Tests;

public sealed class NavigationStackManagerIsAndroidNavControllerErrorTests
{
    [Fact]
    public void IsAndroidNavControllerError_returns_false_for_managed_InvalidOperationException_even_when_message_matches_native_nav_pattern()
    {
        var ex = new InvalidOperationException(
            "The NavController's back stack has no destination for this navigation action.");

        Assert.False(NavigationStackManager.IsAndroidNavControllerError(ex));
    }
}
