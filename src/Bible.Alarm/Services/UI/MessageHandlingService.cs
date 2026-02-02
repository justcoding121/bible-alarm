#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.UI.Interfaces;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace Bible.Alarm.Services.UI;

public sealed class MessageHandlingService(
    ILogger logger,
    IServiceProvider serviceProvider,
    INavigationService navigationService,
    IPlaybackModalService playbackModalService) : IRecipient<ShowToastMessage>, IRecipient<InitializedMessage>, IMessageHandlingService
{
    private bool isDisposed;

    public void RegisterMessageHandlers()
    {
        WeakReferenceMessenger.Default.Register<InitializedMessage>(this);
        WeakReferenceMessenger.Default.Register<ShowToastMessage>(this);
    }

    public void Receive(ShowToastMessage message)
    {
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                // IToastService is a singleton, so don't dispose it
                var toastService = serviceProvider.GetRequiredService<IToastService>();
                await toastService.ShowMessage(message.Value);
            }
            catch (Exception ex)
            {
                // Toast message failures are non-critical (UI feedback only)
                logger.Warning(ex, "Error showing toast message");
            }
        });
    }

    public void Receive(InitializedMessage message)
    {
        logger.Information("Received InitializedMessage, navigating to Home page.");

        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                // Navigate to the initialized home page (this creates a new Home instance)
                await navigationService.NavigateToHomeAsync();

                // Window-creation entrypoint: show PlaybackModal only if playback is already active
                // (Playing/Paused/Loading). Do NOT show for Failed/Stopped/Ended.
                await playbackModalService.ShowPlaybackModalIfNeededOnWindowCreationAsync();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        logger.Information("Starting service initialization...");

                        // Modal visibility is now handled reactively via PlaybackState subscription
                        // No need to manually send ShowPlaybackModalMessage here

                        await Task.Delay(100);

                        logger.Information("Service initialization completed!");

                    }
                    catch (Exception ex)
                    {
                        // Background initialization task failures are non-critical
                        logger.Warning(ex, "Error in background initialization task");
                    }
                });
            }
            catch (Exception e)
            {
                logger.Error(e, "An error happened while showing HomePage after initialization.");
            }
        });
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // Unregister from messages
        WeakReferenceMessenger.Default.Unregister<InitializedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ShowToastMessage>(this);
    }
}
