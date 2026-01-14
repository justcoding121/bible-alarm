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
        // Base StandardFontSize is 12.0, so scale factor = current / base
        const double BaseStandardFontSize = 12.0;
        double fontScaleFactor = service.StandardFontSize / BaseStandardFontSize;

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

        // Spacing resources - scale with font size for fluent UI
        // Base values are defined in XAML, scaled here
        this["SpacingExtraSmall"] = 2.0 * fontScaleFactor;
        this["SpacingSmall"] = 4.0 * fontScaleFactor;
        this["SpacingMedium"] = 8.0 * fontScaleFactor;
        this["SpacingStandard"] = 12.0 * fontScaleFactor;
        this["SpacingLarge"] = 16.0 * fontScaleFactor;
        this["SpacingExtraLarge"] = 20.0 * fontScaleFactor;

        // Padding resources - scale with font size
        this["PaddingSmall"] = 4.0 * fontScaleFactor;
        this["PaddingMedium"] = 8.0 * fontScaleFactor;
        this["PaddingStandard"] = 12.0 * fontScaleFactor;
        this["PaddingLarge"] = 16.0 * fontScaleFactor;
        this["PaddingExtraLarge"] = 20.0 * fontScaleFactor;

        // Corner radius resources - scale slightly with font size for better proportions
        // Corner radius scales less aggressively than spacing (0.8x factor) to maintain visual balance
        double cornerRadiusScale = 0.8 + (fontScaleFactor - 1.0) * 0.5; // Scale between 0.8x and 1.3x
        this["CornerRadiusSmall"] = 4.0 * cornerRadiusScale;
        this["CornerRadiusMedium"] = 6.0 * cornerRadiusScale;
        this["CornerRadiusStandard"] = 8.0 * cornerRadiusScale;
        this["CornerRadiusLarge"] = 12.0 * cornerRadiusScale;
    }
}

