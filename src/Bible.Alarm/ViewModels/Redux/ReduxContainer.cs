using Bible.Alarm.Common.Redux;
using Bible.Alarm.ViewModels.Redux.Reducers;

namespace Bible.Alarm.ViewModels.Redux;

public static class ReduxContainer
{
    public static IStore<ApplicationState> Store { get; set; }
        = new Store<ApplicationState>(RootReducer.Execute, new ApplicationState());
}