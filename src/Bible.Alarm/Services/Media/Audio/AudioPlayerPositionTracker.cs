#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Stores.Actions.Playback;
using CommunityToolkit.Mvvm.Messaging;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media.Audio;

public class AudioPlayerPositionTracker
{
    private readonly IDispatcher dispatcher;
    private DateTime? lastPositionUpdateTime;
    private TimeSpan lastDuration = TimeSpan.Zero;
    private const int PositionUpdateIntervalMs = 500;

    public AudioPlayerPositionTracker(IDispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
    }

    public void ResetDuration()
    {
        lastDuration = TimeSpan.Zero;
    }

    /// <summary>
    /// Sends an immediate position reset so the UI progress bar moves to the start of the new track
    /// instead of staying at the end of the previous track until the platform reports new position (e.g. Windows delay).
    /// </summary>
    public static void SendPositionResetForNewTrack()
    {
        WeakReferenceMessenger.Default.Send(new PlaybackPositionChangedMessage
        {
            CurrentPosition = TimeSpan.Zero,
            Duration = null
        });
    }

    public void UpdateDuration(TimeSpan currentDuration)
    {
        if (currentDuration != lastDuration && currentDuration > TimeSpan.Zero)
        {
            lastDuration = currentDuration;
            dispatcher.Dispatch(new PlaybackDurationChangedAction
            {
                Duration = currentDuration
            });
        }
    }

    public void SendPositionUpdate(TimeSpan? currentPosition, TimeSpan duration)
    {
        // Throttle position updates to 500ms
        var now = DateTime.UtcNow;
        if (lastPositionUpdateTime.HasValue)
        {
            var timeSinceLastUpdate = (now - lastPositionUpdateTime.Value).TotalMilliseconds;
            if (timeSinceLastUpdate < PositionUpdateIntervalMs)
            {
                return;
            }
        }

        lastPositionUpdateTime = now;

        // Send position update via MVVM messaging (include duration for Android Auto progress bar)
        WeakReferenceMessenger.Default.Send(new PlaybackPositionChangedMessage
        {
            CurrentPosition = currentPosition,
            Duration = duration > TimeSpan.Zero ? duration : null
        });

        // Check if duration changed
        UpdateDuration(duration);
    }
}

