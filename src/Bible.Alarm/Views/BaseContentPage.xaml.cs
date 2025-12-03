using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

namespace Bible.Alarm.Views;

/// <summary>
/// Base class for all content pages in the application.
/// Theme-aware color resources are managed at the Application level (in App.xaml.cs),
/// so pages can use DynamicResource to automatically update when the theme changes.
/// </summary>
[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BaseContentPage : ContentPage
{
    public BaseContentPage()
    {
        InitializeComponent();
        // Theme-aware resources are managed at Application level in App.xaml.cs
        // Pages should use DynamicResource to reference these resources
    }
}

