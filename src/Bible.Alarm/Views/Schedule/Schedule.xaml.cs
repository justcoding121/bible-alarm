using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels;
using Bible.Alarm.Views;

namespace Bible.Alarm.Views.Schedule;

public partial class Schedule : BaseContentPage
{
    public ScheduleViewModel ViewModel => BindingContext as ScheduleViewModel;

    public Schedule(ScheduleViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        MusicButton.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => AnimateUtils.FlickUponTouched(MusicButton, 1500,
                ColorUtils.ToHexString(Colors.LightGray), ColorUtils.ToHexString(Colors.WhiteSmoke), 1))
        });

        BibleButton.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => AnimateUtils.FlickUponTouched(BibleButton, 1500,
                ColorUtils.ToHexString(Colors.LightGray), ColorUtils.ToHexString(Colors.WhiteSmoke), 1))
        });
    }

    protected override bool OnBackButtonPressed()
    {
        ViewModel.CancelCommand.Execute(null);
        return true;
    }
}