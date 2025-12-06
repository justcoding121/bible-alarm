namespace Bible.Alarm.Services.Media.Interfaces;

public interface IMediaIndexService : IDisposable
{
    Task UpdateMediaIndex();
    Task<bool> UpdateIndexIfAvailable();
    Task Verify();
    string IndexRoot { get; }
}