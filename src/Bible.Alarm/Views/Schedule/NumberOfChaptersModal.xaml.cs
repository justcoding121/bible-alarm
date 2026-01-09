#nullable enable

using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Serilog;

namespace Bible.Alarm.Views.Schedule;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class NumberOfChaptersModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public NumberOfChapterContainerViewModel? ViewModel => BindingContext as NumberOfChapterContainerViewModel;

    public NumberOfChaptersModal()
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

    private async void OnChapterItemTapped(object? sender, TappedEventArgs e)
    {
        // Cancel any ongoing scroll operation to prevent race conditions
        try { cancellationTokenSource.Cancel(); } catch { }

        if (sender is Grid grid && grid.BindingContext is NumberOfChaptersListViewItemModel chapterItem)
        {
            if (ViewModel != null && ViewModel.SelectNumberOfChaptersCommand is IAsyncRelayCommand<NumberOfChaptersListViewItemModel> asyncCommand)
            {
                if (asyncCommand.CanExecute(chapterItem))
                {
                    await asyncCommand.ExecuteAsync(chapterItem);
                }
            }
        }
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        try
        {
            if (ViewModel?.CurrentNumberOfChapters != null && ChaptersCollectionView != null)
            {
                // Hide CollectionView to prevent visible scroll jump
                ChaptersCollectionView.Opacity = 0;

                // Small delay to ensure CollectionView is rendered
                await Task.Delay(150, cancellationTokenSource.Token);

                await CollectionViewHelper.ScrollToWhenReadyAsync(ChaptersCollectionView, ViewModel.CurrentNumberOfChapters, animated: false, cancellationToken: cancellationTokenSource.Token);

                // Small delay to ensure scroll completes
                await Task.Delay(50, cancellationTokenSource.Token);

                // Reveal CollectionView (now in correct position)
                ChaptersCollectionView.Opacity = 1;
            }
        }
        catch (OperationCanceledException)
        {
            // User tapped an item - reveal immediately
            if (ChaptersCollectionView != null)
                ChaptersCollectionView.Opacity = 1;
        }
        catch (Exception ex)
        {
            // OnAppearing errors are non-critical (UI initialization)
            Serilog.Log.Warning(ex, "Error in NumberOfChaptersModal.OnAppearing");
            if (ChaptersCollectionView != null)
                ChaptersCollectionView.Opacity = 1;
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
