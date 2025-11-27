#nullable enable
using Bible.Alarm.Services.UI;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Views;
using Microsoft.Maui.ApplicationModel;
using System.Linq;

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

        // Set font size resources for hot reload compatibility
        // Using StaticResource instead of x:Static allows hot reload to work
        SetFontSizeResources();

        // Set up global exception handlers
        _exceptionHandlingService.SetupGlobalExceptionHandlers();

        // Register message handlers
        _messageHandlingService.RegisterMessageHandlers();
        
        // Subscribe to PlaybackState changes to reactively show/hide alarm modal
        _alarmModalService.SubscribeToPlaybackStateChanges();
    }

    private void SetFontSizeResources()
    {
        // Set font size resources as StaticResource values for hot reload compatibility
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
}
