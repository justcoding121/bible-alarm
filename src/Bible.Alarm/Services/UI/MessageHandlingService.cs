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
                // Check if playback is active before navigating
                // Use try-catch in case Fluxor store isn't initialized yet (prevents deadlock)
                bool isPlaybackActive = false;
                try
                {
                    isPlaybackActive = this.playbackState.Value.IsPreparingOrPlaying;
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "Failed to access PlaybackState (Fluxor may not be initialized yet), checking MediaSession as fallback");
                    // On Android, check MediaSession directly as fallback when PlaybackState isn't ready yet
                    // This is critical for cold starts from Android Auto where playback is active
                    isPlaybackActive = CheckMediaSessionPlaybackState();
                }

                // Navigate to the initialized home page (this creates a new Home instance)
                await navigationService.NavigateToHomeAsync();

                // Immediately hide Home page if playback is active to prevent visual flash before modal appears
                // This must happen synchronously right after navigation to prevent the page from being visible
                if (isPlaybackActive)
                {
                    logger.Information("Playback is active - hiding Home page immediately to prevent flash before AlarmModal");
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

    /// <summary>
    /// Checks MediaSession directly to determine if playback is active.
    /// Used as a fallback when PlaybackState Fluxor store isn't initialized yet (e.g., cold start from Android Auto).
    /// </summary>
    private bool CheckMediaSessionPlaybackState()
    {
#if ANDROID
        try
        {
            var mediaSession = Platforms.Android.Services.Media.MediaSessionHelper.Create();
            var playbackState = mediaSession?.Controller?.PlaybackState;
            
            if (playbackState != null)
            {
                // Check if playback state indicates active playback (Playing, Buffering, or Paused)
                // Paused is included because it means playback was active and can be resumed
                var isActive = playbackState.State is 
                    Android.Support.V4.Media.Session.PlaybackStateCompat.StatePlaying or
                    Android.Support.V4.Media.Session.PlaybackStateCompat.StateBuffering or
                    Android.Support.V4.Media.Session.PlaybackStateCompat.StatePaused;
                
                logger.Information("MediaSession playback state check: State={State}, IsActive={IsActive}", 
                    playbackState.State, isActive);
                
                return isActive;
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to check MediaSession playback state, assuming playback is not active");
        }
#endif
        return false;
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
