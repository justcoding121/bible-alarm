#nullable enable

using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class StateTests
{
    [Fact]
    public void ApplicationState_constructor_replaces_null_schedules_with_empty_set()
    {
        var sut = new ApplicationState(null!);

        Assert.NotNull(sut.Schedules);
        Assert.Empty(sut.Schedules);
    }

    [Fact]
    public void ApplicationState_constructor_defaults_null_container_readiness_to_NotReady()
    {
        var sut = new ApplicationState([], null, false, false, null, null);

        Assert.Equal(ContainerReadiness.NotReady, sut.ContainerReadiness);
    }
}
