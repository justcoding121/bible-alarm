#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using Bible.Alarm.Services.Media.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackMediaEndedRequestRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_slots_are_equal()
    {
        var a = new PlaybackMediaEndedRequest(
            Playlist: null,
            GetCurrentTrackIndex: null!,
            SetCurrentTrackIndex: null!,
            CurrentScheduleId: null,
            IsIndefinitePlayback: false,
            TryAppendNextTrackAsync: null!,
            PlayCurrentTrackAsync: null!,
            StopAsyncInternal: null!,
            GetIsManualNavigationPending: null!,
            GetIsAlarm: null!,
            ShowPlaybackErrorInModalKeepSessionAsync: null!);

        var b = new PlaybackMediaEndedRequest(
            a.Playlist,
            a.GetCurrentTrackIndex,
            a.SetCurrentTrackIndex,
            a.CurrentScheduleId,
            a.IsIndefinitePlayback,
            a.TryAppendNextTrackAsync,
            a.PlayCurrentTrackAsync,
            a.StopAsyncInternal,
            a.GetIsManualNavigationPending,
            a.GetIsAlarm,
            a.ShowPlaybackErrorInModalKeepSessionAsync);

        Assert.Equal(a, b);
    }
}
