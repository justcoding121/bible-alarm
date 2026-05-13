#nullable enable

using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class ContainerReadinessRecordEqualityTests
{
    [Fact]
    public void Default_instances_match_NotReady_singleton()
    {
        Assert.Equal(ContainerReadiness.NotReady, new ContainerReadiness());
    }
}
