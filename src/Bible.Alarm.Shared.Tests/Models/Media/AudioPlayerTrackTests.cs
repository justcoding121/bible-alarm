#nullable enable

using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class AudioPlayerTrackTests
{
    private static PlayItem SamplePlayItem()
    {
        var metadata = new TrackMetadata
        {
            LanguageCode = "en",
            PublicationCode = "nwt",
            TrackCode = "1",
            LookUpPath = "/lookup",
        };
        return new PlayItem(metadata, "https://example.com/stream");
    }

    [Fact]
    public void Uri_Defaults_To_Empty_String_With_Play_Item()
    {
        var playItem = SamplePlayItem();
        var track = new AudioPlayerTrack { PlayItem = playItem };

        Assert.Equal(string.Empty, track.Uri);
        Assert.Same(playItem, track.PlayItem);
    }

    [Fact]
    public void Uri_Is_Settable_After_Construction()
    {
        var track = new AudioPlayerTrack { PlayItem = SamplePlayItem() };
        track.Uri = "file:///local/track.mp3";

        Assert.Equal("file:///local/track.mp3", track.Uri);
    }
}
