using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Music;

namespace Bible.Alarm.Views.Music;

public partial class TrackSelection : BaseContentPage
{
    public TrackSelectionViewModel ViewModel => BindingContext as TrackSelectionViewModel;
    private readonly TaskScheduler _taskScheduler;

    public TrackSelection(TrackSelectionViewModel viewModel, TaskScheduler taskScheduler)
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
        
        if (ViewModel?.SelectedTrack != null && trackListView != null)
        {
            await ListViewHelper.ScrollToWhenReadyAsync(trackListView, ViewModel.SelectedTrack);
        }
    }

    protected override bool OnBackButtonPressed()
    {
        ViewModel.BackCommand.Execute(null);
        return true;
    }
}