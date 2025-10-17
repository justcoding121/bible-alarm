namespace Mvvmicro
{
    using System;
    using System.ComponentModel;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// An helper command to create asynchronous implementations of ICommand with a typed argument.
    /// </summary>
    public class AsyncRelayCommand<T>(Func<T, CancellationToken, Task> execute, Func<T, bool> canExecute = null)
        : IAsyncRelayCommand
    {
        #region Fields

        private readonly Func<T, bool> canExecute = canExecute ?? ((p) => true);

        private DateTime? lastExecution;

        private Task execution;

        private CancellationTokenSource cts;

        #endregion

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler<Exception> ExecutionFailed;

        public event EventHandler CanExecuteChanged;

        #endregion

        #region Properties

        public bool IsExecuting => execution != null;

        public DateTime? LastSuccededExecution => this.lastExecution;

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
                this.cts = new CancellationTokenSource();
                this.execution = execute((T)parameter, cts.Token);
                this.RaiseIsExecuting();
                await this.execution;
                this.lastExecution = DateTime.Now;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastSuccededExecution)));
            }
            catch (Exception e)
            {
                this.ExecutionFailed?.Invoke(this, e);
            }
            finally
            {
                this.cts = null;
                this.execution = null;
                this.RaiseIsExecuting();
            }
        }

        public void Cancel() => this.cts?.Cancel();

        public bool CanExecute(object parameter) => !this.IsExecuting && this.canExecute((T)parameter);

        #endregion
    }
}
