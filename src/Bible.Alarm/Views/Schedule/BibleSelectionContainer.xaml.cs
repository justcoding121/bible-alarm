using Bible.Alarm.ViewModels.Schedule;

namespace Bible.Alarm.Views.Schedule;

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

