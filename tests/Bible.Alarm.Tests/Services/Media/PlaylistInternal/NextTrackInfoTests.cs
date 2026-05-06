#nullable enable

using Bible.Alarm.Services.Media.PlaylistInternal;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Tests;

public sealed class NextTrackInfoTests
{
    [Fact]
    public void Record_defaults_optional_section_code_to_null()
    {
        var sut = new NextTrackInfo("nxt", null);

        Assert.Equal("nxt", sut.NextTrackCode);
        Assert.Null(sut.NextTrack);
        Assert.Null(sut.NextSectionCode);
    }

    [Fact]
    public void Record_preserves_navigation_result_and_section_code()
    {
        var track = new BiblePublicationTrack { TrackCode = "t1", Title = "T" };
        var nav = new TrackNavigationResult("pub", null, track);
        var sut = new NextTrackInfo("nxt", nav, "sec");

        Assert.Same(nav, sut.NextTrack);
        Assert.Equal("sec", sut.NextSectionCode);
    }
}
