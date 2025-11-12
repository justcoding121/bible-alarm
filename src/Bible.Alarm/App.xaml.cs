using System.Diagnostics;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media;
using Bible.Alarm.ViewModels;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Views;
using Bible.Alarm.Views.General;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace Bible.Alarm;

public partial class App : Application,
    IRecipient<ShowAlarmModalMessage>,
    IRecipient<HideAlarmModalMessage>,
    IRecipient<ShowMediaProgressModalMessage>,
    IRecipient<HideMediaProgressModalMessage>,
    IRecipient<ShowToastMessage>,
    IRecipient<ClearToastsMessage>,
    IRecipient<InitializedMessage>
{
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;

    public static bool IsInForeground { get; set; } 
    private INavigation Navigation => _serviceProvider.GetService<INavigation>();

    public App(ILogger logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        InitializeComponent();

        WeakReferenceMessenger.Default.Register<InitializedMessage>(this);


        WeakReferenceMessenger.Default.Register<ShowAlarmModalMessage>(this);
        WeakReferenceMessenger.Default.Register<HideAlarmModalMessage>(this);
        WeakReferenceMessenger.Default.Register<ShowMediaProgressModalMessage>(this);
        WeakReferenceMessenger.Default.Register<HideMediaProgressModalMessage>(this);
        WeakReferenceMessenger.Default.Register<ShowToastMessage>(this);
        WeakReferenceMessenger.Default.Register<ClearToastsMessage>(this);
    }

    protected override Window CreateWindow(IActivationState activationState)
    {
        var loadingPage = _serviceProvider.GetRequiredService<LoadingPage>();
        var navigationPage = new NavigationPage(loadingPage)
        {
            BarBackgroundColor = Colors.Transparent,
            BarTextColor = Colors.White
        };

        NavigationPage.SetHasNavigationBar(loadingPage, false);

        return new Window(navigationPage);
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

                if (playbackService.IsPrepared) 
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
            if (Navigation == null) return;

            var playbackService = _serviceProvider.GetRequiredService<IPlaybackService>();
            if (!playbackService.IsPlaying)
                return;

            if (Navigation.ModalStack.LastOrDefault()?.GetType() == typeof(AlarmModal)) return;

            var vm = _serviceProvider.GetRequiredService<AlarmViewModal>();
            var modal = _serviceProvider.GetRequiredService<AlarmModal>();
            modal.BindingContext = vm;
            await Navigation.PushModalAsync(modal);
        });
    }

    public void Receive(HideAlarmModalMessage message)
    {
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Navigation == null) return;
            if (Navigation.ModalStack.Count > 0)
            {
                var modal = await Navigation.PopModalAsync();
                if (modal.BindingContext is IDisposable disposable) disposable.Dispose();
            }
        });
    }

    public void Receive(ShowMediaProgressModalMessage message)
    {
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Navigation == null) return;

            if (Navigation.ModalStack.LastOrDefault()?.GetType() == typeof(MediaProgressModal)) return;

            var vm = _serviceProvider.GetRequiredService<MediaProgressViewModal>();
            var modal = _serviceProvider.GetRequiredService<MediaProgressModal>();
            modal.BindingContext = vm;
            await Navigation.PushModalAsync(modal);
        });
    }

    public void Receive(HideMediaProgressModalMessage message)
    {
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Navigation == null) return;
            if (Navigation.ModalStack.Count > 0)
            {
                var modal = await Navigation.PopModalAsync();
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

    public void Receive(InitializedMessage message)
    {
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Navigation == null) return;

            try
            {
                var homePage = _serviceProvider.GetRequiredService<Home>();

                NavigationPage.SetHasNavigationBar(homePage, false);

                await Navigation.PushAsync(homePage); 
                
                var pagesToRemove = Navigation.NavigationStack.Where(p => p != homePage).ToList();
                foreach (var page in pagesToRemove)
                {
                    Navigation.RemovePage(page);
                }

                if (homePage.BindingContext is HomeViewModel homeViewModel)
                {
                    await homeViewModel.InitializeAsync();
                }

      
                _ = Task.Run(async () =>
                {
                    try
                    {
                        Debug.WriteLine("Starting service initialization...");
                        
                        var playbackService = _serviceProvider.GetRequiredService<IPlaybackService>();
                        if (playbackService.IsPrepared) WeakReferenceMessenger.Default.Send(new ShowAlarmModalMessage(null));
                        
                        await Task.Delay(100); 
                        
                        Debug.WriteLine("Service initialization completed!");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in initialization: {ex.Message}");
                        Debug.WriteLine($"Stack trace: {ex.StackTrace}");
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