#nullable enable

using Bible.Alarm.Stores.Actions.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackMetadataChangedActionTests
{
    [Fact]
    public void Init_sets_metadata_fields()
    {
        var sut = new PlaybackMetadataChangedAction
        {
            Title = "t",
            Artist = "a",
            Album = "alb",
            ArtworkUrl = "https://x",
        };

        Assert.Equal("t", sut.Title);
        Assert.Equal("a", sut.Artist);
        Assert.Equal("alb", sut.Album);
        Assert.Equal("https://x", sut.ArtworkUrl);
    }
}
