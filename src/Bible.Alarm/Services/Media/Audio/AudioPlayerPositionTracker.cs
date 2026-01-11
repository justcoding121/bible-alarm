#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Stores.Actions.Playback;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media.Audio;

/// <summary>
/// Handles position tracking and updates for AudioPlayer.
/// Separated from AudioPlayer for better modularity.
/// </summary>
public class AudioPlayerPositionTracker
{
    private readonly ILogger logger;
    private readonly IDispatcher dispatcher;
    private DateTime? lastPositionUpdateTime;
    private TimeSpan lastDuration = TimeSpan.Zero;
    private const int PositionUpdateIntervalMs = 500;

    public AudioPlayerPositionTracker(ILogger logger, IDispatcher dispatcher)
    {
        this.logger = logger;
        this.dispatcher = dispatcher;
    }

    public void ResetDuration()
    {
        lastDuration = TimeSpan.Zero;
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

        // Send position update via MVVM messaging
        WeakReferenceMessenger.Default.Send(new PlaybackPositionChangedMessage
        {
            CurrentPosition = currentPosition
        });

        // Check if duration changed
        UpdateDuration(duration);
    }
}

