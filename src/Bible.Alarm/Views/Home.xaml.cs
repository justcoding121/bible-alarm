using Bible.Alarm.ViewModels;

namespace Bible.Alarm.Views;

public partial class Home : ContentPage
{
    public Home(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}