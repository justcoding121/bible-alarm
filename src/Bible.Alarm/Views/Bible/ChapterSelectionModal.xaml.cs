#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Bible;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Serilog;

namespace Bible.Alarm.Views.Bible;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class ChapterSelectionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public ChapterSelectionViewModel? ViewModel => BindingContext as ChapterSelectionViewModel;

    public ChapterSelectionModal()
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
                // Refresh from state when modal appears to ensure chapters are populated
                await ViewModel.RefreshFromState();

                // Force hide busy overlay since binding might not work
                ForceHideBusyOverlay();

                if (ViewModel.SelectedChapter != null && chapterCollectionView != null)
                {
                    await CollectionViewHelper.ScrollToWhenReadyAsync(chapterCollectionView, ViewModel.SelectedChapter, animated: false, cancellationToken: cancellationTokenSource.Token);
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error in ChapterSelectionModal.OnAppearing");
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

    private async void OnChapterItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is BibleChapterListViewItemModel chapterItem)
        {
            if (ViewModel != null && ViewModel.SetChapterCommand is IAsyncRelayCommand<BibleChapterListViewItemModel> asyncCommand)
            {
                if (asyncCommand.CanExecute(chapterItem))
                {
                    await asyncCommand.ExecuteAsync(chapterItem);
                }
            }
        }
    }
}

