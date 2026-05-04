#nullable enable

using Bible.Alarm.Platforms.Windows.Helpers;

namespace Bible.Alarm.Tests;

[Trait("Platform", "Windows")]
public sealed class WindowsBootstrapHelperTests
{
    [Fact]
    public void IsBackgroundTaskEnabled_round_trips()
    {
        var prior = WindowsBootstrapHelper.IsBackgroundTaskEnabled;
        try
        {
            WindowsBootstrapHelper.IsBackgroundTaskEnabled = false;
            Assert.False(WindowsBootstrapHelper.IsBackgroundTaskEnabled);

            WindowsBootstrapHelper.IsBackgroundTaskEnabled = true;
            Assert.True(WindowsBootstrapHelper.IsBackgroundTaskEnabled);
        }
        finally
        {
            WindowsBootstrapHelper.IsBackgroundTaskEnabled = prior;
        }
    }
}
