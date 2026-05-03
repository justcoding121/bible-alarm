#nullable enable

using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class TrackUrlModelTests
{
    [Fact]
    public void Properties_roundtrip_with_optional_track_navigation()
    {
        var track = new BiblePublicationTrack { Id = 55, TrackCode = "1", Title = "T" };

        var sut = new TrackUrl
        {
            Id = 900,
            Url = "https://cdn.example/stream.mp3",
            BiblePublicationTrackId = track.Id,
            BiblePublicationTrack = track,
        };

        Assert.Equal(900, sut.Id);
        Assert.Equal("https://cdn.example/stream.mp3", sut.Url);
        Assert.Equal(55, sut.BiblePublicationTrackId);
        Assert.Same(track, sut.BiblePublicationTrack);

        sut.BiblePublicationTrackId = null;
        sut.BiblePublicationTrack = null;
        Assert.Null(sut.BiblePublicationTrackId);
        Assert.Null(sut.BiblePublicationTrack);
    }
}
