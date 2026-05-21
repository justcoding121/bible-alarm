#nullable enable
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;

namespace Bible.Alarm.Views.Music;

[XamlCompilation(XamlCompilationOptions.Compile)]
public sealed partial class MusicPublicationSelection : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private bool isSelectingPublication;
    private readonly MusicPublicationSelectionViewModel viewModel;

    public MusicPublicationSelectionViewModel? ViewModel => viewModel;

    public MusicPublicationSelection(MusicPublicationSelectionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        this.viewModel = viewModel;

        // Note: We don't clear selection here because this page navigates away when an item is selected
        // The page will be disposed, so clearing selection is unnecessary and can interfere with navigation on iOS
    }

    protected override bool OnBackButtonPressed()
    {
        viewModel.BackCommand.Execute(null);
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
            // ViewModel was injected via constructor, so dispose it
            if (viewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }
            // Clear BindingContext to break reference and allow garbage collection
            BindingContext = null;
        }

        isDisposed = true;
    }

    private async void OnSongPublicationItemTapped(object? sender, TappedEventArgs e)
    {
        if (isSelectingPublication)
        {
            return;
        }

        if (sender is not View view || view.BindingContext is not PublicationListViewItemModel publicationItem)
        {
            return;
        }

        isSelectingPublication = true;

        // Reset progress and show row indicator immediately
        publicationItem.DownloadProgress = 0.0;
        publicationItem.IsNavigating = true;

        // Wait 50ms to ensure UI thread renders the update before doing backend work
        await Task.Delay(50);

        try
        {
            if (ViewModel is null ||
                ViewModel.TrackSelectionCommand is not IAsyncRelayCommand<PublicationListViewItemModel> asyncCommand ||
                !asyncCommand.CanExecute(publicationItem))
            {
                return;
            }

            await asyncCommand.ExecuteAsync(publicationItem);
        }
        finally
        {
            // Reset IsNavigating after operation completes
            publicationItem.IsNavigating = false;
            isSelectingPublication = false;
        }
    }
}
