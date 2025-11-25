#nullable enable
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media;
using Bible.Alarm.ViewModels;
using Bible.Alarm.Views;
using Bible.Alarm.Views.General;
using Bible.Alarm.Stores;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;
using Bible.Alarm.Common;

namespace Bible.Alarm;

public partial class App : Application,
    IRecipient<ShowToastMessage>,
    IRecipient<InitializedMessage>
{
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly INavigationService _navigationService;
    private readonly IState<PlaybackState> _playbackState;
    private bool _isModalOpen;

    public static bool IsInForeground { get; set; }

    public App(ILogger logger, IServiceProvider serviceProvider, INavigationService navigationService, IState<PlaybackState> playbackState)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _navigationService = navigationService;
        _playbackState = playbackState;
        InitializeComponent();

        // Set up global exception handlers
        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException += UnobservedTaskExceptionHandler;

        // Use ObservableMessenger for InitializedMessage so message is not lost if sent before registration
        ObservableMessenger.InitializationMessenger.Register<InitializedMessage>(this);

        WeakReferenceMessenger.Default.Register<ShowToastMessage>(this);
        
        // Subscribe to PlaybackState changes to reactively show/hide alarm modal
        // Modal visibility is driven entirely by PlaybackState.IsPreparingOrPlaying
        _playbackState.StateChanged += OnPlaybackStateChanged;
    }

    private void UnobservedTaskExceptionHandler(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger.Error(e.Exception, "Unobserved task exception.");
    }

    private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        _logger.Error("Unhandled exception.", e.SerializeObject());
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var bootstrapPage = _serviceProvider.GetRequiredService<BootstrapPage>();
        var navigationPage = new NavigationPage(bootstrapPage)
        {
            BarBackgroundColor = Colors.Transparent,
            BarTextColor = Colors.White
        };

        NavigationPage.SetHasNavigationBar(bootstrapPage, false);

        var window = new Window(navigationPage);

#if WINDOWS
        // Set window size preferences (matching stable code)
        window.Width = 400;
        window.Height = 700;
        window.MinimumWidth = 400;
        window.MinimumHeight = 700;
#endif

        return window;
    }

    protected override void OnStart()
    {
        IsInForeground = true;

        base.OnStart();

        MauiProgram.InitializePlatformBootstrap(_serviceProvider, isForeground: true);

        Task.Run(async () =>
        {
            try
            {

                await Task.Delay(1000);

                var mediaIndexService = _serviceProvider.GetRequiredService<MediaIndexService>();
                await mediaIndexService.UpdateIndexIfAvailable();

#if WINDOWS
                // Reschedule any enabled alarms that may have fired while app was closed
                // This is a fallback for WinUI 3 which doesn't have background tasks
                var schedulerService = _serviceProvider.GetRequiredService<Services.Scheduler.Interfaces.ISchedulerService>();
                await schedulerService.HandleAsync();
#endif
            }
            catch (Exception e)
            {
                _logger.Error(e, "An error happened inside OnStart task.");
            }
        });
    }

    protected override void OnSleep()
    {
        IsInForeground = false;

        base.OnSleep();
    }

    protected override void OnResume()
    {
        IsInForeground = true;

        base.OnResume();
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1000);

                var mediaIndexService = _serviceProvider.GetRequiredService<MediaIndexService>();
                await mediaIndexService.UpdateIndexIfAvailable();

#if WINDOWS
                // Reschedule any enabled alarms that may have fired while app was in background
                // This is a fallback for WinUI 3 which doesn't have background tasks
                var schedulerService = _serviceProvider.GetRequiredService<Services.Scheduler.Interfaces.ISchedulerService>();
                await schedulerService.HandleAsync();
#endif
            }
            catch (Exception e)
            {
                _logger.Error(e, "An error happened inside OnResume task.");
            }
        });
    }

    public void Receive(ShowToastMessage message)
    {
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            using var toastService = _serviceProvider.GetRequiredService<IToastService>();
            await toastService.ShowMessage(message.Value);
        });
    }

    private void OnPlaybackStateChanged(object? sender, EventArgs e)
    {
        var shouldShowModal = _playbackState.Value.IsPreparingOrPlaying;
        
        if (shouldShowModal && !_isModalOpen)
        {
            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    _logger.Information("PlaybackState changed - showing AlarmModal (IsPreparingOrPlaying: true)");
                    await _navigationService.OpenAlarmModalAsync();
                    _isModalOpen = true;
                    _logger.Information("AlarmModal opened");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error showing AlarmModal");
                }
            });
        }
        else if (!shouldShowModal && _isModalOpen)
        {
            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    _logger.Information("PlaybackState changed - hiding AlarmModal (IsPreparingOrPlaying: false)");
                    await _navigationService.PopModalAsync();
                    _isModalOpen = false;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error hiding AlarmModal");
                }
            });
        }
    }

    public void Receive(InitializedMessage message)
    {
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                // Create Home page and initialize ViewModel before navigating
                var homePage = _serviceProvider.GetRequiredService<Home>();
                if (homePage.BindingContext is HomeViewModel homeViewModel)
                {
                    // Wait for initialization to complete before navigating
                    await homeViewModel.InitializeAsync();
                }

                // Navigate to the initialized home page
                await _navigationService.NavigateToHomeAsync();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        _logger.Information("Starting service initialization...");
                        
                        // Modal visibility is now handled reactively via PlaybackState subscription
                        // No need to manually send ShowAlarmModalMessage here
                        
                        await Task.Delay(100); 
                        
                        _logger.Information("Service initialization completed!");

                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error in initialization");
                    }
                });
            }
            catch (Exception e)
            {
                _logger.Error(e, "An error happened while showing HomePage after initialization.");
            }
        });
    }
}