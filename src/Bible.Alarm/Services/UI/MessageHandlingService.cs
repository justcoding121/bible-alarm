#nullable enable
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.ViewModels;
using Bible.Alarm.Views;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace Bible.Alarm.Services.UI;

public class MessageHandlingService : IRecipient<ShowToastMessage>, IRecipient<InitializedMessage>
{
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly INavigationService _navigationService;

    public MessageHandlingService(
        ILogger logger,
        IServiceProvider serviceProvider,
        INavigationService navigationService)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _navigationService = navigationService;
    }

    public void RegisterMessageHandlers()
    {
        // Use ObservableMessenger for InitializedMessage so message is not lost if sent before registration
        ObservableMessenger.InitializationMessenger.Register<InitializedMessage>(this);

        WeakReferenceMessenger.Default.Register<ShowToastMessage>(this);
    }

    public void Receive(ShowToastMessage message)
    {
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            using var toastService = _serviceProvider.GetRequiredService<IToastService>();
            await toastService.ShowMessage(message.Value);
        });
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

