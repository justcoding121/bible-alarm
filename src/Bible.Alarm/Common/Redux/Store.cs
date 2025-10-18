// Decompiled with JetBrains decompiler
// Type: Redux.Store`1
// Assembly: Redux, Version=1.0.1.0, Culture=neutral, PublicKeyToken=null
// MVID: C5F64108-560B-4DF4-8351-166C386E1779
// Assembly location: C:\Work\Repositories\Bible-Alarm\src\Bible.Alarm\Bible.Alarm.UWP\bin\x86\Debug\Redux.dll

using System.Reactive.Subjects;

namespace Redux;

public class Store<TState> : IStore<TState>, IObservable<TState>
{
    private readonly object _syncRoot = new();
    private readonly ReplaySubject<TState> _stateSubject = new(1);
    private readonly Dispatcher _dispatcher;
    private readonly Reducer<TState> _reducer;
    private TState _lastState;

    public Store(
        Reducer<TState> reducer,
        TState initialState = default,
        params Middleware<TState>[] middlewares)
    {
        _reducer = reducer;
        _dispatcher = ApplyMiddlewares(middlewares);
        _lastState = initialState;
        _stateSubject.OnNext(_lastState);
    }

    public IAction Dispatch(IAction action)
    {
        return _dispatcher(action);
    }

    public TState GetState()
    {
        return _lastState;
    }

    public IDisposable Subscribe(IObserver<TState> observer)
    {
        return _stateSubject.Subscribe(observer);
    }

    private Dispatcher ApplyMiddlewares(params Middleware<TState>[] middlewares)
    {
        var dispatcher = new Dispatcher(InnerDispatch);
        foreach (var middleware in middlewares)
            dispatcher = middleware((IStore<TState>)this)(dispatcher);
        return dispatcher;
    }

    private IAction InnerDispatch(IAction action)
    {
        lock (_syncRoot)
        {
            _lastState = _reducer(_lastState, action);
        }

        _stateSubject.OnNext(_lastState);
        return action;
    }
}