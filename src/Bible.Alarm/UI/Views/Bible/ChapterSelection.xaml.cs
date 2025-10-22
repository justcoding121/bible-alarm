using Bible.Alarm.UI.ViewHelpers;
using Bible.Alarm.ViewModels;
using Bible.Alarm.ViewModels.Bible;

namespace Bible.Alarm.UI.Views.Bible;

public partial class ChapterSelection : ContentPage
{
    public ChapterSelectionViewModel ViewModel => BindingContext as ChapterSelectionViewModel;
    private readonly TaskScheduler _taskScheduler;

    public ChapterSelection(TaskScheduler taskScheduler)
    {
        _taskScheduler = taskScheduler;
        InitializeComponent();

        BackButton.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => AnimateUtils.FlickUponTouched(BackButton, 1500,
                ColorUtils.ToHexString(Colors.LightGray), ColorUtils.ToHexString(Colors.WhiteSmoke), 1))
        });

        Appearing += OnAppearing;
    }

    private void OnAppearing(object sender, EventArgs e)
    {
        Task.Delay(100).ContinueWith(x =>
        {
            chapterListView.ScrollTo(ViewModel.SelectedChapter, ScrollToPosition.Center, true);
            Appearing -= OnAppearing;
        }, _taskScheduler);
    }


    protected override bool OnBackButtonPressed()
    {
        ViewModel.BackCommand.Execute(null);
        return true;
    }
}