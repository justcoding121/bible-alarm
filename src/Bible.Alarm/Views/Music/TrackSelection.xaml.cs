#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Music;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace Bible.Alarm.Views.Music;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class TrackSelection : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly TrackSelectionViewModel viewModel;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public TrackSelectionViewModel? ViewModel => BindingContext as TrackSelectionViewModel;


    public TrackSelection(TrackSelectionViewModel viewModel, TaskScheduler taskScheduler)
    {
        InitializeComponent();
        BindingContext = viewModel;
        this.viewModel = viewModel;

        // SelectionChanged handler removed - using SelectionMode="None" with TapGestureRecognizer instead
        // This eliminates the orange flash visual feedback

        Appearing += OnAppearing;
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        // Wait for the page to be fully loaded and data to be ready before attempting to scroll
        if (ViewModel != null)
        {
            // Wait for IsBusy to become false (data loaded) using Polly retry policy
            await CollectionViewHelper.WaitForNotBusyAsync(() => ViewModel.IsBusy, cancellationToken: cancellationTokenSource.Token);

            // Small additional delay to ensure CollectionView is rendered
            await Task.Delay(200, cancellationTokenSource.Token);

            if (ViewModel.SelectedTrack != null && trackCollectionView != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(trackCollectionView, ViewModel.SelectedTrack, cancellationToken: cancellationTokenSource.Token);
            }
        }
    }

    protected override bool OnBackButtonPressed()
    {
        ViewModel?.BackCommand?.Execute(null);
        return true;
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            // Cancel and dispose cancellation token source
            try
            {
                cancellationTokenSource?.Cancel();
                cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                // Ignore errors during cancellation/disposal
                Log.Logger.Warning(ex, "Error during cancellation token source disposal");
            }

            // ViewModel was injected via constructor, so dispose it
            if (viewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }
            // Clear BindingContext to break reference and allow garbage collection
            BindingContext = null;
            isDisposed = true;
        }
    }

    private async void OnTrackItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is View view && view.BindingContext is MusicTrackListViewItemModel trackItem)
        {
            if (ViewModel != null && ViewModel.SetTrackCommand is IAsyncRelayCommand<MusicTrackListViewItemModel> asyncCommand)
            {
                if (asyncCommand.CanExecute(trackItem))
                {
                    await asyncCommand.ExecuteAsync(trackItem);
                }
            }
        }
    }
}
