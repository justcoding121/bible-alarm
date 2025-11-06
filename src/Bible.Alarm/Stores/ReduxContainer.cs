using Bible.Alarm.Common.Redux;
using Bible.Alarm.Stores.Reducers;

namespace Bible.Alarm.Stores;

public static class ReduxContainer
{
    public static IStore<ApplicationState> Store { get; set; }
        = new Store<ApplicationState>(RootReducer.Execute, new ApplicationState());
}