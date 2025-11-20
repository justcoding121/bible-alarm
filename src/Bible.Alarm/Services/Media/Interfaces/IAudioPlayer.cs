#nullable enable
using Bible.Alarm.Services.Media.Models;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IAudioPlayer : IDisposable
{
    Task PrepareAsync(string uri);

    Task PlayAsync();

    Task PauseAsync();

    Task ResumeAsync();

    Task StopAsync();

    Task SeekToAsync(TimeSpan position);

    TimeSpan? CurrentPosition { get; }

    TimeSpan Duration { get; }

    PlayStatus Status { get; }

    event EventHandler<EventArgs>? MediaEnded;

    event EventHandler<EventArgs>? MediaFailed;

    event EventHandler<MetaData>? MetaDataParsed;
}

