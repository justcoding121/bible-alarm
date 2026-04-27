#nullable enable

using System;
using System.Threading.Tasks;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Adapts AudioPlayer MediaEnded/MediaFailed events to PlaybackEventHandler. Extracted from PlaybackService for LOC compliance.
/// </summary>
public sealed class PlaybackMediaEventAdapter
{
    private readonly PlaybackEventHandler eventHandler;
    private readonly ProgressTracker progressTracker;
    private readonly ILogger logger;
    private readonly PlaybackMediaEventAdapterCallbacks callbacks;

    public PlaybackMediaEventAdapter(
        PlaybackEventHandler eventHandler,
        ProgressTracker progressTracker,
        ILogger logger,
        PlaybackMediaEventAdapterCallbacks callbacks)
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
            logger.Error(ex, "Error handling media ended event");
        }
    }

    public async void OnMediaFailed(object? sender, EventArgs e)
    {
        try
        {
            var playlist = callbacks.GetPlaylist();
            var index = callbacks.GetCurrentTrackIndex();
            var trackUri = playlist != null && index >= 0 && index < playlist.Count
                ? playlist[index].Uri ?? "Unknown"
                : "Unknown";
            var trackUrl = playlist != null && index >= 0 && index < playlist.Count
                ? playlist[index].PlayItem?.Url ?? "Unknown"
                : "Unknown";

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
            logger.Error(ex, "Error handling media failed event");
            try
            {
                await callbacks.ShowPlaybackErrorInModalKeepSessionAsync("Playback failed. Tap Retry.", callbacks.GetIsAlarm());
            }
            catch (Exception innerEx)
            {
                logger.Error(innerEx, "Failure recovery after media failed handler error");
            }
        }
    }
}
