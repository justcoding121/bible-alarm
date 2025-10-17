namespace Mvvmicro
{
    using System;
    using System.ComponentModel;
    using System.Threading;
    using System.Threading.Tasks;

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

        public DateTime? LastSuccededExecution => this._lastExecution;

        #endregion

        #region Constructors

        #endregion

        #region Methods

        public void RaiseCanExecuteChanged() => this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);

        private void RaiseIsExecuting()
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExecuting)));
            this.RaiseCanExecuteChanged();
        }

        public async void Execute(object parameter)
        {
            try
            {
                this._cts = new CancellationTokenSource();
                this._execution = execute(parameter, _cts.Token);
                this.RaiseIsExecuting();
                await this._execution;
                this._lastExecution = DateTime.Now;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastSuccededExecution)));
            }
            catch (Exception e)
            {
                this.ExecutionFailed?.Invoke(this, e);
            }
            finally
            {
                this._cts = null;
                this._execution = null;
                this.RaiseIsExecuting();
            }
        }

        public void Cancel() => this._cts?.Cancel();

        public bool CanExecute(object parameter) => !this.IsExecuting && this._canExecute();

        #endregion
    }
}
