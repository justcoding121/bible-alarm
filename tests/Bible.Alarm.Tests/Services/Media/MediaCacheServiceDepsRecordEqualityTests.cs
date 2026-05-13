#nullable enable

using Bible.Alarm.Services.Media;

namespace Bible.Alarm.Tests;

public sealed class MediaCacheServiceDepsRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new MediaCacheServiceDeps(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var b = new MediaCacheServiceDeps(
            a.Logger,
            a.StorageService,
            a.DownloadService,
            a.MediaPlayService,
            a.MediaService,
            a.NetworkStatusService,
            a.UrlRefreshService,
            a.AlarmScheduleService,
            a.TrackCdnUrlRefresher);

        Assert.Equal(a, b);
    }
}
