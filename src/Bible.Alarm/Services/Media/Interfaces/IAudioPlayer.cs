#nullable enable
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IAudioPlayer : IDisposable
{
    Task PrepareAsync(AudioPlayerTrack track, bool isFirstTrack = false, bool isLastTrack = false);

    Task PlayAsync();

    Task PauseAsync();

    Task ResumeAsync();

    Task StopAsync();

    Task ResetAsync();

    Task SeekToAsync(TimeSpan position);

    /// <summary>
    /// Syncs playback metadata for the given track to Fluxor/MediaSession (e.g. so Android Auto Now Playing shows correct title after track change).
    /// </summary>
    Task SyncMetadataForTrackAsync(AudioPlayerTrack track);

    TimeSpan? CurrentPosition { get; }

    TimeSpan Duration { get; }

    PlayStatus Status { get; }

    /// <summary>
    /// Gets the actual current state of the MediaElement, not just the cached Status
    /// This checks the MediaElement's CurrentState property directly
    /// </summary>
    bool IsActuallyPlayingOrPaused { get; }

    event EventHandler<EventArgs>? MediaEnded;

    event EventHandler<EventArgs>? MediaFailed;
}

