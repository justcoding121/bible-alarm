#nullable enable
using Android.Media;
using Bible.Alarm.Platforms.Android.Services.Audio.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using Fluxor;
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
    private readonly IState<PlaybackState> playbackState;
    private bool resumeAfterTransientLoss;

    /// <summary>
    /// Initializes the audio focus listener with playback service injection.
    /// </summary>
    public AudioFocusListener(IPlaybackService playbackService, IState<PlaybackState> playbackState)
    {
        this.playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        this.playbackState = playbackState ?? throw new ArgumentNullException(nameof(playbackState));
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
                    // A call or a duckable interruption pauses. Resume when focus returns,
                    // unless the listener was already paused by the user.
                    resumeAfterTransientLoss = playbackState.Value.Status is PlayStatus.Playing or PlayStatus.Loading;
                    logger.Information("Audio focus lost temporarily - pausing playback");
                    _ = Task.Run(async () => await playbackService.PauseAsync());
                    break;

                case AudioFocus.Loss:
                    resumeAfterTransientLoss = false;
                    logger.Information("Audio focus lost - pausing playback");
                    _ = Task.Run(async () => await playbackService.PauseAsync());
                    break;

                case AudioFocus.Gain:
                    if (resumeAfterTransientLoss)
                    {
                        resumeAfterTransientLoss = false;
                        logger.Information("Audio focus regained - resuming playback");
                        _ = Task.Run(async () => await playbackService.PlayAsync());
                    }
                    else
                    {
                        logger.Debug("Audio focus regained");
                    }
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

