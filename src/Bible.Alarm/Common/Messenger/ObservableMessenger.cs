#nullable enable
using CommunityToolkit.Mvvm.Messaging;

namespace Bible.Alarm.Common.Messenger;

public class ObservableMessenger
{
    private readonly WeakReferenceMessenger _messenger = WeakReferenceMessenger.Default;
    private readonly Dictionary<Type, object?> _lastMessages = new();

    public void Send<TMessage>(TMessage message) where TMessage : class
    {
        _lastMessages[typeof(TMessage)] = message;
        _messenger.Send(message);
    }

    public void Register<TMessage>(IRecipient<TMessage> recipient) where TMessage : class
    {
        _messenger.Register(recipient);
        
        if (_lastMessages.TryGetValue(typeof(TMessage), out var lastMessage) && lastMessage is TMessage message)
        {
            recipient.Receive(message);
        }
    }

    public void Unregister<TMessage>(IRecipient<TMessage> recipient) where TMessage : class
    {
        _messenger.Unregister<TMessage>(recipient);
    }
}

