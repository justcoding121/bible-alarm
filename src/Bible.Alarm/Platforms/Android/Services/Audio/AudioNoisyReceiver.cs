#nullable enable
using Android.Content;
using Android.Media;
using Bible.Alarm.Common.Messenger;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Audio;

/// <summary>
/// Pauses playback when the audio output route changes (e.g. Bluetooth disconnects).
/// Without this, audio silently continues through the phone speaker after leaving
/// the car, tracks finish and advance, and the saved progress position is lost.
/// </summary>
public sealed class AudioNoisyReceiver : BroadcastReceiver
{
    private static readonly ILogger logger = Log.ForContext<AudioNoisyReceiver>();

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action != AudioManager.ActionAudioBecomingNoisy)
        {
            return;
        }

        logger.Information("Audio becoming noisy (output route changed, e.g. Bluetooth disconnected) — pausing playback");
        WeakReferenceMessenger.Default.Send(new PauseButtonPressedMessage());
    }
}
