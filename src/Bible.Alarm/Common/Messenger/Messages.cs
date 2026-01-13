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
    
    /// <summary>
    /// Current track download progress (0.0 to 1.0).
    /// When downloading, this shows byte-level progress for the current track.
    /// </summary>
    public double CurrentTrackProgress { get; init; }
    
    /// <summary>
    /// Bytes downloaded for the current track.
    /// </summary>
    public long BytesDownloaded { get; init; }
    
    /// <summary>
    /// Total bytes expected for the current track (if known).
    /// </summary>
    public long? TotalBytes { get; init; }
    
    /// <summary>
    /// Total bytes downloaded across all tracks (for overall progress calculation).
    /// </summary>
    public long TotalBytesDownloaded { get; init; }
    
    /// <summary>
    /// Total bytes expected across all tracks (if known, for overall progress calculation).
    /// </summary>
    public long? TotalBytesExpected { get; init; }
}

/// <summary>
/// Message sent when the Next button is pressed in system media controls.
/// Used by Android (MediaSession), iOS (MPRemoteCommandCenter), and Windows (SMTC).
/// </summary>
public class NextButtonPressedMessage
{
}

/// <summary>
/// Message sent when the Previous button is pressed in system media controls.
/// Used by Android (MediaSession), iOS (MPRemoteCommandCenter), and Windows (SMTC).
/// </summary>
public class PreviousButtonPressedMessage
{
}

/// <summary>
/// Message sent when the Play button is pressed in system media controls.
/// </summary>
public class PlayButtonPressedMessage
{
}

/// <summary>
/// Message sent when the Pause button is pressed in system media controls.
/// </summary>
public class PauseButtonPressedMessage
{
}

/// <summary>
/// Message sent when the Fast Forward button is pressed in system media controls.
/// </summary>
public class SeekForwardButtonPressedMessage
{
}

/// <summary>
/// Message sent when the Rewind button is pressed in system media controls.
/// </summary>
public class SeekBackwardButtonPressedMessage
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
