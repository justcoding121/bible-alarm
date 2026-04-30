#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.ViewModels.BiblePublications;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace Bible.Alarm.Views.Bible;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BiblePublicationTrackSelection : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly BiblePublicationTrackSelectionViewModel viewModel;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public BiblePublicationTrackSelectionViewModel? ViewModel => BindingContext as BiblePublicationTrackSelectionViewModel;


    public BiblePublicationTrackSelection(BiblePublicationTrackSelectionViewModel viewModel, TaskScheduler taskScheduler)
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
                Log.Logger.Warning(ex, AppConstants.Logging.DisposableLifetimeLog.ErrorDuringCancellationTokenSourceDisposal);
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
        if (sender is View view && view.BindingContext is BiblePublicationTrackListViewItemModel trackItem)
        {
            // Set IsNavigating immediately to show progress indicator
            trackItem.IsNavigating = true;
            
            // Wait 50ms to ensure UI thread renders the update before doing backend work
            await Task.Delay(50);

            try
            {
                if (ViewModel != null
                    && ViewModel.SetTrackCommand is IAsyncRelayCommand<BiblePublicationTrackListViewItemModel> asyncCommand
                    && asyncCommand.CanExecute(trackItem))
                {
                    await asyncCommand.ExecuteAsync(trackItem);
                }
            }
            finally
            {
                // Reset IsNavigating after operation completes
                trackItem.IsNavigating = false;
            }
        }
    }
}
