namespace Bible.Alarm.Services.UI.Interfaces;

public interface IExceptionHandlingService : IDisposable
{
    void SetupGlobalExceptionHandlers();
}
