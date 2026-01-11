#nullable enable

using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using Windows.Media;

namespace Bible.Alarm.Platforms.Windows.Services.Media;

/// <summary>
/// Windows-specific service that subscribes to System Media Transport Controls (SMTC) button events
/// and dispatches messages that trigger Fluxor actions for playback control.
/// </summary>
public sealed class WindowsSmtcService : IDisposable
{
    private static readonly ILogger logger = Log.ForContext<WindowsSmtcService>();
    private readonly IMediaElementService mediaElementService;
    private SystemMediaTransportControls? systemMediaControls;
    private bool isSubscribed;

    public WindowsSmtcService(IMediaElementService mediaElementService)
    {
        this.mediaElementService = mediaElementService;
    }

    /// <summary>
    /// Initializes SMTC button handlers by subscribing to button press events.
    /// Should be called when MediaElement is created and ready.
    /// Retries up to 3 times with delays if MediaElement isn't ready yet.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            if (isSubscribed)
            {
                logger.Debug("SMTC service already initialized");
                return;
            }

            // Retry initialization up to 3 times with delays
            // MediaElement might not be fully initialized immediately
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    // Get MediaElement to access SystemMediaTransportControls
                    var mediaElement = await mediaElementService.GetMediaElementAsync();

                    // Get SystemMediaTransportControls directly from MediaElement (Windows-specific API)
                    systemMediaControls = mediaElement.GetSystemMediaTransportControls();

                    if (systemMediaControls == null)
                    {
                        if (attempt < 3)
                        {
                            logger.Debug("SystemMediaTransportControls not available yet, retrying in 500ms (attempt {Attempt}/3)", attempt);
                            await Task.Delay(500);
                            continue;
                        }
                        logger.Warning("SystemMediaTransportControls is null after {Attempts} attempts - SMTC button handlers will not work", attempt);
                        return;
                    }

                    // Subscribe to button press events
                    systemMediaControls.ButtonPressed += OnSystemMediaControlsButtonPressed;
                    isSubscribed = true;

                    // Ensure buttons are enabled after initialization
                    // This ensures buttons are visible even before navigation state is updated
                    systemMediaControls.IsNextEnabled = true;
                    systemMediaControls.IsPreviousEnabled = true;
                    systemMediaControls.IsFastForwardEnabled = true;
                    systemMediaControls.IsRewindEnabled = true;

                    logger.Information("SMTC service initialized successfully (attempt {Attempt}) - Next/Previous/Play/Pause/Seek button handlers active", attempt);
                    return; // Success - exit retry loop
                }
                catch (Exception ex)
                {
                    if (attempt < 3)
                    {
                        logger.Debug(ex, "Initialization attempt {Attempt} failed, retrying in 500ms", attempt);
                        await Task.Delay(500);
                        continue;
                    }
                    throw; // Re-throw on final attempt
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to initialize SMTC service after all retry attempts");
        }
    }

    /// <summary>
    /// Updates SMTC button states based on navigation capabilities.
    /// Also ensures seek buttons are enabled when playback is active.
    /// </summary>
    public void UpdateButtonStates(bool canPlayNext, bool canPlayPrevious)
    {
        try
        {
            if (systemMediaControls == null)
            {
                return;
            }

            systemMediaControls.IsNextEnabled = canPlayNext;
            systemMediaControls.IsPreviousEnabled = canPlayPrevious;

            // Enable seek buttons when next/prev buttons are enabled (playback is active)
            // Seek buttons should be available whenever we can navigate
            var isPlaybackActive = canPlayNext || canPlayPrevious;
            systemMediaControls.IsFastForwardEnabled = isPlaybackActive;
            systemMediaControls.IsRewindEnabled = isPlaybackActive;

            logger.Debug("SMTC button states updated: CanPlayNext={CanPlayNext}, CanPlayPrevious={CanPlayPrevious}, SeekEnabled={SeekEnabled}",
                canPlayNext, canPlayPrevious, isPlaybackActive);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to update SMTC button states");
        }
    }

    private void OnSystemMediaControlsButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        try
        {
            logger.Debug("SMTC button pressed: {Button}", args.Button);

            // Send message on main thread to trigger Fluxor actions
            MainThread.BeginInvokeOnMainThread(() =>
            {
                switch (args.Button)
                {
                    case SystemMediaTransportControlsButton.Next:
                        logger.Information("Next button pressed in SMTC - sending NextButtonPressedMessage");
                        WeakReferenceMessenger.Default.Send(new NextButtonPressedMessage());
                        break;

                    case SystemMediaTransportControlsButton.Previous:
                        logger.Information("Previous button pressed in SMTC - sending PreviousButtonPressedMessage");
                        WeakReferenceMessenger.Default.Send(new PreviousButtonPressedMessage());
                        break;

                    case SystemMediaTransportControlsButton.Play:
                        logger.Information("Play button pressed in SMTC - sending PlayButtonPressedMessage");
                        WeakReferenceMessenger.Default.Send(new PlayButtonPressedMessage());
                        break;

                    case SystemMediaTransportControlsButton.Pause:
                        logger.Information("Pause button pressed in SMTC - sending PauseButtonPressedMessage");
                        WeakReferenceMessenger.Default.Send(new PauseButtonPressedMessage());
                        break;

                    case SystemMediaTransportControlsButton.FastForward:
                        logger.Information("Fast Forward button pressed in SMTC - sending SeekForwardButtonPressedMessage");
                        WeakReferenceMessenger.Default.Send(new SeekForwardButtonPressedMessage());
                        break;

                    case SystemMediaTransportControlsButton.Rewind:
                        logger.Information("Rewind button pressed in SMTC - sending SeekBackwardButtonPressedMessage");
                        WeakReferenceMessenger.Default.Send(new SeekBackwardButtonPressedMessage());
                        break;
                }
            });
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error handling SMTC button press");
        }
    }


    public void Dispose()
    {
        if (systemMediaControls != null && isSubscribed)
        {
            try
            {
                systemMediaControls.ButtonPressed -= OnSystemMediaControlsButtonPressed;
                isSubscribed = false;
                logger.Debug("SMTC service disposed - unsubscribed from button events");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error disposing SMTC service");
            }
        }
    }
}
