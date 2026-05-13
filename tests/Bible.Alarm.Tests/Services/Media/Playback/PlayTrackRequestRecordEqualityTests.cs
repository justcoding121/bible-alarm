#nullable enable

using System.Collections.Generic;
using System.Threading;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Tests;

public sealed class PlayTrackRequestRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_slots_are_equal()
    {
        var track = new AudioPlayerTrack();
        var played = new HashSet<string>();
        var a = new PlayTrackRequest(
            Track: track,
            CurrentTrackIndex: 0,
            StartFromBeginning: false,
            CurrentScheduleId: null,
            IsPreparingOrPlaying: null!,
            GetPlaylist: null!,
            SetIsPreparingTrack: null!,
            PlayedBibleTrackKeys: played,
            CancellationToken: default);

        var b = new PlayTrackRequest(
            a.Track,
            a.CurrentTrackIndex,
            a.StartFromBeginning,
            a.CurrentScheduleId,
            a.IsPreparingOrPlaying,
            a.GetPlaylist,
            a.SetIsPreparingTrack,
            a.PlayedBibleTrackKeys,
            a.CancellationToken);

        Assert.Equal(a, b);
    }
}
