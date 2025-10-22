using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.UI;
using Bible.Alarm.ViewModels;
using Serilog;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm;

public partial class App : Application
{
    private readonly Serilog.ILogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public static bool IsInForeground { get; set; } = false;

    public App(Serilog.ILogger logger, IServiceScopeFactory scopeFactory)
    {
        System.Diagnostics.Debug.WriteLine("App constructor called!");
        _logger = logger;
        _scopeFactory = scopeFactory;
        InitializeComponent();
    }

    private INavigationService _navigationService;

    protected override Window CreateWindow(IActivationState? activationState)
    {
        System.Diagnostics.Debug.WriteLine("CreateWindow called!");
        
        // Initialize platform-specific bootstrap helper after ServiceProviderManager is available
        InitializePlatformBootstrap();
        
        // Create a simple window with a basic page for now
        var window = new Window(new ContentPage
        {
            Title = "Bible Alarm",
            Content = new Label 
            { 
                Text = "Bible Alarm is starting...", 
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        });
        
        // Initialize services in background
        Task.Run(async () =>
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Starting service initialization...");
                
                // Create and store the navigation service for later use
                using var scope = _scopeFactory.CreateScope();
                _navigationService = new NavigationService(
                    scope.ServiceProvider.GetRequiredService<ILogger>(), 
                    null, // Will be set when we have proper navigation
                    _scopeFactory);
                System.Diagnostics.Debug.WriteLine("NavigationService created!");

                // Initialize the home page
                await InitializeHomePage(window);
                
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
        
        return window;
    }
    
    private async Task InitializeHomePage(Window window)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("InitializeHomePage called!");
            using var scope = _scopeFactory.CreateScope();
            var homePage = new Home { BindingContext = scope.ServiceProvider.GetRequiredService<HomeViewModel>() };
            
            // Set the main page to the home page
            window.Page = homePage;
            System.Diagnostics.Debug.WriteLine("Home page set!");
            
            // Add a small delay to ensure proper initialization
            await Task.Delay(100);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in InitializeHomePage: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private void InitializePlatformBootstrap()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Initializing platform-specific bootstrap...");
            
            // Initialize platform-specific bootstrap helper after ServiceProviderManager is available
            #if WINDOWS
            using var scope = _scopeFactory.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<Serilog.ILogger>();
            Bible.Alarm.Services.Windows.Helpers.BootstrapHelper.Initialize(logger);
            System.Diagnostics.Debug.WriteLine("Windows bootstrap helper initialized!");
            #elif ANDROID
            using var scope = _scopeFactory.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<Serilog.ILogger>();
            // Android bootstrap is handled in MainActivity, but we also need to call it here for database initialization
            _ = Task.Run(async () => await Bible.Alarm.Services.Droid.Helpers.BootstrapHelper.VerifyServices());
            System.Diagnostics.Debug.WriteLine("Android bootstrap helper initialized!");
            #elif IOS
            using var scope = _scopeFactory.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<Serilog.ILogger>();
            Bible.Alarm.Services.iOS.Helpers.BootstrapHelper.Initialize(logger);
            System.Diagnostics.Debug.WriteLine("iOS bootstrap helper initialized!");
            #endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing platform bootstrap: {ex.Message}");
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