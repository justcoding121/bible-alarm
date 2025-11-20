namespace Bible.Alarm.Services.Media.Interfaces;

public interface IMediaElementAudioService
{
    event EventHandler<EventArgs> MediaEnded;
    event EventHandler<EventArgs> MediaFailed;

    // Audio properties
    TimeSpan CurrentTrackPosition { get; }
    bool IsPlaying { get; }
    bool IsPrepared { get; }

    // Audio operations
    Task SetSource(string source);
    Task Play();
    Task Pause();
    Task Stop();
    Task SeekTo(TimeSpan position);
    void Dispose();
}

