using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Bible;

namespace Bible.Alarm.Views.Bible;

public partial class BookSelection : BaseContentPage
{
    public BookSelectionViewModel ViewModel => BindingContext as BookSelectionViewModel;
    private readonly TaskScheduler _taskScheduler;

    public BookSelection(BookSelectionViewModel viewModel, TaskScheduler taskScheduler)
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
        
        // Wait for the page to be fully loaded before attempting to scroll
        await Task.Delay(300);
        
        if (ViewModel?.SelectedBook != null && bookListView != null)
        {
            await ListViewHelper.ScrollToWhenReadyAsync(bookListView, ViewModel.SelectedBook);
        }
    }

    protected override bool OnBackButtonPressed()
    {
        ViewModel.BackCommand.Execute(null);
        return true;
    }
}