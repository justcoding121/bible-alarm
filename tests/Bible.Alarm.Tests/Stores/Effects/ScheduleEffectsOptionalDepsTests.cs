#nullable enable

using Bible.Alarm.Stores.Effects;

namespace Bible.Alarm.Tests;

public sealed class ScheduleEffectsOptionalDepsTests
{
    [Fact]
    public void Record_defaults_all_optional_dependencies_to_null()
    {
        var deps = new ScheduleEffectsOptionalDeps();

        Assert.Null(deps.BiblePublicationService);
        Assert.Null(deps.BiblePublicationSectionService);
        Assert.Null(deps.AlarmScheduleService);
        Assert.Null(deps.AlarmService);
        Assert.Null(deps.MediaCacheService);
        Assert.Null(deps.MediaService);
        Assert.Null(deps.State);
        Assert.Null(deps.ScheduleDisplayNameService);
    }

    [Fact]
    public void Record_with_values_preserves_each_property()
    {
        var marker = new object();
        var deps = new ScheduleEffectsOptionalDeps(
            BiblePublicationService: null,
            BiblePublicationSectionService: null,
            AlarmScheduleService: null,
            AlarmService: null,
            MediaCacheService: null,
            MediaService: null,
            State: null,
            ScheduleDisplayNameService: null);

        Assert.NotSame(marker, deps);
        Assert.Null(deps.AlarmScheduleService);
    }
}
