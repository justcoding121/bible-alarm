using Bible.Alarm.ViewModels;
using Bible.Alarm.Common.ViewHelpers;

namespace Bible.Alarm.Views.Schedule;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class NumberOfChaptersModal : BaseContentPage, IDisposable
{
    private bool _isDisposed;
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private bool _isClearingSelection;

    public ScheduleViewModel ViewModel => BindingContext as ScheduleViewModel;

    public NumberOfChaptersModal()
    {
        InitializeComponent();
        
        // Clear selection after SelectionChanged fires to allow command to execute first
        ChaptersCollectionView.SelectionChanged += (sender, e) =>
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
                    if (!_isDisposed && ChaptersCollectionView != null)
                    {
                        _isClearingSelection = true;
                        ChaptersCollectionView.SelectedItem = null;
                        _isClearingSelection = false;
                    }
                });
            });
        };
        
        Appearing += OnAppearing;
    }

    private async void OnAppearing(object sender, EventArgs e)
    {
        Appearing -= OnAppearing;
        
        if (ViewModel?.CurrentNumberOfChapters != null && ChaptersCollectionView != null)
        {
            await CollectionViewHelper.ScrollToWhenReadyAsync(ChaptersCollectionView, ViewModel.CurrentNumberOfChapters, cancellationToken: _cancellationTokenSource.Token);
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            // Cancel and dispose cancellation token source
            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                // Ignore errors during cancellation/disposal
                Serilog.Log.Logger.Warning(ex, "Error during cancellation token source disposal");
            }
            
            // This modal uses parent page view model, so do NOT dispose it
            // Clear BindingContext to break reference and allow garbage collection
            BindingContext = null;
            _isDisposed = true;
        }
    }
}