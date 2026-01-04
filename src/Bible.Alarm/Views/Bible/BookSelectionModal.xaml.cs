#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Bible;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Serilog;

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

    // Force busy overlay to hide when needed
    private void ForceHideBusyOverlay()
    {
        if (BusyOverlay != null)
        {
            BusyOverlay.IsVisible = false;
            BusyOverlay.InputTransparent = true;
        }
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        try
        {
            if (ViewModel != null)
            {
                // Refresh from state when modal appears to ensure we have the latest language/publication
                // This is important when the modal is opened after language or translation changes
                await ViewModel.RefreshFromState();

                // Force hide busy overlay since binding might not work
                ForceHideBusyOverlay();

                // Scroll to selected book - matching the pattern used in MusicLanguageModal
                // No need to wait for IsBusy - RefreshFromState handles that internally
                if (ViewModel.SelectedBook != null && bookCollectionView != null)
                {
                    await CollectionViewHelper.ScrollToWhenReadyAsync(bookCollectionView, ViewModel.SelectedBook, animated: false, cancellationToken: cancellationTokenSource.Token);
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error in BookSelectionModal.OnAppearing");
            ForceHideBusyOverlay();
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

    private async void OnBookItemTapped(object? sender, TappedEventArgs e)
    {
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

