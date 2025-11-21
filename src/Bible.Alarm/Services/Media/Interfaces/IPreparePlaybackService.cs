#nullable enable
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IPreparePlaybackService
{
    Task<List<PreparedTrack>> PrepareTracksAsync(int scheduleId);
}

