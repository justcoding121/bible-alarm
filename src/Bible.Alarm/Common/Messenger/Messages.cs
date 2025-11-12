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

