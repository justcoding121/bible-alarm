#nullable enable
using Android.Media;
using Serilog;
using MediaStream = Android.Media.Stream;

namespace Bible.Alarm.Platforms.Android.Services.Audio;

/// <summary>
/// Service that manages audio focus requests and releases.
/// Centralizes audio focus management for use by AudioFocusEffect and MediaSessionManager.
/// </summary>
public sealed class AudioFocusService
{
    private static readonly ILogger logger = Log.ForContext<AudioFocusService>();
    private readonly AudioFocusListener audioFocusListener;
    private readonly AudioManager audioManager;
    private AudioFocusRequestClass? audioFocusRequest;

    private readonly object @lock = new();

    /// <summary>
    /// Initializes the audio focus service with the global audio focus listener.
    /// </summary>
    public AudioFocusService(AudioFocusListener audioFocusListener)
    {
        this.audioFocusListener = audioFocusListener ?? throw new ArgumentNullException(nameof(audioFocusListener));

        // Initialize AudioManager in constructor - Application.Context is available at this point
        var context = global::Android.App.Application.Context;
        if (context == null)
        {
            throw new InvalidOperationException("Android Application.Context is null - cannot initialize AudioFocusService");
        }

        audioManager = context.GetSystemService(global::Android.Content.Context.AudioService) as AudioManager;
        if (audioManager == null)
        {
            throw new InvalidOperationException("Failed to get AudioManager from Application.Context");
        }

        logger.Debug("AudioFocusService initialized with AudioManager");
    }

    /// <summary>
    /// Requests audio focus for media playback.
    /// </summary>
    public void RequestAudioFocus()
    {
        lock (@lock)
        {
            try
            {
                // Request audio focus for media playback
                if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
                {
                    // Android 8.0+ (API 26+)
                    var audioAttributes = new AudioAttributes.Builder()
                        .SetUsage(AudioUsageKind.Media)
                        .SetContentType(AudioContentType.Music)
                        .Build();

                    audioFocusRequest = new AudioFocusRequestClass.Builder(AudioFocus.Gain)
                        .SetAudioAttributes(audioAttributes)
                        .SetAcceptsDelayedFocusGain(true)
                        .SetOnAudioFocusChangeListener(audioFocusListener)
                        .Build();

                    var result = audioManager.RequestAudioFocus(audioFocusRequest);

                    if (result == AudioFocusRequest.Granted)
                    {
                        logger.Information("Audio focus granted - playback can start");
                    }
                    else
                    {
                        logger.Warning("Audio focus request returned: {Result}", result);
                    }
                }
                else
                {
                    // Android 7.1 and below - use legacy API
                    var result = audioManager.RequestAudioFocus(
                        audioFocusListener,
                        MediaStream.Music,
                        AudioFocus.Gain);

                    if (result == AudioFocusRequest.Granted)
                    {
                        logger.Information("Audio focus granted (legacy API) - playback can start");
                    }
                    else
                    {
                        logger.Warning("Audio focus request returned: {Result}", result);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error requesting audio focus");
            }
        }
    }

    /// <summary>
    /// Releases audio focus when playback stops.
    /// </summary>
    public void ReleaseAudioFocus()
    {
        lock (@lock)
        {
            ReleaseAudioFocusInternal();
        }
    }

    /// <summary>
    /// Internal method to release audio focus (must be called within lock).
    /// </summary>
    private void ReleaseAudioFocusInternal()
    {
        try
        {
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
            {
                // Android 8.0+ (API 26+)
                if (audioFocusRequest != null)
                {
                    audioManager.AbandonAudioFocusRequest(audioFocusRequest);
                    audioFocusRequest = null;
                    logger.Debug("Audio focus released");
                }
            }
            else
            {
                // Android 7.1 and below - use legacy API
                // Safe to call even if we don't have focus
                audioManager.AbandonAudioFocus(audioFocusListener);
                logger.Debug("Audio focus released (legacy API)");
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error releasing audio focus");
            // Reset flag on error to prevent getting stuck
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
            {
                audioFocusRequest = null;
            }
        }
    }
}

