#nullable enable

using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;

namespace Bible.Alarm.Views.Schedule;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class NumberOfTracksModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public NumberOfTrackContainerViewModel? ViewModel => BindingContext as NumberOfTrackContainerViewModel;
    public IListViewModel? ListViewModel => ViewModel;

    public NumberOfTracksModal()
    {
        InitializeComponent();

        // SelectionChanged handler removed - using SelectionMode="None" with TapGestureRecognizer instead
        Appearing += OnAppearing;
    }

    private async void OnTrackItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not View view || view.BindingContext is not NumberOfTracksListViewItemModel trackItem)
        {
            return;
        }

        await SafeTeardown.CancelAsyncNoThrow(cancellationTokenSource);

        trackItem.IsNavigating = true;
        await Task.Delay(50);

        try
        {
            if (ViewModel is null ||
                ViewModel.SelectNumberOfTracksCommand is not IAsyncRelayCommand<NumberOfTracksListViewItemModel> asyncCommand ||
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

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        await ModalScrollHelper.HandleModalAppearingAsync(
            ListViewModel,
            BusyOverlay,
            TracksCollectionView,
            getSelectedItem: () => ViewModel?.CurrentNumberOfTracks,
            refreshAction: ViewModel != null
                ? () => ViewModel.PopulateNumberOfTracksListViewAsync()
                : null,
            onFetchFailed: null,
            cancellationToken: cancellationTokenSource.Token);
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
            ModalScrollHelper.DisposeModal(cancellationTokenSource, () => BindingContext = null, ViewModel);
        }

        isDisposed = true;
    }
}
