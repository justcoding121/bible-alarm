#nullable enable
using System.IO;
using AVFoundation;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Platforms.iOS.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Foundation;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.Services.Media
{
    public class iOSAudioPreviewer(IDownloadService downloadService, ILogger logger)
        : IAudioPreviewer, IDisposable
    {
        private AVAudioPlayer? _player;
        private readonly IDownloadService _downloadService = downloadService;
        private readonly ILogger _logger = logger;
        private bool _disposed;

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
                var bytes = await _downloadService.DownloadAsync(url);
                using var stream = new MemoryStream(bytes);
                var data = NSData.FromStream(stream);
                if (data == null)
                {
                    _logger.Error("Failed to create NSData from stream for URL: {Url}", url);
                    return false;
                }
                _player = AVAudioPlayer.FromData(data);

                return PreparePlayer();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error loading preview audio from URL: {Url}", url);
                return false;
            }
        }

        private bool PreparePlayer()
        {
            try
            {
                if (_player != null)
                {
                    _player.FinishedPlaying += OnPlaybackEnded;
                    _player.PrepareToPlay();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error preparing AVAudioPlayer");
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
                    if (_player == null)
                    {
                        _logger.Warning("AVAudioPlayer is null after loading, cannot play");
                        return;
                    }

                    if (_player.Playing)
                    {
                        _player.CurrentTime = 0;
                    }
                    else
                    {
                        _player.Play();
                        _logger.Debug("AVAudioPlayer.Play() called. Playing: {Playing}, Volume: {Volume}", 
                            _player.Playing, 
                            _player.Volume);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error playing preview audio from URL: {Url}", url);
                throw;
            }
        }

        private void ConfigureAudioSession()
        {
            iOSAudioSessionHelper.ConfigureAudioSession(_logger, "preview");
        }

        public void Stop()
        {
            try
            {
                _player?.Stop();
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Error stopping AVAudioPlayer, player may already be disposed");
            }
        }

        private void DeletePlayer()
        {
            try
            {
                Stop();

                if (_player != null)
                {
                    _player.FinishedPlaying -= OnPlaybackEnded;
                    _player.Dispose();
                    _player = null;
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Error deleting AVAudioPlayer, player may already be disposed");
            }
        }

        private void OnPlaybackEnded(object? sender, AVStatusEventArgs e)
        {
            OnStopped?.Invoke();
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                DeletePlayer();
                // Note: AVAudioPlayer is created from data for each track (unlike Android/Windows MediaPlayer)
                // The player is disposed in DeletePlayer() when switching tracks or on disposal
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Error disposing iOSAudioPreviewer");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}