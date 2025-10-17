using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Services.Contracts;
using NLog;
using System.Threading;

namespace Bible.Alarm.Services.Media
{
    public class MediaElementAudioService : IMediaElementAudioService
    {
        private static readonly Lazy<Logger> LazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger Logger => LazyLogger.Value;

        private MediaElement _mediaElement;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1);
        
        private bool _isPlaying = false;
        private long _currentScheduleId;
        private int _currentTrackIndex = 0;
        private TimeSpan _currentTrackPosition = TimeSpan.Zero;
        private List<string> _currentPlaylist = new List<string>();
        private bool _isPrepared = false;

        public TimeSpan CurrentTrackPosition => _currentTrackPosition;
        public int CurrentTrackIndex => _currentTrackIndex;
        public long CurrentlyPlayingScheduleId => _currentScheduleId;
        public bool IsPlaying => _isPlaying;
        public bool IsPrepared => _isPrepared;

        public MediaElementAudioService()
        {
            // Initialize MediaElement
            _mediaElement = new MediaElement
            {
                ShouldAutoPlay = false,
                ShouldLoopPlayback = false,
                ShouldShowPlaybackControls = false
            };

            // Subscribe to events
            _mediaElement.MediaOpened += OnMediaOpened;
            _mediaElement.MediaEnded += OnMediaEnded;
            _mediaElement.MediaFailed += OnMediaFailed;
            _mediaElement.PositionChanged += OnPositionChanged;
        }

        public async Task PrepareRelavantPlaylist()
        {
            await _lock.WaitAsync();
            try
            {
                // This method would be implemented based on your playlist logic
                // For now, we'll just mark as prepared
                _isPrepared = true;
                Logger.Info("Playlist prepared");
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task Play()
        {
            await _lock.WaitAsync();
            try
            {
                if (!_isPrepared)
                {
                    throw new InvalidOperationException("Cannot play without preparing.");
                }

                if (_mediaElement != null)
                {
                    _mediaElement.Play();
                    _isPlaying = true;
                    Logger.Info("Playback started");
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task PrepareAndPlay(long scheduleId, bool isImmediate)
        {
            await _lock.WaitAsync();
            try
            {
                _currentScheduleId = scheduleId;
                _isPrepared = true;
                
                if (isImmediate)
                {
                    await Play();
                }
                
                Logger.Info($"Prepared and {(isImmediate ? "playing" : "ready to play")} schedule {scheduleId}");
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task Dismiss()
        {
            await _lock.WaitAsync();
            try
            {
                if (_mediaElement != null)
                {
                    _mediaElement.Stop();
                }
                
                _isPlaying = false;
                _isPrepared = false;
                _currentScheduleId = 0;
                _currentTrackIndex = 0;
                _currentTrackPosition = TimeSpan.Zero;
                _currentPlaylist.Clear();
                
                Logger.Info("Playback dismissed");
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SetSource(string source)
        {
            await _lock.WaitAsync();
            try
            {
                if (_mediaElement != null)
                {
                    _mediaElement.Source = source;
                    Logger.Info($"Media source set to: {source}");
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task Pause()
        {
            await _lock.WaitAsync();
            try
            {
                if (_mediaElement != null)
                {
                    _mediaElement.Pause();
                    _isPlaying = false;
                    Logger.Info("Playback paused");
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task Stop()
        {
            await _lock.WaitAsync();
            try
            {
                if (_mediaElement != null)
                {
                    _mediaElement.Stop();
                    _isPlaying = false;
                    _currentTrackPosition = TimeSpan.Zero;
                    Logger.Info("Playback stopped");
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SeekTo(TimeSpan position)
        {
            await _lock.WaitAsync();
            try
            {
                if (_mediaElement != null)
                {
                    // MediaElement doesn't support direct position setting
                    // This would need to be implemented differently for seeking
                    _currentTrackPosition = position;
                    Logger.Info($"Seeked to position: {position}");
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        private void OnMediaOpened(object sender, EventArgs e)
        {
            Logger.Info("Media opened successfully");
        }

        private void OnMediaEnded(object sender, EventArgs e)
        {
            Logger.Info("Media playback ended");
            _isPlaying = false;
            
            // Handle next track logic here if needed
            // This would depend on your playlist implementation
        }

        private void OnMediaFailed(object sender, EventArgs e)
        {
            Logger.Error("Media playback failed");
            _isPlaying = false;
        }

        private void OnPositionChanged(object sender, EventArgs e)
        {
            if (_mediaElement != null)
            {
                _currentTrackPosition = _mediaElement.Position;
            }
        }

        public async Task PrepareRelevantPlaylist()
        {
            await _lock.WaitAsync();
            try
            {
                // Implementation for preparing relevant playlist
                Logger.Info("Preparing relevant playlist");
            }
            finally
            {
                _lock.Release();
            }
        }


        public void Dispose()
        {
            _mediaElement?.Dispose();
            _lock?.Dispose();
        }
    }
}
