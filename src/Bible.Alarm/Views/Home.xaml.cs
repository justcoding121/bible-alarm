using Bible.Alarm.ViewModels;

namespace Bible.Alarm.Views;

public partial class Home : BaseContentPage
{
    public Home(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}