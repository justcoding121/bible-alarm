#nullable enable

using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;

namespace Bible.Alarm.Views.Schedule;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class NumberOfTracksModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public NumberOfTrackContainerViewModel? ViewModel => BindingContext as NumberOfTrackContainerViewModel;

    public NumberOfTracksModal()
    {
        InitializeComponent();

        // Apply platform-specific styling in code-behind for better performance
        // This avoids expensive OnPlatform markup extension evaluation at runtime
        ApplyPlatformSpecificStyling();

        // SelectionChanged handler removed - using SelectionMode="None" with TapGestureRecognizer instead
        Appearing += OnAppearing;
    }

    private void ApplyPlatformSpecificStyling()
    {
        var platform = DeviceInfo.Platform;

        // Platform-specific margins for main grid
        if (MainGrid != null)
        {
            if (platform == DevicePlatform.iOS)
            {
                MainGrid.Margin = new Thickness(0, 20, 0, 0);
            }
            else if (platform == DevicePlatform.Android)
            {
                MainGrid.Margin = new Thickness(0, 24, 0, 0);
            }
            else
            {
                MainGrid.Margin = new Thickness(0);
            }
        }
    }

    private async void OnTrackItemTapped(object? sender, TappedEventArgs e)
    {
        // Cancel any ongoing scroll operation to prevent race conditions
        try { cancellationTokenSource.Cancel(); } catch { }

        if (sender is View view && view.BindingContext is NumberOfTracksListViewItemModel trackItem)
        {
            // Set IsNavigating immediately to show progress indicator
            trackItem.IsNavigating = true;
            
            // Wait 50ms to ensure UI thread renders the update before doing backend work
            await Task.Delay(50);

            try
            {
                if (ViewModel != null && ViewModel.SelectNumberOfTracksCommand is IAsyncRelayCommand<NumberOfTracksListViewItemModel> asyncCommand)
                {
                    if (asyncCommand.CanExecute(trackItem))
                    {
                        await asyncCommand.ExecuteAsync(trackItem);
                    }
                }
            }
            finally
            {
                // Reset IsNavigating after operation completes
                trackItem.IsNavigating = false;
            }
        }
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
                TracksCollectionView.Opacity = 0;
            }

            // Small delay to ensure CollectionView is rendered
            await Task.Delay(150, cancellationTokenSource.Token);

            // Scroll to selected item if any
            if (ViewModel?.CurrentNumberOfTracks != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(
                    TracksCollectionView,
                    ViewModel.CurrentNumberOfTracks,
                    animated: false,
                    cancellationToken: cancellationTokenSource.Token);

                // Small delay to ensure scroll completes
                await Task.Delay(50, cancellationTokenSource.Token);
            }

            // Hide overlay and reveal list together (binding: set IsBusy = false)
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (ViewModel != null)
                    ViewModel.IsBusy = false;
                if (DeviceInfo.Platform != DevicePlatform.WinUI)
                    TracksCollectionView.Opacity = 1;
            });
        }
        catch (OperationCanceledException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (ViewModel != null)
                    ViewModel.IsBusy = false;
                if (DeviceInfo.Platform != DevicePlatform.WinUI)
                    TracksCollectionView.Opacity = 1;
            });
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Error in NumberOfTracksModal.OnAppearing");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (ViewModel != null)
                    ViewModel.IsBusy = false;
                if (DeviceInfo.Platform != DevicePlatform.WinUI)
                    TracksCollectionView.Opacity = 1;
            });
        }
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            ModalScrollHelper.DisposeModal(cancellationTokenSource, () => BindingContext = null, ViewModel);
            isDisposed = true;
        }
    }
}
