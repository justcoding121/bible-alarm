namespace Mvvmicro;

using System;

/// <summary>
/// An helper command to create implementations of ICommand.
/// </summary>
public class RelayCommand(Action execute, Func<bool> canExecute = null) : IRelayCommand
{
    #region Constructors

    #endregion

    #region Fields

    private Func<bool> _canExecute = canExecute ?? (() => true);

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
        return _canExecute();
    }

    public void Execute(object parameter)
    {
        execute();
    }

    #endregion
}