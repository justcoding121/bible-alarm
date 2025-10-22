namespace Bible.Alarm.Contracts.Media;

public interface IPreviewPlayService : IDisposable
{
    Task Play(string url);
    void Stop();
    event Action OnStopped;
}