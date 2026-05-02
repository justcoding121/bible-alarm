#nullable enable

using Bible.Alarm.Services.Scheduler.Models;

namespace Bible.Alarm.Tests;

public sealed class ScheduleTrackMetadataTests
{
    [Fact]
    public void Init_sets_properties()
    {
        var sut = new ScheduleTrackMetadata
        {
            ScheduleId = 9,
            Title = "Morning",
            Artist = "Speaker",
            Album = "Vol 1",
            ArtworkUrl = "https://example.com/a.png",
        };

        Assert.Equal(9, sut.ScheduleId);
        Assert.Equal("Morning", sut.Title);
        Assert.Equal("Speaker", sut.Artist);
        Assert.Equal("Vol 1", sut.Album);
        Assert.Equal("https://example.com/a.png", sut.ArtworkUrl);
    }
}
