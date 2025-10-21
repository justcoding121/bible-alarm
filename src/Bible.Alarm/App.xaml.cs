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
        System.Diagnostics.Debug.WriteLine("Init() called!");
        try
        {
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("InitializeComponent() completed!");

            var navigationPage = new NavigationPage();
            System.Diagnostics.Debug.WriteLine("NavigationPage created!");
            
            // Use the MAUI approach for setting the main page
            MainPage = navigationPage;
            System.Diagnostics.Debug.WriteLine("MainPage set!");

            navigationPage.SetValue(NavigationPage.BarBackgroundColorProperty, Colors.SlateBlue);
            navigationPage.SetValue(NavigationPage.BarTextColorProperty, Colors.White);
            System.Diagnostics.Debug.WriteLine("Navigation page properties set!");

            // Try to create a simple home page first
            var simpleHomePage = new ContentPage
            {
                Title = "Bible Alarm",
                Content = new Label 
                { 
                    Text = "Bible Alarm is starting...", 
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };
            
            navigationPage.Navigation.PushAsync(simpleHomePage);
            System.Diagnostics.Debug.WriteLine("Simple home page pushed!");

            // Try to initialize services in background
            Task.Run(async () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("Starting service initialization...");
                    var taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
                    
                    // Create and store the navigation service for later use
                    using var scope = _scopeFactory.CreateScope();
                    _navigationService = new NavigationService(
                        scope.ServiceProvider.GetRequiredService<ILogger>(), 
                        navigationPage.Navigation,
                        _scopeFactory);
                    System.Diagnostics.Debug.WriteLine("NavigationService created!");

                    if (DeviceInfo.Platform != DevicePlatform.Android) await HomePageSetter();
                    else
                    {
                        await Task.Delay(100);
                        await HomePageSetter();
                    }

                    var playbackService = scope.ServiceProvider.GetRequiredService<IPlaybackService>();
                    if (playbackService.IsPrepared) Messenger<object>.Publish(MvvmMessages.ShowAlarmModal);
                    
                    System.Diagnostics.Debug.WriteLine("Service initialization completed!");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in service initialization: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            });

            async Task HomePageSetter()
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("HomePageSetter called!");
                    using var scope = _scopeFactory.CreateScope();
                    var homePage = new Home { BindingContext = scope.ServiceProvider.GetRequiredService<HomeViewModel>() };
                    await navigationPage.Navigation.PushAsync(homePage);
                    System.Diagnostics.Debug.WriteLine("Home page pushed!");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in HomePageSetter: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in Init(): {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
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