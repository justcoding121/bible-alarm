#nullable enable
using Bible.Alarm.Platforms.Android.Services.Audio;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores.Actions.Playback;
using Fluxor;
using Serilog;
using FluxorDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Platforms.Android.Effects;

/// <summary>
/// Fluxor effect that manages audio focus based on playback state.
/// Requests audio focus when playback starts (Playing) and releases it when playback stops (Stopped/Ended).
/// This is global and not specific to Android Auto.
/// </summary>
public class AudioFocusEffect(AudioFocusService audioFocusService)
{
    private static readonly ILogger logger = Log.ForContext<AudioFocusEffect>();
    private readonly AudioFocusService audioFocusService = audioFocusService ?? throw new ArgumentNullException(nameof(audioFocusService));

    [EffectMethod]
    public Task HandlePlaybackStatusChanged(PlaybackStatusChangedAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            if (action.Status == PlayStatus.Playing)
            {
                audioFocusService.RequestAudioFocus();
            }
            else if (action.Status == PlayStatus.Stopped || action.Status == PlayStatus.Ended)
            {
                audioFocusService.ReleaseAudioFocus();
            }
            // Note: We keep audio focus when Paused to allow quick resume
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error managing audio focus for playback status: {Status}", action.Status);
        }

        return Task.CompletedTask;
    }
}

