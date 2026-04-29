#nullable enable
using System.ComponentModel;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.UI;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Views;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace Bible.Alarm;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class App : Application
{
    private readonly IExceptionHandlingService exceptionHandlingService;
    private readonly IWindowSetupService windowSetupService;
    private readonly IAppLifecycleService appLifecycleService;
    private readonly IMessageHandlingService messageHandlingService;
    private readonly IFontService fontService;

    private static volatile bool isInForeground;

    public static bool IsInForeground
    {
        get => isInForeground;
        set => isInForeground = value;
    }

    public App(
        IExceptionHandlingService exceptionHandlingService,
        IWindowSetupService windowSetupService,
        IAppLifecycleService appLifecycleService,
        IMessageHandlingService messageHandlingService,
        IFontService fontService)
    {
        this.exceptionHandlingService = exceptionHandlingService;
        this.windowSetupService = windowSetupService;
        this.appLifecycleService = appLifecycleService;
        this.messageHandlingService = messageHandlingService;
        this.fontService = fontService;

        InitializeComponent();

        UserAppTheme = AppTheme.Unspecified;
        InitializeThemeAwareColorResources();
        RequestedThemeChanged += OnRequestedThemeChanged;
        SetFontSizeResources();

        if (this.fontService is INotifyPropertyChanged notifyPropertyChanged)
        {
            notifyPropertyChanged.PropertyChanged += (_, e) =>
            {
                if (string.IsNullOrEmpty(e.PropertyName))
                {
                    SetFontSizeResources();
                }
            };
        }

        this.exceptionHandlingService.SetupGlobalExceptionHandlers();
        this.messageHandlingService.RegisterMessageHandlers();
    }

    private void SetFontSizeResources()
    {
        try
        {
            Styles? stylesDict = null;
            foreach (var d in Resources.MergedDictionaries)
            {
                if (d is Styles s)
                {
                    stylesDict = s;
                    break;
                }
            }
            if (stylesDict != null)
            {
                stylesDict["StandardFontSize"] = fontService.StandardFontSize;
                stylesDict["HeaderFontSize"] = fontService.HeaderFontSize;
                stylesDict["ButtonFontSize"] = fontService.ButtonFontSize;
                stylesDict["SmallFontSize"] = fontService.SmallFontSize;
                stylesDict["MediumFontSize"] = fontService.MediumFontSize;
                stylesDict["LargeFontSize"] = fontService.LargeFontSize;
                stylesDict["TitleFontSize"] = fontService.TitleFontSize;
                stylesDict["AlarmTimeFontSize"] = fontService.AlarmTimeFontSize;
                stylesDict["AlarmMeridianFontSize"] = fontService.AlarmMeridianFontSize;
                stylesDict["AlarmBellIconFontSize"] = fontService.AlarmBellIconFontSize;
            }

            Resources["StandardFontSize"] = fontService.StandardFontSize;
            Resources["HeaderFontSize"] = fontService.HeaderFontSize;
            Resources["ButtonFontSize"] = fontService.ButtonFontSize;
            Resources["SmallFontSize"] = fontService.SmallFontSize;
            Resources["MediumFontSize"] = fontService.MediumFontSize;
            Resources["LargeFontSize"] = fontService.LargeFontSize;
            Resources["TitleFontSize"] = fontService.TitleFontSize;
            Resources["AlarmTimeFontSize"] = fontService.AlarmTimeFontSize;
            Resources["AlarmMeridianFontSize"] = fontService.AlarmMeridianFontSize;
            Resources["AlarmBellIconFontSize"] = fontService.AlarmBellIconFontSize;
        }
        catch (Exception ex)
        {
            Log.Logger.Debug(ex, "Error updating font size resources, continuing without update");
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return windowSetupService.CreateWindow(activationState);
    }

    protected override void OnStart()
    {
        base.OnStart();
        appLifecycleService.OnStart();
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        AppLifecycleService.OnSleep();
    }

    protected override void OnResume()
    {
        base.OnResume();
        // Re-apply status bar (e.g. on iOS) when app returns from background so it stays correct.
        WindowSetupService.UpdateNavigationBarColors();
        appLifecycleService.OnResume();
    }

    private void InitializeThemeAwareColorResources() => UpdateThemeAwareColorResources();

    private void UpdateThemeAwareColorResources()
    {
        try
        {
            var theme = RequestedTheme;

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

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        UpdateThemeAwareColorResources();
        WindowSetupService.UpdateNavigationBarColors();
        // Notify ViewModels to update theme-aware bindings (e.g., day button colors)
        // Use BeginInvokeOnMainThread to ensure resources are fully propagated before ViewModels update
        MainThread.BeginInvokeOnMainThread(() =>
        {
            WeakReferenceMessenger.Default.Send(new ThemeChangedMessage());
        });
    }
}
