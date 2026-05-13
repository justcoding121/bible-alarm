#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using Bible.Alarm.Services.Media.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackNavigationPreviousRequestRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_slots_are_equal()
    {
        var manual = new HashSet<int>();
        var a = new PlaybackNavigationPreviousRequest(
            Playlist: null,
            GetCurrentTrackIndex: null!,
            SetCurrentTrackIndex: null!,
            CurrentScheduleId: null,
            IsIndefinitePlayback: false,
            TryPrependPreviousTrackAsync: null!,
            ManuallyVisitedTrackIndices: manual,
            MarkCurrentTrackAsPlayedAsync: null!,
            PlayCurrentTrackAsync: null!,
            HandlePlaybackFailureAsync: null!);

        var b = new PlaybackNavigationPreviousRequest(
            a.Playlist,
            a.GetCurrentTrackIndex,
            a.SetCurrentTrackIndex,
            a.CurrentScheduleId,
            a.IsIndefinitePlayback,
            a.TryPrependPreviousTrackAsync,
            a.ManuallyVisitedTrackIndices,
            a.MarkCurrentTrackAsPlayedAsync,
            a.PlayCurrentTrackAsync,
            a.HandlePlaybackFailureAsync);

        Assert.Equal(a, b);
    }
}
