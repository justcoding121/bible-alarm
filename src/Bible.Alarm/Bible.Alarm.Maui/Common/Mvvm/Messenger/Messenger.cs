using System;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace Bible.Alarm.Common.Mvvm
{
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
        private class MessageWrapper
        {
            public T Parameter { get; private set; }

            public MessageWrapper(T parameter)
            {
                Parameter = parameter;
            }
        }

        private static ConcurrentDictionary<MvvmMessages, BehaviorSubject<MessageWrapper>> cache
            = new ConcurrentDictionary<MvvmMessages, BehaviorSubject<MessageWrapper>>();
        public static void Publish(MvvmMessages stream, T parameter = default)
        {
            var subject = cache.GetOrAdd(stream, new BehaviorSubject<MessageWrapper>(null));
            subject.OnNext(new MessageWrapper(parameter));
        }

        public static IDisposable Subscribe(MvvmMessages stream,
            Func<T, Task> action,
            bool getMostRecentEvent = false)
        {
            var subject = cache.GetOrAdd(stream, new BehaviorSubject<MessageWrapper>(null));

            if (getMostRecentEvent)
            {
                return subject.Where(x => x != null)
                    .Subscribe(x => action(x.Parameter));
            }
            else
            {
                return subject
                 .Skip(1)
                 .Subscribe(x => action(x.Parameter));
            }
        }
    }

}
