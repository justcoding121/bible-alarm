#nullable enable

using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Stores.Reducers;

namespace Bible.Alarm.Tests;

public sealed class PlaybackReducerTests
{
    private static PlaybackState PlayingState(int scheduleId = 7, string? artwork = "https://img.example/a.png") =>
        new(
            new PlaybackTransportSlice(scheduleId, true, true, true, PlayStatus.Playing, false, false),
            new PlaybackMediaSlice("T", "A", "Al", artwork, TimeSpan.FromSeconds(30), null),
            new PlaybackDefaultScheduleSlice(1, "DT", "DA", "DAl", "https://default"));

    [Fact]
    public void OnPlaybackStarted_ClearsArtworkWhenScheduleChanges()
    {
        var prior = PlayingState(scheduleId: 5, artwork: "https://keep");

        var next = PlaybackReducer.OnPlaybackStarted(prior, new PlaybackStartedAction(99));

        Assert.Equal(99, next.CurrentScheduleId);
        Assert.Null(next.ArtworkUrl);
        Assert.Equal(PlayStatus.Loading, next.Status);
        Assert.Null(next.DefaultScheduleArtworkUrl);
    }

    [Fact]
    public void OnPlaybackStarted_KeepsArtworkWhenSameSchedule()
    {
        var prior = PlayingState(scheduleId: 4, artwork: "https://art");

        var next = PlaybackReducer.OnPlaybackStarted(prior, new PlaybackStartedAction(4));

        Assert.Equal("https://art", next.ArtworkUrl);
    }

    [Fact]
    public void OnPlaybackNavigationChanged_UpdatesNavigationFlags()
    {
        var prior = PlayingState();

        var next = PlaybackReducer.OnPlaybackNavigationChanged(prior, new PlaybackNavigationChangedAction(false, true));

        Assert.False(next.CanPlayNext);
        Assert.True(next.CanPlayPrevious);
    }

    [Fact]
    public void OnPlaybackMetadataChanged_OverwritesMediaSlice()
    {
        var prior = PlayingState();

        var next = PlaybackReducer.OnPlaybackMetadataChanged(prior, new PlaybackMetadataChangedAction
        {
            Title = "New",
            Artist = "Artist",
            Album = "Album",
            ArtworkUrl = "https://x",
        });

        Assert.Equal("New", next.Title);
        Assert.Equal("Artist", next.Artist);
        Assert.Equal("Album", next.Album);
        Assert.Equal("https://x", next.ArtworkUrl);
    }

}
