#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.BiblePublications;
using CommunityToolkit.Mvvm.Input;

namespace Bible.Alarm.Views.Bible;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BiblePublicationTrackSelectionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public BiblePublicationTrackSelectionViewModel? ViewModel => BindingContext as BiblePublicationTrackSelectionViewModel;

    public BiblePublicationTrackSelectionModal()
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

        if (sender is View view && view.BindingContext is BiblePublicationTrackListViewItemModel trackItem)
        {
            // Set IsNavigating immediately to show progress indicator
            trackItem.IsNavigating = true;
            
            // Wait 50ms to ensure UI thread renders the update before doing backend work
            await Task.Delay(50);

            try
            {
                if (ViewModel != null && ViewModel.SetTrackCommand is IAsyncRelayCommand<BiblePublicationTrackListViewItemModel> asyncCommand)
                {
                    if (asyncCommand.CanExecute(trackItem))
                    {
                        await asyncCommand.ExecuteAsync(trackItem);
                    }
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
