#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;

namespace Bible.Alarm.Tests;

public sealed class ContainerReadyActionTests
{
    [Fact]
    public void Constructor_sets_container_name()
    {
        var sut = new ContainerReadyAction("MusicSelectionContainer");

        Assert.Equal("MusicSelectionContainer", sut.ContainerName);
    }
}
