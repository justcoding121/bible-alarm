namespace Bible.Alarm.Services.Database.Interfaces;

public interface IScheduleMigrationService : IDisposable
{
    Task MigrateBibleGatewaySchedulesAsync();
}

