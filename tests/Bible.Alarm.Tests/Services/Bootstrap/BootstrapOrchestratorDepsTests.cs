#nullable enable

using Bible.Alarm.Services.Bootstrap;

namespace Bible.Alarm.Tests;

public sealed class BootstrapOrchestratorDepsTests
{
    [Fact]
    public void Record_round_trips_dependency_slots()
    {
        var sut = new BootstrapOrchestratorDeps(null!, null!, null!, null!, null!, null!, null!);

        Assert.Null(sut.DatabaseBootstrapService);
        Assert.Null(sut.CategoryNameService);
    }
}
