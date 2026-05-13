#nullable enable

using Bible.Alarm.Services.Media.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackServiceInjectionContextRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new PlaybackServiceInjectionContext(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var b = new PlaybackServiceInjectionContext(
            a.PreparePlaybackService,
            a.PlaylistService,
            a.FallbackAlarmSoundService,
            a.MediaCacheService,
            a.CdnPlaybackUrlProbe,
            a.TrackCdnUrlRefresher,
            a.NotificationService,
            a.DefaultDeviceRingtoneService);

        Assert.Equal(a, b);
    }
}
