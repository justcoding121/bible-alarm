#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;

namespace Bible.Alarm.Views.Shared;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class MusicLanguageModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public IListViewModel? ViewModel => BindingContext as IListViewModel;

    public MusicLanguageModal()
    {
        InitializeComponent();
        Appearing += OnAppearing;
    }

    private async void OnLanguageItemTapped(object? sender, TappedEventArgs e)
    {
        // Cancel any ongoing scroll operation to prevent race conditions
        try { cancellationTokenSource.Cancel(); } catch { }

        if (sender is View view && view.BindingContext is LanguageListViewItemModel languageItem)
        {
            // Set IsNavigating immediately to show progress indicator
            languageItem.IsNavigating = true;
            
            // Wait 50ms to ensure UI thread renders the update before doing backend work
            await Task.Delay(50);

            try
            {
                if (ViewModel is SongPublicationSelectionViewModel songPublicationViewModel)
                {
                    if (songPublicationViewModel.SelectLanguageCommand is IAsyncRelayCommand<LanguageListViewItemModel> asyncCommand)
                    {
                        if (asyncCommand.CanExecute(languageItem))
                        {
                            await asyncCommand.ExecuteAsync(languageItem);
                        }
                    }
                }
            }
            finally
            {
                // Reset IsNavigating after operation completes
                languageItem.IsNavigating = false;
            }
        }
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        try
        {
            // Yield immediately to let spinner start animating
            await Task.Yield();

            var songPublicationViewModel = ViewModel as SongPublicationSelectionViewModel;

            // Hide CollectionView - overlay is already visible (no binding)
            // Skip on Windows to avoid access violation crash
            if (DeviceInfo.Platform != DevicePlatform.WinUI)
            {
                LanguageCollectionView.Opacity = 0;
            }

            // Another yield before starting data load
            await Task.Yield();

            // Trigger data refresh
            if (songPublicationViewModel != null)
            {
                await songPublicationViewModel.RefreshFromState();
            }

            // Wait for IsBusy to become false (data loaded)
            await CollectionViewHelper.WaitForNotBusyAsync(
                () => ViewModel?.IsBusy ?? false,
                cancellationToken: cancellationTokenSource.Token);

            // Delay for CollectionView to render
            await Task.Delay(150, cancellationTokenSource.Token);

            // Scroll to selected item
            var selectedItem = songPublicationViewModel?.Languages?.FirstOrDefault(l => l.IsSelected);
            if (selectedItem != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(
                    LanguageCollectionView,
                    selectedItem,
                    animated: false,
                    cancellationToken: cancellationTokenSource.Token);

                await Task.Delay(50, cancellationTokenSource.Token);
            }

            // Hide overlay and reveal list together
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                BusyOverlay.IsVisible = false;
                // Only set Opacity on non-Windows (we didn't hide it there)
                if (DeviceInfo.Platform != DevicePlatform.WinUI)
                {
                    LanguageCollectionView.Opacity = 1;
                }
            });
        }
        catch (OperationCanceledException)
        {
            // User tapped item - reveal immediately
            BusyOverlay.IsVisible = false;
            if (DeviceInfo.Platform != DevicePlatform.WinUI)
            {
                LanguageCollectionView.Opacity = 1;
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
}

