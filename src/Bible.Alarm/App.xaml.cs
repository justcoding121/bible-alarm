using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.UI;
using Bible.Alarm.ViewModels;
using Serilog;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm;

public partial class App
{
    private readonly ILogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public static bool IsInForeground { get; set; } = false;

    public App(ILogger logger, IServiceScopeFactory scopeFactory)
    {
        System.Diagnostics.Debug.WriteLine("App constructor called!");
        _logger = logger;
        _scopeFactory = scopeFactory;
        Init();
    }

    private INavigationService _navigationService;

    private void Init()
    {
        InitializeComponent();

        var navigationPage = new NavigationPage();
        var taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
        
        // Create and store the navigation service for later use
        using var scope = _scopeFactory.CreateScope();
        _navigationService = new NavigationService(
            scope.ServiceProvider.GetRequiredService<ILogger>(), 
            navigationPage.Navigation,
            _scopeFactory);

        // Use the MAUI approach for setting the main page
        MainPage = navigationPage;

        navigationPage.SetValue(NavigationPage.BarBackgroundColorProperty, Colors.SlateBlue);
        navigationPage.SetValue(NavigationPage.BarTextColorProperty, Colors.White);

        if (DeviceInfo.Platform != DevicePlatform.Android) HomePageSetter().Wait();

        Task.Delay(100).ContinueWith(async (a) =>
            {
                if (DeviceInfo.Platform == DevicePlatform.Android) await HomePageSetter();
            }, taskScheduler)
            .ContinueWith(x =>
            {
                using var scope = _scopeFactory.CreateScope();
                var playbackService = scope.ServiceProvider.GetRequiredService<IPlaybackService>();

                if (playbackService.IsPrepared) Messenger<object>.Publish(MvvmMessages.ShowAlarmModal);
            });
        return;

        async Task HomePageSetter()
        {
            using var scope = _scopeFactory.CreateScope();
            var homePage = new Home { BindingContext = scope.ServiceProvider.GetRequiredService<HomeViewModel>() };
            await navigationPage.Navigation.PushAsync(homePage);
        }
    }

    protected override void OnStart()
    {
        IsInForeground = true;

        base.OnStart();
        Task.Run(async () =>
        {
            try
            {
                // Handle when your app starts  
                await _navigationService.NavigateToHome();

                using var scope = _scopeFactory.CreateScope();
                var playbackService = scope.ServiceProvider.GetRequiredService<IPlaybackService>();

                if (playbackService.IsPrepared) Messenger<object>.Publish(MvvmMessages.ShowAlarmModal);

                await Task.Delay(1000);

                using var mediaIndexService = scope.ServiceProvider.GetRequiredService<MediaIndexService>();
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
                using var scope = _scopeFactory.CreateScope();
                var playbackService = scope.ServiceProvider.GetRequiredService<IPlaybackService>();
                // Handle when your app resumes
                if (playbackService.IsPrepared) Messenger<object>.Publish(MvvmMessages.ShowAlarmModal);

                await Task.Delay(1000);

                using var mediaIndexService = scope.ServiceProvider.GetRequiredService<MediaIndexService>();
                await mediaIndexService.UpdateIndexIfAvailable();
            }
            catch (Exception e)
            {
                _logger.Error(e, "An error happened inside OnResume task.");
            }
        });
    }
}