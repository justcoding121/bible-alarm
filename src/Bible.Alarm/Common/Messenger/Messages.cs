#nullable enable

namespace Bible.Alarm.Common.Messenger;

// Message classes for WeakReferenceMessenger
/// <summary>
/// App initialization message. Sent after bootstrap completes to trigger navigation to Home page.
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

/// <summary>
/// Message sent when the Next button is pressed in Android system media controls.
/// </summary>
public class NextButtonPressedMessage
{
}

/// <summary>
/// Message sent when the Previous button is pressed in Android system media controls.
/// </summary>
public class PreviousButtonPressedMessage
{
}

/// <summary>
/// Message sent to MediaElementService to destroy the MediaElement after MediaSession release.
/// MediaElement will be recreated automatically by GetMediaElementAsync() when needed for the next playlist.
/// This ensures a fresh ExoPlayer instance is created for each new playback session.
/// </summary>
public class DestroyMediaElementMessage
{
}
