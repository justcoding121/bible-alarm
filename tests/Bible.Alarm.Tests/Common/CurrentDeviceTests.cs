#nullable enable

using Bible.Alarm.Common;

namespace Bible.Alarm.Tests;

public sealed class CurrentDeviceTests
{
    [Fact]
    public void RuntimePlatform_round_trips_assignment()
    {
        var prior = CurrentDevice.RuntimePlatform;
        try
        {
            CurrentDevice.RuntimePlatform = "WinTest";
            Assert.Equal("WinTest", CurrentDevice.RuntimePlatform);
        }
        finally
        {
            CurrentDevice.RuntimePlatform = prior;
        }
    }
}
