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
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                // Show PlaybackModal BEFORE pushing Home so the user transitions directly
                // from BootstrapOverlay to the modal without a flash of Home page content.
                // The modal sits on the modal stack and covers whatever is in the navigation stack.
                var modalWasShown = await playbackModalService.ShowPlaybackModalIfNeededOnWindowCreationAsync();

                if (modalWasShown)
                {
                    // Let the rendering pipeline draw the modal on screen before pushing Home
                    // underneath. Without this, Android may render one frame of Home before the
                    // modal fragment is composited, causing a visible flash.
                    await Task.Delay(300);
                }

                // Push Home into the navigation stack (underneath the modal if it was shown).
                await navigationService.NavigateToHomeAsync();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(100);
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
