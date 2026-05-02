#nullable enable

using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class PlaybackNavigationManagerTests
{
    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
            Dispatched.Add(action);
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
        }
    }

    private static TrackMetadata Meta() =>
        new()
        {
            ScheduleId = 1,
            IsBibleContent = true,
            LanguageCode = "E",
            PublicationCode = "nwt",
            SectionCode = "1",
            TrackCode = "1",
            LookUpPath = "/x"
        };

    [Fact]
    public void NotifyNavigationChanged_null_playlist_dispatches_both_disabled()
    {
        var dispatcher = new RecordingDispatcher();
        var sut = new PlaybackNavigationManager(dispatcher);

        sut.NotifyNavigationChanged(null, 0);

        var action = Assert.Single(dispatcher.Dispatched);
        var nav = Assert.IsType<PlaybackNavigationChangedAction>(action);
        Assert.False(nav.CanPlayNext);
        Assert.False(nav.CanPlayPrevious);
    }

    [Fact]
    public void NotifyNavigationChanged_empty_playlist_dispatches_both_disabled()
    {
        var dispatcher = new RecordingDispatcher();
        var sut = new PlaybackNavigationManager(dispatcher);

        sut.NotifyNavigationChanged([], 0);

        var nav = Assert.IsType<PlaybackNavigationChangedAction>(Assert.Single(dispatcher.Dispatched));
        Assert.False(nav.CanPlayNext);
        Assert.False(nav.CanPlayPrevious);
    }

    [Fact]
    public void NotifyNavigationChanged_with_tracks_dispatches_both_enabled()
    {
        var dispatcher = new RecordingDispatcher();
        var sut = new PlaybackNavigationManager(dispatcher);
        var playlist = new List<AudioPlayerTrack>
        {
            new() { PlayItem = new PlayItem(Meta(), "https://a") }
        };

        sut.NotifyNavigationChanged(playlist, 0);

        var nav = Assert.IsType<PlaybackNavigationChangedAction>(Assert.Single(dispatcher.Dispatched));
        Assert.True(nav.CanPlayNext);
        Assert.True(nav.CanPlayPrevious);
    }
}
