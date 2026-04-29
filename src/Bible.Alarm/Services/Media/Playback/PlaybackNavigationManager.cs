#nullable enable
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
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

    private static bool CanPlayNext(List<AudioPlayerTrack>? playlist, int currentTrackIndex)
    {
        // Next is always enabled during playback sessions.
        return playlist is not null && playlist.Count > 0;
    }

    private static bool CanPlayPrevious(List<AudioPlayerTrack>? playlist, int currentTrackIndex)
    {
        // Previous is always enabled during playback sessions.
        return playlist is not null && playlist.Count > 0;
    }

    public void NotifyNavigationChanged(List<AudioPlayerTrack>? playlist, int currentTrackIndex)
    {
        var canPlayNext = CanPlayNext(playlist, currentTrackIndex);
        var canPlayPrevious = CanPlayPrevious(playlist, currentTrackIndex);

        // Dispatch Fluxor action
        dispatcher.Dispatch(new PlaybackNavigationChangedAction(canPlayNext, canPlayPrevious));
    }
}

