#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media.Playback;

public sealed class PlaybackStopHandler
{
    private readonly IAudioPlayer audioPlayer;
    private readonly IPlaylistService playlistService;
    private readonly IDispatcher dispatcher;
    private readonly ILogger logger;

    public PlaybackStopHandler(
        IAudioPlayer audioPlayer,
        IPlaylistService playlistService,
        IDispatcher dispatcher,
        ILogger logger)
    {
        this.audioPlayer = audioPlayer;
        this.playlistService = playlistService;
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    public async Task StopAsync(PlaybackStopRequest request)
    {
        var scheduleIdToSave = request.ScheduleIdToSave;
        var trackMetadataToMark = request.TrackMetadataToMark;
        var skipMarkAsPlayed = request.SkipMarkAsPlayed;
        var skipSaveLastPlayed = request.SkipSaveLastPlayed;
        var preparationCancellationTokenSource = request.PreparationCancellationTokenSource;
        var resetState = request.ResetState;
        var stopProgressTimer = request.StopProgressTimer;
        var skipDispatchStopped = request.SkipDispatchStopped;

        logger.Information(AppConstants.Logging.PlaybackStopHandlerDiagnosticsLog.StopAsyncCalledStoppingAlarmCompletely);

        var dispatched = false;

        try
        {
            await CancelPreparationTokenBestEffortAsync(preparationCancellationTokenSource);

            WarnIfFailure(stopProgressTimer, ex => logger.Warning(ex, AppConstants.Logging.PlaybackStopHandlerDiagnosticsLog.ErrorStoppingProgressTimer));

            WarnIfFailure(resetState, ex => logger.Warning(ex, AppConstants.Logging.PlaybackStopHandlerDiagnosticsLog.ErrorResettingPlaybackState));

            await RunIgnoringWarningAsync(audioPlayer.StopAsync, ex =>
                logger.Warning(ex, AppConstants.Logging.PlaybackStopHandlerDiagnosticsLog.ErrorStoppingPlayerWillContinueWithReset));

            await TryMarkStoppedTrackPlayedWhenApplicableAsync(skipMarkAsPlayed, trackMetadataToMark);

            await TrySaveLastPlayedWhenApplicableAsync(scheduleIdToSave, skipSaveLastPlayed);

            await ResetAudioPlayerWithFallbackAsync();

            if (!skipDispatchStopped)
            {
                dispatched = true;
                dispatcher.Dispatch(new PlaybackStoppedAction());
#if ANDROID || IOS
                dispatcher.Dispatch(new SetCarPlayScreenAction());
                logger.Debug(AppConstants.Logging.PlaybackStopHandlerDiagnosticsLog.SetCarPlayScreenActionDispatchedAfterPlaybackReset);
#endif
            }
        }
        finally
        {
            if (!skipDispatchStopped && !dispatched)
            {
                logger.Warning(AppConstants.Logging.PlaybackStopHandlerDiagnosticsLog.PlaybackStoppedActionNotDispatchedDispatchingInFinally);
                try
                {
                    dispatcher.Dispatch(new PlaybackStoppedAction());
#if ANDROID || IOS
                    dispatcher.Dispatch(new SetCarPlayScreenAction());
#endif
                }
                catch (Exception ex)
                {
                    logger.Error(ex, AppConstants.Logging.PlaybackStopHandlerDiagnosticsLog.FailedToDispatchPlaybackStoppedActionInFinallyBlock);
                }
            }
        }
    }

    private async Task CancelPreparationTokenBestEffortAsync(CancellationTokenSource? preparationCancellationTokenSource)
    {
        try
        {
            if (preparationCancellationTokenSource != null)
            {
                await preparationCancellationTokenSource.CancelAsync();
            }

            logger.Debug(AppConstants.Logging.PlaybackStopHandlerDiagnosticsLog.CancelledPreparationCancellationToken);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.PlaybackStopHandlerDiagnosticsLog.ErrorCancellingPreparationToken);
        }
    }

    private static void WarnIfFailure(Action step, Action<Exception> warn)
    {
        try
        {
            step();
        }
        catch (Exception ex)
        {
            warn(ex);
        }
    }

    private static async Task RunIgnoringWarningAsync(Func<Task> asyncStep, Action<Exception> warn)
    {
        try
        {
            await asyncStep();
        }
        catch (Exception ex)
        {
            warn(ex);
        }
    }

    private async Task TryMarkStoppedTrackPlayedWhenApplicableAsync(bool skipMarkAsPlayed, TrackMetadata? trackMetadataToMark)
    {
        if (skipMarkAsPlayed || trackMetadataToMark == null)
        {
            return;
        }

        try
        {
            if (trackMetadataToMark.PlayType != PlayType.Music)
            {
                await playlistService.MarkTrackAsPlayed(trackMetadataToMark);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.PlaybackStopHandlerDiagnosticsLog.ErrorMarkingCurrentTrackAsPlayedOrFinished);
        }
    }

    private async Task TrySaveLastPlayedWhenApplicableAsync(int? scheduleIdToSave, bool skipSaveLastPlayed)
    {
        if (!scheduleIdToSave.HasValue || skipSaveLastPlayed)
        {
            return;
        }

        try
        {
            await playlistService.SaveLastPlayed(scheduleIdToSave.Value);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.PlaybackStopHandlerDiagnosticsLog.ErrorSavingLastPlayed);
        }
    }

    private async Task ResetAudioPlayerWithFallbackAsync()
    {
        try
        {
            await audioPlayer.ResetAsync();
            return;
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.PlaybackStopHandlerDiagnosticsLog.ErrorInAudioPlayerResetAttemptingMinimalCleanup);
        }

        try
        {
            await audioPlayer.ResetAsync();
        }
        catch (Exception resetEx)
        {
            logger.Warning(resetEx, AppConstants.Logging.PlaybackStopHandlerDiagnosticsLog.ErrorResettingPlayerInFallback);
        }
    }
}

