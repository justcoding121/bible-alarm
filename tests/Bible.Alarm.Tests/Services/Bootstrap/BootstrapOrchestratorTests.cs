#nullable enable

using Bible.Alarm.Services.Bootstrap;

namespace Bible.Alarm.Tests;

public sealed class BootstrapOrchestratorTests
{
    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var deps = new BootstrapOrchestratorDeps(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var sut = new BootstrapOrchestrator(deps);

        Assert.NotNull(sut);
    }
}
