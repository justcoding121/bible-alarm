#nullable enable

using Bible.Alarm.Services.Media.Audio;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Primitives;

#if ANDROID
using Bible.Alarm.Platforms.Android.Services.Audio.Interfaces;
using Microsoft.Extensions.DependencyInjection;
#endif
#if IOS
using Bible.Alarm.Platforms.iOS.Helpers;
#endif
using Serilog;

namespace Bible.Alarm.Services.Media.AudioPlayerHelpers;

/// <summary>
/// Handles playback operations for AudioPlayer.
/// </summary>
public class PlaybackController
{
    private readonly ILogger logger;
    private readonly AudioPlayerStateManager stateManager;
    private readonly Func<MediaElement?> getMediaElement;

    public PlaybackController(ILogger logger, AudioPlayerStateManager stateManager, Func<MediaElement?> getMediaElement)
    {
        this.logger = logger;
        this.stateManager = stateManager;
        this.getMediaElement = getMediaElement;
    }

    public async Task PlayAsync()
    {
#if IOS
        var mediaElement = getMediaElement();
        if (mediaElement == null)
        {
            return;
        }

        await IosMediaElementHelper.ConfigureAudioSessionBeforePlayAsync(logger);
        var currentState = await IosMediaElementHelper.GetCurrentStateAsync(mediaElement, logger);
        await IosMediaElementHelper.HandlePausedStateAsync(mediaElement, currentState, logger);
        await IosMediaElementHelper.InvokePlayAsync(mediaElement, logger);

        // Wait briefly and check if playback started
        await Task.Delay(100);

        var stateAfterPlay = await IosMediaElementHelper.GetCurrentStateAsync(mediaElement, logger);
        await IosMediaElementHelper.RetryPlayIfNeededAsync(mediaElement, stateAfterPlay, logger);
#else
#if ANDROID
        // Request audio focus proactively before play so we own the audio output
        // immediately when the media starts. The Fluxor AudioFocusEffect also requests
        // focus reactively on PlayStatus.Playing as a safety net.
        // Resolved on-demand (not via constructor) to avoid a circular DI dependency:
        // AudioFocusEffect -> IAudioFocusService -> IAudioFocusListener -> IPlaybackService -> IAudioPlayer -> IAudioFocusService
        try
        {
            var audioFocusService = IPlatformApplication.Current?.Services?.GetService<IAudioFocusService>();
            audioFocusService?.RequestAudioFocus();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to request audio focus before play");
        }
#endif
        await InvokePlayOnMainThreadAsync();

        // Wait briefly and check if playback started
        await Task.Delay(100);

#endif
    }

    private async Task InvokePlayOnMainThreadAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() => getMediaElement()?.Play());
    }

    public Task PauseAsync() => MainThread.InvokeOnMainThreadAsync(() => getMediaElement()?.Pause());

    public Task SetMutedAsync(bool muted) => MainThread.InvokeOnMainThreadAsync(() =>
    {
        var mediaElement = getMediaElement();
        if (mediaElement != null)
        {
            mediaElement.ShouldMute = muted;
        }
    });

    public Task ResumeAsync() => MainThread.InvokeOnMainThreadAsync(() => getMediaElement()?.Play());

    public Task StopAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            var mediaElement = getMediaElement();
            if (mediaElement == null)
            {
                return;
            }

            try
            {
                mediaElement.Stop();
            }
            catch (InvalidOperationException)
            {
                // On iOS, Stop() internally tries to seek to zero, which can fail if the player isn't ready
            }
            catch (Exception ex)
            {
                // Catch any other exceptions
                logger.Warning(ex, "Exception occurred while stopping MediaElement");
            }
        });
    }

    public async Task SeekToAsync(TimeSpan position)
    {
        stateManager.StartSeeking();

        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var mediaElement = getMediaElement();
                if (mediaElement == null)
                {
                    stateManager.EndSeeking();
                    return;
                }

                try
                {
                    // Start fallback timer to reset seeking flag if SeekCompleted doesn't fire
                    _ = ResetSeekingFlagWithTimeoutAsync();

                    await mediaElement.SeekTo(position);
                }
                catch (InvalidOperationException ex)
                {
                    // Seek failed (e.g., position outside seekable ranges, player not ready)
                    logger.Warning(ex, "[AudioPlayer] SeekTo failed - {Message}", ex.Message);
                    stateManager.EndSeeking();
                }
            });
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[AudioPlayer] Error during SeekToAsync");
            stateManager.EndSeeking();
        }
    }

    private async Task ResetSeekingFlagWithTimeoutAsync()
    {
        // Fallback: If SeekCompleted doesn't fire within 3 seconds, reset the flag anyway
        await Task.Delay(3000);
        if (stateManager.IsSeeking)
        {
            logger.Warning("[AudioPlayer] SeekCompleted event did not fire within 3 seconds - resetting _isSeeking flag as fallback");
            stateManager.EndSeeking();
        }
    }
}

