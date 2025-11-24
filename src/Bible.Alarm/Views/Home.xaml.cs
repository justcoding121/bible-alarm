using Bible.Alarm.ViewModels;

namespace Bible.Alarm.Views;

public partial class Home : ContentPage, IDisposable
{
    private bool _isDisposed;
    private readonly HomeViewModel _viewModel;

    private bool _isClearingSelection;

    public Home(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _viewModel = vm;
        
        // Clear selection after SelectionChanged fires to allow command to execute first
        SchedulesCollectionView.SelectionChanged += (sender, e) =>
        {
            // Don't clear if we're already clearing or if selection is being cleared (CurrentSelection is empty or null)
            if (_isClearingSelection || e?.CurrentSelection == null || e.CurrentSelection.Count == 0)
            {
                return;
            }
            
            // Clear selection after a short delay to allow command to execute
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!_isDisposed && SchedulesCollectionView != null)
                    {
                        _isClearingSelection = true;
                        SchedulesCollectionView.SelectedItem = null;
                        _isClearingSelection = false;
                    }
                });
            });
        };
    }

    protected override bool OnBackButtonPressed()
    {
        // Prevent back navigation on Home page - it's the root page
        return true;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            // ViewModel was injected via constructor, so dispose it
            if (_viewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _isDisposed = true;
        }
    }
}