namespace Bible.Alarm.Services.UI.Interfaces;

public interface IAlarmModalService : IDisposable
{
    void SubscribeToPlaybackStateChanges();
    void UnsubscribeToPlaybackStateChanges();
}
