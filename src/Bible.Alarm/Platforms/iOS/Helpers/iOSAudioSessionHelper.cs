#nullable enable
using AVFoundation;
using Bible.Alarm.Common.Messenger;
using CommunityToolkit.Mvvm.Messaging;
using Foundation;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.Helpers;

/// <summary>
/// Helper class for configuring iOS audio session.
/// </summary>
public static class IOsAudioSessionHelper
{
    private static NSObject? routeChangeObserver;

    /// <summary>
    /// Configures the iOS audio session for playback.
    /// Sets the category to Playback and activates the session.
    /// This allows audio to play even in silent mode.
    /// </summary>
    public static void ConfigureAudioSession(ILogger logger, string context = "playback")
    {
        try
        {
            logger.Debug("Attempting to configure iOS audio session for {Context}.", context);
            var audioSession = AVAudioSession.SharedInstance();

            var categoryName = new NSString("AVAudioSessionCategoryPlayback");
            var categoryResult = audioSession.SetCategory(categoryName, out var error);

            if (!categoryResult || error != null)
            {
                logger.Warning("Failed to set AVAudioSession category for {Context}: {Error}",
                    context,
                    error?.LocalizedDescription ?? "Unknown error");
            }
            else
            {
                logger.Debug("Successfully set AVAudioSession category to Playback for {Context}", context);
            }

            var activateResult = audioSession.SetActive(true, out error);
            if (!activateResult || error != null)
            {
                logger.Warning("Failed to activate AVAudioSession for {Context}: {Error}",
                    context,
                    error?.LocalizedDescription ?? "Unknown error");
            }
            else
            {
                logger.Debug("Successfully activated AVAudioSession for {Context}", context);
            }

            RegisterRouteChangeObserver(logger);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error configuring iOS audio session for {Context}", context);
        }
    }

    /// <summary>
    /// Observes audio route changes (e.g. Bluetooth disconnects, headphones unplugged)
    /// and pauses playback when the old output device becomes unavailable.
    /// iOS equivalent of Android's ACTION_AUDIO_BECOMING_NOISY / AudioNoisyReceiver.
    /// </summary>
    private static void RegisterRouteChangeObserver(ILogger logger)
    {
        if (routeChangeObserver != null)
        {
            return;
        }

        routeChangeObserver = AVAudioSession.Notifications.ObserveRouteChange((sender, args) =>
        {
            try
            {
                if (args.Reason == AVAudioSessionRouteChangeReason.OldDeviceUnavailable)
                {
                    logger.Information(
                        "Audio route changed (old device unavailable, e.g. Bluetooth disconnected) — pausing playback");
                    WeakReferenceMessenger.Default.Send(new PauseButtonPressedMessage());
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error handling audio route change notification");
            }
        });

        logger.Debug("Registered iOS audio route change observer");
    }
}

