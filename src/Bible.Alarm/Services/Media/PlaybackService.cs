using Bible.Alarm.Services.Contracts;
using NLog;
using System;
using System.Threading.Tasks;

namespace Bible.Alarm.Services.Media
{
    public class PlaybackService : IPlaybackService
    {
        private static readonly Lazy<Logger> LazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger Logger => LazyLogger.Value;

        private readonly IMediaElementAudioService _mediaElementService;

        public PlaybackService(IMediaElementAudioService mediaElementService)
        {
            _mediaElementService = mediaElementService;
        }

        public TimeSpan CurrentTrackPosition => _mediaElementService.CurrentTrackPosition;
        public int CurrentTrackIndex => _mediaElementService.CurrentTrackIndex;
        public long CurrentlyPlayingScheduleId => _mediaElementService.CurrentlyPlayingScheduleId;
        public bool IsPlaying => _mediaElementService.IsPlaying;
        public bool IsPrepared => _mediaElementService.IsPrepared;

        public async Task PrepareRelevantPlaylist()
        {
            await _mediaElementService.PrepareRelevantPlaylist();
        }

        public async Task PrepareRelavantPlaylist()
        {
            await _mediaElementService.PrepareRelavantPlaylist();
        }

        public async Task Play()
        {
            await _mediaElementService.Play();
        }

        public async Task PrepareAndPlay(long scheduleId, bool isImmediate)
        {
            await _mediaElementService.PrepareAndPlay(scheduleId, isImmediate);
        }

        public async Task Dismiss()
        {
            await _mediaElementService.Dismiss();
        }
    }
}
