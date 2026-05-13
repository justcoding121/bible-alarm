#nullable enable

using Bible.Alarm.Services.Media.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackHandleFailureRequestRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_slots_are_equal()
    {
        var a = new PlaybackHandleFailureRequest(
            IsAlarm: false,
            CurrentScheduleId: null,
            ResetAsync: null!,
            SetPlaylist: null!,
            SetCurrentTrackIndex: null!,
            PlayCurrentTrackAsync: null!);

        var b = new PlaybackHandleFailureRequest(
            a.IsAlarm,
            a.CurrentScheduleId,
            a.ResetAsync,
            a.SetPlaylist,
            a.SetCurrentTrackIndex,
            a.PlayCurrentTrackAsync);

        Assert.Equal(a, b);
    }
}
