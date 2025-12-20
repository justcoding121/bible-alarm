#nullable enable
using AVFoundation;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Platforms.iOS.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Foundation;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.Services.Media;

public class IOsAudioPreviewer(IDownloadService downloadService, ILogger logger)
    : IAudioPreviewer, IDisposable
{
    private AVAudioPlayer? player;
    private readonly IDownloadService downloadService = downloadService;
    private readonly ILogger logger = logger;
    private bool disposed;

    public event Action? OnStopped;

    ///<Summary>
    /// Load wave or mp3 audio file from the iOS assets folder
    ///</Summary>
    private async Task<bool> Load(string url)
    {
        try
        {
            DeletePlayer();

            // Download and create new player instance with the audio data
            // Note: AVAudioPlayer must be created from data, unlike Android MediaPlayer which can be reused
            var bytes = await downloadService.DownloadAsync(url);
            using var stream = new MemoryStream(bytes);
            var data = NSData.FromStream(stream);
            if (data == null)
            {
                logger.Error("Failed to create NSData from stream for URL: {Url}", url);
                return false;
            }
            player = AVAudioPlayer.FromData(data);

            return PreparePlayer();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error loading preview audio from URL: {Url}", url);
            return false;
        }
    }

    private bool PreparePlayer()
    {
        try
        {
            if (player != null)
            {
                player.FinishedPlaying += OnPlaybackEnded;
                player.PrepareToPlay();
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error preparing AVAudioPlayer");
            return false;
        }
    }

    public async Task Play(string url)
    {
        try
        {
            // Configure audio session before playing
            ConfigureAudioSession();

            if (await Load(url))
            {
                if (player == null)
                {
                    logger.Warning("AVAudioPlayer is null after loading, cannot play");
                    return;
                }

                if (player.Playing)
                {
                    player.CurrentTime = 0;
                }
                else
                {
                    player.Play();
                    logger.Debug("AVAudioPlayer.Play() called. Playing: {Playing}, Volume: {Volume}",
                        player.Playing,
                        player.Volume);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error playing preview audio from URL: {Url}", url);
            throw;
        }
    }

    private void ConfigureAudioSession()
    {
        IOsAudioSessionHelper.ConfigureAudioSession(logger, "preview");
    }

    public void Stop()
    {
        try
        {
            player?.Stop();
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Error stopping AVAudioPlayer, player may already be disposed");
        }
    }

    private void DeletePlayer()
    {
        try
        {
            Stop();

            if (player != null)
            {
                player.FinishedPlaying -= OnPlaybackEnded;
                player.Dispose();
                player = null;
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Error deleting AVAudioPlayer, player may already be disposed");
        }
    }

    private void OnPlaybackEnded(object? sender, AVStatusEventArgs e)
    {
        OnStopped?.Invoke();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        try
        {
            DeletePlayer();
            // Note: AVAudioPlayer is created from data for each track (unlike Android/Windows MediaPlayer)
            // The player is disposed in DeletePlayer() when switching tracks or on disposal
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Error disposing iOSAudioPreviewer");
        }
        finally
        {
            disposed = true;
        }
    }
}
