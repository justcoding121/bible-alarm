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

    [Fact]
    public void OnPlaybackStatusChanged_keeps_schedule_id_while_failed_when_already_playing()
    {
        var prior = PlayingState(scheduleId: 3);

        var next = PlaybackReducer.OnPlaybackStatusChanged(prior, new PlaybackStatusChangedAction(PlayStatus.Failed));

        Assert.Equal(3, next.CurrentScheduleId);
        Assert.True(next.IsPreparingOrPlaying);
        Assert.Equal(PlayStatus.Failed, next.Status);
    }

    [Fact]
    public void OnPlaybackError_sets_error_message_on_media_slice()
    {
        var prior = PlayingState();

        var next = PlaybackReducer.OnPlaybackError(prior, new PlaybackErrorAction
        {
            ErrorMessage = "Network error",
        });

        Assert.Equal("Network error", next.ErrorMessage);
    }

    [Fact]
    public void OnPlaybackDurationChanged_updates_duration()
    {
        var prior = PlayingState();

        var next = PlaybackReducer.OnPlaybackDurationChanged(
            prior,
            new PlaybackDurationChangedAction { Duration = TimeSpan.FromMinutes(4) });

        Assert.Equal(TimeSpan.FromMinutes(4), next.Duration);
    }

    [Fact]
    public void OnPlaybackTrackTransitionStarted_clears_duration_while_preserving_transport()
    {
        var prior = PlayingState();

        var next = PlaybackReducer.OnPlaybackTrackTransitionStarted(
            prior,
            new PlaybackTrackTransitionStartedAction());

        Assert.Equal(TimeSpan.Zero, next.Duration);
        Assert.Equal(7, next.CurrentScheduleId);
        Assert.Equal(PlayStatus.Playing, next.Status);
    }

    [Fact]
    public void OnPlaybackTrackTransitionEnded_clears_transition_flag()
    {
        var prior = new PlaybackState(
            new PlaybackTransportSlice(7, true, true, true, PlayStatus.Playing, false, true),
            new PlaybackMediaSlice("T", "A", "Al", null, TimeSpan.FromSeconds(10), null),
            new PlaybackDefaultScheduleSlice(1, "DT", "DA", "DAl", null));

        var next = PlaybackReducer.OnPlaybackTrackTransitionEnded(
            prior,
            new PlaybackTrackTransitionEndedAction());

        Assert.False(next.IsTransitioningTrack);
    }

}
