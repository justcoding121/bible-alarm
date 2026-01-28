#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Music;
using CommunityToolkit.Mvvm.Input;

namespace Bible.Alarm.Views.Music;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class MusicTypeSelectionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public MusicTypeSelectionViewModel? ViewModel => BindingContext as MusicTypeSelectionViewModel;

    public MusicTypeSelectionModal()
    {
        InitializeComponent();
        Appearing += OnAppearing;
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        // MusicTypeSelectionModal has a static list of 2 items (Melodies, Vocals)
        // No DB loading needed, so use simplified flow without IsBusy polling
        try
        {
            // Hide CollectionView while we set up
            // Skip on Windows to avoid access violation crash
            if (DeviceInfo.Platform != DevicePlatform.WinUI)
            {
                musicTypesCollectionView.Opacity = 0;
            }

            // Refresh state synchronously
            ViewModel?.RefreshFromState();

            // Small delay for UI to settle
            await Task.Delay(100, cancellationTokenSource.Token);

            // Scroll to selected item if any
            var selectedItem = ViewModel?.SelectedMusicType;
            if (selectedItem != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(
                    musicTypesCollectionView,
                    selectedItem,
                    animated: false,
                    cancellationToken: cancellationTokenSource.Token);
            }

            // Hide overlay and reveal list together
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                BusyOverlay.IsVisible = false;
                // Only set Opacity on non-Windows (we didn't hide it there)
                if (DeviceInfo.Platform != DevicePlatform.WinUI)
                {
                    musicTypesCollectionView.Opacity = 1;
                }
            });
        }
        catch (OperationCanceledException)
        {
            // User tapped item - reveal immediately
            BusyOverlay.IsVisible = false;
            if (DeviceInfo.Platform != DevicePlatform.WinUI)
            {
                musicTypesCollectionView.Opacity = 1;
            }
        }
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            ModalScrollHelper.DisposeModal(cancellationTokenSource, () => BindingContext = null);
            isDisposed = true;
        }
    }

    private async void OnMusicTypeItemTapped(object? sender, TappedEventArgs e)
    {
        // Cancel any ongoing scroll operation to prevent race conditions
        try { cancellationTokenSource.Cancel(); } catch { }

        if (sender is View view && view.BindingContext is MusicTypeListItemViewModel musicTypeItem)
        {
            // Set IsNavigating immediately to show progress indicator
            musicTypeItem.IsNavigating = true;
            
            // Wait 50ms to ensure UI thread renders the update before doing backend work
            await Task.Delay(50);

            try
            {
                if (ViewModel != null && ViewModel.MusicPublicationSelectionCommand is IAsyncRelayCommand<MusicTypeListItemViewModel> asyncCommand)
                {
                    if (asyncCommand.CanExecute(musicTypeItem))
                    {
                        await asyncCommand.ExecuteAsync(musicTypeItem);
                    }
                }
            }
            finally
            {
                // Reset IsNavigating after operation completes
                musicTypeItem.IsNavigating = false;
            }
        }
    }
}
