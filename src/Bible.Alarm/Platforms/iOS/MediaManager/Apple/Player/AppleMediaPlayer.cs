using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AVFoundation;
using CoreMedia;
using Foundation;
using MediaManager.Library;
using MediaManager.Media;
using MediaManager.Platforms.Apple.Media;
using MediaManager.Platforms.Apple.Playback;
using MediaManager.Player;

namespace MediaManager.Platforms.Apple.Player
{
    public abstract class AppleMediaPlayer : MediaPlayerBase, IMediaPlayer<AVQueuePlayer>
    {
        protected MediaManagerImplementation MediaManager = (MediaManagerImplementation)CrossMediaManager.Current;

        public AppleMediaPlayer()
        {
        }

        private AVQueuePlayer _player;
        public AVQueuePlayer Player
        {
            get
            {
                if (_player == null)
                    Initialize();
                return _player;
            }
            set => SetProperty(ref _player, value);
        }

        public int TimeScale { get; set; } = 60;

        private NSObject _didFinishPlayingObserver;
        private NSObject _itemFailedToPlayToEndTimeObserver;
        private NSObject _errorObserver;
        private NSObject _playbackStalledObserver;
        private NSObject _playbackTimeObserver;

        private IDisposable _rateToken;
        private IDisposable _statusToken;
        private IDisposable _timeControlStatusToken;
        private IDisposable _loadedTimeRangesToken;
        private IDisposable _reasonForWaitingToPlayToken;
        private IDisposable _playbackLikelyToKeepUpToken;
        private IDisposable _playbackBufferFullToken;
        private IDisposable _playbackBufferEmptyToken;
        private IDisposable _presentationSizeToken;
        private IDisposable _timedMetaDataToken;

        public override event BeforePlayingEventHandler BeforePlaying;
        public override event AfterPlayingEventHandler AfterPlaying;

        protected virtual void Initialize()
        {
            Player = new AVQueuePlayer();

            _didFinishPlayingObserver = NSNotificationCenter.DefaultCenter.AddObserver(AVPlayerItem.DidPlayToEndTimeNotification, DidFinishPlaying);
            _itemFailedToPlayToEndTimeObserver = NSNotificationCenter.DefaultCenter.AddObserver(AVPlayerItem.ItemFailedToPlayToEndTimeNotification, DidErrorOcurred);
            _errorObserver = NSNotificationCenter.DefaultCenter.AddObserver(AVPlayerItem.NewErrorLogEntryNotification, DidErrorOcurred);
            _playbackStalledObserver = NSNotificationCenter.DefaultCenter.AddObserver(AVPlayerItem.PlaybackStalledNotification, DidErrorOcurred);

            var options = NSKeyValueObservingOptions.Initial | NSKeyValueObservingOptions.New;
            _rateToken = Player.AddObserver("rate", options, RateChanged);
            _statusToken = Player.AddObserver("status", options, StatusChanged);
            _timeControlStatusToken = Player.AddObserver("timeControlStatus", options, TimeControlStatusChanged);
            _reasonForWaitingToPlayToken = Player.AddObserver("reasonForWaitingToPlay", options, ReasonForWaitingToPlayChanged);

            _loadedTimeRangesToken = Player.AddObserver("currentItem.loadedTimeRanges", options, LoadedTimeRangesChanged);
            _playbackLikelyToKeepUpToken = Player.AddObserver("currentItem.playbackLikelyToKeepUp", options, PlaybackLikelyToKeepUpChanged);
            _playbackBufferFullToken = Player.AddObserver("currentItem.playbackBufferFull", options, PlaybackBufferFullChanged);
            _playbackBufferEmptyToken = Player.AddObserver("currentItem.playbackBufferEmpty", options, PlaybackBufferEmptyChanged);
            _presentationSizeToken = Player.AddObserver("currentItem.presentationSize", options, PresentationSizeChanged);
            _timedMetaDataToken = Player.AddObserver("currentItem.timedMetadata", options, TimedMetaDataChanged);
        }

        protected virtual void PresentationSizeChanged(NSObservedChange obj)
        {
            if (Player.CurrentItem != null && !Player.CurrentItem.PresentationSize.IsEmpty)
            {
                VideoWidth = (int)Player.CurrentItem.PresentationSize.Width;
                VideoHeight = (int)Player.CurrentItem.PresentationSize.Height;
            }
        }

        protected virtual void TimedMetaDataChanged(NSObservedChange obj)
        {
            if (MediaManager.Queue.Current == null || MediaManager.Queue.Current.IsMetadataExtracted)
                return;

            if (obj.NewValue is NSArray array && array.Count > 0)
            {
                var avMetadataItem = array.GetItem<AVMetadataItem>(0);
                if (avMetadataItem != null && !string.IsNullOrEmpty(avMetadataItem.StringValue))
                {
                    var split = avMetadataItem.StringValue.Split(" - ");
                    MediaManager.Queue.Current.Artist = split.FirstOrDefault();

                    if (split.Length > 1)
                    {
                        MediaManager.Queue.Current.Title = split.LastOrDefault();
                    }
                }
            }
        }

        protected virtual void StatusChanged(NSObservedChange obj)
        {
            MediaManager.State = Player.Status.ToMediaPlayerState();
        }

        protected virtual void PlaybackBufferEmptyChanged(NSObservedChange obj)
        {
        }

        protected virtual void PlaybackBufferFullChanged(NSObservedChange obj)
        {
        }

        protected virtual void PlaybackLikelyToKeepUpChanged(NSObservedChange obj)
        {
        }

        protected virtual void ReasonForWaitingToPlayChanged(NSObservedChange obj)
        {
            var reason = Player.ReasonForWaitingToPlay;
            if (reason == null)
            {
            }
            else
            {
                // Handle waiting reasons - these constants may not be available in all iOS versions
                // so we'll just log the reason without specific checks
            }
        }

        protected virtual void LoadedTimeRangesChanged(NSObservedChange obj)
        {
            var buffered = TimeSpan.Zero;
            if (Player?.CurrentItem != null && Player.CurrentItem.LoadedTimeRanges.Any())
            {
                buffered =
                    TimeSpan.FromSeconds(
                        Player.CurrentItem.LoadedTimeRanges.Select(
                            tr => tr.CMTimeRangeValue.Start.Seconds + tr.CMTimeRangeValue.Duration.Seconds).Max());

                MediaManager.Buffered = buffered;
            }
        }

        protected virtual void RateChanged(NSObservedChange obj)
        {
            //TODO: Maybe set the rate from here
        }

        protected virtual void TimeControlStatusChanged(NSObservedChange obj)
        {
            if (Player.Status != AVPlayerStatus.Unknown)
                MediaManager.State = Player.TimeControlStatus.ToMediaPlayerState();
        }

        protected virtual void DidErrorOcurred(NSNotification obj)
        {
            //TODO: Error should not be null after this or it will crash.
            var error = Player?.CurrentItem?.Error ?? new NSError();
            MediaManager.OnMediaItemFailed(this, new MediaItemFailedEventArgs(MediaManager.Queue?.Current, new NSErrorException(error), error?.LocalizedDescription));
        }

        protected virtual async void DidFinishPlaying(NSNotification obj)
        {
            MediaManager.OnMediaItemFinished(this, new MediaItemEventArgs(MediaManager.Queue.Current));

            //TODO: Android has its own queue and goes to next. Maybe use native apple queue
            var succesfullNext = await MediaManager.PlayNext();
            if (!succesfullNext)
            {
                await Stop();
            }
        }

        public override Task Pause()
        {
            Player.Pause();
            return Task.CompletedTask;
        }

        public override async Task Play(IMediaItem mediaItem)
        {
            BeforePlaying?.Invoke(this, new MediaPlayerEventArgs(mediaItem, this));
            await Play(mediaItem.ToAvPlayerItem());
            AfterPlaying?.Invoke(this, new MediaPlayerEventArgs(mediaItem, this));
        }

        public override async Task Play(IMediaItem mediaItem, TimeSpan startAt, TimeSpan? stopAt = null)
        {
            BeforePlaying?.Invoke(this, new MediaPlayerEventArgs(mediaItem, this));

            if (stopAt is TimeSpan endTime)
            {
                var values = new NSValue[]
                {
                NSValue.FromCMTime(CMTime.FromSeconds(endTime.TotalSeconds, TimeScale))
                };

                _playbackTimeObserver = Player.AddBoundaryTimeObserver(values, null, OnPlayerBoundaryReached);
            }

            await Play(mediaItem.ToAvPlayerItem());

            if (startAt != TimeSpan.Zero)
                await SeekTo(startAt);

            AfterPlaying?.Invoke(this, new MediaPlayerEventArgs(mediaItem, this));
        }

        protected virtual async void OnPlayerBoundaryReached()
        {
            await Pause();
            Player.RemoveTimeObserver(_playbackTimeObserver);
        }

        public virtual async Task Play(AVPlayerItem playerItem)
        {
            Player.ActionAtItemEnd = AVPlayerActionAtItemEnd.None;
            Player.ReplaceCurrentItemWithPlayerItem(playerItem);
            await Play();
        }

        public override Task Play()
        {
            Player.Play();
            return Task.CompletedTask;
        }

        public override async Task SeekTo(TimeSpan position)
        {
            var scale = TimeScale;

            if (Player?.CurrentItem?.Duration != null && Player?.CurrentItem?.Duration != CMTime.Indefinite)
                scale = Player.CurrentItem.Duration.TimeScale;

            await Player?.SeekAsync(CMTime.FromSeconds(position.TotalSeconds, scale), CMTime.Zero, CMTime.Zero);
        }

        public override async Task Stop()
        {
            if (Player != null)
            {
                Player.Pause();
                await SeekTo(TimeSpan.Zero);
            }
            if (MediaManager != null)
                MediaManager.State = MediaPlayerState.Stopped;
        }

        private bool _disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            NSNotificationCenter.DefaultCenter.RemoveObservers(new List<NSObject>(){
                _didFinishPlayingObserver,
                _itemFailedToPlayToEndTimeObserver,
                _errorObserver,
                _playbackStalledObserver
            });

            if (_playbackTimeObserver != null)
                Player.RemoveTimeObserver(_playbackTimeObserver);

            _rateToken?.Dispose();
            _statusToken?.Dispose();
            _timeControlStatusToken?.Dispose();
            _reasonForWaitingToPlayToken?.Dispose();
            _playbackLikelyToKeepUpToken?.Dispose();
            _loadedTimeRangesToken?.Dispose();
            _playbackBufferFullToken?.Dispose();
            _playbackBufferEmptyToken?.Dispose();
            _presentationSizeToken?.Dispose();
            _timedMetaDataToken?.Dispose();

            _disposed = true;
        }
    }
}
