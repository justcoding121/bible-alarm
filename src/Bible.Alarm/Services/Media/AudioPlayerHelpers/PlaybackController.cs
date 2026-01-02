#nullable enable

using Bible.Alarm.Services.Media.Audio;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Essentials;
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
        // Check state using string comparison since helper returns object
        var stateString = stateAfterPlay.ToString();
        if (stateString is "Playing" or "Buffering")
        {
            logger.Debug("MediaElement is in {State} state after Play()", stateAfterPlay);
        }
        await IosMediaElementHelper.RetryPlayIfNeededAsync(mediaElement, stateAfterPlay, logger);
#else
        await InvokePlayOnMainThreadAsync();

        // Wait briefly and check if playback started
        await Task.Delay(100);

        var stateAfterPlay = await MainThread.InvokeOnMainThreadAsync(() => getMediaElement()?.CurrentState ?? MediaElementState.None);
        logger.Debug("After Play() call, MediaElement state: {State}", stateAfterPlay);
#endif
    }

    private async Task InvokePlayOnMainThreadAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var mediaElement = getMediaElement();
            logger.Debug("About to call MediaElement.Play(). Current state: {State}, Source: {Source}",
                mediaElement?.CurrentState ?? MediaElementState.None,
                mediaElement?.Source?.ToString() ?? "null");

            mediaElement?.Play();
        });
    }

    public Task PauseAsync() => MainThread.InvokeOnMainThreadAsync(() => getMediaElement()?.Pause());

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
            catch (InvalidOperationException ex)
            {
                // On iOS, Stop() internally tries to seek to zero, which can fail if the player isn't ready
                // Log and continue - the player will be in a stopped state anyway
                logger.Debug(ex, "Stop() failed because player isn't ready to seek, but player should be stopped");
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
        // Track that we're seeking to prevent state flickering during seek
        stateManager.StartSeeking();

        logger.Debug("[AudioPlayer] SeekToAsync called - Position: {Position}, StatusBeforeSeek: {Status}, Setting _isSeeking = true",
            position, stateManager.Status);

        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var mediaElement = getMediaElement();
                if (mediaElement == null)
                {
                    logger.Warning("[AudioPlayer] MediaElement is null, cannot seek");
                    stateManager.EndSeeking();
                    return;
                }

                logger.Debug("[AudioPlayer] About to call MediaElement.SeekTo({Position})", position);

                try
                {
                    // Start fallback timer to reset seeking flag if SeekCompleted doesn't fire
                    _ = ResetSeekingFlagWithTimeoutAsync();

                    // Await the seek operation - it will complete when SeekCompleted event fires
                    await mediaElement.SeekTo(position);
                    logger.Debug("[AudioPlayer] MediaElement.SeekTo() completed successfully");
                    // Note: _isSeeking will be reset in OnSeekCompleted when seek actually finishes
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

