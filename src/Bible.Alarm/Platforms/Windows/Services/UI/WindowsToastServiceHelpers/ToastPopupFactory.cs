#nullable enable
using Bible.Alarm.Shared.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Application = Microsoft.Maui.Controls.Application;
using Border = Microsoft.UI.Xaml.Controls.Border;
using Colors = Microsoft.UI.Colors;
using CornerRadius = Microsoft.UI.Xaml.CornerRadius;
using HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment;
using SolidColorBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;
using Thickness = Microsoft.UI.Xaml.Thickness;
using VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment;
using Window = Microsoft.UI.Xaml.Window;
using WinUIColor = global::Windows.UI.Color;

namespace Bible.Alarm.Platforms.Windows.Services.UI.WindowsToastServiceHelpers;

/// <summary>
/// Creates toast popup UI elements.
/// </summary>
internal static class ToastPopupFactory
{
    public static Popup CreateToastPopup(string message, Window currentWindow)
    {
        var theme = Application.Current?.RequestedTheme ?? AppTheme.Light;
        var backgroundColor = GetToastBackgroundColor(theme);
        var textColor = GetToastTextColor();

        var text = ToastMessageNormalizer.Normalize(message);

        // Create a TextBlock for the message
        var textBlock = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Padding = new Thickness(16, 12, 16, 12),
            Foreground = new SolidColorBrush(textColor),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 400
        };

        // Create a Border for the toast background
        var border = new Border
        {
            Background = new SolidColorBrush(backgroundColor),
            CornerRadius = new CornerRadius(8),
            Child = textBlock,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // Create the Popup
        var popup = new Popup
        {
            Child = border,
            IsLightDismissEnabled = false,
            ShouldConstrainToRootBounds = true
        };

        return popup;
    }

    private static WinUIColor GetToastBackgroundColor(AppTheme theme)
    {
        var argb = ToastPopupThemeArgb.Background(theme == AppTheme.Dark);
        return WinUIColor.FromArgb(argb.A, argb.R, argb.G, argb.B);
    }

    private static WinUIColor GetToastTextColor() => Colors.White;
}

