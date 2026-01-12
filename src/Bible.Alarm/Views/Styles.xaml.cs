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

        // Update resources with actual scaled values (includes OS accessibility font scale)
        // DynamicResource bindings will automatically pick up these changes
        this["StandardFontSize"] = service.StandardFontSize;
        this["HeaderFontSize"] = service.HeaderFontSize;
        this["ButtonFontSize"] = service.ButtonFontSize;
        this["SmallFontSize"] = service.SmallFontSize;
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
    }
}

