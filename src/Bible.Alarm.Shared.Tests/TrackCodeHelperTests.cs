using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;

namespace Bible.Alarm.Shared.Tests;

public sealed class TrackCodeHelperTests
{
    [Fact]
    public void GetFromTrack_ReturnsPublicationTrackCodeProperty()
    {
        var bible = new BiblePublicationTrack { TrackCode = "7" };
        Assert.Equal("7", TrackCodeHelper.GetFromTrack(bible));
    }

    [Fact]
    public void GetFromTrack_MusicTrack_UsesNullableOrEmpty()
    {
        Assert.Equal("", TrackCodeHelper.GetFromTrack(new MusicTrack { TrackCode = null }));
        Assert.Equal("12", TrackCodeHelper.GetFromTrack(new MusicTrack { TrackCode = "12" }));
    }
}
