#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Manages navigation state for playback (next/previous track capabilities).
/// Separated from PlaybackService for better modularity.
/// </summary>
public sealed class PlaybackNavigationManager
{
    private readonly IDispatcher dispatcher;
    private readonly ILogger logger;

    public PlaybackNavigationManager(IDispatcher dispatcher, ILogger logger)
    {
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    public bool CanPlayNext(List<AudioPlayerTrack>? playlist, int currentTrackIndex)
    {
        return playlist is not null &&
               currentTrackIndex >= 0 &&
               currentTrackIndex < playlist.Count - 1;
    }

    public bool CanPlayPrevious(List<AudioPlayerTrack>? playlist, int currentTrackIndex)
    {
        return playlist is not null &&
               currentTrackIndex > 0;
    }

    public void NotifyNavigationChanged(List<AudioPlayerTrack>? playlist, int currentTrackIndex)
    {
        var canPlayNext = CanPlayNext(playlist, currentTrackIndex);
        var canPlayPrevious = CanPlayPrevious(playlist, currentTrackIndex);

        // Dispatch Fluxor action
        dispatcher.Dispatch(new PlaybackNavigationChangedAction(canPlayNext, canPlayPrevious));
    }
}

