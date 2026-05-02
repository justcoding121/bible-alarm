#nullable enable

using Bible.Alarm.Services.Media.Audio;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Tests.Support;
using CommunityToolkit.Maui.Primitives;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class AudioPlayerStateManagerTests
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

    [Fact]
    public void ShouldIgnoreStateChange_true_when_resetting_and_not_stopped()
    {
        var d = new RecordingDispatcher();
        var sut = new AudioPlayerStateManager(TestLogging.CreateLogger(), d)
        {
            IsResetting = true,
            Status = PlayStatus.Loading,
        };

        Assert.True(sut.ShouldIgnoreStateChange(MediaElementState.Playing, mediaElement: null));
    }

    [Fact]
    public void ShouldIgnoreStateChange_true_for_stopped_while_status_loading()
    {
        var d = new RecordingDispatcher();
        var sut = new AudioPlayerStateManager(TestLogging.CreateLogger(), d) { Status = PlayStatus.Loading };

        Assert.True(sut.ShouldIgnoreStateChange(MediaElementState.Stopped, mediaElement: null));
    }

    [Fact]
    public void ShouldIgnoreStateChange_when_source_null_sets_stopped_and_dispatches()
    {
        var d = new RecordingDispatcher();
        var sut = new AudioPlayerStateManager(TestLogging.CreateLogger(), d)
        {
            Status = PlayStatus.Playing,
        };

        Assert.True(sut.ShouldIgnoreStateChange(MediaElementState.Playing, mediaElement: null));
        Assert.Equal(PlayStatus.Stopped, sut.Status);
        var statusAction = Assert.Single(d.Dispatched);
        var changed = Assert.IsType<PlaybackStatusChangedAction>(statusAction);
        Assert.Equal(PlayStatus.Stopped, changed.Status);
    }

    [Fact]
    public void ShouldIgnoreStateChange_false_when_stopped_with_null_source()
    {
        var d = new RecordingDispatcher();
        var sut = new AudioPlayerStateManager(TestLogging.CreateLogger(), d);

        Assert.False(sut.ShouldIgnoreStateChange(MediaElementState.Stopped, mediaElement: null));
    }

    [Fact]
    public void UpdateStatus_maps_media_element_state_and_dispatches()
    {
        var d = new RecordingDispatcher();
        var sut = new AudioPlayerStateManager(TestLogging.CreateLogger(), d);

        sut.UpdateStatus(MediaElementState.Buffering);

        Assert.Equal(PlayStatus.Loading, sut.Status);
        Assert.IsType<PlaybackStatusChangedAction>(Assert.Single(d.Dispatched));
    }

    [Fact]
    public void UpdateStatus_while_seeking_buffering_preserves_status_before_seek()
    {
        var d = new RecordingDispatcher();
        var sut = new AudioPlayerStateManager(TestLogging.CreateLogger(), d);
        sut.UpdateStatus(MediaElementState.Playing);
        d.Dispatched.Clear();

        sut.StartSeeking();
        sut.UpdateStatus(MediaElementState.Buffering);

        Assert.Equal(PlayStatus.Playing, sut.Status);
        Assert.Single(d.Dispatched);
    }

    [Fact]
    public void EndSeekingAndReevaluateState_redispatches_when_still_buffering()
    {
        var d = new RecordingDispatcher();
        var sut = new AudioPlayerStateManager(TestLogging.CreateLogger(), d);
        sut.StartSeeking();
        d.Dispatched.Clear();

        sut.EndSeekingAndReevaluateState(MediaElementState.Buffering);

        Assert.Equal(PlayStatus.Loading, sut.Status);
        Assert.Single(d.Dispatched);
    }

    [Fact]
    public void Reset_clears_transition_flags_and_dispatches_stopped()
    {
        var d = new RecordingDispatcher();
        var sut = new AudioPlayerStateManager(TestLogging.CreateLogger(), d);
        sut.UpdateStatus(MediaElementState.Playing);
        d.Dispatched.Clear();

        sut.Reset();

        Assert.Equal(PlayStatus.Stopped, sut.Status);
        Assert.IsType<PlaybackStatusChangedAction>(Assert.Single(d.Dispatched));
    }
}
