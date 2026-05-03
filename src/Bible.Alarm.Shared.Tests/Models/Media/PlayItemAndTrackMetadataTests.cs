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
    public void TrackMetadata_OptionalKeys_AndFlags_Roundtrip()
    {
        var now = DateTimeOffset.Parse("2026-03-02T06:05:04Z");

        var meta = new TrackMetadata
        {
            ScheduleId = 42,
            NotificationTime = now,
            NaturalKey = "natural",
            DownloadCode = "disc-01",
            OriginalTrackCode = 9,
            IsLastTrack = true,
            FinishedDuration = TimeSpan.FromMinutes(12),
            SectionCode = "sec-x",
            TrackCode = "3",
            LanguageCode = "E",
            PublicationCode = "nwt",
            IsBibleContent = true,
            LookUpPath = "/?q=1",
        };

        Assert.Equal(42L, meta.ScheduleId);
        Assert.Equal(now, meta.NotificationTime);
        Assert.Equal("natural", meta.NaturalKey);
        Assert.Equal("disc-01", meta.DownloadCode);
        Assert.Equal(9, meta.OriginalTrackCode);
        Assert.True(meta.IsLastTrack);
        Assert.Equal(TimeSpan.FromMinutes(12), meta.FinishedDuration);
        Assert.Equal("/?q=1", meta.LookUpPath);
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

    [Fact]
    public void PlayItem_mutates_playback_URL_and_recovery_state_flags_after_construction()
    {
        var meta = new TrackMetadata
        {
            LanguageCode = "E",
            PublicationCode = "osg",
            IsBibleContent = false,
            TrackCode = "5",
        };
        meta.LookUpPath = "?playback=1";

        var item = new PlayItem(meta, "https://first.example/track");

        Assert.Equal("https://first.example/track", item.Url);
        Assert.Same(meta, item.Metadata);

        item.Url = "https://second.example/track";
        item.CdnStaleUrlRecoveryConsumed = true;
        item.CdnStaleUrlRefetchReplayIssued = true;
        item.StreamingOpenPhaseMediaFailedRetryDone = true;

        Assert.Equal("https://second.example/track", item.Url);
        Assert.True(item.CdnStaleUrlRecoveryConsumed);
        Assert.True(item.CdnStaleUrlRefetchReplayIssued);
        Assert.True(item.StreamingOpenPhaseMediaFailedRetryDone);
    }

    [Fact]
    public void PlayItem_ToString_Bible_With_empty_section_string_keeps_spacing_before_track()
    {
        var meta = new TrackMetadata
        {
            LanguageCode = "q",
            PublicationCode = "r",
            IsBibleContent = true,
            SectionCode = "",
            TrackCode = "9",
        };
        meta.LookUpPath = "?";

        Assert.Equal("q r  9", new PlayItem(meta, string.Empty).ToString());
    }

    [Fact]
    public void PlayItem_ToString_Music_with_empty_track_code_ends_after_publication_space()
    {
        var meta = new TrackMetadata
        {
            LanguageCode = "E",
            PublicationCode = "sing",
            IsBibleContent = false,
            TrackCode = string.Empty,
        };
        meta.LookUpPath = "?";

        Assert.Equal("E sing ", new PlayItem(meta, string.Empty).ToString());
    }

    [Fact]
    public void PlayItem_recovery_flags_Default_false_until_set()
    {
        var meta = new TrackMetadata
        {
            LanguageCode = "E",
            PublicationCode = "flat",
            TrackCode = "1",
            IsBibleContent = false,
        };
        meta.LookUpPath = "?";
        var sut = new PlayItem(meta, string.Empty);

        Assert.False(sut.CdnStaleUrlRecoveryConsumed);
        Assert.False(sut.CdnStaleUrlRefetchReplayIssued);
        Assert.False(sut.StreamingOpenPhaseMediaFailedRetryDone);
    }

    [Fact]
    public void PlayItem_metadata_setter_Rebinds_and_ToString_uses_updated_metadata()
    {
        var bible = new TrackMetadata
        {
            LanguageCode = "E",
            PublicationCode = "nwt",
            IsBibleContent = true,
            SectionCode = "1",
            TrackCode = "2",
            LookUpPath = "?",
        };
        var music = new TrackMetadata
        {
            LanguageCode = "F",
            PublicationCode = "iam",
            IsBibleContent = false,
            TrackCode = "8",
            LookUpPath = "?",
        };

        var sut = new PlayItem(bible, "a");
        Assert.Equal("E nwt 1 2", sut.ToString());

        sut.Metadata = music;
        Assert.Same(music, sut.Metadata);
        Assert.Equal("F iam 8", sut.ToString());
    }
}
