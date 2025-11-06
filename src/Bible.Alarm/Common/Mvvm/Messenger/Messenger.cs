using System.Collections.Concurrent;

namespace Bible.Alarm.Common.Mvvm.Messenger;

public enum MvvmMessages
{
    Initialized,
    ShowAlarmModal,
    HideAlarmModal,
    ShowMediaProgessModal,
    HideMediaProgressModal,
    MediaProgress,
    TrackChanged,
    ShowToast,
    ClearToasts
}

public static class Messenger<T>
{
    private class MessageWrapper(T parameter)
    {
        public T Parameter { get; private set; } = parameter;
    }

    private class MessageStream
    {
        private MessageWrapper _lastMessage;
        private readonly List<Func<T, Task>> _subscribers = new();
        private readonly bool _getMostRecentEvent;
        private bool _hasReceivedMessage;

        public MessageStream(bool getMostRecentEvent)
        {
            _getMostRecentEvent = getMostRecentEvent;
        }

        public void Publish(MessageWrapper message)
        {
            _lastMessage = message;
            _hasReceivedMessage = true;

            // Notify all subscribers
            var subscribersCopy = _subscribers.ToArray();
            foreach (var subscriber in subscribersCopy)
            {
                try
                {
                    _ = subscriber(message.Parameter);
                }
                catch
                {
                    // Ignore errors from subscribers
                }
            }
        }

        public IDisposable Subscribe(Func<T, Task> action)
        {
            _subscribers.Add(action);

            // If getMostRecentEvent is true and we have a message, call immediately
            if (_getMostRecentEvent && _hasReceivedMessage && _lastMessage != null)
            {
                _ = action(_lastMessage.Parameter);
            }

            return new Subscription(() => _subscribers.Remove(action));
        }
    }

    private class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;
        private bool _disposed;

        public Subscription(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _unsubscribe();
                _disposed = true;
            }
        }
    }

    private static readonly ConcurrentDictionary<MvvmMessages, MessageStream> Cache = new();

    public static void Publish(MvvmMessages stream, T parameter = default)
    {
        var messageStream = Cache.GetOrAdd(stream, _ => new MessageStream(false));
        messageStream.Publish(new MessageWrapper(parameter));
    }

    public static IDisposable Subscribe(MvvmMessages stream,
        Func<T, Task> action,
        bool getMostRecentEvent = false)
    {
        var messageStream = Cache.GetOrAdd(stream, _ => new MessageStream(getMostRecentEvent));
        return messageStream.Subscribe(action);
    }
}