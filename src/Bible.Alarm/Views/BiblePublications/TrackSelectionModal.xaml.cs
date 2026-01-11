#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.BiblePublications;
using CommunityToolkit.Mvvm.Input;

namespace Bible.Alarm.Views.Bible;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class TrackSelectionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public TrackSelectionViewModel? ViewModel => BindingContext as TrackSelectionViewModel;

    public TrackSelectionModal()
    {
        InitializeComponent();
        Appearing += OnAppearing;
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        await ModalScrollHelper.HandleModalAppearingAsync(
            () => ViewModel?.IsBusy ?? false,
            BusyOverlay,
            trackCollectionView,
            getSelectedItem: () => ViewModel?.SelectedTrack,
            refreshAction: ViewModel != null
                ? async () => await ViewModel.RefreshFromState()
                : null,
            cancellationToken: cancellationTokenSource.Token);
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            ModalScrollHelper.DisposeModal(cancellationTokenSource, () => BindingContext = null);
            isDisposed = true;
        }
    }

    private async void OnTrackItemTapped(object? sender, TappedEventArgs e)
    {
        // Cancel any ongoing scroll operation to prevent race conditions
        try { cancellationTokenSource.Cancel(); } catch { }

        if (sender is Grid grid && grid.BindingContext is BiblePublicationTrackListViewItemModel trackItem)
        {
            if (ViewModel != null && ViewModel.SetTrackCommand is IAsyncRelayCommand<BiblePublicationTrackListViewItemModel> asyncCommand)
            {
                if (asyncCommand.CanExecute(trackItem))
                {
                    await asyncCommand.ExecuteAsync(trackItem);
                }
            }
        }
    }
}

