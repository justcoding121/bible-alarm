#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Music;
using CommunityToolkit.Mvvm.Input;

namespace Bible.Alarm.Views.Music;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class MusicTrackSelectionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public MusicTrackSelectionViewModel? ViewModel => BindingContext as MusicTrackSelectionViewModel;

    public MusicTrackSelectionModal()
    {
        InitializeComponent();
        Appearing += OnAppearing;
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        await ModalScrollHelper.HandleModalAppearingAsync(
            ViewModel,
            BusyOverlay,
            trackCollectionView,
            getSelectedItem: () => ViewModel?.SelectedTrack,
            refreshAction: ViewModel != null ? async () => await ViewModel.RefreshFromState() : null,
            cancellationToken: cancellationTokenSource.Token);
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            ModalScrollHelper.DisposeModal(cancellationTokenSource, () => BindingContext = null, ViewModel);
            isDisposed = true;
        }
    }

    private async void OnTrackItemTapped(object? sender, TappedEventArgs e)
    {
        try { cancellationTokenSource.Cancel(); } catch { }

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
