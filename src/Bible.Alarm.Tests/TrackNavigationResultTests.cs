#nullable enable

using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Tests;

public sealed class TrackNavigationResultTests
{
    [Fact]
    public void TrackNavigationResult_exposes_publication_section_and_track()
    {
        var section = new BiblePublicationSection { SectionCode = "40", Name = "Matthew" };
        var track = new BiblePublicationTrack
        {
            TrackCode = "1",
            Title = "Chapter One",
            Publication = null!,
        };

        var result = new TrackNavigationResult("nwt", section, track);

        Assert.Equal("nwt", result.PublicationCode);
        Assert.Same(section, result.Section);
        Assert.Same(track, result.Track);
    }

    [Fact]
    public void TrackNavigationResult_accepts_null_section_for_flat_navigation()
    {
        var track = new BiblePublicationTrack { TrackCode = "x", Title = "Flat", Publication = null! };

        var result = new TrackNavigationResult("dramas", Section: null, track);

        Assert.Null(result.Section);
        Assert.Equal("dramas", result.PublicationCode);
        Assert.Equal("x", result.Track.TrackCode);
    }
}
