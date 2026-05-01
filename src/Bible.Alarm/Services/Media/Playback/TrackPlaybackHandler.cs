#nullable enable
using System.Collections.Generic;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Serilog;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Handles playing a single track.
/// Separated from PlaybackService for better modularity.
/// </summary>
public sealed class TrackPlaybackHandler
{
    private readonly IAudioPlayer audioPlayer;
    private readonly ILogger logger;
    private readonly TrackPreparationHandler trackPreparationHandler;
    private readonly ProgressTracker progressTracker;

    public TrackPlaybackHandler(
        IAudioPlayer audioPlayer,
        ILogger logger,
        TrackPreparationHandler trackPreparationHandler,
        ProgressTracker progressTracker)
    {
        this.audioPlayer = audioPlayer;
        this.logger = logger;
        this.trackPreparationHandler = trackPreparationHandler;
        this.progressTracker = progressTracker;
    }

    public async Task<bool> PlayTrackAsync(PlayTrackRequest request)
    {
        var track = request.Track;
        var currentTrackIndex = request.CurrentTrackIndex;
        var startFromBeginning = request.StartFromBeginning;
        var currentScheduleId = request.CurrentScheduleId;
        Func<bool> isPreparingOrPlaying = request.IsPreparingOrPlaying;
        Func<List<AudioPlayerTrack>?> getPlaylist = request.GetPlaylist;
        Action<bool> setIsPreparingTrack = request.SetIsPreparingTrack;
        var playedBibleTrackKeys = request.PlayedBibleTrackKeys;
        var cancellationToken = request.CancellationToken;

        if (string.IsNullOrEmpty(track.Uri))
        {
            logger.Error(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.CannotPlayTrackUriEmpty,
                currentTrackIndex,
                track.PlayItem?.Url ?? MediaTrackTitleHelper.UnknownTitle);
            return false;
        }

        setIsPreparingTrack(true);
        try
        {
            var preparationOk = await TryPrepareTrackSourceAsync(
                track,
                currentTrackIndex,
                cancellationToken,
                isPreparingOrPlaying,
                getPlaylist);
            if (!preparationOk)
            {
                return false;
            }
        }
        finally
        {
            setIsPreparingTrack(false);
        }

        // Check again after WaitForMediaReadyAsync in case stop was called during the wait.
        // Only check playlist validity (null / index out of range) — a genuine StopAsync sets playlist to null.
        // Do NOT check isPreparingOrPlaying() here: IsPreparingTrack was just cleared in finally,
        // and AudioPlayer.Status may be Stopped from a MediaElement reset (e.g. CDN refresh replay)
        // even though no stop was requested.
        var playlistAfterWait = getPlaylist();
        if (playlistAfterWait == null || currentTrackIndex >= playlistAfterWait.Count)
        {
            logger.Information(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.PlaybackStoppedDuringWaitAborting);
            return false;
        }

        var bibleSession = TryBuildBibleTrackSession(track, playedBibleTrackKeys);
        TimeSpan? seekPosition = await ResolveResumeSeekPositionAsync(
            track,
            startFromBeginning,
            bibleSession,
            playedBibleTrackKeys,
            currentScheduleId);

        MarkBibleTrackPlayedIfNeeded(bibleSession, playedBibleTrackKeys);

#if IOS || ANDROID
        var postPlayNeedsDurationRecheck = false;
        seekPosition = ClampSeekAgainstPrePlayDuration(seekPosition, ref postPlayNeedsDurationRecheck);
#else
        seekPosition = ClampSeekAgainstPrePlayDuration(seekPosition);
#endif

#if !IOS && !ANDROID
        if (seekPosition.HasValue)
        {
            await SeekWithRetryAsync(seekPosition.Value, cancellationToken);
        }
#endif

        cancellationToken.ThrowIfCancellationRequested();

        var playlistBeforePlay = getPlaylist();
        if (playlistBeforePlay == null || currentTrackIndex >= playlistBeforePlay.Count)
        {
            logger.Information(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.PlaybackStoppedBeforePlayAborting);
            return false;
        }

        var didMute = seekPosition.HasValue;
        if (didMute)
        {
            await audioPlayer.SetMutedAsync(true);
        }

        try
        {
            await audioPlayer.PlayAsync();

#if IOS || ANDROID
            await Task.Delay(seekPosition.HasValue ? 300 : 100);

            if (seekPosition.HasValue && postPlayNeedsDurationRecheck)
            {
                seekPosition = RevalidateSeekAgainstPostPlayDuration(seekPosition.Value);
            }

            if (seekPosition.HasValue)
            {
                await SeekWithRetryAsync(seekPosition.Value, cancellationToken);
                await Task.Delay(100, cancellationToken);
            }
#endif
        }
        finally
        {
            if (didMute)
            {
                await audioPlayer.SetMutedAsync(false);
            }
        }

        progressTracker.StartIfBiblePublicationTrack(playlistBeforePlay, currentTrackIndex);

        // Don't clear auto-advancing flag here - let the reducer handle it when status stabilizes to Playing
        // This prevents rapid state changes from causing flicker

        return true;
    }

    private readonly record struct BibleTrackPlaySession(bool IsBibleTrack, string? BibleTrackKey, bool IsFirstEncounter);

    private static BibleTrackPlaySession TryBuildBibleTrackSession(AudioPlayerTrack track, HashSet<string> playedBibleTrackKeys)
    {
        var metadata = track.PlayItem?.Metadata;
        if (metadata == null || metadata.PlayType != PlayType.Bible)
        {
            return new BibleTrackPlaySession(false, null, false);
        }

        var bibleTrackKey =
            $"{metadata.ScheduleId}:{metadata.LanguageCode}:{metadata.PublicationCode}:{metadata.SectionCode ?? "null"}:{metadata.TrackCode}";
        var isFirstEncounter = !playedBibleTrackKeys.Contains(bibleTrackKey);

        return new BibleTrackPlaySession(true, bibleTrackKey, isFirstEncounter);
    }

    private async Task<TimeSpan?> ResolveResumeSeekPositionAsync(
        AudioPlayerTrack track,
        bool startFromBeginning,
        BibleTrackPlaySession bibleSession,
        HashSet<string> playedBibleTrackKeys,
        int? currentScheduleId)
    {
        if (!bibleSession.IsBibleTrack || track.PlayItem?.Metadata == null)
        {
            return null;
        }

        var inMemoryFinishedDuration = track.PlayItem?.Metadata?.FinishedDuration ?? TimeSpan.Zero;

        var shouldCheckSeek =
            !startFromBeginning && bibleSession.IsFirstEncounter && inMemoryFinishedDuration != TimeSpan.Zero;

        if (shouldCheckSeek)
        {
            var shouldResume = await trackPreparationHandler.ShouldResumeFromLastPositionAsync(currentScheduleId);

            if (shouldResume)
            {
                logger.Information(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.ResumeSeekFromInMemoryFinishedDuration,
                    inMemoryFinishedDuration, currentScheduleId);
                return inMemoryFinishedDuration;
            }

            return null;
        }

        if (!startFromBeginning &&
            bibleSession.IsFirstEncounter &&
            inMemoryFinishedDuration == TimeSpan.Zero &&
            currentScheduleId.HasValue &&
            playedBibleTrackKeys.Count == 0)
        {
            var dbFinishedDuration = await trackPreparationHandler.GetScheduleFinishedDurationAsync(currentScheduleId);
            if (dbFinishedDuration > TimeSpan.Zero)
            {
                var shouldResume = await trackPreparationHandler.ShouldResumeFromLastPositionAsync(currentScheduleId);
                if (shouldResume)
                {
                    logger.Warning(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.ResumeFallbackDbFinishedDuration,
                        dbFinishedDuration, currentScheduleId);
                    return dbFinishedDuration;
                }
            }
            else
            {
                logger.Debug(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.ResumeNoSeekBothZero,
                    currentScheduleId);
            }
        }

        return null;
    }

    private static void MarkBibleTrackPlayedIfNeeded(BibleTrackPlaySession bibleSession, HashSet<string> playedBibleTrackKeys)
    {
        if (bibleSession.IsBibleTrack && bibleSession.BibleTrackKey != null)
        {
            playedBibleTrackKeys.Add(bibleSession.BibleTrackKey);
        }
    }

#if IOS || ANDROID

    private TimeSpan? ClampSeekAgainstPrePlayDuration(TimeSpan? seekPosition, ref bool postPlayNeedsDurationRecheck)
    {
        if (!seekPosition.HasValue)
        {
            return seekPosition;
        }

        var prePlayDuration = audioPlayer.Duration;
        if (prePlayDuration > TimeSpan.Zero && seekPosition.Value >= prePlayDuration)
        {
            logger.Warning(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.ResumeSeekExceedsDurationStartingBeginning,
                seekPosition.Value, prePlayDuration);
            return null;
        }

        if (prePlayDuration == TimeSpan.Zero)
        {
            postPlayNeedsDurationRecheck = true;
        }

        return seekPosition;
    }

    private TimeSpan? RevalidateSeekAgainstPostPlayDuration(TimeSpan seekPosition)
    {
        var postPlayDuration = audioPlayer.Duration;
        if (postPlayDuration > TimeSpan.Zero && seekPosition >= postPlayDuration)
        {
            logger.Warning(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.ResumePostPlaySeekExceedsDurationSkippingSeek,
                seekPosition, postPlayDuration);
            return null;
        }

        return seekPosition;
    }

#else

    private TimeSpan? ClampSeekAgainstPrePlayDuration(TimeSpan? seekPosition)
    {
        if (!seekPosition.HasValue)
        {
            return seekPosition;
        }

        var prePlayDuration = audioPlayer.Duration;
        if (prePlayDuration > TimeSpan.Zero && seekPosition.Value >= prePlayDuration)
        {
            logger.Warning(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.ResumeSeekExceedsDurationStartingBeginning,
                seekPosition.Value, prePlayDuration);
            return null;
        }

        return seekPosition;
    }

#endif

    private async Task<bool> TryPrepareTrackSourceAsync(
        AudioPlayerTrack track,
        int currentTrackIndex,
        CancellationToken cancellationToken,
        Func<bool> isPreparingOrPlaying,
        Func<List<AudioPlayerTrack>?> getPlaylist)
    {
        // Sync metadata (including artwork) before opening the file for playback.
        // When the track is cached, the same file is used for playback and for TagLib artwork extraction.
        // If we extract after PrepareAsync, the media player may have the file open and extraction can fail
        // on some platforms (first track shows spinner; dismiss and play again then works). Doing it first
        // ensures artwork is in state before the modal displays and avoids file contention.
        await audioPlayer.SyncMetadataForTrackAsync(track);

        // Set internal status to Loading before changing the source so intermediate
        // MediaElement states (Stopped, Paused) during the source change are filtered
        // by ShouldIgnoreStateChange, preventing rapid play/pause button toggling.
        audioPlayer.NotifyTrackTransitionStarting();

        await audioPlayer.PrepareAsync(track);

        // On Android with queue (SetSourceWithDummyQueue), MediaOpened may not fire when changing tracks.
        // We already synced metadata above; HandleMediaOpenedAsync will run when MediaOpened fires and can
        // refresh if needed (e.g. duration). No need to call SyncMetadataForTrackAsync again here.

        // On iOS, MediaElement may need a brief moment after PrepareAsync before it can play
        // Wait for the media to be in a ready state (not None or Failed)
        // This also gives time for the state to transition from "Opening" to "Paused"
        await trackPreparationHandler.WaitForMediaReadyAsync(cancellationToken);

        // Check if stop was called during PrepareAsync or WaitForMediaReadyAsync
        var playlist = getPlaylist();
        if (!isPreparingOrPlaying() || playlist == null || currentTrackIndex < 0 || currentTrackIndex >= playlist.Count)
        {
            logger.Information(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.PlaybackStoppedDuringPrepareAborting);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Attempts to seek to a position with retry logic for iOS and Android.
    /// On iOS, the AVPlayer may not be fully ready even after WaitForMediaReadyAsync returns,
    /// especially when transitioning between tracks (e.g., music to Bible).
    /// On Android, ExoPlayer may need time after SetSourceWithDummyQueue and PlayAsync before seeking works reliably.
    /// This method retries the seek operation if it fails due to player not being ready.
    /// </summary>
    private async Task SeekWithRetryAsync(TimeSpan position, CancellationToken cancellationToken = default)
    {
#if IOS || ANDROID
        const int maxRetries = 10;
        const int retryDelayMs = 300;
        var seekBudgetStart = DateTime.UtcNow;
        var totalSeekBudget = TimeSpan.FromSeconds(15);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (DateTime.UtcNow - seekBudgetStart > totalSeekBudget)
            {
                logger.Warning(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.SeekTotalBudgetExceeded,
                    totalSeekBudget.TotalSeconds, attempt - 1);
                return;
            }

            var outcome = await TrySeekSingleAttemptAsync(position, attempt, maxRetries, retryDelayMs, cancellationToken);
            if (outcome == SeekAttemptOutcome.Success || outcome == SeekAttemptOutcome.FatalError)
            {
                return;
            }
        }
#else
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await audioPlayer.SeekToAsync(position);
        }
        catch (InvalidOperationException ex)
        {
            logger.Debug(ex, AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.SeekFailedPlayerNotReadySeekableRanges);
        }
        catch (Exception ex)
        {
            // Catch any other exceptions during seek
            logger.Warning(ex, AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.UnexpectedErrorDuringSeekResumeWillStartBeginning);
        }
#endif
    }

#if IOS || ANDROID
    private enum SeekAttemptOutcome
    {
        Retry,
        Success,
        FatalError
    }

    private async Task<SeekAttemptOutcome> TrySeekSingleAttemptAsync(
        TimeSpan position,
        int attempt,
        int maxRetries,
        int retryDelayMs,
        CancellationToken cancellationToken)
    {
        try
        {
            await audioPlayer.SeekToAsync(position);

            await Task.Delay(150, cancellationToken);
            var currentPos = audioPlayer.CurrentPosition;

            if (currentPos.HasValue && Math.Abs(currentPos.Value.TotalSeconds - position.TotalSeconds) < 1.0)
            {
                return SeekAttemptOutcome.Success;
            }

            logger.Warning(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.SeekAttemptWasNoOpWillRetry,
                attempt, position, currentPos);

            if (attempt < maxRetries)
            {
                await Task.Delay(retryDelayMs, cancellationToken);
            }
            else
            {
                logger.Error(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.SeekAllAttemptsNoOpStartingBeginning, maxRetries);
            }

            return SeekAttemptOutcome.Retry;
        }
        catch (InvalidOperationException ex)
        {
            logger.Warning(ex, AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.SeekAttemptFailedInvalidOperation,
                attempt, ex.Message);
            if (attempt < maxRetries)
            {
                await Task.Delay(retryDelayMs, cancellationToken);
            }
            else
            {
                logger.Error(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.SeekAllAttemptsFailedStartingBeginning, maxRetries);
            }

            return SeekAttemptOutcome.Retry;
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.SeekAttemptFailedUnexpectedError, attempt, ex.Message);
            return SeekAttemptOutcome.FatalError;
        }
    }
#endif
}

