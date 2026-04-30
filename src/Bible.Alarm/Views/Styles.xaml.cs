#nullable enable
using System.ComponentModel;
using Bible.Alarm.Services.UI;
using Bible.Alarm.Services.UI.Interfaces;

namespace Bible.Alarm.Views;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class Styles : ResourceDictionary
{
    private IFontService? fontService;

    public Styles()
    {
        InitializeComponent();

        // Update font size resources with actual scaled values from FontService
        // This must be done after InitializeComponent() but the resources are already
        // available for StaticResource bindings to resolve correctly
        UpdateFontSizeResources();

        // Subscribe to FontService property changes for instant updates
        // when OS font size changes (similar to light/dark mode instant switching)
        SubscribeToFontServiceChanges();
    }

    private void SubscribeToFontServiceChanges()
    {
        fontService = FontServiceHelper.GetFontService();

        // FontService implements INotifyPropertyChanged and fires when OS font scale changes
        if (fontService is INotifyPropertyChanged notifyPropertyChanged)
        {
            notifyPropertyChanged.PropertyChanged += OnFontServicePropertyChanged;
        }
    }

    private void OnFontServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // FontService fires with empty string when all properties change (after Recalculate)
        // Update all font resources on the main thread to ensure UI updates correctly
        MainThread.BeginInvokeOnMainThread(UpdateFontSizeResources);
    }

    private void UpdateFontSizeResources()
    {
        // Get FontService from FontServiceHelper (initialized in MauiProgram)
        var service = fontService ?? FontServiceHelper.GetFontService();

        // Calculate font scale factor based on StandardFontSize (which already includes accessibility scaling)
        // Use platform-specific base size to calculate scale factor for spacing/padding
        // This ensures spacing scales proportionally with font size changes (accessibility, density, etc.)
        var currentPlatform = DeviceInfo.Platform;
        double baseStandardSize;
        if (currentPlatform == DevicePlatform.iOS)
            baseStandardSize = 17.0;      // iOS Body default
        else if (currentPlatform == DevicePlatform.Android)
            baseStandardSize = 16.0;      // Android Body Large default
        else if (currentPlatform == DevicePlatform.WinUI)
            baseStandardSize = 14.0;      // Windows Body default
        else
            baseStandardSize = 16.0;       // Default fallback
        double fontScaleFactor = service.StandardFontSize / baseStandardSize;

        // Update resources with actual scaled values (includes OS accessibility font scale)
        // DynamicResource bindings will automatically pick up these changes
        this["StandardFontSize"] = service.StandardFontSize;
        this["HeaderFontSize"] = service.HeaderFontSize;
        this["ButtonFontSize"] = service.ButtonFontSize;
        this["SmallFontSize"] = service.SmallFontSize;
        this["SmallMediumFontSize"] = service.SmallMediumFontSize;
        this["MediumFontSize"] = service.MediumFontSize;
        this["LargeFontSize"] = service.LargeFontSize;
        this["TitleFontSize"] = service.TitleFontSize;
        this["AlarmTimeFontSize"] = service.AlarmTimeFontSize;
        this["AlarmMeridianFontSize"] = service.AlarmMeridianFontSize;
        this["AlarmBellIconFontSize"] = service.AlarmBellIconFontSize;

        // Icon font sizes (for Font Awesome icons etc.)
        this["IconSmallFontSize"] = service.IconSmallFontSize;
        this["IconStandardFontSize"] = service.IconStandardFontSize;
        this["IconLargeFontSize"] = service.IconLargeFontSize;

        // Icon container sizes (for icon button containers)
        this["IconSmallContainerSize"] = service.IconSmallContainerSize;
        this["IconStandardContainerSize"] = service.IconStandardContainerSize;
        this["IconLargeContainerSize"] = service.IconLargeContainerSize;

        var playButtonHeight = service.IconLargeContainerSize;
        this["ScheduleListPlayButtonWidth"] = Math.Round(playButtonHeight * 1.14, 1);
        this["ScheduleListPlayButtonCornerRadius"] = Math.Round(Math.Min(playButtonHeight * 0.4, 28.0), 1);

        // Spinner sizes (larger on iOS)
        this["SpinnerContainerSize"] = service.SpinnerContainerSize;
        this["SpinnerFontSize"] = service.SpinnerFontSize;

        // Content widths (for modal and content widths)
        this["ContentWidthSmall"] = service.ContentWidthSmall;
        this["ContentWidthMedium"] = service.ContentWidthMedium;
        this["ContentWidthLarge"] = service.ContentWidthLarge;

        // Spacing resources - scale with font size for fluent UI
        // Base values are defined in XAML, scaled here
        this["SpacingExtraSmall"] = 2.0 * fontScaleFactor;
        this["SpacingSmall"] = 4.0 * fontScaleFactor;
        this["SpacingMedium"] = 8.0 * fontScaleFactor;
        this["SpacingStandard"] = 12.0 * fontScaleFactor;
        this["SpacingLarge"] = 16.0 * fontScaleFactor;
        this["SpacingExtraLarge"] = 20.0 * fontScaleFactor;
        this["SpacingXLarge"] = 24.0 * fontScaleFactor;

        // Padding resources - scale with font size
        this["PaddingSmall"] = 4.0 * fontScaleFactor;
        this["PaddingMedium"] = 8.0 * fontScaleFactor;
        this["PaddingStandard"] = 12.0 * fontScaleFactor;
        this["PaddingLarge"] = 16.0 * fontScaleFactor;
        this["PaddingExtraLarge"] = 20.0 * fontScaleFactor;
        this["PaddingXLarge"] = 24.0 * fontScaleFactor;
        this["PaddingXXLarge"] = 32.0 * fontScaleFactor;
        this["PaddingXXXLarge"] = 40.0 * fontScaleFactor;
        this["PaddingXXXXLarge"] = 48.0 * fontScaleFactor;

        // Margin resources - scale with font size
        this["MarginSmall"] = 4.0 * fontScaleFactor;
        this["MarginMedium"] = 8.0 * fontScaleFactor;
        this["MarginStandard"] = 12.0 * fontScaleFactor;
        this["MarginLarge"] = 16.0 * fontScaleFactor;
        this["MarginExtraLarge"] = 20.0 * fontScaleFactor;
        this["MarginXLarge"] = 24.0 * fontScaleFactor;

        // Minimum height resources - scale with font size for touch targets
        this["MinimumHeightStandard"] = 56.0 * fontScaleFactor;

        // Thickness resources for common padding/margin combinations - scale with font size
        double paddingLarge = 16.0 * fontScaleFactor;
        double paddingXLarge = 24.0 * fontScaleFactor;
        double paddingXXXLarge = 40.0 * fontScaleFactor;
        double paddingXXXXLarge = 48.0 * fontScaleFactor;
        double marginSmall = 4.0 * fontScaleFactor;
        double marginMedium = 8.0 * fontScaleFactor;
        double marginLarge = 16.0 * fontScaleFactor;
        double marginXLarge = 24.0 * fontScaleFactor;
        double marginExtraSmall = 2.0 * fontScaleFactor;
        double marginStandard = 12.0 * fontScaleFactor;
        double marginExtraLarge = 20.0 * fontScaleFactor;
        double paddingSmall = 4.0 * fontScaleFactor;
        double paddingMedium = 8.0 * fontScaleFactor;
        double paddingStandard = 12.0 * fontScaleFactor;
        double paddingExtraLarge = 20.0 * fontScaleFactor;
        double paddingXXLarge = 32.0 * fontScaleFactor;

        // Common padding Thickness values
        this["PaddingThicknessLarge"] = new Thickness(paddingLarge);
        this["PaddingThicknessXLarge"] = new Thickness(paddingXLarge);
        this["PaddingThicknessXXXLarge"] = new Thickness(paddingXXXLarge);
        this["PaddingThicknessXXXXLarge"] = new Thickness(paddingXXXXLarge);
        this["PaddingThicknessStandard"] = new Thickness(paddingStandard);
        this["PaddingThicknessExtraLarge"] = new Thickness(paddingExtraLarge);
        this["PaddingThicknessXXLarge"] = new Thickness(paddingXXLarge);
        
        // Specific padding combinations found in XAML files
        this["PaddingThickness24_40_24_24"] = new Thickness(paddingXLarge, paddingXXXLarge, paddingXLarge, paddingXLarge);
        this["PaddingThickness24_20"] = new Thickness(paddingXLarge, paddingExtraLarge);
        this["PaddingThickness24_20_24_48"] = new Thickness(paddingXLarge, paddingExtraLarge, paddingXLarge, paddingXXXXLarge);
        this["PaddingThickness24_20_24_32"] = new Thickness(paddingXLarge, paddingExtraLarge, paddingXLarge, paddingXXLarge);
        this["PaddingThickness16_16"] = new Thickness(paddingLarge, paddingLarge);
        this["PaddingThickness16_8"] = new Thickness(paddingLarge, paddingMedium);
        this["PaddingThickness16_8_16_0"] = new Thickness(paddingLarge, paddingMedium, paddingLarge, 0);
        this["PaddingThickness4_2"] = new Thickness(paddingSmall, marginExtraSmall);
        this["PaddingThickness16_0"] = new Thickness(paddingLarge, 0);
        this["PaddingThickness0"] = new Thickness(0);

        // Common margin Thickness values
        this["MarginThicknessSmall"] = new Thickness(marginSmall);
        this["MarginThicknessMedium"] = new Thickness(marginMedium);
        this["MarginThicknessLarge"] = new Thickness(marginLarge);
        this["MarginThicknessXLarge"] = new Thickness(marginXLarge);
        this["MarginThicknessStandard"] = new Thickness(marginStandard);
        this["MarginThicknessExtraLarge"] = new Thickness(marginExtraLarge);
        
        // Specific margin combinations found in XAML files
        this["MarginThickness0"] = new Thickness(0);
        this["MarginThickness0_8_0_8"] = new Thickness(0, marginMedium, 0, marginMedium);
        this["MarginThickness0_0_10_0"] = new Thickness(0, 0, 10.0 * fontScaleFactor, 0);
        this["MarginThickness16_0_0_0"] = new Thickness(marginLarge, 0, 0, 0); // Left margin only, scales with font size
        this["MarginThicknessPaddingLarge"] = new Thickness(paddingLarge, 0, paddingLarge, 0); // Left and right margins matching schedule item padding
        this["MarginThicknessPaddingLarge_Standard"] = new Thickness(paddingLarge, 0, paddingStandard, 0); // Left margin matches card padding (16pt), right margin uses standard spacing (12pt) for better list item spacing
        this["MarginThickness0_0_Standard_0"] = new Thickness(0, 0, paddingStandard, 0); // No left margin, right margin uses standard spacing (12pt) for list item spacing
        this["MarginThickness0_0_16_16"] = new Thickness(0, 0, marginLarge, marginLarge);
        this["MarginThickness0_0_0_16"] = new Thickness(0, 0, 0, marginLarge);
        this["MarginThickness0_8_0_0"] = new Thickness(0, marginMedium, 0, 0);
        this["MarginThickness10_5"] = new Thickness(10.0 * fontScaleFactor, 5.0 * fontScaleFactor);
        this["MarginThickness1"] = new Thickness(marginExtraSmall);
        this["MarginThickness0_5"] = new Thickness(1.0 * fontScaleFactor); // Half of MarginThickness1
        this["MarginThickness0_20_0_0"] = new Thickness(0, marginExtraLarge, 0, 0);
        this["MarginThickness0_24_0_0"] = new Thickness(0, marginXLarge, 0, 0);
        this["MarginThickness16_0"] = new Thickness(marginLarge, 0, marginLarge, 0);
        this["MarginThickness16_0_16_0"] = new Thickness(marginLarge, 0, marginLarge, 0);

        this["MarginThicknessModalClose"] = new Thickness(paddingLarge, paddingMedium, paddingLarge, paddingLarge);

        if (currentPlatform == DevicePlatform.iOS)
        {
            this["MarginThicknessNotificationFloatingButton"] = new Thickness(0, 0, marginXLarge, marginXLarge);
        }
        else
        {
            this["MarginThicknessNotificationFloatingButton"] = new Thickness(marginXLarge, 0, 0, marginXLarge);
        }

        this["MarginThicknessAlarmSettingsFloatingButton"] = new Thickness(0, 0, marginXLarge, marginXLarge);

        // Corner radius resources - scale slightly with font size for better proportions
        // Corner radius scales less aggressively than spacing (0.8x factor) to maintain visual balance
        double cornerRadiusScale = 0.8 + (fontScaleFactor - 1.0) * 0.5; // Scale between 0.8x and 1.3x
        this["CornerRadiusSmall"] = 4.0 * cornerRadiusScale;
        this["CornerRadiusMedium"] = 6.0 * cornerRadiusScale;
        this["CornerRadiusStandard"] = 8.0 * cornerRadiusScale;
        this["CornerRadiusLarge"] = 12.0 * cornerRadiusScale;
    }
}

