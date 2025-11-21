#nullable enable
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IAudioPlayer : IDisposable
{
    Task PrepareAsync(AudioPlayerTrack track);

    Task PlayAsync();

    Task PauseAsync();

    Task ResumeAsync();

    Task StopAsync();

    Task ResetAsync();

    Task SeekToAsync(TimeSpan position);

    TimeSpan? CurrentPosition { get; }

    TimeSpan Duration { get; }

    PlayStatus Status { get; }

    event EventHandler<EventArgs>? MediaEnded;

    event EventHandler<EventArgs>? MediaFailed;
}

