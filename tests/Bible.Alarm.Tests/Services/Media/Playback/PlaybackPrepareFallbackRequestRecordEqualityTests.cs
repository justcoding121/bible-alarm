#nullable enable

using Bible.Alarm.Services.Media.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackPrepareFallbackRequestRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_slots_are_equal()
    {
        var a = new PlaybackPrepareFallbackRequest(
            ScheduleId: 0,
            KeepErrorMessage: false,
            SetPlaylist: null!,
            SetCurrentTrackIndex: null!,
            ClearManuallyVisited: null!,
            NotifyNavigationChanged: null!,
            PlayCurrentTrackAsync: null!);

        var b = new PlaybackPrepareFallbackRequest(
            a.ScheduleId,
            a.KeepErrorMessage,
            a.SetPlaylist,
            a.SetCurrentTrackIndex,
            a.ClearManuallyVisited,
            a.NotifyNavigationChanged,
            a.PlayCurrentTrackAsync);

        Assert.Equal(a, b);
    }
}
