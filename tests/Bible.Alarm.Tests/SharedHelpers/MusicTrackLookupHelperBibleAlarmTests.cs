#nullable enable

using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.Music;

namespace Bible.Alarm.Tests;

public sealed class MusicTrackLookupHelperBibleAlarmTests
{
    [Fact]
    public void TryGetByCode_returns_false_when_no_matching_track_code()
    {
        var tracks = new Dictionary<int, MusicTrack>
        {
            [1] = new() { TrackCode = "10", Title = "Ten" },
        };

        Assert.False(MusicTrackLookupHelper.TryGetByCode(tracks, "99", out _));
        Assert.Null(MusicTrackLookupHelper.GetKeyByCode(tracks, "99"));
    }
}
