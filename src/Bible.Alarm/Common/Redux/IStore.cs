namespace Bible.Alarm.Common.Redux;

public interface IStore<TState>
{
    event EventHandler<TState> StateChanged;
    
    IAction Dispatch(IAction action);

    TState GetState();
    
    IDisposable Subscribe(Action<TState> onNext);
}