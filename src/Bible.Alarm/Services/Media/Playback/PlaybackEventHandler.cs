#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Handles playback events (media ended, media failed).
/// <see cref="HandleMediaFailedAsync"/>: never auto-skips to the next track; shows the playback modal error with Retry.
/// Actual-alarm sessions also start the device default ringtone until Retry/Dismiss/stop.
/// Stale CDN (404/410): one catalog refresh + one auto-replay; second failure stops with error (no loop).
/// </summary>
public sealed class PlaybackEventHandler
{
    private readonly IPlaylistService playlistService;
    private readonly IDispatcher dispatcher;
    private readonly ILogger logger;
    private readonly PlaybackNavigationManager navigationManager;
    private readonly ICdnPlaybackUrlProbe cdnPlaybackUrlProbe;
    private readonly ITrackCdnUrlRefresher trackCdnUrlRefresher;

    public PlaybackEventHandler(
        IPlaylistService playlistService,
        IDispatcher dispatcher,
        ILogger logger,
        PlaybackNavigationManager navigationManager,
        ICdnPlaybackUrlProbe cdnPlaybackUrlProbe,
        ITrackCdnUrlRefresher trackCdnUrlRefresher)
    {
        this.playlistService = playlistService;
        this.dispatcher = dispatcher;
        this.logger = logger;
        this.navigationManager = navigationManager;
        this.cdnPlaybackUrlProbe = cdnPlaybackUrlProbe;
        this.trackCdnUrlRefresher = trackCdnUrlRefresher;
    }

    public async Task HandleMediaEndedAsync(PlaybackMediaEndedRequest request)
    {
        var playlist = request.Playlist;
        Func<int> getCurrentTrackIndex = request.GetCurrentTrackIndex;
        Action<int> setCurrentTrackIndex = request.SetCurrentTrackIndex;
        var currentScheduleId = request.CurrentScheduleId;
        var isIndefinitePlayback = request.IsIndefinitePlayback;
        Func<Task<bool>> tryAppendNextTrackAsync = request.TryAppendNextTrackAsync;
        Func<bool, Task> playCurrentTrackAsync = request.PlayCurrentTrackAsync;
        Func<bool, Task> stopAsyncInternal = request.StopAsyncInternal;
        Func<bool> getIsManualNavigationPending = request.GetIsManualNavigationPending;
        Func<bool> getIsAlarm = request.GetIsAlarm;
        Func<string, bool, Task> showPlaybackErrorInModalKeepSessionAsync = request.ShowPlaybackErrorInModalKeepSessionAsync;

        if (getIsManualNavigationPending())
        {
            logger.Debug(AppConstants.Logging.PlaybackEventHandlerDiagnosticsLog.HandleMediaEndedManualNavigationPendingSkipping);
            return;
        }

        var currentTrackIndex = getCurrentTrackIndex();
        if (isIndefinitePlayback &&
            playlist is { Count: > 0 } pl &&
            currentTrackIndex >= 0 &&
            currentTrackIndex == pl.Count - 1)
        {
            await HandleIndefiniteLastTrackEndedAsync(new IndefiniteLastTrackEndedArgs
            {
                Playlist = pl,
                CurrentTrackIndex = currentTrackIndex,
                CurrentScheduleId = currentScheduleId,
                TryAppendNextTrackAsync = tryAppendNextTrackAsync,
                SetCurrentTrackIndex = setCurrentTrackIndex,
                PlayCurrentTrackAsync = playCurrentTrackAsync,
                ShowPlaybackErrorInModalKeepSessionAsync = showPlaybackErrorInModalKeepSessionAsync,
                GetIsAlarm = getIsAlarm
            });
            return;
        }

        await MarkCurrentTrackAsFinishedAsync(playlist, currentTrackIndex);

        if (playlist is not null && currentTrackIndex < playlist.Count - 1)
        {
            var nextTrackIndex = currentTrackIndex + 1;
            var nextTrack = playlist[nextTrackIndex];
            var isNextTrackBible = nextTrack.PlayItem?.Metadata?.PlayType == PlayType.Bible;

            dispatcher.Dispatch(new PlaybackTrackTransitionStartedAction());

            logger.Information(
                AppConstants.Logging.PlaybackEventHandlerDiagnosticsLog.OnMediaEndedDispatchingSetAutoAdvancingForAutomaticNextTrack,
                currentScheduleId,
                currentTrackIndex,
                nextTrackIndex,
                isNextTrackBible);
            dispatcher.Dispatch(new SetAutoAdvancingAction(true));

            setCurrentTrackIndex(nextTrackIndex);

            navigationManager.NotifyNavigationChanged(playlist, nextTrackIndex);

            logger.Debug(AppConstants.Logging.PlaybackEventHandlerDiagnosticsLog.OnMediaEndedCallingPlayCurrentTrackAsyncAutomaticTransition, nextTrackIndex);
            await playCurrentTrackAsync(false);
        }
        else
        {
            WeakReferenceMessenger.Default.Send(new BeginStoppingPlaybackMessage());
            await Task.Delay(50);
            await stopAsyncInternal(true);
        }
    }

    public async Task HandleMediaFailedAsync(PlaybackMediaFailedRequest request)
    {
        var playlist = request.Playlist;
        Func<int> getCurrentTrackIndex = request.GetCurrentTrackIndex;
        var trackUri = request.TrackUri;
        var trackUrl = request.TrackUrl;
        Func<bool, Task> playCurrentTrackAsync = request.PlayCurrentTrackAsync;
        Func<bool> getIsManualNavigationPending = request.GetIsManualNavigationPending;
        Func<bool> getIsAlarm = request.GetIsAlarm;
        Func<string, bool, Task> showPlaybackErrorInModalKeepSessionAsync = request.ShowPlaybackErrorInModalKeepSessionAsync;
        Func<int, bool> isPlaybackEstablishedForTrack = request.IsPlaybackEstablishedForTrack;

        try
        {
            if (getIsManualNavigationPending())
            {
                logger.Debug(AppConstants.Logging.PlaybackEventHandlerDiagnosticsLog.HandleMediaFailedManualNavigationPendingSkipping);
                return;
            }

            var currentTrackIndex = getCurrentTrackIndex();
            logger.Warning(AppConstants.Logging.PlaybackEventHandlerDiagnosticsLog.MediaFailedForTrackAtIndexUriUrl,
                currentTrackIndex,
                trackUri,
                trackUrl);

            var isAlarm = getIsAlarm();

            var currentPlayerTrack = playlist is { Count: > 0 } &&
                currentTrackIndex >= 0 &&
                currentTrackIndex < playlist.Count
                    ? playlist[currentTrackIndex]
                    : null;
            var failedPlayItem = currentPlayerTrack?.PlayItem;
            var playbackUriUsedByPlayer = !string.IsNullOrEmpty(trackUri) && !string.Equals(trackUri, MediaTrackTitleHelper.UnknownTitle, StringComparison.Ordinal)
                ? trackUri
                : currentPlayerTrack?.Uri ?? string.Empty;
            var playedFromCdnStream = playbackUriUsedByPlayer.StartsWith("http", StringComparison.OrdinalIgnoreCase);

            CdnUrlProbeOutcome? probeOutcome = null;

            if (failedPlayItem != null && playedFromCdnStream)
            {
                var (outcome, replayedAfterRefresh) = await TryProbeAndReplayAfterStaleCdnRecoveryAsync(
                    failedPlayItem,
                    currentPlayerTrack!,
                    playbackUriUsedByPlayer,
                    playCurrentTrackAsync);

                probeOutcome = outcome;

                if (replayedAfterRefresh)
                {
                    return;
                }

                LogIfCdnReachableDespiteFailure(probeOutcome);
            }

            if (playedFromCdnStream &&
                failedPlayItem != null &&
                !failedPlayItem.CdnStaleUrlRefetchReplayIssued &&
                await TryReplayStreamingOpenPhaseOnceAsync(
                    failedPlayItem,
                    currentTrackIndex,
                    playCurrentTrackAsync,
                    isPlaybackEstablishedForTrack))
            {
                return;
            }

            var hadStarted = isPlaybackEstablishedForTrack(currentTrackIndex);
            var message = BuildNonAlarmMediaFailedMessage(playedFromCdnStream, probeOutcome, failedPlayItem, hadStarted);
            await showPlaybackErrorInModalKeepSessionAsync(message, isAlarm);
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.PlaybackDiagnosticsLog.ErrorInHandleMediaFailedAsync);
            try
            {
                await showPlaybackErrorInModalKeepSessionAsync(
                    AppConstants.Media.PlaybackModalMessages.PlaybackFailedTapRetry,
                    getIsAlarm());
            }
            catch (Exception innerEx)
            {
                logger.Error(innerEx, AppConstants.Logging.PlaybackDiagnosticsLog.FailureRecoveryInHandleMediaFailedCatch);
            }
        }
    }

    private async Task<(CdnUrlProbeOutcome Outcome, bool ReplayIssued)> TryProbeAndReplayAfterStaleCdnRecoveryAsync(
        PlayItem failedPlayItem,
        AudioPlayerTrack currentPlayerTrack,
        string playbackUriUsedByPlayer,
        Func<bool, Task> playCurrentTrackAsync)
    {
        var probeOutcome =
            await cdnPlaybackUrlProbe.ProbeStreamingUrlAsync(playbackUriUsedByPlayer, CancellationToken.None);

        var isStaleOrUnreachableUrl = probeOutcome == CdnUrlProbeOutcome.NotFoundOrGone ||
            probeOutcome == CdnUrlProbeOutcome.Indeterminate;

        if (isStaleOrUnreachableUrl && !failedPlayItem.CdnStaleUrlRecoveryConsumed)
        {
            failedPlayItem.CdnStaleUrlRecoveryConsumed = true;
            var refreshedUrl = await trackCdnUrlRefresher.TryRefreshTrackCdnUrlFromApiAsync(
                failedPlayItem.Metadata,
                CancellationToken.None);

            if (!string.IsNullOrEmpty(refreshedUrl))
            {
                failedPlayItem.CdnStaleUrlRefetchReplayIssued = true;
                failedPlayItem.Url = refreshedUrl;
                currentPlayerTrack.Uri = refreshedUrl;

                logger.Information(
                    AppConstants.Logging.PlaybackEventHandlerDiagnosticsLog.PlaybackCdnUrlUnreachableRefreshedAutoReplayingSameTrackOnce);
                await playCurrentTrackAsync(true);
                return (probeOutcome, true);
            }
        }

        return (probeOutcome, false);
    }

    private void LogIfCdnReachableDespiteFailure(CdnUrlProbeOutcome? probeOutcome)
    {
        if (probeOutcome == CdnUrlProbeOutcome.ResourceReachable)
        {
            logger.Warning(
                AppConstants.Logging.PlaybackEventHandlerDiagnosticsLog.PlaybackMediaFailedWhileCdnUrlStillResponds);
        }
    }

    private async Task<bool> TryReplayStreamingOpenPhaseOnceAsync(
        PlayItem failedPlayItem,
        int currentTrackIndex,
        Func<bool, Task> playCurrentTrackAsync,
        Func<int, bool> isPlaybackEstablishedForTrack)
    {
        if (failedPlayItem.CdnStaleUrlRefetchReplayIssued)
        {
            return false;
        }

        var established = isPlaybackEstablishedForTrack(currentTrackIndex);
        var refreshAttemptedButNoNewUrl = failedPlayItem.CdnStaleUrlRecoveryConsumed &&
            !failedPlayItem.CdnStaleUrlRefetchReplayIssued;

        if (established ||
            failedPlayItem.StreamingOpenPhaseMediaFailedRetryDone ||
            refreshAttemptedButNoNewUrl)
        {
            return false;
        }

        failedPlayItem.StreamingOpenPhaseMediaFailedRetryDone = true;
        logger.Information(
            AppConstants.Logging.PlaybackEventHandlerDiagnosticsLog.MediaFailedDuringStreamOpenBufferSilentRetryNoModal);
        await playCurrentTrackAsync(true);
        return true;
    }

    private static string BuildNonAlarmMediaFailedMessage(
        bool playedFromCdnStream,
        CdnUrlProbeOutcome? probeOutcome,
        PlayItem? failedPlayItem,
        bool playbackHadStartedForThisTrack)
    {
        if (!playedFromCdnStream)
        {
            return AppConstants.Media.PlaybackModalMessages.PlaybackFailedTapRetry;
        }

        if (playbackHadStartedForThisTrack)
        {
            return AppConstants.Media.PlaybackModalMessages.PlaybackStoppedConnectionLostTapRetry;
        }

        if (probeOutcome == CdnUrlProbeOutcome.ResourceReachable)
        {
            return AppConstants.Media.PlaybackModalMessages.CouldNotStartPlaybackNetworkBusyTapRetry;
        }

        if (failedPlayItem != null &&
            probeOutcome == CdnUrlProbeOutcome.NotFoundOrGone &&
            failedPlayItem.CdnStaleUrlRecoveryConsumed &&
            !failedPlayItem.CdnStaleUrlRefetchReplayIssued)
        {
            return AppConstants.Media.PlaybackModalMessages.CouldNotUpdatePlaybackLinksTapRetry;
        }

        if (failedPlayItem != null && failedPlayItem.CdnStaleUrlRefetchReplayIssued)
        {
            return AppConstants.Media.PlaybackModalMessages.StillCouldNotPlayAfterUpdatingLinksTapRetry;
        }

        return AppConstants.Media.PlaybackModalMessages.CouldNotStartPlaybackCheckConnectionTapRetry;
    }

    private async Task HandleIndefiniteLastTrackEndedAsync(IndefiniteLastTrackEndedArgs args)
    {
        var playlist = args.Playlist;
        var currentTrackIndex = args.CurrentTrackIndex;
        var currentScheduleId = args.CurrentScheduleId;
        var tryAppendNextTrackAsync = args.TryAppendNextTrackAsync;
        var setCurrentTrackIndex = args.SetCurrentTrackIndex;
        var playCurrentTrackAsync = args.PlayCurrentTrackAsync;
        var showPlaybackErrorInModalKeepSessionAsync = args.ShowPlaybackErrorInModalKeepSessionAsync;
        var getIsAlarm = args.GetIsAlarm;

        var appended = await tryAppendNextTrackAsync();
        var canAdvance = appended && playlist.Count > currentTrackIndex + 1;

        if (canAdvance)
        {
            await MarkCurrentTrackAsFinishedAsync(playlist, currentTrackIndex);

            dispatcher.Dispatch(new PlaybackTrackTransitionStartedAction());
            dispatcher.Dispatch(new SetAutoAdvancingAction(true));

            var nextTrackIndex = currentTrackIndex + 1;
            setCurrentTrackIndex(nextTrackIndex);
            navigationManager.NotifyNavigationChanged(playlist, nextTrackIndex);
            logger.Information(
                AppConstants.Logging.PlaybackEventHandlerDiagnosticsLog.IndefiniteAdvancingToNextTrack,
                currentScheduleId, nextTrackIndex, playlist.Count);
            await playCurrentTrackAsync(false);
            return;
        }

        if (appended)
        {
            logger.Warning(
                AppConstants.Logging.PlaybackEventHandlerDiagnosticsLog.HandleMediaEndedAppendSucceededCannotAdvance,
                currentScheduleId, currentTrackIndex, playlist.Count);
        }

        var finishedMeta = playlist[currentTrackIndex].PlayItem?.Metadata;
        if (finishedMeta != null && finishedMeta.ScheduleId > 0)
        {
            try
            {
                await playlistService.PersistSchedulePointerToFinishedTrackAsync(finishedMeta);
            }
            catch (Exception ex)
            {
                logger.Error(ex,
                    AppConstants.Logging.PlaybackEventHandlerDiagnosticsLog.HandleMediaEndedFailedPersistSchedulePointerAfterIndefiniteAppend,
                    finishedMeta.ScheduleId);
            }
        }

        logger.Warning(
            AppConstants.Logging.PlaybackEventHandlerDiagnosticsLog.HandleMediaEndedIndefiniteAppendFailedShowingErrorModal,
            currentScheduleId);
        await showPlaybackErrorInModalKeepSessionAsync(
            AppConstants.Media.PlaybackModalMessages.CouldNotLoadNextPartCheckConnectionTapRetry,
            getIsAlarm());
    }

    private sealed class IndefiniteLastTrackEndedArgs
    {
        public required List<AudioPlayerTrack> Playlist { get; init; }
        public required int CurrentTrackIndex { get; init; }
        public int? CurrentScheduleId { get; init; }
        public required Func<Task<bool>> TryAppendNextTrackAsync { get; init; }
        public required Action<int> SetCurrentTrackIndex { get; init; }
        public required Func<bool, Task> PlayCurrentTrackAsync { get; init; }
        public required Func<string, bool, Task> ShowPlaybackErrorInModalKeepSessionAsync { get; init; }
        public required Func<bool> GetIsAlarm { get; init; }
    }

    private async Task MarkCurrentTrackAsFinishedAsync(List<AudioPlayerTrack>? playlist, int currentTrackIndex)
    {
        if (playlist == null || currentTrackIndex < 0 || currentTrackIndex >= playlist.Count)
        {
            return;
        }

        try
        {
            var track = playlist[currentTrackIndex];

            if (track.PlayItem.Metadata.PlayType == PlayType.Music)
            {
                return;
            }

            await playlistService.MarkTrackAsFinished(track.PlayItem.Metadata);
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.PlaybackDiagnosticsLog.ErrorMarkingTrackAsFinished);
        }
    }
}
