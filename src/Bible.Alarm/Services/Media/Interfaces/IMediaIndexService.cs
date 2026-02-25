namespace Bible.Alarm.Services.Media.Interfaces;

public interface IMediaIndexService : IDisposable
{
    Task Verify();
    string IndexRoot { get; }

    /// <summary>
    /// True when the media index was replaced this bootstrap run (version change).
    /// Use to run non-English fetch only on version change.
    /// </summary>
    bool WasIndexReplacedThisRun { get; }

    /// <summary>
    /// On version update (old media index was replaced by new packaged index), fetches missing
    /// non-English publication/section data for all schedules into the new media index, then
    /// cleans up orphaned schedule references. Must be called after resource bootstrap so
    /// the new media index is extracted and the schedule DB is readable.
    /// </summary>
    Task MigrateNonEnglishDataIfNeededAsync();
}
