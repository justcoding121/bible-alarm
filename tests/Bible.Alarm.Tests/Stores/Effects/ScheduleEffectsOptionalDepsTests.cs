#nullable enable

using Bible.Alarm.Stores.Effects;

namespace Bible.Alarm.Tests;

public sealed class ScheduleEffectsOptionalDepsTests
{
    [Fact]
    public void Default_constructor_sets_all_optional_services_to_null()
    {
        var sut = new ScheduleEffectsOptionalDeps();

        Assert.Null(sut.BiblePublicationService);
        Assert.Null(sut.BiblePublicationSectionService);
        Assert.Null(sut.AlarmScheduleService);
        Assert.Null(sut.AlarmService);
        Assert.Null(sut.MediaCacheService);
        Assert.Null(sut.MediaService);
        Assert.Null(sut.State);
        Assert.Null(sut.ScheduleDisplayNameService);
    }
}
