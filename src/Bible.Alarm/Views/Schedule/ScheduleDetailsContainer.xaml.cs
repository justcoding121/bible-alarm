#nullable enable

namespace Bible.Alarm.Views.Schedule;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class ScheduleDetailsContainer : ContentView
{
    public ScheduleDetailsContainer()
    {
        InitializeComponent();
    }

    public Entry? GetScheduleNameEntry() => ScheduleNameEntry;

    private void OnGridTapped(object? sender, TappedEventArgs e)
    {
        if (ScheduleNameEntry?.IsFocused == true)
        {
            ScheduleNameEntry.Unfocus();
        }
    }
}

