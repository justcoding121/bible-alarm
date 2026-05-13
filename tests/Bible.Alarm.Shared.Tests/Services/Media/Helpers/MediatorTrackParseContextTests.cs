#nullable enable

using Bible.Alarm.Shared.Services.Media.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class MediatorTrackParseContextTests
{
    [Fact]
    public void Same_values_are_equal_including_default_optional_flags()
    {
        var a = new MediatorTrackParseContext("E", "40");
        var b = new MediatorTrackParseContext("E", "40", IsVideo: false, TrackNumber: null,
            AllowAudioDescriptionTitles: false, OmitTrackFromUrlParams: false, UseDocidParam: false);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Optional_flags_change_equality()
    {
        var baseline = new MediatorTrackParseContext("E", "40");
        var video = baseline with { IsVideo = true };
        var withTrack = baseline with { TrackNumber = 3 };

        Assert.NotEqual(baseline, video);
        Assert.NotEqual(baseline, withTrack);
    }
}
