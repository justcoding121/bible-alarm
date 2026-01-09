#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Bible;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

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

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        await ModalScrollHelper.HandleModalAppearingAsync(
            () => ViewModel?.IsBusy ?? false,
            BusyOverlay,
            chapterCollectionView,
            getSelectedItem: () => ViewModel?.SelectedChapter,
            refreshAction: ViewModel != null 
                ? async () => await ViewModel.RefreshFromState() 
                : null,
            cancellationToken: cancellationTokenSource.Token);
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            ModalScrollHelper.DisposeModal(cancellationTokenSource, () => BindingContext = null);
            isDisposed = true;
        }
    }

    private async void OnChapterItemTapped(object? sender, TappedEventArgs e)
    {
        // Cancel any ongoing scroll operation to prevent race conditions
        try { cancellationTokenSource.Cancel(); } catch { }

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

