#nullable enable

using Bible.Alarm.Services.Media.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackStopRequestTests
{
    [Fact]
    public void Struct_preserves_parameters_and_invokes_callbacks()
    {
        var resetCalls = 0;
        var timerCalls = 0;

        var sut = new PlaybackStopRequest(
            ScheduleIdToSave: 42,
            TrackMetadataToMark: null,
            SkipMarkAsPlayed: true,
            SkipSaveLastPlayed: false,
            PreparationCancellationTokenSource: null,
            ResetState: () => resetCalls++,
            StopProgressTimer: () => timerCalls++,
            SkipDispatchStopped: true);

        Assert.Equal(42, sut.ScheduleIdToSave);
        Assert.Null(sut.TrackMetadataToMark);
        Assert.True(sut.SkipMarkAsPlayed);
        Assert.False(sut.SkipSaveLastPlayed);
        Assert.Null(sut.PreparationCancellationTokenSource);
        Assert.True(sut.SkipDispatchStopped);

        sut.ResetState();
        sut.StopProgressTimer();

        Assert.Equal(1, resetCalls);
        Assert.Equal(1, timerCalls);
    }
}
