#nullable enable

using Bible.Alarm.Services.Media.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackMediaFailedRequestRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_slots_are_equal()
    {
        var a = new PlaybackMediaFailedRequest(
            Playlist: null,
            GetCurrentTrackIndex: null!,
            TrackUri: "",
            TrackUrl: "",
            PlayCurrentTrackAsync: null!,
            GetIsManualNavigationPending: null!,
            GetIsAlarm: null!,
            ShowPlaybackErrorInModalKeepSessionAsync: null!,
            IsPlaybackEstablishedForTrack: null!);

        var b = new PlaybackMediaFailedRequest(
            a.Playlist,
            a.GetCurrentTrackIndex,
            a.TrackUri,
            a.TrackUrl,
            a.PlayCurrentTrackAsync,
            a.GetIsManualNavigationPending,
            a.GetIsAlarm,
            a.ShowPlaybackErrorInModalKeepSessionAsync,
            a.IsPlaybackEstablishedForTrack);

        Assert.Equal(a, b);
    }
}
