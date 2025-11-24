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
