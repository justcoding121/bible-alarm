using CommunityToolkit.Mvvm.Messaging;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Views;
using Bible.Alarm.ViewModels;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Views.Shared;

#nullable enable

namespace Bible.Alarm;

public partial class App : Application,
    IRecipient<ShowAlarmModalMessage>,
    IRecipient<HideAlarmModalMessage>,
    IRecipient<ShowMediaProgressModalMessage>,
    IRecipient<HideMediaProgressModalMessage>,
    IRecipient<ShowToastMessage>,
    IRecipient<ClearToastsMessage>
{
    private readonly Serilog.ILogger _logger;
    private readonly IServiceProvider _serviceProvider;

    public static bool IsInForeground { get; set; } = false;
    private INavigation? _navigation;

    public App(Serilog.ILogger logger, IServiceProvider serviceProvider)
    {
        System.Diagnostics.Debug.WriteLine("App constructor called!");
        _logger = logger;
        _serviceProvider = serviceProvider;
        InitializeComponent();

        // Register for modal messages
        WeakReferenceMessenger.Default.Register<ShowAlarmModalMessage>(this);
        WeakReferenceMessenger.Default.Register<HideAlarmModalMessage>(this);
        WeakReferenceMessenger.Default.Register<ShowMediaProgressModalMessage>(this);
        WeakReferenceMessenger.Default.Register<HideMediaProgressModalMessage>(this);
        WeakReferenceMessenger.Default.Register<ShowToastMessage>(this);
        WeakReferenceMessenger.Default.Register<ClearToastsMessage>(this);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        System.Diagnostics.Debug.WriteLine("CreateWindow called!");
        
        var homePage = _serviceProvider.GetRequiredService<Home>();
        var navigationPage = new NavigationPage(homePage)
        {
            BarBackgroundColor = Colors.Transparent,
            BarTextColor = Colors.White
#if IOS
            , PrefersLargeTitles = true
#endif
        };

        // Hide the nav bar on HomePage only
        NavigationPage.SetHasNavigationBar(homePage, false);

        _navigation = navigationPage.Navigation;
        
        var window = new Window(navigationPage);
        
        // Initialize services in background
        Task.Run(async () =>
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Starting service initialization...");
                
                var playbackService = _serviceProvider.GetRequiredService<IPlaybackService>();
                if (playbackService.IsPrepared) WeakReferenceMessenger.Default.Send(new ShowAlarmModalMessage(null));
                
                System.Diagnostics.Debug.WriteLine("Service initialization completed!");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in initialization: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        });
        
        return window;
    }

    protected override void OnStart()
    {
        IsInForeground = true;

        base.OnStart();
        Task.Run(async () =>
        {
            try
            {
                // Navigate to home
                if (_navigation != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        while (_navigation.ModalStack.Count > 0)
                            await _navigation.PopModalAsync();

                        while (_navigation.NavigationStack.Count > 1)
                            await _navigation.PopAsync();
                    });
                }

                var playbackService = _serviceProvider.GetRequiredService<IPlaybackService>();

                if (playbackService.IsPrepared) WeakReferenceMessenger.Default.Send(new ShowAlarmModalMessage(null));

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
                // Handle when your app resumes
                if (playbackService.IsPrepared) WeakReferenceMessenger.Default.Send(new ShowAlarmModalMessage(null));

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

    // Modal message handlers
    public void Receive(ShowAlarmModalMessage message)
    {
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (_navigation == null) return;

            // Prevent showing alarm modal when playback is not active
            var playbackService = _serviceProvider.GetRequiredService<IPlaybackService>();
            if (!playbackService.IsPlaying) return;

            if (_navigation.ModalStack.LastOrDefault()?.GetType() == typeof(AlarmModal)) return;

            var vm = _serviceProvider.GetRequiredService<AlarmViewModal>();
            var modal = _serviceProvider.GetRequiredService<AlarmModal>();
            modal.BindingContext = vm;
            await _navigation.PushModalAsync(modal);
        });
    }

    public void Receive(HideAlarmModalMessage message)
    {
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (_navigation == null) return;
            if (_navigation.ModalStack.Count > 0)
            {
                var modal = await _navigation.PopModalAsync();
                if (modal.BindingContext is IDisposable disposable) disposable.Dispose();
            }
        });
    }

    public void Receive(ShowMediaProgressModalMessage message)
    {
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (_navigation == null) return;

            if (_navigation.ModalStack.LastOrDefault()?.GetType() == typeof(MediaProgressModal)) return;

            var vm = _serviceProvider.GetRequiredService<MediaProgressViewModal>();
            var modal = _serviceProvider.GetRequiredService<MediaProgressModal>();
            modal.BindingContext = vm;
            await _navigation.PushModalAsync(modal);
        });
    }

    public void Receive(HideMediaProgressModalMessage message)
    {
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (_navigation == null) return;
            if (_navigation.ModalStack.Count > 0)
            {
                var modal = await _navigation.PopModalAsync();
                if (modal.BindingContext is IDisposable disposable) disposable.Dispose();
            }
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
}