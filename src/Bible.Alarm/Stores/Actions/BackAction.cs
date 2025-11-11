namespace Bible.Alarm.Stores.Actions;

public class BackAction
{
    public IDisposable CurrentViewModel { get; }

    public BackAction(IDisposable currentViewModel)
    {
        CurrentViewModel = currentViewModel;
    }
}