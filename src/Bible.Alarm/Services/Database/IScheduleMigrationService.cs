namespace Bible.Alarm.Services.Database;

public interface IScheduleMigrationService
{
    Task MigrateBibleGatewaySchedulesAsync();
}

