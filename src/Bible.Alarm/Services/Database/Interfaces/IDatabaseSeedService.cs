namespace Bible.Alarm.Services.Database.Interfaces;

public interface IDatabaseSeedService : IDisposable
{
    /// <summary>
    /// Seeds a default alarm schedule if the database is empty.
    /// </summary>
    /// <returns>True if a schedule was seeded, false if schedules already existed.</returns>
    Task<bool> SeedDefaultAlarmAsync();
}

