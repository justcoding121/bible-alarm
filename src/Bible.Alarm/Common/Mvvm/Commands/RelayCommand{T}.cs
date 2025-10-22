namespace Bible.Alarm.Common.Mvvm.Commands;

/// <summary>
/// An helper command to create implementations of ICommand with a typed argument.
/// </summary>
public class RelayCommand<T>(Action<T> execute, Func<T, bool> canExecute = null) : IRelayCommand
{
    #region Constructors

    #endregion

    #region Fields

    private readonly Func<T, bool> _canExecute = canExecute ?? ((o) => true);

    #endregion

    #region Events

    public event EventHandler CanExecuteChanged;

    #endregion

    #region Methods

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool CanExecute(object parameter)
    {
        return _canExecute((T)parameter);
    }

    public void Execute(object parameter)
    {
        execute((T)parameter);
    }

    #endregion
}