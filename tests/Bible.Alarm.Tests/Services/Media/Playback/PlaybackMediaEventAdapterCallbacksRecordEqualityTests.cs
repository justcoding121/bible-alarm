#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using Bible.Alarm.Services.Media.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackMediaEventAdapterCallbacksRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new PlaybackMediaEventAdapterCallbacks(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var b = new PlaybackMediaEventAdapterCallbacks(
            a.GetPlaylist,
            a.GetCurrentTrackIndex,
            a.SetCurrentTrackIndex,
            a.GetCurrentScheduleId,
            a.GetIsIndefinitePlayback,
            a.TryAppendNextTrackAsync,
            a.PlayCurrentTrackAsync,
            a.StopAsyncInternal,
            a.GetIsManualNavigationPending,
            a.GetIsAlarm,
            a.ShowPlaybackErrorInModalKeepSessionAsync,
            a.IsPlaybackEstablishedForTrack);

        Assert.Equal(a, b);
    }
}
