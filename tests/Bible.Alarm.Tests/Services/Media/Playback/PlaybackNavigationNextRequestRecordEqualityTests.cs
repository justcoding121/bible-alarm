#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using Bible.Alarm.Services.Media.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackNavigationNextRequestRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_slots_are_equal()
    {
        var manual = new HashSet<int>();
        var a = new PlaybackNavigationNextRequest(
            Playlist: null,
            GetCurrentTrackIndex: null!,
            SetCurrentTrackIndex: null!,
            CurrentScheduleId: null,
            IsIndefinitePlayback: false,
            TryAppendNextTrackAsync: null!,
            ManuallyVisitedTrackIndices: manual,
            MarkCurrentTrackAsPlayedAsync: null!,
            PlayCurrentTrackAsync: null!,
            StopPlaybackAsync: null!,
            HandlePlaybackFailureAsync: null!);

        var b = new PlaybackNavigationNextRequest(
            a.Playlist,
            a.GetCurrentTrackIndex,
            a.SetCurrentTrackIndex,
            a.CurrentScheduleId,
            a.IsIndefinitePlayback,
            a.TryAppendNextTrackAsync,
            a.ManuallyVisitedTrackIndices,
            a.MarkCurrentTrackAsPlayedAsync,
            a.PlayCurrentTrackAsync,
            a.StopPlaybackAsync,
            a.HandlePlaybackFailureAsync);

        Assert.Equal(a, b);
    }
}
