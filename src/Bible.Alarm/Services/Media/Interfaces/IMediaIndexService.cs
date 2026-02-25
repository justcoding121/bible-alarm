namespace Bible.Alarm.Services.Media.Interfaces;

public interface IMediaIndexService : IDisposable
{
    Task Verify();
    string IndexRoot { get; }

    /// <summary>
    /// True when the media index was replaced this bootstrap run (version change).
    /// Use to run orphan cleanup and non-EnglishSpanish fetch only on version change.
    /// </summary>
    bool WasIndexReplacedThisRun { get; }

    /// <summary>
    /// Runs only on version change (new media index was copied and overwritten). Runs ad-hoc
    /// non-EnglishSpanish fetch first, then cleans up orphaned schedule/alarm music by comparing
    /// only against fetched (harvested) tables. Must be called after resource bootstrap.
    /// </summary>
    Task MigrateNonEnglishDataIfNeededAsync();
}
