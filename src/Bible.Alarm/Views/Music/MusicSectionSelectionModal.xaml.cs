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

        try
        {
            // Hide CollectionView - overlay is already visible (no binding)
            // Skip on Windows to avoid access violation crash
            if (DeviceInfo.Platform != DevicePlatform.WinUI)
            {
                sectionCollectionView.Opacity = 0;
            }

            // Trigger data refresh
            if (ViewModel != null)
            {
                await ViewModel.RefreshFromState();
            }

            // Wait for IsBusy to become false (data loaded)
            await CollectionViewHelper.WaitForNotBusyAsync(
                () => ViewModel?.IsBusy ?? false,
                cancellationToken: cancellationTokenSource.Token);

            // Delay for CollectionView to render
            await Task.Delay(150, cancellationTokenSource.Token);

            // Scroll to selected item
            var selectedItem = ViewModel?.SelectedSection;
            if (selectedItem != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(
                    sectionCollectionView,
                    selectedItem,
                    animated: false,
                    cancellationToken: cancellationTokenSource.Token);

                await Task.Delay(50, cancellationTokenSource.Token);
            }

            // Hide overlay and reveal list together
            // Don't directly set BusyOverlay.IsVisible - let the binding handle it via IsBusy
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Only set Opacity on non-Windows (we didn't hide it there)
                if (DeviceInfo.Platform != DevicePlatform.WinUI)
                {
                    sectionCollectionView.Opacity = 1;
                }
            });
        }
        catch (OperationCanceledException)
        {
            // User tapped item - TrackSelectionCommand will handle IsBusy, don't interfere
            // Just reveal the list if needed
            if (DeviceInfo.Platform != DevicePlatform.WinUI)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    sectionCollectionView.Opacity = 1;
                });
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
