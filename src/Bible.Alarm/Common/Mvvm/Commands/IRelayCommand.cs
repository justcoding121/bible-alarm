namespace Bible.Alarm.Common.Mvvm.Commands;

public interface IRelayCommand : System.Windows.Input.ICommand
{
    /// <summary>
    /// Raises the CanExecuteChanged event.
    /// </summary>
    void RaiseCanExecuteChanged();
}