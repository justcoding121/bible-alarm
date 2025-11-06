using Bible.Alarm.Common.Redux;

namespace Bible.Alarm.Stores.Actions;

public class BackAction(IDisposable currentViewModel) : IAction
{
    public IDisposable CurrentViewModel { get; set; } = currentViewModel;
}