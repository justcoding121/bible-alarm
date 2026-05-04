#nullable enable

using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;
using Bible.Alarm.Tests.Support;
using Serilog;

namespace Bible.Alarm.Tests;

public sealed class CategorySelectionAutoPopulateHandlerDepsTests
{
    [Fact]
    public void Deps_record_equal_when_all_component_references_match()
    {
        ILogger logger = TestLogging.CreateLogger();
#pragma warning disable CS8625 // Intentional null-forgiving: record only used for structural equality smoke test.
        var a = new CategorySelectionAutoPopulateHandlerDeps(
            null!, null!, null!, null!, null!, null!, null!, logger);
        var b = new CategorySelectionAutoPopulateHandlerDeps(
            null!, null!, null!, null!, null!, null!, null!, logger);
#pragma warning restore CS8625

        Assert.Equal(a, b);
    }
}
