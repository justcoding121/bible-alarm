#nullable enable
using System;
using Android.Content;
using Android.Media;
using Bible.Alarm.Platforms.Android.Services.Audio.Interfaces;
using Serilog;
using Application = Android.App.Application;

namespace Bible.Alarm.Platforms.Android.Services.Audio;

/// <summary>
/// Service that manages audio focus requests and releases.
/// Also registers/unregisters an ACTION_AUDIO_BECOMING_NOISY receiver so
/// playback pauses when the audio output route changes (e.g. Bluetooth disconnects).
/// </summary>
public sealed class AudioFocusService : IAudioFocusService
{
    private static readonly ILogger logger = Log.ForContext<AudioFocusService>();
    private readonly IAudioFocusListener audioFocusListener;
    private readonly AudioManager audioManager;
    private AudioFocusRequestClass? audioFocusRequest;
    private AudioNoisyReceiver? noisyReceiver;
    private bool noisyReceiverRegistered;

    private readonly Lock @lock = new();

    public AudioFocusService(IAudioFocusListener audioFocusListener)
    {
        this.audioFocusListener = audioFocusListener ?? throw new ArgumentNullException(nameof(audioFocusListener));

        var context = Application.Context ?? throw new InvalidOperationException("Android Application.Context is null - cannot initialize AudioFocusService");
        var audioManagerService = context.GetSystemService(Context.AudioService) as AudioManager;
        audioManager = audioManagerService ?? throw new InvalidOperationException("Failed to get AudioManager from Application.Context");

        logger.Debug("AudioFocusService initialized with AudioManager");
    }

    /// <summary>
    /// Requests audio focus for media playback and starts listening for
    /// ACTION_AUDIO_BECOMING_NOISY so we pause when the output route changes.
    /// </summary>
    public void RequestAudioFocus()
    {
        lock (@lock)
        {
            try
            {
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
                audioFocusRequestBuilder.SetOnAudioFocusChangeListener((AudioManager.IOnAudioFocusChangeListener)audioFocusListener);
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

                RegisterNoisyReceiverInternal();
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
            if (audioFocusRequest != null)
            {
                audioManager.AbandonAudioFocusRequest(audioFocusRequest);
                audioFocusRequest = null;
                logger.Debug("Audio focus released");
            }

            UnregisterNoisyReceiverInternal();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error releasing audio focus");
            audioFocusRequest = null;
        }
    }

    private void RegisterNoisyReceiverInternal()
    {
        if (noisyReceiverRegistered)
        {
            return;
        }

        try
        {
            noisyReceiver = new AudioNoisyReceiver();
            var filter = new IntentFilter(AudioManager.ActionAudioBecomingNoisy);

            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                Application.Context.RegisterReceiver(noisyReceiver, filter, ReceiverFlags.NotExported);
            }
            else
            {
#pragma warning disable CA1422 // Validate platform compatibility
                Application.Context.RegisterReceiver(noisyReceiver, filter);
#pragma warning restore CA1422
            }

            noisyReceiverRegistered = true;
            logger.Debug("AudioNoisyReceiver registered");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error registering AudioNoisyReceiver");
        }
    }

    private void UnregisterNoisyReceiverInternal()
    {
        if (!noisyReceiverRegistered || noisyReceiver == null)
        {
            return;
        }

        try
        {
            Application.Context.UnregisterReceiver(noisyReceiver);
            noisyReceiverRegistered = false;
            noisyReceiver = null;
            logger.Debug("AudioNoisyReceiver unregistered");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error unregistering AudioNoisyReceiver");
            noisyReceiverRegistered = false;
            noisyReceiver = null;
        }
    }
}

