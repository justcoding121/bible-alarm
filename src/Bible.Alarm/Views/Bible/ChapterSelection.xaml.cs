using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Bible;

namespace Bible.Alarm.Views.Bible;

public partial class ChapterSelection : ContentPage, IDisposable
{
    private bool _isDisposed;
    private readonly ChapterSelectionViewModel _viewModel;
    private readonly TaskScheduler _taskScheduler;

    public ChapterSelectionViewModel ViewModel => BindingContext as ChapterSelectionViewModel;

    private bool _isClearingSelection;

    public ChapterSelection(ChapterSelectionViewModel viewModel, TaskScheduler taskScheduler)
    {
        _taskScheduler = taskScheduler;
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;

        BackButton.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => AnimateUtils.FlickUponTouched(BackButton, 1500,
                ColorUtils.ToHexString(Colors.LightGray), ColorUtils.ToHexString(Colors.WhiteSmoke), 1))
        });

        // Clear selection after SelectionChanged fires to allow command to execute first
        chapterCollectionView.SelectionChanged += (sender, e) =>
        {
            // Don't clear if we're already clearing or if selection is being cleared (CurrentSelection is empty or null)
            if (_isClearingSelection || e?.CurrentSelection == null || e.CurrentSelection.Count == 0)
            {
                return;
            }
            
            // Clear selection after a short delay to allow command to execute
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!_isDisposed && chapterCollectionView != null)
                    {
                        _isClearingSelection = true;
                        chapterCollectionView.SelectedItem = null;
                        _isClearingSelection = false;
                    }
                });
            });
        };

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