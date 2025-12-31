using Bible.Alarm.ViewModels.Schedule;
using Microsoft.Maui.Controls.Xaml;

namespace Bible.Alarm.Views.Schedule;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BibleSelectionContainer : ContentView
{
    public BibleSelectionContainer()
    {
        InitializeComponent();
    }

    public BibleSelectionContainer(BibleSelectionContainerViewModel viewModel) : this()
    {
        BindingContext = viewModel;
    }
}

