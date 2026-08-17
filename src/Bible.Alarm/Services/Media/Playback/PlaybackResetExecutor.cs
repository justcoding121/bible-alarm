#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Stores.Actions.Playback;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Isolates full playback reset (progress, player, Fluxor state, car-screen) from PlaybackService orchestration.
/// </summary>
public sealed class PlaybackResetExecutor
{
    private readonly ProgressTracker progressTracker;
    private readonly IAudioPlayer audioPlayer;
    private readonly PlaybackStateManager stateManager;
    private readonly IDispatcher dispatcher;
    private readonly ILogger logger;

    public PlaybackResetExecutor(
        ProgressTracker progressTracker,
        IAudioPlayer audioPlayer,
        PlaybackStateManager stateManager,
        IDispatcher dispatcher,
        ILogger logger)
    {
        this.progressTracker = progressTracker;
        this.audioPlayer = audioPlayer;
        this.stateManager = stateManager;
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    public async Task ResetAsync()
    {
        progressTracker.Stop();
        await audioPlayer.ResetAsync();
        stateManager.Reset();
        dispatcher.Dispatch(new PlaybackStoppedAction());

#if ANDROID || IOS
        dispatcher.Dispatch(new SetCarPlayScreenAction());
        logger.Debug("SetCarPlayScreenAction dispatched after playback reset");
#endif

        logger.Debug("Playback reset completed. Status: {Status}, ScheduleId: {ScheduleId}",
            audioPlayer.Status,
            stateManager.CurrentScheduleId);
    }

    public async Task ResetStateForRetryAsync()
    {
        dispatcher.Dispatch(new PlaybackErrorAction { ErrorMessage = null });
        progressTracker.Stop();
        await audioPlayer.ResetAsync();
        stateManager.Reset();
    }
}
