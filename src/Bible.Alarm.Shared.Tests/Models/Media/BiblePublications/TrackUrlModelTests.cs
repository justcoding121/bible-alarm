#nullable enable

using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class TrackUrlModelTests
{
    [Fact]
    public void Default_ctor_uses_clr_defaults_for_all_fields()
    {
        var sut = new TrackUrl();
        Assert.Equal(0, sut.Id);
        Assert.Equal(string.Empty, sut.Url);
        Assert.Null(sut.BiblePublicationTrackId);
        Assert.Null(sut.BiblePublicationTrack);

        sut.Url = "https://refresh.example/track.m4a";
        Assert.False(string.IsNullOrEmpty(sut.Url));
    }

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

    [Fact]
    public void Track_navigation_attachable_before_or_after_foreign_key_assignment()
    {
        var track = new BiblePublicationTrack { Id = 909, TrackCode = "a", Title = "Episode" };

        var sut = new TrackUrl
        {
            Id = 1,
            Url = "https://cdn.example/hello.mp3",
            BiblePublicationTrack = track,
        };

        sut.BiblePublicationTrackId = track.Id;
        Assert.Same(track, sut.BiblePublicationTrack);
        Assert.Equal(909, sut.BiblePublicationTrackId);

        sut.BiblePublicationTrackId = 800;
        Assert.Equal(800, sut.BiblePublicationTrackId);
    }
}
