#nullable enable

using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Stores.Reducers;

namespace Bible.Alarm.Tests;

public sealed class SetAutoAdvancingActionTests
{
    private static PlaybackState PlayingState(int scheduleId = 7, string? artwork = "https://img.example/a.png") =>
        new(
            new PlaybackTransportSlice(scheduleId, true, true, true, PlayStatus.Playing, false, false),
            new PlaybackMediaSlice("T", "A", "Al", artwork, TimeSpan.FromSeconds(30), null),
            new PlaybackDefaultScheduleSlice(1, "DT", "DA", "DAl", "https://default"));

    [Fact]
    public void PlaybackReducer_OnSetAutoAdvancing_flips_flag()
    {
        var prior = PlayingState();
        Assert.False(prior.IsAutoAdvancing);

        var next = PlaybackReducer.OnSetAutoAdvancing(prior, new SetAutoAdvancingAction(true));

        Assert.True(next.IsAutoAdvancing);
    }
}
