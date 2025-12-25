#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Bible;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Serilog;

namespace Bible.Alarm.Views.Bible;

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

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        if (ViewModel != null)
        {
            // Refresh from state when modal appears to ensure chapters are populated
            await ViewModel.RefreshFromState();

            await Task.Delay(200, cancellationTokenSource.Token);

            if (ViewModel.SelectedChapter != null && chapterCollectionView != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(chapterCollectionView, ViewModel.SelectedChapter, animated: false, cancellationToken: cancellationTokenSource.Token);
            }
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

