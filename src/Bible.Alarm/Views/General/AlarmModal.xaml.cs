using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.Views.General;

public partial class AlarmModal : BaseContentPage
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