#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.UI.Interfaces;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace Bible.Alarm.Services.UI;

public sealed partial class MessageHandlingService(
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
                await navigationService.NavigateToHomeAsync();
                await Task.Delay(100);

                var modalWasShown = await playbackModalService.ShowPlaybackModalIfNeededOnWindowCreationAsync();
                logger.Information("Post-initialization: modalWasShown={ModalWasShown}", modalWasShown);
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
