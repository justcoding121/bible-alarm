#nullable enable

using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Audio;
using Bible.Alarm.Stores.Actions.Playback;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class AudioPlayerPositionTrackerTests
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

    private sealed class MessengerRecipient<T> : IRecipient<T>, IDisposable
        where T : class
    {
        public List<T> Received { get; } = [];

        public MessengerRecipient() =>
            WeakReferenceMessenger.Default.Register<T>(this);

        public void Receive(T message) => Received.Add(message);

        public void Dispose() =>
            WeakReferenceMessenger.Default.Unregister<T>(this);
    }

    [Fact]
    public void SendPositionResetForNewTrack_publishes_zero_position()
    {
        using var recipient = new MessengerRecipient<PlaybackPositionChangedMessage>();

        AudioPlayerPositionTracker.SendPositionResetForNewTrack();

        var msg = Assert.Single(recipient.Received);
        Assert.Equal(TimeSpan.Zero, msg.CurrentPosition);
        Assert.Null(msg.Duration);
    }

    [Fact]
    public void UpdateDuration_dispatches_only_when_duration_changes_and_positive()
    {
        var dispatcher = new RecordingDispatcher();
        var sut = new AudioPlayerPositionTracker(dispatcher);

        sut.UpdateDuration(TimeSpan.FromSeconds(10));
        sut.UpdateDuration(TimeSpan.FromSeconds(10));
        sut.UpdateDuration(TimeSpan.Zero);

        var durationAction = Assert.Single(dispatcher.Dispatched);
        var payload = Assert.IsType<PlaybackDurationChangedAction>(durationAction);
        Assert.Equal(TimeSpan.FromSeconds(10), payload.Duration);
    }

    [Fact]
    public void SendPositionUpdate_throttles_rapid_calls()
    {
        using var recipient = new MessengerRecipient<PlaybackPositionChangedMessage>();
        var dispatcher = new RecordingDispatcher();
        var sut = new AudioPlayerPositionTracker(dispatcher);

        sut.SendPositionUpdate(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60));
        sut.SendPositionUpdate(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60));

        Assert.Single(recipient.Received);
    }

    [Fact]
    public async Task SendPositionUpdate_after_interval_sends_again()
    {
        using var recipient = new MessengerRecipient<PlaybackPositionChangedMessage>();
        var dispatcher = new RecordingDispatcher();
        var sut = new AudioPlayerPositionTracker(dispatcher);

        sut.SendPositionUpdate(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60));
        await Task.Delay(550);
        sut.SendPositionUpdate(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60));

        Assert.Equal(2, recipient.Received.Count);
    }

    [Fact]
    public void ResetDuration_allows_repeat_duration_dispatch()
    {
        var dispatcher = new RecordingDispatcher();
        var sut = new AudioPlayerPositionTracker(dispatcher);

        sut.UpdateDuration(TimeSpan.FromSeconds(5));
        sut.ResetDuration();
        sut.UpdateDuration(TimeSpan.FromSeconds(5));

        Assert.Equal(2, dispatcher.Dispatched.Count);
    }
}
