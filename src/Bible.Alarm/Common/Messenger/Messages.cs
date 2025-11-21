using Bible.Alarm.Services.Media.Models;

namespace Bible.Alarm.Common.Messenger;

// Message classes for WeakReferenceMessenger
public class InitializedMessage(bool value)
{
    public bool Value { get; } = value;
}

public class ShowAlarmModalMessage(object value)
{
    public object Value { get; } = value;
}

public class HideAlarmModalMessage(object value)
{
    public object Value { get; } = value;
}

public class ShowMediaProgressModalMessage(object value)
{
    public object Value { get; } = value;
}

public class HideMediaProgressModalMessage(object value)
{
    public object Value { get; } = value;
}

public class MediaProgressMessage(object value)
{
    public object Value { get; } = value;
}

public class TrackChangedMessage(int value)
{
    public int Value { get; } = value;
}

public class ShowToastMessage(object value)
{
    public object Value { get; } = value;
}

public class ClearToastsMessage(object value)
{
    public object Value { get; } = value;
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

