#nullable enable
using Android.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Serilog;
using MediaStream = Android.Media.Stream;

namespace Bible.Alarm.Platforms.Android.Services.Audio;

/// <summary>
/// Singleton global audio focus listener that handles audio focus changes system-wide.
/// Pauses playback when audio focus is lost (e.g., phone call, other app starts playing).
/// </summary>
public sealed class AudioFocusListener : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
{
    private static readonly ILogger Logger = Log.ForContext<AudioFocusListener>();
    private readonly IPlaybackService _playbackService;

    /// <summary>
    /// Initializes the audio focus listener with playback service injection.
    /// </summary>
    public AudioFocusListener(IPlaybackService playbackService)
    {
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        Logger.Information("AudioFocusListener initialized");
    }

    /// <summary>
    /// Called when audio focus changes.
    /// Pauses playback when focus is lost temporarily or permanently.
    /// </summary>
    public void OnAudioFocusChange(AudioFocus focusChange)
    {
        try
        {
            Logger.Debug("Audio focus changed: {FocusChange}", focusChange);

            switch (focusChange)
            {
                case AudioFocus.LossTransientCanDuck:
                case AudioFocus.LossTransient:
                case AudioFocus.Loss:
                    // Permanent loss of audio focus - pause playback
                    Logger.Information("Audio focus lost - pausing playback");

                    _ = Task.Run(async () => await _playbackService.PauseAsync());
                    break;


                case AudioFocus.Gain:
                    // Audio focus regained - don't auto-resume, let user control playback
                    Logger.Debug("Audio focus regained");
                    break;

                default:
                    Logger.Debug("Unknown audio focus change: {FocusChange}", focusChange);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling audio focus change: {FocusChange}", focusChange);
        }
    }
}

