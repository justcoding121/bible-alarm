#nullable enable

using Bible.Alarm.Services.Media;

namespace Bible.Alarm.Tests;

public sealed class PlaylistServiceDepsTests
{
    [Fact]
    public void Record_round_trips_required_slots_and_optional_defaults()
    {
        var sut = new PlaylistServiceDeps(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            LanguageContentService: null,
            ScopeFactory: null,
            ScheduleDisplayNameService: null);

        Assert.Null(sut.Logger);
        Assert.Null(sut.LanguageContentService);
    }
}
