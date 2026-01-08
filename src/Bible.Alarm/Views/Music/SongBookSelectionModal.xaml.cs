#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Serilog;

namespace Bible.Alarm.Views.Music;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class SongBookSelectionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public SongBookSelectionViewModel? ViewModel => BindingContext as SongBookSelectionViewModel;

    public SongBookSelectionModal()
    {
        InitializeComponent();

        Appearing += OnAppearing;
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        try
        {
            if (ViewModel != null)
            {
                // Refresh from state when modal appears to ensure song books are populated
                await ViewModel.RefreshFromState();

                // Force hide busy overlay since binding might not work
                ForceHideBusyOverlay();

                // Wait for IsBusy to become false (data loaded) using Polly retry policy
                // This ensures the CollectionView is ready and SelectedSongBook is set before scrolling
                await CollectionViewHelper.WaitForNotBusyAsync(() => ViewModel.IsBusy, cancellationToken: cancellationTokenSource.Token);

                // Small additional delay to ensure CollectionView is rendered
                await Task.Delay(200, cancellationTokenSource.Token);

                if (ViewModel.SelectedSongBook != null && songBooksCollectionView != null)
                {
                    await CollectionViewHelper.ScrollToWhenReadyAsync(songBooksCollectionView, ViewModel.SelectedSongBook, animated: false, cancellationToken: cancellationTokenSource.Token);
                }
            }
        }
        catch (Exception ex)
        {
            // OnAppearing errors are non-critical (UI initialization)
            Serilog.Log.Warning(ex, "Error in SongBookSelectionModal.OnAppearing");
            ForceHideBusyOverlay();
        }
    }

    private void ForceHideBusyOverlay()
    {
        if (BusyOverlay != null)
        {
            BusyOverlay.IsVisible = false;
            BusyOverlay.InputTransparent = true; // Ensure input is allowed through
        }
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
                Log.Logger.Warning(ex, "Error during cancellation token source disposal");
            }

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

