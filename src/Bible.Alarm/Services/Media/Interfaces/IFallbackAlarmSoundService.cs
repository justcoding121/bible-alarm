#nullable enable
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IFallbackAlarmSoundService : IDisposable
{
    Task<AudioPlayerTrack?> GetFallbackAlarmTrackAsync();
}

