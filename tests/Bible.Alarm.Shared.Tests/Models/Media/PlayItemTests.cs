#nullable enable

using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class PlayItemTests
{
    [Fact]
    public void ToString_joins_language_publication_and_track_for_alarm_music()
    {
        var meta = new TrackMetadata
        {
            LanguageCode = "E",
            PublicationCode = "osg",
            IsBibleContent = false,
            TrackCode = "3",
        };
        var sut = new PlayItem(meta, "https://x");

        Assert.Equal("E osg 3", sut.ToString());
    }

    [Fact]
    public void ToString_inserts_section_before_track_for_bible_content()
    {
        var meta = new TrackMetadata
        {
            LanguageCode = "E",
            PublicationCode = "nwt",
            IsBibleContent = true,
            SectionCode = "40",
            TrackCode = "12",
        };
        var sut = new PlayItem(meta, "https://y");

        Assert.Equal("E nwt 40 12", sut.ToString());
    }

    [Fact]
    public void ToString_uses_empty_section_token_when_section_null_for_bible_content()
    {
        var meta = new TrackMetadata
        {
            LanguageCode = "E",
            PublicationCode = "vid",
            IsBibleContent = true,
            SectionCode = null,
            TrackCode = "5",
        };
        var sut = new PlayItem(meta, "https://z");

        Assert.Equal("E vid  5", sut.ToString());
    }
}
