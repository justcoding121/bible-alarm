#nullable enable
using Android.Media;
using Bible.Alarm.Platforms.Android.Services.Audio;
using Serilog;
using MediaStream = Android.Media.Stream;

namespace Bible.Alarm.Platforms.Android.Services.Audio;

/// <summary>
/// Service that manages audio focus requests and releases.
/// Centralizes audio focus management for use by AudioFocusEffect and MediaSessionManager.
/// </summary>
public sealed class AudioFocusService
{
    private static readonly ILogger Logger = Log.ForContext<AudioFocusService>();
    private readonly AudioFocusListener _audioFocusListener;
    private readonly AudioManager _audioManager;
    private AudioFocusRequestClass? _audioFocusRequest;

    private readonly object _lock = new();

    /// <summary>
    /// Initializes the audio focus service with the global audio focus listener.
    /// </summary>
    public AudioFocusService(AudioFocusListener audioFocusListener)
    {
        _audioFocusListener = audioFocusListener ?? throw new ArgumentNullException(nameof(audioFocusListener));

        // Initialize AudioManager in constructor - Application.Context is available at this point
        var context = global::Android.App.Application.Context;
        if (context == null)
        {
            throw new InvalidOperationException("Android Application.Context is null - cannot initialize AudioFocusService");
        }

        _audioManager = context.GetSystemService(global::Android.Content.Context.AudioService) as AudioManager;
        if (_audioManager == null)
        {
            throw new InvalidOperationException("Failed to get AudioManager from Application.Context");
        }

        Logger.Debug("AudioFocusService initialized with AudioManager");
    }

    /// <summary>
    /// Requests audio focus for media playback.
    /// </summary>
    public void RequestAudioFocus()
    {
        lock (_lock)
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

                    _audioFocusRequest = new AudioFocusRequestClass.Builder(AudioFocus.Gain)
                        .SetAudioAttributes(audioAttributes)
                        .SetAcceptsDelayedFocusGain(true)
                        .SetOnAudioFocusChangeListener(_audioFocusListener)
                        .Build();

                    var result = _audioManager.RequestAudioFocus(_audioFocusRequest);

                    if (result == AudioFocusRequest.Granted)
                    {
                        Logger.Information("Audio focus granted - playback can start");
                    }
                    else
                    {
                        Logger.Warning("Audio focus request returned: {Result}", result);
                    }
                }
                else
                {
                    // Android 7.1 and below - use legacy API
                    var result = _audioManager.RequestAudioFocus(
                        _audioFocusListener,
                        MediaStream.Music,
                        AudioFocus.Gain);

                    if (result == AudioFocusRequest.Granted)
                    {
                        Logger.Information("Audio focus granted (legacy API) - playback can start");
                    }
                    else
                    {
                        Logger.Warning("Audio focus request returned: {Result}", result);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error requesting audio focus");
            }
        }
    }

    /// <summary>
    /// Releases audio focus when playback stops.
    /// </summary>
    public void ReleaseAudioFocus()
    {
        lock (_lock)
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
                if (_audioFocusRequest != null)
                {
                    _audioManager.AbandonAudioFocusRequest(_audioFocusRequest);
                    _audioFocusRequest = null;
                    Logger.Debug("Audio focus released");
                }
            }
            else
            {
                // Android 7.1 and below - use legacy API
                // Safe to call even if we don't have focus
                _audioManager.AbandonAudioFocus(_audioFocusListener);
                Logger.Debug("Audio focus released (legacy API)");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error releasing audio focus");
            // Reset flag on error to prevent getting stuck
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
            {
                _audioFocusRequest = null;
            }
        }
    }
}

