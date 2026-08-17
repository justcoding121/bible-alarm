#nullable enable
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media.Playback;

public sealed class PlaybackNavigationManager
{
    private readonly IDispatcher dispatcher;

    public PlaybackNavigationManager(IDispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
    }

    private static bool CanPlayNext(List<AudioPlayerTrack>? playlist)
    {
        // Next is always enabled during playback sessions.
        return playlist is not null && playlist.Count > 0;
    }

    private static bool CanPlayPrevious(List<AudioPlayerTrack>? playlist)
    {
        // Previous is always enabled during playback sessions.
        return playlist is not null && playlist.Count > 0;
    }

    public void NotifyNavigationChanged(List<AudioPlayerTrack>? playlist, int currentTrackIndex)
    {
        var canPlayNext = CanPlayNext(playlist);
        var canPlayPrevious = CanPlayPrevious(playlist);

        // Dispatch Fluxor action
        dispatcher.Dispatch(new PlaybackNavigationChangedAction(canPlayNext, canPlayPrevious));
    }
}

