using System.ComponentModel;

namespace Bible.Alarm.Common.Mvvm.Commands;

/// <summary>
/// An helper command to create asynchronous implementations of ICommand.
/// </summary>
public class AsyncRelayCommand(Func<object, CancellationToken, Task> execute, Func<bool> canExecute = null)
    : IAsyncRelayCommand
{
    #region Fields

    private readonly Func<bool> _canExecute = canExecute ?? (() => true);

    private DateTime? _lastExecution;

    private Task _execution;

    private CancellationTokenSource _cts;

    #endregion

    #region Events

    public event PropertyChangedEventHandler PropertyChanged;

    public event EventHandler<Exception> ExecutionFailed;

    public event EventHandler CanExecuteChanged;

    #endregion

    #region Properties

    public bool IsExecuting => _execution != null;

    public DateTime? LastSuccededExecution => _lastExecution;

    #endregion

    #region Constructors

    #endregion

    #region Methods

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseIsExecuting()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExecuting)));
        RaiseCanExecuteChanged();
    }

    public async void Execute(object parameter)
    {
        try
        {
            _cts = new CancellationTokenSource();
            _execution = execute(parameter, _cts.Token);
            RaiseIsExecuting();
            await _execution;
            _lastExecution = DateTime.Now;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastSuccededExecution)));
        }
        catch (Exception e)
        {
            ExecutionFailed?.Invoke(this, e);
        }
        finally
        {
            _cts = null;
            _execution = null;
            RaiseIsExecuting();
        }
    }

    public void Cancel()
    {
        _cts?.Cancel();
    }

    public bool CanExecute(object parameter)
    {
        return !IsExecuting && _canExecute();
    }

    #endregion
}