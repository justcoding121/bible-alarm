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
        // Height is now dynamic (no HeightRequest) to properly support large accessibility font sizes
        // Buttons use FlexLayout with wrapping to handle narrow widths with large fonts
        // On WinUI, buttons are inside SfEffectsView; on Android/iOS, they're separate buttons
        Border? cancelButton = platform == DevicePlatform.WinUI ? CancelButton : CancelButtonNoEffects;
        Border? saveButton = platform == DevicePlatform.WinUI ? SaveButton : SaveButtonNoEffects;
        
        if (platform == DevicePlatform.WinUI)
        {
            if (cancelButton != null)
            {
                cancelButton.Padding = new Thickness(10, 10);
                cancelButton.VerticalOptions = LayoutOptions.Fill;
            }
            if (saveButton != null)
            {
                saveButton.Padding = new Thickness(10, 10);
                saveButton.VerticalOptions = LayoutOptions.Fill;
            }
            if (ButtonContainer != null)
            {
                ButtonContainer.Margin = new Thickness(5, 5, 5, 10);
            }
        }
        else if (platform == DevicePlatform.iOS)
        {
            if (cancelButton != null)
            {
                cancelButton.Padding = new Thickness(10, 12);
                cancelButton.VerticalOptions = LayoutOptions.Fill;
            }
            if (saveButton != null)
            {
                saveButton.Padding = new Thickness(10, 12);
                saveButton.VerticalOptions = LayoutOptions.Fill;
            }
            if (ButtonContainer != null)
            {
                ButtonContainer.Margin = new Thickness(5, 8, 5, 10);
            }
        }
        else if (platform == DevicePlatform.Android)
        {
            if (cancelButton != null)
            {
                cancelButton.Padding = new Thickness(10, 6, 10, 10);
                cancelButton.VerticalOptions = LayoutOptions.Center;
            }
            if (saveButton != null)
            {
                saveButton.Padding = new Thickness(10, 6, 10, 10);
                saveButton.VerticalOptions = LayoutOptions.Center;
            }
            if (ButtonContainer != null)
            {
                ButtonContainer.Margin = new Thickness(5, 5, 5, 10);
            }
        }
        else
        {
            if (cancelButton != null)
            {
                cancelButton.Padding = new Thickness(10, 8);
                cancelButton.VerticalOptions = LayoutOptions.Fill;
            }
            if (saveButton != null)
            {
                saveButton.Padding = new Thickness(10, 8);
                saveButton.VerticalOptions = LayoutOptions.Fill;
            }
            if (ButtonContainer != null)
            {
                ButtonContainer.Margin = new Thickness(5, 5, 5, 10);
            }
        }

        // Platform-specific styling for Delete button
        // FontSize is now set via XAML using ButtonFontSize resource which scales with accessibility settings
        // Height is now dynamic (no HeightRequest) to properly support large accessibility font sizes
        // On WinUI, button is inside SfEffectsView; on Android/iOS, it's a separate button
        Border? deleteButton = platform == DevicePlatform.WinUI ? DeleteButton : DeleteButtonNoEffectsBorder;
        if (deleteButton != null)
        {
            if (platform == DevicePlatform.WinUI)
            {
                deleteButton.Padding = new Thickness(10, 10);
                deleteButton.VerticalOptions = LayoutOptions.Fill;
            }
            else if (platform == DevicePlatform.iOS)
            {
                deleteButton.Padding = new Thickness(10, 12);
                deleteButton.VerticalOptions = LayoutOptions.Fill;
            }
            else if (platform == DevicePlatform.Android)
            {
                deleteButton.Padding = new Thickness(10, 6, 10, 10);
                deleteButton.VerticalOptions = LayoutOptions.Center;
            }
            else
            {
                deleteButton.Padding = new Thickness(10, 8);
                deleteButton.VerticalOptions = LayoutOptions.Fill;
            }
        }
        
        // Ensure DeleteButtonNoEffects is only visible on Android/iOS
        if (DeleteButtonNoEffects != null && platform == DevicePlatform.WinUI)
        {
            DeleteButtonNoEffects.IsVisible = false;
        }
    }
}

