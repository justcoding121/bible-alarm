using Redux;

namespace Bible.Alarm.ViewModels.Redux.Actions;

public class BackAction(IDisposable currentViewModel) : IAction
{
    public IDisposable CurrentViewModel { get; set; } = currentViewModel;
}