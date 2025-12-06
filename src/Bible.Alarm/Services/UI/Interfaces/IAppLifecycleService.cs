namespace Bible.Alarm.Services.UI.Interfaces;

public interface IAppLifecycleService : IDisposable
{
    void OnStart();
    void OnResume();
}
