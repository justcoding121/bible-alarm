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
    private readonly IState<PlaybackState> playbackState = playbackState;

    // IState is injected but we access .Value safely with try-catch
    // This prevents deadlock if Fluxor store isn't initialized when App constructor runs

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
                // Check if playback is active before navigating
                // Use try-catch in case Fluxor store isn't initialized yet (prevents deadlock)
                bool isPlaybackActive = false;
                try
                {
                    isPlaybackActive = this.playbackState.Value.IsPreparingOrPlaying;
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "Failed to access PlaybackState (Fluxor may not be initialized yet), assuming playback is not active");
                    isPlaybackActive = false;
                }

                // Navigate to the initialized home page (this creates a new Home instance)
                await navigationService.NavigateToHomeAsync();

                // If playback is active, hide Home page to prevent visual flash before modal appears
                if (isPlaybackActive)
                {
                    logger.Information("Playback is active - hiding Home page before opening AlarmModal");
                    navigationService.SetHomePageVisibility(isPlaybackActive: true);
                }

                // After Home is navigated (and hidden if playback active), open the alarm modal.
                // AlarmModalService defers opening while Bootstrap is on top, so we "catch up" here.
                if (isPlaybackActive)
                {
                    logger.Information("Home is navigated and playback is already started; opening AlarmModal.");
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
