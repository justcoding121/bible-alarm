#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Music;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Serilog;

namespace Bible.Alarm.Views.Music;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class MusicSelectionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public MusicSelectionViewModel? ViewModel => BindingContext as MusicSelectionViewModel;

    public MusicSelectionModal()
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
                // Refresh from state when modal appears to ensure we have the latest music type
                // This is important when the modal is opened after music is enabled or changed
                ViewModel.RefreshFromState();

                // Force hide busy overlay since binding might not work
                ForceHideBusyOverlay();

                // Wait for IsBusy to become false (data loaded) using Polly retry policy
                // This ensures the CollectionView is ready and SelectedMusicType is set before scrolling
                await CollectionViewHelper.WaitForNotBusyAsync(() => ViewModel.IsBusy, cancellationToken: cancellationTokenSource.Token);

                // Small additional delay to ensure CollectionView is rendered
                await Task.Delay(200, cancellationTokenSource.Token);

                if (ViewModel.SelectedMusicType != null && musicTypesCollectionView != null)
                {
                    await CollectionViewHelper.ScrollToWhenReadyAsync(musicTypesCollectionView, ViewModel.SelectedMusicType, animated: false, cancellationToken: cancellationTokenSource.Token);
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error in MusicSelectionModal.OnAppearing");
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

    private async void OnMusicTypeItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is MusicTypeListItemViewModel musicTypeItem)
        {
            if (ViewModel != null && ViewModel.SongBookSelectionCommand is IAsyncRelayCommand<MusicTypeListItemViewModel> asyncCommand)
            {
                if (asyncCommand.CanExecute(musicTypeItem))
                {
                    await asyncCommand.ExecuteAsync(musicTypeItem);
                }
            }
        }
    }
}

