#nullable enable

using Bible.Alarm.Services.Bootstrap;

namespace Bible.Alarm.Tests;

public sealed class BootstrapOrchestratorDepsRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new BootstrapOrchestratorDeps(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var b = new BootstrapOrchestratorDeps(
            a.DatabaseBootstrapService,
            a.FluxorBootstrapService,
            a.ResourceBootstrapService,
            a.ScheduleBootstrapService,
            a.PlatformBootstrapService,
            a.LanguageNameService,
            a.CategoryNameService);

        Assert.Equal(a, b);
    }
}
