namespace Bible.Alarm.Stores.Actions;

public class BackAction(IDisposable currentViewModel)
{
    public IDisposable CurrentViewModel { get; } = currentViewModel;
}
