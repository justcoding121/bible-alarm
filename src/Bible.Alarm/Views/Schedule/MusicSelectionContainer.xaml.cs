using Bible.Alarm.ViewModels.Schedule;

namespace Bible.Alarm.Views.Schedule;

public partial class MusicSelectionContainer : ContentView
{
    public MusicSelectionContainer()
    {
        InitializeComponent();
    }

    public MusicSelectionContainer(MusicSelectionContainerViewModel viewModel) : this()
    {
        BindingContext = viewModel;
    }
}

