namespace Bible.Alarm.Services.Media.Interfaces;

public interface IMediaIndexService : IDisposable
{
    Task Verify();
    string IndexRoot { get; }

    /// <summary>
    /// Migrates non-English publication data from old media index to new packaged media index.
    /// Must be called after both database bootstrap and resource bootstrap complete,
    /// so the schedule DB is readable and the new media index is extracted.
    /// </summary>
    Task MigrateNonEnglishDataIfNeededAsync();
}
