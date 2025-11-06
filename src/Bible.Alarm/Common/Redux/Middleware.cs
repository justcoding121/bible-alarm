namespace Bible.Alarm.Common.Redux;

public delegate Func<Dispatcher, Dispatcher> Middleware<TState>(
    IStore<TState> store);