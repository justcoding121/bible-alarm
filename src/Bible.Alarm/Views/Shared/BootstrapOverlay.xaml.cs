#nullable enable
namespace Bible.Alarm.Views.Shared;

public partial class BootstrapOverlay : ContentPage
{
    public BootstrapOverlay()
    {
        InitializeComponent();
    }

    public void Deactivate()
    {
        Content = null;
        BackgroundColor = Colors.Transparent;
    }
}
