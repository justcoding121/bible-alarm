#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.ViewModels;
using Bible.Alarm.Views;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace Bible.Alarm.Services.UI;

public class MessageHandlingService(
    ILogger logger,
    IServiceProvider serviceProvider,
    INavigationService navigationService) : IRecipient<ShowToastMessage>, IRecipient<InitializedMessage>, IMessageHandlingService
{
    private readonly ILogger _logger = logger;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly INavigationService _navigationService = navigationService;
    private bool _isDisposed;

    public void RegisterMessageHandlers()
    {
        WeakReferenceMessenger.Default.Register<InitializedMessage>(this);
        WeakReferenceMessenger.Default.Register<ShowToastMessage>(this);
    }

    public void Receive(ShowToastMessage message)
    {
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            // IToastService is a singleton, so don't dispose it
            var toastService = _serviceProvider.GetRequiredService<IToastService>();
            await toastService.ShowMessage(message.Value);
        });
    }

    public void Receive(InitializedMessage message)
    {
        _logger.Information("Received InitializedMessage, navigating to Home page.");

        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                    // Navigate to the initialized home page (this creates a new Home instance)
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
    
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        
        // Unregister from messages
        WeakReferenceMessenger.Default.Unregister<InitializedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ShowToastMessage>(this);
    }
}

