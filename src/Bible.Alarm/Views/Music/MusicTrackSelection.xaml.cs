#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.ViewModels.Music;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace Bible.Alarm.Views.Music;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class MusicTrackSelection : BaseContentPage, IDisposable
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

        if (ViewModel != null)
        {
            await CollectionViewHelper.WaitForNotBusyAsync(() => ViewModel.IsBusy, cancellationToken: cancellationTokenSource.Token);
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
            try
            {
                cancellationTokenSource?.Cancel();
                cancellationTokenSource?.Dispose();
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
            isDisposed = true;
        }
    }

    private async void OnTrackItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is View view && view.BindingContext is MusicTrackListViewItemModel trackItem)
        {
            trackItem.IsNavigating = true;
            await Task.Delay(50);

            try
            {
                if (ViewModel != null && ViewModel.SetTrackCommand is IAsyncRelayCommand<MusicTrackListViewItemModel> asyncCommand)
                {
                    if (asyncCommand.CanExecute(trackItem))
                        await asyncCommand.ExecuteAsync(trackItem);
                }
            }
            finally
            {
                trackItem.IsNavigating = false;
            }
        }
    }
}
