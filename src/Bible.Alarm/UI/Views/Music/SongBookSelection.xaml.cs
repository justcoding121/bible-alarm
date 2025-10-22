using Bible.Alarm.UI.ViewHelpers;
using Bible.Alarm.ViewModels;
using Bible.Alarm.ViewModels.Music;

namespace Bible.Alarm.UI.Views.Music;

public partial class SongBookSelection : ContentPage
{
    public SongBookSelectionViewModel ViewModel => BindingContext as SongBookSelectionViewModel;

    public SongBookSelection()
    {
        InitializeComponent();

        BackButton.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => AnimateUtils.FlickUponTouched(BackButton, 1500,
                ColorUtils.ToHexString(Colors.LightGray), ColorUtils.ToHexString(Colors.WhiteSmoke), 1))
        });
    }

    protected override bool OnBackButtonPressed()
    {
        ViewModel.BackCommand.Execute(null);
        return true;
    }
}