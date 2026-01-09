#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Bible;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

namespace Bible.Alarm.Views.Bible;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BookSelectionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public BookSelectionViewModel? ViewModel => BindingContext as BookSelectionViewModel;

    public BookSelectionModal()
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
                bookCollectionView.Opacity = 0;
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
            var selectedItem = ViewModel?.SelectedBook;
            if (selectedItem != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(
                    bookCollectionView,
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
                    bookCollectionView.Opacity = 1;
                }
            });
        }
        catch (OperationCanceledException)
        {
            // User tapped item - reveal immediately
            BusyOverlay.IsVisible = false;
            if (DeviceInfo.Platform != DevicePlatform.WinUI)
            {
                bookCollectionView.Opacity = 1;
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

    private async void OnBookItemTapped(object? sender, TappedEventArgs e)
    {
        // Cancel any ongoing scroll operation to prevent race conditions
        try { cancellationTokenSource.Cancel(); } catch { }

        if (sender is Grid grid && grid.BindingContext is BibleBookListViewItemModel bookItem)
        {
            if (ViewModel != null && ViewModel.ChapterSelectionCommand is IAsyncRelayCommand<BibleBookListViewItemModel> asyncCommand)
            {
                if (asyncCommand.CanExecute(bookItem))
                {
                    await asyncCommand.ExecuteAsync(bookItem);
                }
            }
        }
    }
}

