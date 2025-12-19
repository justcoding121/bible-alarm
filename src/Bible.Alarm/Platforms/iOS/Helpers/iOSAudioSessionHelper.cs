#nullable enable
using AVFoundation;
using Foundation;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.Helpers;

/// <summary>
/// Helper class for configuring iOS audio session.
/// Shared between AudioPlayer and preview audio players.
/// </summary>
public static class iOSAudioSessionHelper
{
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
            NSError? error;

            // Set category for playback - this allows audio to play even in silent mode
            var categoryName = new NSString("AVAudioSessionCategoryPlayback");
            var categoryResult = audioSession.SetCategory(categoryName, out error);

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

            // Activate the audio session
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
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error configuring iOS audio session for {Context}", context);
        }
    }
}

