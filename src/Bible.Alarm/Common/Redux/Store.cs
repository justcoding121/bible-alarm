using Bible.Alarm.Common.Extensions;

namespace Bible.Alarm.Common.Redux;

public class Store<TState> : IStore<TState>
{
    private readonly object _syncRoot = new();
    private readonly Dispatcher _dispatcher;
    private readonly Reducer<TState> _reducer;
    private TState _lastState;
    private readonly List<Action<TState>> _subscribers = new();

    public event EventHandler<TState> StateChanged;

    public Store(
        Reducer<TState> reducer,
        TState initialState = default,
        params Middleware<TState>[] middlewares)
    {
        _reducer = reducer;
        _dispatcher = ApplyMiddlewares(middlewares);
        _lastState = initialState;
        
        // Notify initial state
        NotifyStateChanged(_lastState);
    }

    public IAction Dispatch(IAction action)
    {
        return _dispatcher(action);
    }

    public TState GetState()
    {
        lock (_syncRoot)
        {
            return _lastState;
        }
    }

    public IDisposable Subscribe(Action<TState> onNext)
    {
        lock (_syncRoot)
        {
            _subscribers.Add(onNext);
            // Immediately call with current state
            onNext(_lastState);
        }

        return new Subscription(() =>
        {
            lock (_syncRoot)
            {
                _subscribers.Remove(onNext);
            }
        });
    }

    private void NotifyStateChanged(TState newState)
    {
        StateChanged?.Invoke(this, newState);
        
        // Notify all subscribers
        Action<TState>[] subscribersCopy;
        lock (_syncRoot)
        {
            subscribersCopy = _subscribers.ToArray();
        }
        
        foreach (var subscriber in subscribersCopy)
        {
            try
            {
                subscriber(newState);
            }
            catch
            {
                // Ignore errors from subscribers
            }
        }
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
        TState newState;
        lock (_syncRoot)
        {
            // Deep clone the state before passing to reducer to ensure immutability
            var clonedState = _lastState.DeepClone();
            newState = _reducer(clonedState, action);
            _lastState = newState;
        }

        NotifyStateChanged(newState);
        return action;
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
}