#nullable enable
using Bible.Alarm.Services.Media.Models;

namespace Bible.Alarm.Common.Messenger;

// Message classes for WeakReferenceMessenger
public class InitializedMessage
{
}

public class ShowAlarmModalMessage
{
}

public class HideAlarmModalMessage
{
}

public class ShowMediaProgressModalMessage
{
}

public class HideMediaProgressModalMessage
{
}

public class MediaProgressMessage(object value)
{
    public object Value { get; } = value;
}

public class TrackChangedMessage(int value)
{
    public int Value { get; } = value;
}

public class ShowToastMessage(string value)
{
    public string Value { get; } = value;
}

public class ClearToastsMessage
{
}

public class AudioMetadataMessage
{
    public string? Title { get; init; }
    public string? Artist { get; init; }
    public string? Album { get; init; }
    public string? ArtworkUrl { get; init; }
}

public class AudioPositionMessage
{
    public TimeSpan? CurrentPosition { get; init; }
    public TimeSpan Duration { get; init; }
}

public class AudioStatusMessage
{
    public PlayStatus Status { get; init; }
}

