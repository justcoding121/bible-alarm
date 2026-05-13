#nullable enable

using Bible.Alarm.Services.Media;

namespace Bible.Alarm.Tests;

public sealed class PlaylistServiceDepsRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new PlaylistServiceDeps(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var b = new PlaylistServiceDeps(
            a.Logger,
            a.MediaService,
            a.Dispatcher,
            a.ApplicationState,
            a.AlarmScheduleService,
            a.GeneralSettingsService,
            a.BiblePublicationService,
            a.UrlRefreshService,
            a.UrlConstructionService);

        Assert.Equal(a, b);
    }
}
