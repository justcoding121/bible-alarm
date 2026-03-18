#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
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
    private readonly Func<List<AudioPlayerTrack>?> getPlaylist;
    private readonly Func<int> getCurrentTrackIndex;
    private readonly Action<int> setCurrentTrackIndex;
    private readonly Func<int?> getCurrentScheduleId;
    private readonly Func<bool> getIsIndefinitePlayback;
    private readonly Func<Task<bool>> tryAppendNextTrackAsync;
    private readonly Func<bool, Task> playCurrentTrackAsync;
    private readonly Func<bool, Task> stopAsyncInternal;
    private readonly Func<Task> handlePlaybackFailureAsync;
    private readonly Func<bool> getIsManualNavigationPending;
    private readonly Func<bool> getIsAlarm;
    private readonly Func<string, bool, Task> showPlaybackErrorInModalKeepSessionAsync;
    private readonly Func<int, bool> isPlaybackEstablishedForTrack;

    public PlaybackMediaEventAdapter(
        PlaybackEventHandler eventHandler,
        ProgressTracker progressTracker,
        ILogger logger,
        Func<List<AudioPlayerTrack>?> getPlaylist,
        Func<int> getCurrentTrackIndex,
        Action<int> setCurrentTrackIndex,
        Func<int?> getCurrentScheduleId,
        Func<bool> getIsIndefinitePlayback,
        Func<Task<bool>> tryAppendNextTrackAsync,
        Func<bool, Task> playCurrentTrackAsync,
        Func<bool, Task> stopAsyncInternal,
        Func<Task> handlePlaybackFailureAsync,
        Func<bool> getIsManualNavigationPending,
        Func<bool> getIsAlarm,
        Func<string, bool, Task> showPlaybackErrorInModalKeepSessionAsync,
        Func<int, bool> isPlaybackEstablishedForTrack)
    {
        this.eventHandler = eventHandler;
        this.progressTracker = progressTracker;
        this.logger = logger;
        this.getPlaylist = getPlaylist;
        this.getCurrentTrackIndex = getCurrentTrackIndex;
        this.setCurrentTrackIndex = setCurrentTrackIndex;
        this.getCurrentScheduleId = getCurrentScheduleId;
        this.getIsIndefinitePlayback = getIsIndefinitePlayback;
        this.tryAppendNextTrackAsync = tryAppendNextTrackAsync;
        this.playCurrentTrackAsync = playCurrentTrackAsync;
        this.stopAsyncInternal = stopAsyncInternal;
        this.handlePlaybackFailureAsync = handlePlaybackFailureAsync;
        this.getIsManualNavigationPending = getIsManualNavigationPending;
        this.getIsAlarm = getIsAlarm;
        this.showPlaybackErrorInModalKeepSessionAsync = showPlaybackErrorInModalKeepSessionAsync;
        this.isPlaybackEstablishedForTrack = isPlaybackEstablishedForTrack;
    }

    public async void OnMediaEnded(object? sender, EventArgs e)
    {
        try
        {
            progressTracker.Stop();
            await eventHandler.HandleMediaEndedAsync(
                getPlaylist(),
                getCurrentTrackIndex,
                setCurrentTrackIndex,
                getCurrentScheduleId(),
                getIsIndefinitePlayback(),
                tryAppendNextTrackAsync,
                playCurrentTrackAsync,
                stopAsyncInternal,
                handlePlaybackFailureAsync,
                getIsManualNavigationPending,
                getIsAlarm,
                showPlaybackErrorInModalKeepSessionAsync);
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
            var playlist = getPlaylist();
            var index = getCurrentTrackIndex();
            var trackUri = playlist != null && index >= 0 && index < playlist.Count
                ? playlist[index].Uri ?? "Unknown"
                : "Unknown";
            var trackUrl = playlist != null && index >= 0 && index < playlist.Count
                ? playlist[index].PlayItem?.Url ?? "Unknown"
                : "Unknown";

            await eventHandler.HandleMediaFailedAsync(
                playlist,
                getCurrentTrackIndex,
                setCurrentTrackIndex,
                trackUri,
                trackUrl,
                playCurrentTrackAsync,
                handlePlaybackFailureAsync,
                getIsManualNavigationPending,
                getIsAlarm,
                showPlaybackErrorInModalKeepSessionAsync,
                isPlaybackEstablishedForTrack);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error handling media failed event");
            try
            {
                await showPlaybackErrorInModalKeepSessionAsync("Playback failed. Tap Retry.", getIsAlarm());
            }
            catch (Exception innerEx)
            {
                logger.Error(innerEx, "Failure recovery after media failed handler error");
            }
        }
    }
}
