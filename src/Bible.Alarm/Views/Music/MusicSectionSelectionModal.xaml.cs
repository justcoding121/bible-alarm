#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.Music;
using CommunityToolkit.Mvvm.Input;

namespace Bible.Alarm.Views.Music;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class MusicSectionSelectionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private bool isSelectingSection;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public MusicSectionSelectionViewModel? ViewModel => BindingContext as MusicSectionSelectionViewModel;

    public MusicSectionSelectionModal()
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
            sectionCollectionView,
            getSelectedItem: () => ViewModel?.SelectedSection,
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

    private async void OnSectionItemTapped(object? sender, TappedEventArgs e)
    {
        // Cancel any ongoing scroll operation to prevent race conditions
        try { cancellationTokenSource.Cancel(); } catch { }

        if (isSelectingSection)
        {
            return;
        }

        if (sender is View view && view.BindingContext is BiblePublicationSectionListViewItemModel sectionItem)
        {
            isSelectingSection = true;

            // Reset progress and show row indicator immediately
            sectionItem.DownloadProgress = 0.0;
            sectionItem.IsNavigating = true;
            
            // Wait 50ms to ensure UI thread renders the update before doing backend work
            await Task.Delay(50);

            try
            {
                if (ViewModel != null && ViewModel.TrackSelectionCommand is IAsyncRelayCommand<BiblePublicationSectionListViewItemModel> asyncCommand)
                {
                    if (asyncCommand.CanExecute(sectionItem))
                    {
                        await asyncCommand.ExecuteAsync(sectionItem);
                    }
                }
            }
            finally
            {
                // Reset IsNavigating after operation completes
                sectionItem.IsNavigating = false;
                isSelectingSection = false;
            }
        }
    }
}
