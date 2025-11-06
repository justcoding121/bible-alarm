using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Bible;

namespace Bible.Alarm.Views.Bible;

public partial class BibleSelection : ContentPage
{
    public BibleSelectionViewModel ViewModel => BindingContext as BibleSelectionViewModel;

    public BibleSelection()
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