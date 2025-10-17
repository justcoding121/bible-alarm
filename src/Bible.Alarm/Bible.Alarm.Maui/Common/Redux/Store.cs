// Decompiled with JetBrains decompiler
// Type: Redux.Store`1
// Assembly: Redux, Version=1.0.1.0, Culture=neutral, PublicKeyToken=null
// MVID: C5F64108-560B-4DF4-8351-166C386E1779
// Assembly location: C:\Work\Repositories\Bible-Alarm\src\Bible.Alarm\Bible.Alarm.UWP\bin\x86\Debug\Redux.dll

using System;
using System.Reactive.Subjects;

namespace Redux
{
    public class Store<TState> : IStore<TState>, IObservable<TState>
    {
        private readonly object _syncRoot = new object();
        private readonly ReplaySubject<TState> _stateSubject = new ReplaySubject<TState>(1);
        private readonly Dispatcher _dispatcher;
        private readonly Reducer<TState> _reducer;
        private TState _lastState;

        public Store(
          Reducer<TState> reducer,
          TState initialState = default(TState),
          params Middleware<TState>[] middlewares)
        {
            this._reducer = reducer;
            this._dispatcher = this.ApplyMiddlewares(middlewares);
            this._lastState = initialState;
            this._stateSubject.OnNext(this._lastState);
        }

        public IAction Dispatch(IAction action)
        {
            return this._dispatcher(action);
        }

        public TState GetState()
        {
            return this._lastState;
        }

        public IDisposable Subscribe(IObserver<TState> observer)
        {
            return this._stateSubject.Subscribe(observer);
        }

        private Dispatcher ApplyMiddlewares(params Middleware<TState>[] middlewares)
        {
            Dispatcher dispatcher = new Dispatcher(this.InnerDispatch);
            foreach (Middleware<TState> middleware in middlewares)
                dispatcher = middleware((IStore<TState>)this)(dispatcher);
            return dispatcher;
        }

        private IAction InnerDispatch(IAction action)
        {
            lock (this._syncRoot)
                this._lastState = this._reducer(this._lastState, action);
            this._stateSubject.OnNext(this._lastState);
            return action;
        }
    }
}
