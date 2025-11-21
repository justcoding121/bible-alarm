using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media;
using Bible.Alarm.ViewModels;
using Bible.Alarm.Views;
using Bible.Alarm.Views.General;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace Bible.Alarm;

public partial class App : Application,
    IRecipient<ShowAlarmModalMessage>,
    IRecipient<HideAlarmModalMessage>,
    IRecipient<ShowToastMessage>,
    IRecipient<ClearToastsMessage>,
    IRecipient<InitializedMessage>
{
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly INavigationService _navigationService;

    public static bool IsInForeground { get; set; }

    public App(ILogger logger, IServiceProvider serviceProvider, INavigationService navigationService)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _navigationService = navigationService;
        InitializeComponent();

        WeakReferenceMessenger.Default.Register<InitializedMessage>(this);


        WeakReferenceMessenger.Default.Register<ShowAlarmModalMessage>(this);
        WeakReferenceMessenger.Default.Register<HideAlarmModalMessage>(this);
        WeakReferenceMessenger.Default.Register<ShowToastMessage>(this);
        WeakReferenceMessenger.Default.Register<ClearToastsMessage>(this);
    }

    protected override Window CreateWindow(IActivationState activationState)
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

                var playbackService = _serviceProvider.GetRequiredService<IPlaybackService>();

                if (playbackService.IsPreparingOrPlaying) WeakReferenceMessenger.Default.Send(new ShowAlarmModalMessage(null));

                await Task.Delay(1000);

                var mediaIndexService = _serviceProvider.GetRequiredService<MediaIndexService>();
                await mediaIndexService.UpdateIndexIfAvailable();
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
                var playbackService = _serviceProvider.GetRequiredService<IPlaybackService>();

                if (playbackService.IsPreparingOrPlaying) 
                    WeakReferenceMessenger.Default.Send(new ShowAlarmModalMessage(null));

                await Task.Delay(1000);

                var mediaIndexService = _serviceProvider.GetRequiredService<MediaIndexService>();
                await mediaIndexService.UpdateIndexIfAvailable();
            }
            catch (Exception e)
            {
                _logger.Error(e, "An error happened inside OnResume task.");
            }
        });
    }

    public void Receive(ShowAlarmModalMessage message)
    {
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                _logger.Information("ShowAlarmModalMessage received");
                await _navigationService.OpenAlarmModalAsync();
                _logger.Information("AlarmModal opened");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error showing AlarmModal");
            }
        });
    }

    public void Receive(HideAlarmModalMessage message)
    {
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await _navigationService.PopModalAsync();
        });
    }


    public void Receive(ShowToastMessage message)
    {
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            using var toastService = _serviceProvider.GetRequiredService<IToastService>();
            await toastService.ShowMessage(message.Value as string);
        });
    }

    public void Receive(ClearToastsMessage message)
    {
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            using var toastService = _serviceProvider.GetRequiredService<IToastService>();
            await toastService.Clear();
        });
    }

    public void Receive(InitializedMessage message)
    {
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                await _navigationService.NavigateToHomeAsync();

                var homePage = _serviceProvider.GetRequiredService<Home>();
                if (homePage.BindingContext is HomeViewModel homeViewModel)
                {
                    await homeViewModel.InitializeAsync();
                }

      
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _logger.Information("Starting service initialization...");
                        
                        var playbackService = _serviceProvider.GetRequiredService<IPlaybackService>();
                        if (playbackService.IsPreparingOrPlaying) WeakReferenceMessenger.Default.Send(new ShowAlarmModalMessage(null));
                        
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