using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Music;

namespace Bible.Alarm.Views.Music;

public partial class TrackSelection : ContentPage, IDisposable
{
    private bool _isDisposed;
    private readonly TrackSelectionViewModel _viewModel;
    private readonly TaskScheduler _taskScheduler;

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
        
        // Wait for the page to be fully loaded before attempting to scroll
        await Task.Delay(300);
        
        if (ViewModel?.SelectedTrack != null && trackCollectionView != null)
        {
            await CollectionViewHelper.ScrollToWhenReadyAsync(trackCollectionView, ViewModel.SelectedTrack);
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
            // ViewModel was injected via constructor, so dispose it
            if (_viewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _isDisposed = true;
        }
    }
}