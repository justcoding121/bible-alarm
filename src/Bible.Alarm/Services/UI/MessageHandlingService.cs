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
        // Use ObservableMessenger for InitializedMessage so message is not lost if sent before registration
        ObservableMessenger.InitializationMessenger.Register<InitializedMessage>(this);

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
                // Create Home page and initialize ViewModel before navigating
                // Note: Home is transient, but NavigateToHomeAsync will create a new instance and push it
                // This instance is only used for initialization, so we need to dispose it if navigation fails
                var homePage = _serviceProvider.GetRequiredService<Home>();
                try
                {
                    if (homePage.BindingContext is HomeViewModel homeViewModel)
                    {
                        // Wait for initialization to complete before navigating
                        await homeViewModel.InitializeAsync();
                    }

                    // Navigate to the initialized home page (this creates a new Home instance)
                    await _navigationService.NavigateToHomeAsync();
                }
                finally
                {
                    // Dispose the temporary Home page if it implements IDisposable
                    // (The page pushed by NavigateToHomeAsync will be disposed when popped)
                    if (homePage is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }

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
        ObservableMessenger.InitializationMessenger.Unregister<InitializedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ShowToastMessage>(this);
    }
}

