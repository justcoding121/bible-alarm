#nullable enable

using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Tests.Support;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class PlaybackPreparationFailureHandlerTests
{
    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
            Dispatched.Add(action);
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
        }
    }

    [Fact]
    public async Task HandleAsync_non_alarm_dispatches_error_failed_and_skips_fallback()
    {
        var dispatcher = new RecordingDispatcher();
        var logger = TestLogging.CreateLogger();
        var fallbackCalls = 0;

        await PlaybackPreparationFailureHandler.HandleAsync(
            scheduleId: 7,
            isAlarm: false,
            logMessage: "prep failed {ScheduleId}",
            dispatcher,
            logger,
            (_, _) =>
            {
                fallbackCalls++;
                return Task.CompletedTask;
            });

        Assert.Equal(2, dispatcher.Dispatched.Count);
        var err = Assert.IsType<PlaybackErrorAction>(dispatcher.Dispatched[0]);
        Assert.Equal(PlaybackUserFacingStrings.MediaDownloadFailedCheckInternet, err.ErrorMessage);
        var status = Assert.IsType<PlaybackStatusChangedAction>(dispatcher.Dispatched[1]);
        Assert.Equal(PlayStatus.Failed, status.Status);
        Assert.Equal(0, fallbackCalls);
    }

    [Fact]
    public async Task HandleAsync_alarm_dispatches_alarm_copy_and_invokes_fallback()
    {
        var dispatcher = new RecordingDispatcher();
        var logger = TestLogging.CreateLogger();
        var fallbackCalls = new List<(int id, bool flag)>();

        await PlaybackPreparationFailureHandler.HandleAsync(
            scheduleId: 12,
            isAlarm: true,
            logMessage: "alarm prep failed {ScheduleId}",
            dispatcher,
            logger,
            (id, flag) =>
            {
                fallbackCalls.Add((id, flag));
                return Task.CompletedTask;
            });

        var err = Assert.IsType<PlaybackErrorAction>(dispatcher.Dispatched[0]);
        Assert.Equal(PlaybackUserFacingStrings.DownloadFailedPlayingDefaultAlarmSound, err.ErrorMessage);
        Assert.Single(fallbackCalls);
        Assert.Equal((12, true), fallbackCalls[0]);
    }

    [Fact]
    public async Task HandleAsync_alarm_without_fallback_only_dispatches()
    {
        var dispatcher = new RecordingDispatcher();
        var logger = TestLogging.CreateLogger();

        await PlaybackPreparationFailureHandler.HandleAsync(
            1,
            true,
            "x {ScheduleId}",
            dispatcher,
            logger,
            tryPlayFallbackAlarmSoundAsync: null);

        Assert.Equal(2, dispatcher.Dispatched.Count);
    }
}
