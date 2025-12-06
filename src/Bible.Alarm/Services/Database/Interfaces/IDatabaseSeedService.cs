namespace Bible.Alarm.Services.Database.Interfaces;

public interface IDatabaseSeedService : IDisposable
{
    Task SeedDefaultAlarmAsync();
}

