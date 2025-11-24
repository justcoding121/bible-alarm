#nullable enable

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

public class TrackChangedMessage(int value)
{
    public int Value { get; } = value;
}

public class ShowToastMessage(string value)
{
    public string Value { get; } = value;
}
