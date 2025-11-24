using Fluxor;

namespace Bible.Alarm.Stores;

[FeatureState]
public class PlaybackState
{
    public int? CurrentScheduleId { get; init; }
    public bool IsPreparingOrPlaying { get; init; }
    public bool CanPlayNext { get; init; }
    public bool CanPlayPrevious { get; init; }

    public PlaybackState()
    {
        CurrentScheduleId = null;
        IsPreparingOrPlaying = false;
        CanPlayNext = false;
        CanPlayPrevious = false;
    }

    public PlaybackState(
        int? currentScheduleId,
        bool isPreparingOrPlaying,
        bool canPlayNext,
        bool canPlayPrevious)
    {
        CurrentScheduleId = currentScheduleId;
        IsPreparingOrPlaying = isPreparingOrPlaying;
        CanPlayNext = canPlayNext;
        CanPlayPrevious = canPlayPrevious;
    }
}

