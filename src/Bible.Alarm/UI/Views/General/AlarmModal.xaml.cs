using Bible.Alarm.ViewModels;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

namespace Bible.Alarm.UI.Views
{
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
}