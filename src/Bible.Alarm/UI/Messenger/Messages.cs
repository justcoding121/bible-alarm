namespace Bible.Alarm.UI.Messenger;

// Message classes for WeakReferenceMessenger
public class InitializedMessage
{
    public bool Value { get; }
    
    public InitializedMessage(bool value)
    {
        Value = value;
    }
}

public class ShowAlarmModalMessage
{
    public object Value { get; }
    
    public ShowAlarmModalMessage(object value)
    {
        Value = value;
    }
}

public class HideAlarmModalMessage
{
    public object Value { get; }
    
    public HideAlarmModalMessage(object value)
    {
        Value = value;
    }
}

public class ShowMediaProgressModalMessage
{
    public object Value { get; }
    
    public ShowMediaProgressModalMessage(object value)
    {
        Value = value;
    }
}

public class HideMediaProgressModalMessage
{
    public object Value { get; }
    
    public HideMediaProgressModalMessage(object value)
    {
        Value = value;
    }
}

public class MediaProgressMessage
{
    public object Value { get; }
    
    public MediaProgressMessage(object value)
    {
        Value = value;
    }
}

public class TrackChangedMessage
{
    public int Value { get; }
    
    public TrackChangedMessage(int value)
    {
        Value = value;
    }
}

public class ShowToastMessage
{
    public object Value { get; }
    
    public ShowToastMessage(object value)
    {
        Value = value;
    }
}

public class ClearToastsMessage
{
    public object Value { get; }
    
    public ClearToastsMessage(object value)
    {
        Value = value;
    }
}

