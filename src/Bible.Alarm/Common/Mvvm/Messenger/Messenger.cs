using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;

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

    private static readonly ConcurrentDictionary<MvvmMessages, BehaviorSubject<MessageWrapper>> Cache = new();

    public static void Publish(MvvmMessages stream, T parameter = default)
    {
        var subject = Cache.GetOrAdd(stream, new BehaviorSubject<MessageWrapper>(null));
        subject.OnNext(new MessageWrapper(parameter));
    }

    public static IDisposable Subscribe(MvvmMessages stream,
        Func<T, Task> action,
        bool getMostRecentEvent = false)
    {
        var subject = Cache.GetOrAdd(stream, new BehaviorSubject<MessageWrapper>(null));

        if (getMostRecentEvent)
            return subject.Where(x => x != null)
                .Subscribe(x => action(x.Parameter));
        else
            return subject
                .Skip(1)
                .Subscribe(x => action(x.Parameter));
    }
}