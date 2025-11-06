namespace Bible.Alarm.Common.Interfaces.Media;

public interface IPreviewPlayService : IDisposable
{
    Task Play(string url);
    void Stop();
    event Action OnStopped;
}