#nullable enable
using Android.Media;
using Bible.Alarm.Platforms.Android.Services.Audio.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Serilog;
using Object = Java.Lang.Object;

namespace Bible.Alarm.Platforms.Android.Services.Audio;

/// <summary>
/// Singleton global audio focus listener that handles audio focus changes system-wide.
/// Pauses playback when audio focus is lost (e.g., phone call, other app starts playing).
/// </summary>
public sealed class AudioFocusListener : Object, IAudioFocusListener, AudioManager.IOnAudioFocusChangeListener
{
    private static readonly ILogger logger = Log.ForContext<AudioFocusListener>();
    private readonly IPlaybackService playbackService;

    /// <summary>
    /// Initializes the audio focus listener with playback service injection.
    /// </summary>
    public AudioFocusListener(IPlaybackService playbackService)
    {
        this.playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        logger.Information("AudioFocusListener initialized");
    }

    /// <summary>
    /// Called when audio focus changes.
    /// Pauses playback when focus is lost temporarily or permanently.
    /// </summary>
    public void OnAudioFocusChange(AudioFocus focusChange)
    {
        try
        {
            logger.Debug("Audio focus changed: {FocusChange}", focusChange);

            switch (focusChange)
            {
                case AudioFocus.LossTransientCanDuck:
                case AudioFocus.LossTransient:
                case AudioFocus.Loss:
                    // Permanent loss of audio focus - pause playback
                    logger.Information("Audio focus lost - pausing playback");

                    _ = Task.Run(async () => await playbackService.PauseAsync());
                    break;


                case AudioFocus.Gain:
                    // Audio focus regained - don't auto-resume, let user control playback
                    logger.Debug("Audio focus regained");
                    break;

                default:
                    logger.Debug("Unknown audio focus change: {FocusChange}", focusChange);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error handling audio focus change: {FocusChange}", focusChange);
        }
    }
}

