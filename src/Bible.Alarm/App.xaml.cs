#nullable enable
using Bible.Alarm.Services.UI;
using Microsoft.Maui.ApplicationModel;

namespace Bible.Alarm;

public partial class App : Application
{
    private readonly ExceptionHandlingService _exceptionHandlingService;
    private readonly WindowSetupService _windowSetupService;
    private readonly AppLifecycleService _appLifecycleService;
    private readonly AlarmModalService _alarmModalService;
    private readonly MessageHandlingService _messageHandlingService;

    public static bool IsInForeground { get; set; }

    public App(
        ExceptionHandlingService exceptionHandlingService,
        WindowSetupService windowSetupService,
        AppLifecycleService appLifecycleService,
        AlarmModalService alarmModalService,
        MessageHandlingService messageHandlingService)
    {
        _exceptionHandlingService = exceptionHandlingService;
        _windowSetupService = windowSetupService;
        _appLifecycleService = appLifecycleService;
        _alarmModalService = alarmModalService;
        _messageHandlingService = messageHandlingService;
        
        InitializeComponent();

        // Set up global exception handlers
        _exceptionHandlingService.SetupGlobalExceptionHandlers();

        // Register message handlers
        _messageHandlingService.RegisterMessageHandlers();
        
        // Subscribe to PlaybackState changes to reactively show/hide alarm modal
        _alarmModalService.SubscribeToPlaybackStateChanges();
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
