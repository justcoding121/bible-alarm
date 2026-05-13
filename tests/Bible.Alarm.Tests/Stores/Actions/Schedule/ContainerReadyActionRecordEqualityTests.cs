#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;

namespace Bible.Alarm.Tests;

public sealed class ContainerReadyActionRecordEqualityTests
{
    [Fact]
    public void Instances_with_same_container_name_are_equal()
    {
        var a = new ContainerReadyAction("MusicSelectionContainer");
        var b = new ContainerReadyAction(a.ContainerName);
        Assert.Equal(a, b);
    }
}
