#nullable enable

using Bible.Alarm.Services.Bootstrap;

namespace Bible.Alarm.Tests;

public sealed class ScheduleStatePopulatorDepsRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new ScheduleStatePopulatorDeps(
            null,
            null,
            null!,
            null,
            null,
            null,
            null!);

        var b = new ScheduleStatePopulatorDeps(
            a.BiblePublicationService,
            a.BiblePublicationSectionService,
            a.Mapper,
            a.MediaService,
            a.MelodyMusicService,
            a.VocalMusicService,
            a.ScopeFactory);

        Assert.Equal(a, b);
    }
}
