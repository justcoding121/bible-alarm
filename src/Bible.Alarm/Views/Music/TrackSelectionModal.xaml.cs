#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Music;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Serilog;

namespace Bible.Alarm.Views.Music;

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

        try
        {
            if (ViewModel != null)
            {
                // Refresh from state when modal appears to ensure tracks are populated
                await ViewModel.RefreshFromState();

                // Force hide busy overlay since binding might not work
                ForceHideBusyOverlay();

                // Wait for IsBusy to become false (data loaded)
                await CollectionViewHelper.WaitForNotBusyAsync(() => ViewModel.IsBusy, cancellationToken: cancellationTokenSource.Token);

                // Small additional delay to ensure CollectionView is rendered
                await Task.Delay(200, cancellationTokenSource.Token);

                if (ViewModel.SelectedTrack != null && trackCollectionView != null)
                {
                    await CollectionViewHelper.ScrollToWhenReadyAsync(trackCollectionView, ViewModel.SelectedTrack, animated: false, cancellationToken: cancellationTokenSource.Token);
                }
            }
        }
        catch (Exception ex)
        {
            // OnAppearing errors are non-critical (UI initialization)
            Serilog.Log.Warning(ex, "Error in TrackSelectionModal.OnAppearing");
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

    private async void OnTrackItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is MusicTrackListViewItemModel trackItem)
        {
            if (ViewModel != null && ViewModel.SetTrackCommand is IAsyncRelayCommand<MusicTrackListViewItemModel> asyncCommand)
            {
                if (asyncCommand.CanExecute(trackItem))
                {
                    await asyncCommand.ExecuteAsync(trackItem);
                }
            }
        }
    }
}

