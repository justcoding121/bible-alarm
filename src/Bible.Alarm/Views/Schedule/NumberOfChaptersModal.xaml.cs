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

    public ChaptersSelectionContainerViewModel? ViewModel => BindingContext as ChaptersSelectionContainerViewModel;

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

        if (ViewModel?.CurrentNumberOfChapters != null && ChaptersCollectionView != null)
        {
            await CollectionViewHelper.ScrollToWhenReadyAsync(ChaptersCollectionView, ViewModel.CurrentNumberOfChapters, cancellationToken: cancellationTokenSource.Token);
        }
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            // Cancel and dispose cancellation token source
            try
            {
                cancellationTokenSource?.Cancel();
                cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                // Ignore errors during cancellation/disposal
                Log.Logger.Warning(ex, "Error during cancellation token source disposal");
            }

            // This modal uses parent page view model, so do NOT dispose it
            // Clear BindingContext to break reference and allow garbage collection
            BindingContext = null;
            isDisposed = true;
        }
    }
}
