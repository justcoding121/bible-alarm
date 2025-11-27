#nullable enable
using Bible.Alarm.Services.UI;

namespace Bible.Alarm.Views;

public partial class Styles : ResourceDictionary
{
    public Styles()
    {
        InitializeComponent();
        
        // Update font size resources with actual scaled values from FontService
        // This must be done after InitializeComponent() but the resources are already
        // available for StaticResource bindings to resolve correctly
        UpdateFontSizeResources();
    }
    
    private void UpdateFontSizeResources()
    {
        // Get FontService from FontServiceHelper (initialized in MauiProgram)
        var fontService = FontServiceHelper.GetFontService();
        
        // Update resources with actual scaled values
        this["StandardFontSize"] = fontService.StandardFontSize;
        this["HeaderFontSize"] = fontService.HeaderFontSize;
        this["SmallFontSize"] = fontService.SmallFontSize;
        this["MediumFontSize"] = fontService.MediumFontSize;
        this["LargeFontSize"] = fontService.LargeFontSize;
        this["TitleFontSize"] = fontService.TitleFontSize;
        this["AlarmTimeFontSize"] = fontService.AlarmTimeFontSize;
        this["AlarmMeridianFontSize"] = fontService.AlarmMeridianFontSize;
        this["AlarmBellIconFontSize"] = fontService.AlarmBellIconFontSize;
    }
}

