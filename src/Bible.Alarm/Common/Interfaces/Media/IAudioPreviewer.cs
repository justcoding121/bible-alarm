namespace Bible.Alarm.Common.Interfaces.Media;

public interface IAudioPreviewer : IDisposable
{
    Task Play(string url);
    void Stop();
    event Action OnStopped;
}

