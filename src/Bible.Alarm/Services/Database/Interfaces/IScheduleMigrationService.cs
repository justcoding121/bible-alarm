namespace Bible.Alarm.Services.Database.Interfaces;

public interface IScheduleMigrationService
{
    Task MigrateBibleGatewaySchedulesAsync();
}

