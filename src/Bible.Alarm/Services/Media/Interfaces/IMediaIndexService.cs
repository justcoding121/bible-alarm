namespace Bible.Alarm.Services.Media.Interfaces;

public interface IMediaIndexService : IDisposable
{
    Task Verify();
    string IndexRoot { get; }
}
