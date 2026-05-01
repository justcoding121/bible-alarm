#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.ViewModels.Music;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace Bible.Alarm.Views.Music;

[XamlCompilation(XamlCompilationOptions.Compile)]
public sealed partial class MusicTrackSelection : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly MusicTrackSelectionViewModel viewModel;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public MusicTrackSelectionViewModel? ViewModel => BindingContext as MusicTrackSelectionViewModel;

    public MusicTrackSelection(MusicTrackSelectionViewModel viewModel, TaskScheduler taskScheduler)
    {
        InitializeComponent();
        BindingContext = viewModel;
        this.viewModel = viewModel;
        Appearing += OnAppearing;
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        if (ViewModel == null)
        {
            return;
        }

        await CollectionViewHelper.WaitForNotBusyAsync(() => ViewModel.IsBusy, cancellationToken: cancellationTokenSource.Token);
        await Task.Delay(200, cancellationTokenSource.Token);

        if (ViewModel.SelectedTrack != null && trackCollectionView != null)
        {
            await CollectionViewHelper.ScrollToWhenReadyAsync(trackCollectionView, ViewModel.SelectedTrack, cancellationToken: cancellationTokenSource.Token);
        }
    }

    protected override bool OnBackButtonPressed()
    {
        ViewModel?.BackCommand?.Execute(null);
        return true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (isDisposed)
        {
            return;
        }

        if (disposing)
        {
            try
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, AppConstants.Logging.DisposableLifetimeLog.ErrorDuringCancellationTokenSourceDisposal);
            }

            if (viewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }
            BindingContext = null;
        }

        isDisposed = true;
    }

    private async void OnTrackItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not View view || view.BindingContext is not MusicTrackListViewItemModel trackItem)
        {
            return;
        }

        trackItem.IsNavigating = true;
        await Task.Delay(50);

        try
        {
            if (ViewModel is null ||
                ViewModel.SetTrackCommand is not IAsyncRelayCommand<MusicTrackListViewItemModel> asyncCommand ||
                !asyncCommand.CanExecute(trackItem))
            {
                return;
            }

            await asyncCommand.ExecuteAsync(trackItem);
        }
        finally
        {
            trackItem.IsNavigating = false;
        }
    }
}
