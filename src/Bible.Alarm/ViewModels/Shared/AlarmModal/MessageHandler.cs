#nullable enable
using Bible.Alarm.Common.Messenger;
using CommunityToolkit.Mvvm.Messaging;

namespace Bible.Alarm.ViewModels.Shared.AlarmModal;

/// <summary>
/// Handles messaging operations for the alarm modal.
/// </summary>
public sealed class MessageHandler
{
    private readonly PositionManager positionManager;

    public MessageHandler(PositionManager positionManager)
    {
        this.positionManager = positionManager;
    }

    /// <summary>
    /// Handles playback position changed messages.
    /// </summary>
    public void HandlePlaybackPositionMessage(
        PlaybackPositionChangedMessage message,
        TimeSpan currentDuration,
        Func<double, bool> shouldIgnorePositionUpdate,
        Action<string> setCurrentTime,
        Action<double> setProgress,
        Action updateProgressText)
    {
        if (message.CurrentPosition.HasValue && currentDuration.TotalSeconds > 0)
        {
            var actualProgress = message.CurrentPosition.Value.TotalSeconds / currentDuration.TotalSeconds;
            if (shouldIgnorePositionUpdate(actualProgress))
            {
                return;
            }
        }

        // Update position from message
        positionManager.UpdatePositionFromMessage(
            message,
            currentDuration,
            (newProgress) => shouldIgnorePositionUpdate(newProgress),
            setCurrentTime,
            setProgress);

        updateProgressText();
    }

    /// <summary>
    /// Handles playback preparation progress messages.
    /// </summary>
    public void HandlePreparationProgressMessage(
        PlaybackPreparationProgressMessage message,
        Action<int, int, double, bool> updatePreparationState,
        Action updateProgressText)
    {
        if (positionManager.HandlePreparationProgressMessage(message, updatePreparationState))
        {
            updateProgressText();
        }
    }

    /// <summary>
    /// Registers message handlers.
    /// </summary>
    public void RegisterHandlers(
        IRecipient<PlaybackPositionChangedMessage> positionRecipient,
        IRecipient<PlaybackPreparationProgressMessage> preparationRecipient)
    {
        WeakReferenceMessenger.Default.Register<PlaybackPositionChangedMessage>(positionRecipient);
        WeakReferenceMessenger.Default.Register<PlaybackPreparationProgressMessage>(preparationRecipient);
    }

    /// <summary>
    /// Unregisters message handlers.
    /// </summary>
    public void UnregisterHandlers(
        IRecipient<PlaybackPositionChangedMessage> positionRecipient,
        IRecipient<PlaybackPreparationProgressMessage> preparationRecipient)
    {
        WeakReferenceMessenger.Default.Unregister<PlaybackPositionChangedMessage>(positionRecipient);
        WeakReferenceMessenger.Default.Unregister<PlaybackPreparationProgressMessage>(preparationRecipient);
    }
}
