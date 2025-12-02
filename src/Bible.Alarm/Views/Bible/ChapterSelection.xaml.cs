using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Bible;

namespace Bible.Alarm.Views.Bible;

public partial class ChapterSelection : ContentPage, IDisposable
{
    private bool _isDisposed;
    private readonly ChapterSelectionViewModel _viewModel;
    private readonly TaskScheduler _taskScheduler;

    public ChapterSelectionViewModel ViewModel => BindingContext as ChapterSelectionViewModel;


    public ChapterSelection(ChapterSelectionViewModel viewModel, TaskScheduler taskScheduler)
    {
        _taskScheduler = taskScheduler;
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;

        // SelectionChanged handler removed - using SelectionMode="None" with TapGestureRecognizer instead
        // This eliminates the orange flash visual feedback

        Appearing += OnAppearing;
    }

    private async void OnAppearing(object sender, EventArgs e)
    {
        Appearing -= OnAppearing;
        
        // Wait for the page to be fully loaded before attempting to scroll
        await Task.Delay(300);
        
        if (ViewModel?.SelectedChapter != null && chapterCollectionView != null)
        {
            await CollectionViewHelper.ScrollToWhenReadyAsync(chapterCollectionView, ViewModel.SelectedChapter);
        }
    }


    protected override bool OnBackButtonPressed()
    {
        ViewModel.BackCommand.Execute(null);
        return true;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            // ViewModel was injected via constructor, so dispose it
            if (_viewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _isDisposed = true;
        }
    }
}