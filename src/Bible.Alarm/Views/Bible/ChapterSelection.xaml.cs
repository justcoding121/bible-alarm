using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Bible;

namespace Bible.Alarm.Views.Bible;

public partial class ChapterSelection : BaseContentPage
{
    public ChapterSelectionViewModel ViewModel => BindingContext as ChapterSelectionViewModel;
    private readonly TaskScheduler _taskScheduler;

    public ChapterSelection(ChapterSelectionViewModel viewModel, TaskScheduler taskScheduler)
    {
        _taskScheduler = taskScheduler;
        InitializeComponent();
        BindingContext = viewModel;

        BackButton.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => AnimateUtils.FlickUponTouched(BackButton, 1500,
                ColorUtils.ToHexString(Colors.LightGray), ColorUtils.ToHexString(Colors.WhiteSmoke), 1))
        });

        Appearing += OnAppearing;
    }

    private async void OnAppearing(object sender, EventArgs e)
    {
        Appearing -= OnAppearing;
        
        if (ViewModel?.SelectedChapter != null && chapterListView != null)
        {
            await ListViewHelper.ScrollToWhenReadyAsync(chapterListView, ViewModel.SelectedChapter);
        }
    }


    protected override bool OnBackButtonPressed()
    {
        ViewModel.BackCommand.Execute(null);
        return true;
    }
}