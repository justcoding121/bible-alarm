#nullable enable
namespace Bible.Alarm.Views.Schedule;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class ScheduleContent : ContentView
{
    public ScheduleContent()
    {
        InitializeComponent();

        // Apply platform-specific styling in code-behind for better performance
        // This avoids expensive OnPlatform markup extension evaluation at runtime
        ApplyPlatformSpecificStyling();
    }

    private void ApplyPlatformSpecificStyling()
    {
        var platform = DeviceInfo.Platform;

        // Platform-specific margins for main grid
        if (MainGrid != null)
        {
            if (platform == DevicePlatform.iOS)
            {
                MainGrid.Margin = new Thickness(0, 20, 0, 0);
            }
            else if (platform == DevicePlatform.Android)
            {
                MainGrid.Margin = new Thickness(0, 24, 0, 0);
            }
            else
            {
                MainGrid.Margin = new Thickness(0);
            }
        }

        // Platform-specific margins for scroll content grid
        if (ScrollContentGrid != null)
        {
            ScrollContentGrid.Margin = new Thickness(10, 0, 10, 0);
        }

        // Platform-specific styling for Cancel and Save buttons
        // FontSize is now set via XAML using ButtonFontSize resource which scales with accessibility settings
        // Only platform-specific height, padding, and margins are set here
        if (platform == DevicePlatform.WinUI)
        {
            if (CancelButton != null)
            {
                CancelButton.HeightRequest = 45;
                CancelButton.Padding = new Thickness(10, 10);
                CancelButton.VerticalOptions = LayoutOptions.Fill;
            }
            if (SaveButton != null)
            {
                SaveButton.HeightRequest = 45;
                SaveButton.Padding = new Thickness(10, 10);
                SaveButton.VerticalOptions = LayoutOptions.Fill;
            }
            if (ButtonContainer != null)
            {
                ButtonContainer.Margin = new Thickness(5, 5, 5, 10);
            }
        }
        else if (platform == DevicePlatform.iOS)
        {
            if (CancelButton != null)
            {
                CancelButton.HeightRequest = 50;
                CancelButton.Padding = new Thickness(10, 12);
                CancelButton.VerticalOptions = LayoutOptions.Fill;
            }
            if (SaveButton != null)
            {
                SaveButton.HeightRequest = 50;
                SaveButton.Padding = new Thickness(10, 12);
                SaveButton.VerticalOptions = LayoutOptions.Fill;
            }
            if (ButtonContainer != null)
            {
                ButtonContainer.Margin = new Thickness(5, 8, 5, 10);
            }
        }
        else if (platform == DevicePlatform.Android)
        {
            if (CancelButton != null)
            {
                CancelButton.HeightRequest = 45;
                CancelButton.Padding = new Thickness(10, 6, 10, 10);
                CancelButton.VerticalOptions = LayoutOptions.Center;
            }
            if (SaveButton != null)
            {
                SaveButton.HeightRequest = 45;
                SaveButton.Padding = new Thickness(10, 6, 10, 10);
                SaveButton.VerticalOptions = LayoutOptions.Center;
            }
            if (ButtonContainer != null)
            {
                ButtonContainer.Margin = new Thickness(5, 5, 5, 10);
            }
        }
        else
        {
            if (CancelButton != null)
            {
                CancelButton.HeightRequest = 45;
                CancelButton.Padding = new Thickness(10, 8);
                CancelButton.VerticalOptions = LayoutOptions.Fill;
            }
            if (SaveButton != null)
            {
                SaveButton.HeightRequest = 45;
                SaveButton.Padding = new Thickness(10, 8);
                SaveButton.VerticalOptions = LayoutOptions.Fill;
            }
            if (ButtonContainer != null)
            {
                ButtonContainer.Margin = new Thickness(5, 5, 5, 10);
            }
        }

        // Platform-specific styling for Delete button
        // FontSize is now set via XAML using ButtonFontSize resource which scales with accessibility settings
        // Only platform-specific height and padding are set here
        if (DeleteButton != null)
        {
            if (platform == DevicePlatform.WinUI)
            {
                DeleteButton.HeightRequest = 45;
                DeleteButton.Padding = new Thickness(10, 10);
            }
            else if (platform == DevicePlatform.iOS)
            {
                DeleteButton.HeightRequest = 50;
                DeleteButton.Padding = new Thickness(10, 12);
            }
            else if (platform == DevicePlatform.Android)
            {
                DeleteButton.HeightRequest = 45;
                DeleteButton.Padding = new Thickness(10, 6, 10, 10);
            }
            else
            {
                DeleteButton.HeightRequest = 45;
                DeleteButton.Padding = new Thickness(10, 8);
            }
        }
    }
}

