using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Music;

namespace Bible.Alarm.Views.Music;

public partial class SongBookSelection : BaseContentPage
{
    public SongBookSelectionViewModel ViewModel => BindingContext as SongBookSelectionViewModel;

    public SongBookSelection(SongBookSelectionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

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