#nullable enable

namespace Bible.Alarm.Common.Messenger;

// Message classes for WeakReferenceMessenger
/// <summary>
/// App initialization message. Should use ObservableMessenger.InitializationMessenger
/// to ensure the message is not lost if sent before recipient registration.
/// </summary>
public class InitializedMessage
{
}

public class ShowToastMessage(string value)
{
    public string Value { get; } = value;
}

/// <summary>
/// Message sent when media playback position changes.
/// Used for high-frequency position updates instead of Fluxor state.
/// </summary>
public class PlaybackPositionChangedMessage
{
    public TimeSpan? CurrentPosition { get; init; }
}

/// <summary>
/// Message sent when playback preparation progress changes (tracks being loaded).
/// Used for frequent progress updates instead of Fluxor state.
/// </summary>
public class PlaybackPreparationProgressMessage
{
    public int LoadedTracks { get; init; }
    public int TotalTracks { get; init; }
}