#nullable enable

using Bible.Alarm.Stores.Actions.Playback;

namespace Bible.Alarm.Tests;

public sealed class SetDefaultScheduleMetadataActionTests
{
    [Fact]
    public void Init_sets_car_play_metadata_fields()
    {
        var sut = new SetDefaultScheduleMetadataAction
        {
            ScheduleId = 7,
            Title = "Default title",
            Artist = "Artist",
            Album = "Album",
            ArtworkUrl = "https://example.com/x.jpg",
        };

        Assert.Equal(7, sut.ScheduleId);
        Assert.Equal("Default title", sut.Title);
        Assert.Equal("Artist", sut.Artist);
        Assert.Equal("Album", sut.Album);
        Assert.Equal("https://example.com/x.jpg", sut.ArtworkUrl);
    }
}
