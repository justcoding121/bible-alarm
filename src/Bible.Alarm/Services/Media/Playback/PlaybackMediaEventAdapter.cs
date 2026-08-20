#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Bridges AudioPlayer MediaEnded/MediaFailed into PlaybackEventHandler without coupling PlaybackService to those event paths.
/// </summary>
public sealed class PlaybackMediaEventAdapter
{
    /// <summary>
    /// Playlist/session closures the media-event adapter needs from PlaybackService without taking the service itself.
    /// </summary>
    public readonly record struct Callbacks(
        Func<List<AudioPlayerTrack>?> GetPlaylist,
        Func<int> GetCurrentTrackIndex,
        Action<int> SetCurrentTrackIndex,
        Func<int?> GetCurrentScheduleId,
        Func<bool> GetIsIndefinitePlayback,
        Func<Task<bool>> TryAppendNextTrackAsync,
        Func<bool, Task> PlayCurrentTrackAsync,
        Func<bool, Task> StopAsyncInternal,
        Func<bool> GetIsManualNavigationPending,
        Func<bool> GetIsAlarm,
        Func<string, bool, Task> ShowPlaybackErrorInModalKeepSessionAsync,
        Func<int, bool> IsPlaybackEstablishedForTrack);

    private readonly PlaybackEventHandler eventHandler;
    private readonly ProgressTracker progressTracker;
    private readonly ILogger logger;
    private readonly Callbacks callbacks;

    public PlaybackMediaEventAdapter(
        PlaybackEventHandler eventHandler,
        ProgressTracker progressTracker,
        ILogger logger,
        Callbacks callbacks)
    {
        this.eventHandler = eventHandler;
        this.progressTracker = progressTracker;
        this.logger = logger;
        this.callbacks = callbacks;
    }

    public async void OnMediaEnded(object? sender, EventArgs e)
    {
        try
        {
            // For finite playback ending on the last track, enter stopping UI immediately
            // before any async work (DB writes, track marking). The MediaElement fires
            // Paused→Stopped state changes before this handler runs, which disables only
            // prev/next via UpdateControlsFromState. Sending BeginStoppingPlaybackMessage
            // here ensures all controls are disabled simultaneously.
            if (!callbacks.GetIsManualNavigationPending())
            {
                var playlist = callbacks.GetPlaylist();
                var currentIndex = callbacks.GetCurrentTrackIndex();
                var isLastFiniteTrack = !callbacks.GetIsIndefinitePlayback()
                    && (playlist == null || currentIndex >= playlist.Count - 1);

                if (isLastFiniteTrack)
                {
                    WeakReferenceMessenger.Default.Send(new BeginStoppingPlaybackMessage());
                }
            }

            progressTracker.Stop();
            await eventHandler.HandleMediaEndedAsync(new PlaybackMediaEndedRequest(
                callbacks.GetPlaylist(),
                callbacks.GetCurrentTrackIndex,
                callbacks.SetCurrentTrackIndex,
                callbacks.GetCurrentScheduleId(),
                callbacks.GetIsIndefinitePlayback(),
                callbacks.TryAppendNextTrackAsync,
                callbacks.PlayCurrentTrackAsync,
                callbacks.StopAsyncInternal,
                callbacks.GetIsManualNavigationPending,
                callbacks.GetIsAlarm,
                callbacks.ShowPlaybackErrorInModalKeepSessionAsync));
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.PlaybackDiagnosticsLog.ErrorHandlingMediaEndedEvent);
        }
    }

    public async void OnMediaFailed(object? sender, EventArgs e)
    {
        try
        {
            var playlist = callbacks.GetPlaylist();
            var index = callbacks.GetCurrentTrackIndex();
            var trackUri = playlist != null && index >= 0 && index < playlist.Count
                ? playlist[index].Uri ?? MediaTrackTitleHelper.UnknownTitle
                : MediaTrackTitleHelper.UnknownTitle;
            var trackUrl = playlist != null && index >= 0 && index < playlist.Count
                ? playlist[index].PlayItem?.Url ?? MediaTrackTitleHelper.UnknownTitle
                : MediaTrackTitleHelper.UnknownTitle;

            await eventHandler.HandleMediaFailedAsync(new PlaybackMediaFailedRequest(
                playlist,
                callbacks.GetCurrentTrackIndex,
                trackUri,
                trackUrl,
                callbacks.PlayCurrentTrackAsync,
                callbacks.GetIsManualNavigationPending,
                callbacks.GetIsAlarm,
                callbacks.ShowPlaybackErrorInModalKeepSessionAsync,
                callbacks.IsPlaybackEstablishedForTrack));
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.PlaybackDiagnosticsLog.ErrorHandlingMediaFailedEvent);
            try
            {
                await callbacks.ShowPlaybackErrorInModalKeepSessionAsync(
                    AppConstants.Media.PlaybackModalMessages.PlaybackFailedTapRetry,
                    callbacks.GetIsAlarm());
            }
            catch (Exception innerEx)
            {
                logger.Error(innerEx, AppConstants.Logging.PlaybackDiagnosticsLog.FailureRecoveryAfterMediaFailedHandlerError);
            }
        }
    }
}
