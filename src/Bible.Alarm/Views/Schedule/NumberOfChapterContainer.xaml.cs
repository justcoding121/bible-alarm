#nullable enable
using Bible.Alarm.ViewModels.Schedule;
using Microsoft.Maui.Controls.Xaml;

namespace Bible.Alarm.Views.Schedule;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class NumberOfChapterContainer : ContentView
{
    public NumberOfChapterContainer()
    {
        InitializeComponent();
        // Ensure container is visible even when BindingContext is null
        IsVisible = true;
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        
        // Ensure container remains visible even if BindingContext is null
        // The container should always be visible on the schedule page
        IsVisible = true;
    }
}

