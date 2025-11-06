namespace Bible.Alarm.Common.Redux;

public delegate TState Reducer<TState>(TState previousState, IAction action);