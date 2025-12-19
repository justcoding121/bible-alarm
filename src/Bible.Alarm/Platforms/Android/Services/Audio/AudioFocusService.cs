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

    private readonly Lock @lock = new();

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

        var audioManagerService = context.GetSystemService(global::Android.Content.Context.AudioService) as AudioManager;
        if (audioManagerService == null)
        {
            throw new InvalidOperationException("Failed to get AudioManager from Application.Context");
        }
        audioManager = audioManagerService;

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
                // Minimum Android version is 26 (API 26 = BuildVersionCodes.O), so we always use the modern API
                var audioAttributesBuilder = new AudioAttributes.Builder();
                if (audioAttributesBuilder == null)
                {
                    logger.Warning("Failed to create AudioAttributes.Builder");
                    return;
                }
                
                audioAttributesBuilder.SetUsage(AudioUsageKind.Media);
                audioAttributesBuilder.SetContentType(AudioContentType.Music);
                var audioAttributes = audioAttributesBuilder.Build();

                if (audioAttributes == null)
                {
                    logger.Warning("Failed to create AudioAttributes");
                    return;
                }

                var audioFocusRequestBuilder = new AudioFocusRequestClass.Builder(AudioFocus.Gain);
                if (audioFocusRequestBuilder == null)
                {
                    logger.Warning("Failed to create AudioFocusRequest.Builder");
                    return;
                }
                
                audioFocusRequestBuilder.SetAudioAttributes(audioAttributes);
                audioFocusRequestBuilder.SetAcceptsDelayedFocusGain(true);
                audioFocusRequestBuilder.SetOnAudioFocusChangeListener(audioFocusListener);
                audioFocusRequest = audioFocusRequestBuilder.Build();

                if (audioFocusRequest == null)
                {
                    logger.Warning("Failed to create AudioFocusRequest");
                    return;
                }

                if (audioManager == null)
                {
                    logger.Warning("AudioManager is null");
                    return;
                }

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
            // Minimum Android version is 26 (API 26 = BuildVersionCodes.O), so we always use the modern API
            if (audioFocusRequest != null)
            {
                audioManager.AbandonAudioFocusRequest(audioFocusRequest);
                audioFocusRequest = null;
                logger.Debug("Audio focus released");
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error releasing audio focus");
            // Reset flag on error to prevent getting stuck
            audioFocusRequest = null;
        }
    }
}

