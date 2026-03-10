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
        logger.Information("Received InitializedMessage - beginning post-initialization navigation");

        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                var modalWasShown = await playbackModalService.ShowPlaybackModalIfNeededOnWindowCreationAsync();

                logger.Information("Post-initialization: modalWasShown={ModalWasShown}", modalWasShown);

                if (modalWasShown)
                {
                    // Yield to the platform run loop so the modal presentation is
                    // processed and the modal becomes visible on screen.
                    await Task.Delay(300);

#if ANDROID
                    // On Android, PushAsync works while a modal is presented (fragments handle
                    // this correctly). Push Home underneath the modal so it's ready when the
                    // modal is dismissed.
                    await navigationService.NavigateToHomeAsync();
#endif
                    // On iOS, PushAsync on a NavigationPage hangs indefinitely when a modal is
                    // presented (viewDidAppear never fires on hidden views), which holds the
                    // navigationLock and deadlocks modal dismiss. Home is pushed after the
                    // modal is dismissed instead (see PlaybackModalService.CloseModalOnMainThreadAsync).
                }
                else
                {
                    await navigationService.NavigateToHomeAsync();
                }
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
