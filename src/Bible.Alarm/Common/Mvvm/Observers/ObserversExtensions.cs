using System.ComponentModel;

namespace Bible.Alarm.Common.Mvvm.Observers;

public static class ObserversExtensions
{
    public static NotifyPropertyObserver<TObserver, TObservable> CreateObserver<TObservable, TObserver>(
        this TObservable observable, TObserver observer)
        where TObservable : INotifyPropertyChanged
        where TObserver : class
    {
        return new NotifyPropertyObserver<TObserver, TObservable>(observable, observer);
    }
}