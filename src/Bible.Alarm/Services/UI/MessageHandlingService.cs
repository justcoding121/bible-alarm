#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Services.UI;

public sealed class MessageHandlingService(
    ILogger logger,
    IServiceProvider serviceProvider,
    INavigationService navigationService,
    IState<PlaybackState> playbackState) : IRecipient<ShowToastMessage>, IRecipient<InitializedMessage>, IMessageHandlingService
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
                logger.Error(ex, "Error showing toast message");
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

                // After Home is visible, if playback was already started (prepare+play), open the alarm modal.
                // AlarmModalService defers opening while Bootstrap is on top, so we "catch up" here.
                if (playbackState.Value.IsPreparingOrPlaying)
                {
                    logger.Information("Home is visible and playback is already started; opening AlarmModal.");
                    await navigationService.OpenAlarmModalAsync();
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        logger.Information("Starting service initialization...");

                        // Modal visibility is now handled reactively via PlaybackState subscription
                        // No need to manually send ShowAlarmModalMessage here

                        await Task.Delay(100);

                        logger.Information("Service initialization completed!");

                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Error in initialization");
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

