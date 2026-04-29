#nullable enable
using AVFoundation;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Shared.Constants;
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
            logger.Debug(AppConstants.Logging.IosAudioSessionDiagnosticsLog.AttemptingToConfigureAudioSessionForContext, context);
            var audioSession = AVAudioSession.SharedInstance();

            var categoryName = new NSString("AVAudioSessionCategoryPlayback");
            var categoryResult = audioSession.SetCategory(categoryName, out var error);

            if (!categoryResult || error != null)
            {
                logger.Warning(AppConstants.Logging.IosAudioSessionDiagnosticsLog.FailedToSetAvAudioSessionCategoryForContext,
                    context,
                    error?.LocalizedDescription ?? AppConstants.Logging.UnknownErrorFallback);
            }
            else
            {
                logger.Debug(AppConstants.Logging.IosAudioSessionDiagnosticsLog.SuccessfullySetAvAudioSessionCategoryPlaybackForContext, context);
            }

            var activateResult = audioSession.SetActive(true, out error);
            if (!activateResult || error != null)
            {
                logger.Warning(AppConstants.Logging.IosAudioSessionDiagnosticsLog.FailedToActivateAvAudioSessionForContext,
                    context,
                    error?.LocalizedDescription ?? AppConstants.Logging.UnknownErrorFallback);
            }
            else
            {
                logger.Debug(AppConstants.Logging.IosAudioSessionDiagnosticsLog.SuccessfullyActivatedAvAudioSessionForContext, context);
            }

            RegisterRouteChangeObserver(logger);
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.IosAudioSessionDiagnosticsLog.ErrorConfiguringIosAudioSessionForContext, context);
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
                    logger.Information(AppConstants.Logging.IosAudioSessionDiagnosticsLog.AudioRouteChangedOldDeviceUnavailablePausingPlayback);
                    WeakReferenceMessenger.Default.Send(new PauseButtonPressedMessage());
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, AppConstants.Logging.IosAudioSessionDiagnosticsLog.ErrorHandlingAudioRouteChangeNotification);
            }
        });

        logger.Debug(AppConstants.Logging.IosAudioSessionDiagnosticsLog.RegisteredIosAudioRouteChangeObserver);
    }
}

