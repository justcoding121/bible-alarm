#nullable enable
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Views.Music;

public partial class SongBookSelection : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly SongBookSelectionViewModel viewModel;

    public SongBookSelectionViewModel? ViewModel => BindingContext as SongBookSelectionViewModel;

    public SongBookSelection(SongBookSelectionViewModel viewModel)
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
        if (!isDisposed)
        {
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

    private async void OnSongBookItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is PublicationListViewItemModel publicationItem)
        {
            if (ViewModel != null && ViewModel.TrackSelectionCommand is IAsyncRelayCommand<PublicationListViewItemModel> asyncCommand)
            {
                if (asyncCommand.CanExecute(publicationItem))
                {
                    await asyncCommand.ExecuteAsync(publicationItem);
                }
            }
        }
    }
}
