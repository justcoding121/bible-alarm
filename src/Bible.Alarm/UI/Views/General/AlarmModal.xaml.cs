using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.UI.Views.General;

public partial class AlarmModal : ContentPage
{
    public AlarmViewModal ViewModel => BindingContext as AlarmViewModal;

    public AlarmModal()
    {
        InitializeComponent();
    }

    protected override bool OnBackButtonPressed()
    {
        ViewModel.DismissCommand.Execute(null);
        return true;
    }
}