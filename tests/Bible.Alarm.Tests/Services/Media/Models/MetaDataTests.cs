#nullable enable

using Bible.Alarm.Services.Media.Models;

namespace Bible.Alarm.Tests;

public sealed class MetaDataTests
{
    [Fact]
    public void MetaData_round_trips_optional_fields()
    {
        var artwork = new byte[] { 1, 2, 3 };
        var sut = new MetaData
        {
            Title = "T",
            Artist = "A",
            Album = "Al",
            ArtworkUrl = "https://x/a.png",
            ArtworkBytes = artwork,
        };

        Assert.Equal("T", sut.Title);
        Assert.Equal("A", sut.Artist);
        Assert.Equal("Al", sut.Album);
        Assert.Equal("https://x/a.png", sut.ArtworkUrl);
        Assert.Same(artwork, sut.ArtworkBytes);
    }
}
