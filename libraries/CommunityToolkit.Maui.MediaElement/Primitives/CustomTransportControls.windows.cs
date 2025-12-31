using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Visibility = Microsoft.UI.Xaml.Visibility;

namespace CommunityToolkit.Maui.Primitives;

sealed partial class CustomTransportControls : MediaTransportControls
{
    public event EventHandler<EventArgs>? OnTemplateLoaded;
    public AppBarButton FullScreenButton = new();
    bool isFullScreen;

    public CustomTransportControls()
    {
        DefaultStyleKey = typeof(CustomTransportControls);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (GetTemplateChild("FullWindowButton") is AppBarButton appBarButton)
        {
            FullScreenButton = appBarButton;
            FullScreenButton.Visibility = Visibility.Visible;
            OnTemplateLoaded?.Invoke(this, EventArgs.Empty);
            FullScreenButton.Click += FullScreenButton_Click;
        }
    }

    void FullScreenButton_Click(object sender, RoutedEventArgs e)
    {
        if (isFullScreen)
        {
            FullScreenButton.Icon = new FontIcon { Glyph = "\uE740" };
            isFullScreen = false;
        }
        else
        {
            FullScreenButton.Icon = new SymbolIcon(Symbol.BackToWindow);
            isFullScreen = true;
        }
    }
}