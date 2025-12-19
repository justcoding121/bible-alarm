using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Music;
using Serilog;

namespace Bible.Alarm.Views.Music;

public partial class TrackSelection : BaseContentPage, IDisposable
{
    private bool _isDisposed;
    private readonly TrackSelectionViewModel _viewModel;
    private readonly TaskScheduler _taskScheduler;
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

    public TrackSelectionViewModel ViewModel => BindingContext as TrackSelectionViewModel;


    public TrackSelection(TrackSelectionViewModel viewModel, TaskScheduler taskScheduler)
    {
        _taskScheduler = taskScheduler;
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;

        // SelectionChanged handler removed - using SelectionMode="None" with TapGestureRecognizer instead
        // This eliminates the orange flash visual feedback

        Appearing += OnAppearing;
    }

    private async void OnAppearing(object sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        // Wait for the page to be fully loaded and data to be ready before attempting to scroll
        if (ViewModel != null)
        {
            // Wait for IsBusy to become false (data loaded) using Polly retry policy
            await CollectionViewHelper.WaitForNotBusyAsync(() => ViewModel.IsBusy, cancellationToken: _cancellationTokenSource.Token);

            // Small additional delay to ensure CollectionView is rendered
            await Task.Delay(200, _cancellationTokenSource.Token);

            if (ViewModel.SelectedTrack != null && trackCollectionView != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(trackCollectionView, ViewModel.SelectedTrack, cancellationToken: _cancellationTokenSource.Token);
            }
        }
    }

    protected override bool OnBackButtonPressed()
    {
        ViewModel.BackCommand.Execute(null);
        return true;
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
                Log.Logger.Warning(ex, "Error during cancellation token source disposal");
            }

            // ViewModel was injected via constructor, so dispose it
            if (_viewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }
            // Clear BindingContext to break reference and allow garbage collection
            BindingContext = null;
            _isDisposed = true;
        }
    }
}
