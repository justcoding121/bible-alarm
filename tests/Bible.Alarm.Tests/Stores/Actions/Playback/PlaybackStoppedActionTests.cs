#nullable enable

using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Stores.Reducers;

namespace Bible.Alarm.Tests;

public sealed class PlaybackStoppedActionTests
{
    private static PlaybackState PlayingState(int scheduleId = 7, string? artwork = "https://img.example/a.png") =>
        new(
            new PlaybackTransportSlice(scheduleId, true, true, true, PlayStatus.Playing, false, false),
            new PlaybackMediaSlice("T", "A", "Al", artwork, TimeSpan.FromSeconds(30), null),
            new PlaybackDefaultScheduleSlice(1, "DT", "DA", "DAl", "https://default"));

    [Fact]
    public void PlaybackReducer_OnPlaybackStopped_resets_transport_and_media()
    {
        var prior = PlayingState();

        var next = PlaybackReducer.OnPlaybackStopped(prior, new PlaybackStoppedAction());

        Assert.Null(next.CurrentScheduleId);
        Assert.False(next.IsPreparingOrPlaying);
        Assert.Equal(PlayStatus.Stopped, next.Status);
        Assert.Null(next.Title);
        Assert.Equal(TimeSpan.Zero, next.Duration);
    }
}
