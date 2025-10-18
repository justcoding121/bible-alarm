using Bible.Alarm.UI.ViewHelpers;
using Bible.Alarm.ViewModels;

namespace Bible.Alarm.UI.Views.Music;

public partial class TrackSelection : ContentPage
{
    public TrackSelectionViewModel ViewModel => BindingContext as TrackSelectionViewModel;

    public TrackSelection()
    {
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
            trackListView.ScrollTo(ViewModel.SelectedTrack, ScrollToPosition.Center, true);
            Appearing -= OnAppearing;
        }, ServiceProviderManager.GetService<TaskScheduler>());
    }

    protected override bool OnBackButtonPressed()
    {
        ViewModel.BackCommand.Execute(null);
        return true;
    }
}