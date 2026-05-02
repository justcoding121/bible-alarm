#nullable enable

using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class PlayItemAndTrackMetadataTests
{
    [Fact]
    public void TrackMetadata_PlayTypeReflects_IsBibleContentFlag()
    {
        var bible = new TrackMetadata { IsBibleContent = true };
        var music = new TrackMetadata { IsBibleContent = false };

        Assert.Equal(PlayType.Bible, bible.PlayType);
        Assert.Equal(PlayType.Music, music.PlayType);
        Assert.False(bible.IsAlarmMusic);
        Assert.True(music.IsAlarmMusic);
    }

    [Fact]
    public void TrackMetadata_LookUpPath_Throws_WhenNeverOrEmpty_OnGet()
    {
        var meta = new TrackMetadata();

        Assert.Throws<InvalidOperationException>(() => _ = meta.LookUpPath);

        meta.LookUpPath = "";
        Assert.Throws<InvalidOperationException>(() => _ = meta.LookUpPath);
    }

    [Fact]
    public void TrackMetadata_LookUpPath_Allows_WhitespaceOnly_Payload_FromIndex()
    {
        var meta = new TrackMetadata { LookUpPath = "   " };

        Assert.Equal("   ", meta.LookUpPath);
    }

    [Fact]
    public void TrackMetadata_LookUpPath_ReturnsNonEmpty_Value()
    {
        var meta = new TrackMetadata { LookUpPath = "/path?ok=1" };

        Assert.Equal("/path?ok=1", meta.LookUpPath);
    }

    [Fact]
    public void PlayItem_ToString_Formats_BibleVsMusicSuffix()
    {
        var bibleMeta = new TrackMetadata
        {
            LanguageCode = "E",
            PublicationCode = "nw",
            IsBibleContent = true,
            SectionCode = "10",
            TrackCode = "3",
        };

        var musicMeta = new TrackMetadata
        {
            LanguageCode = "E",
            PublicationCode = "iam",
            IsBibleContent = false,
            SectionCode = "ignored",
            TrackCode = "7",
        };

        Assert.Equal("E nw 10 3", new PlayItem(bibleMeta, "https://b").ToString());
        Assert.Equal("E iam 7", new PlayItem(musicMeta, "https://m").ToString());
    }

    [Fact]
    public void PlayItem_ToString_Bible_EmptySection_StillSeparatesTrack()
    {
        var meta = new TrackMetadata
        {
            LanguageCode = "fr",
            PublicationCode = "vod",
            IsBibleContent = true,
            SectionCode = null,
            TrackCode = "x",
        };

        Assert.Equal("fr vod  x", new PlayItem(meta, "u").ToString());
    }
}
