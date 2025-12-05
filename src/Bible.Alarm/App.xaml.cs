#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Services.UI;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Views;
using Microsoft.Maui.ApplicationModel;
using System.Linq;
using Serilog;

namespace Bible.Alarm;

public partial class App : Application
{
    private readonly ExceptionHandlingService _exceptionHandlingService;
    private readonly WindowSetupService _windowSetupService;
    private readonly AppLifecycleService _appLifecycleService;
    private readonly AlarmModalService _alarmModalService;
    private readonly MessageHandlingService _messageHandlingService;
    private readonly IFontService _fontService;

    public static bool IsInForeground { get; set; }

    public App(
        ExceptionHandlingService exceptionHandlingService,
        WindowSetupService windowSetupService,
        AppLifecycleService appLifecycleService,
        AlarmModalService alarmModalService,
        MessageHandlingService messageHandlingService,
        IFontService fontService)
    {
        _exceptionHandlingService = exceptionHandlingService;
        _windowSetupService = windowSetupService;
        _appLifecycleService = appLifecycleService;
        _alarmModalService = alarmModalService;
        _messageHandlingService = messageHandlingService;
        _fontService = fontService;
        
        InitializeComponent();

        // Set app to follow system theme preference (light/dark)
        // Follows system theme
        UserAppTheme = AppTheme.Unspecified;
        
        // Initialize theme-aware color resources at application level
        // These are set programmatically as Color values (not AppThemeBinding)
        // Pages use {DynamicResource} to reference these keys for automatic theme updates
        InitializeThemeAwareColorResources();
        
        // Listen to theme changes to update resources
        RequestedThemeChanged += OnRequestedThemeChanged;

        // Set font size resources for hot reload compatibility
        // Using DynamicResource allows hot reload to work without restarting
        SetFontSizeResources();
        
        // Subscribe to font size changes for display changes (rotation/resize)
        // PropertyChanged with empty string only fires on actual display changes (via Recalculate())
        // This does NOT fire during Hot Reload, so it's safe to update resources here
        if (_fontService is System.ComponentModel.INotifyPropertyChanged notifyPropertyChanged)
        {
            notifyPropertyChanged.PropertyChanged += (sender, e) =>
            {
                // Empty PropertyName means all properties changed = display change (rotation/resize)
                // This only happens when FontService.Recalculate() is called, which only happens
                // on DeviceDisplay.MainDisplayInfoChanged, NOT during Hot Reload
                if (string.IsNullOrEmpty(e.PropertyName))
                {
                    SetFontSizeResources();
                }
            };
        }

        // Set up global exception handlers
        _exceptionHandlingService.SetupGlobalExceptionHandlers();

        // Register message handlers
        _messageHandlingService.RegisterMessageHandlers();
        
        // Subscribe to PlaybackState changes to reactively show/hide alarm modal
        _alarmModalService.SubscribeToPlaybackStateChanges();
    }


    private void SetFontSizeResources()
    {
        try
        {
            // Set font size resources as DynamicResource values for hot reload compatibility
            // Update the merged Styles dictionary where the resources are actually defined
            var stylesDict = Resources.MergedDictionaries.OfType<Views.Styles>().FirstOrDefault();
            if (stylesDict != null)
            {
                stylesDict["StandardFontSize"] = _fontService.StandardFontSize;
                stylesDict["HeaderFontSize"] = _fontService.HeaderFontSize;
                stylesDict["ButtonFontSize"] = _fontService.ButtonFontSize;
                stylesDict["SmallFontSize"] = _fontService.SmallFontSize;
                stylesDict["MediumFontSize"] = _fontService.MediumFontSize;
                stylesDict["LargeFontSize"] = _fontService.LargeFontSize;
                stylesDict["TitleFontSize"] = _fontService.TitleFontSize;
                stylesDict["AlarmTimeFontSize"] = _fontService.AlarmTimeFontSize;
                stylesDict["AlarmMeridianFontSize"] = _fontService.AlarmMeridianFontSize;
                stylesDict["AlarmBellIconFontSize"] = _fontService.AlarmBellIconFontSize;
            }
            
            // Also update in main Resources dictionary for any direct lookups
            Resources["StandardFontSize"] = _fontService.StandardFontSize;
            Resources["HeaderFontSize"] = _fontService.HeaderFontSize;
            Resources["ButtonFontSize"] = _fontService.ButtonFontSize;
            Resources["SmallFontSize"] = _fontService.SmallFontSize;
            Resources["MediumFontSize"] = _fontService.MediumFontSize;
            Resources["LargeFontSize"] = _fontService.LargeFontSize;
            Resources["TitleFontSize"] = _fontService.TitleFontSize;
            Resources["AlarmTimeFontSize"] = _fontService.AlarmTimeFontSize;
            Resources["AlarmMeridianFontSize"] = _fontService.AlarmMeridianFontSize;
            Resources["AlarmBellIconFontSize"] = _fontService.AlarmBellIconFontSize;
        }
        catch (Exception ex)
        {
            // Log errors during resource updates (may occur during Hot Reload)
            // DynamicResource bindings will handle updates automatically
            Log.Logger.Debug(ex, "Error updating font size resources, continuing without update");
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return _windowSetupService.CreateWindow(activationState);
    }

    protected override void OnStart()
    {
        base.OnStart();
        _appLifecycleService.OnStart();
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        AppLifecycleService.OnSleep();
    }

    protected override void OnResume()
    {
        base.OnResume();
        _appLifecycleService.OnResume();
    }

    /// <summary>
    /// Initializes theme-aware color resources at the Application level.
    /// These resources are available globally via DynamicResource for automatic theme updates.
    /// </summary>
    private void InitializeThemeAwareColorResources()
    {
        UpdateThemeAwareColorResources();
    }

    /// <summary>
    /// Updates theme-aware color resources based on the current theme.
    /// When resources are updated, all DynamicResource bindings automatically refresh.
    /// </summary>
    private void UpdateThemeAwareColorResources()
    {
        try
        {
            var theme = RequestedTheme;
            var isDark = theme == AppTheme.Dark;
            
            // Set theme-aware colors at Application level (for DynamicResource to work)
            // All colors now use centralized ThemeColors constants
            Resources["BackgroundColor"] = ThemeColors.Background.Get(theme);
            Resources["PageBackgroundColor"] = ThemeColors.PageBackground.Get(theme);
            Resources["CardBackgroundColor"] = ThemeColors.CardBackground.Get(theme);
            Resources["TextPrimaryColor"] = ThemeColors.TextPrimary.Get(theme);
            Resources["TextSecondaryColor"] = ThemeColors.TextSecondary.Get(theme);
            Resources["DividerColor"] = ThemeColors.Divider.Get(theme);
            Resources["ControlBackgroundColor"] = ThemeColors.ControlBackground.Get(theme);
            Resources["ProgressBarBackgroundColor"] = ThemeColors.ProgressBarBackground.Get(theme);
            Resources["PrimaryTextColor"] = ThemeColors.PrimaryText.Get(theme);
            Resources["SelectedItemBackgroundColor"] = ThemeColors.SelectedItemBackground.Get(theme);
            Resources["DisabledTextColor"] = ThemeColors.DisabledText.Get(theme);
        }
        catch (Exception ex)
        {
            Log.Logger.Debug(ex, "Error updating theme-aware color resources");
        }
    }

    /// <summary>
    /// Handles theme changes and updates all theme-aware color resources.
    /// DynamicResource bindings will automatically refresh when resources are updated.
    /// Navigation bar colors are updated automatically by WindowSetupService.
    /// </summary>
    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        // Update resources first - this triggers DynamicResource updates
        UpdateThemeAwareColorResources();
        
        // Navigation bar colors are updated automatically by WindowSetupService
        // which listens to RequestedThemeChanged
        WindowSetupService.UpdateNavigationBarColors();
    }

}
