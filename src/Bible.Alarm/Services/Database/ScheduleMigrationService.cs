using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Database;

public class ScheduleMigrationService(
    ILogger logger,
    IAlarmScheduleService alarmScheduleService)
    : IScheduleMigrationService, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    public async Task MigrateBibleGatewaySchedulesAsync()
    {
        if (DeviceInfo.Platform != DevicePlatform.Android)
        {
            return;
        }

        try
        {
            var alarmSchedules = await alarmScheduleService.GetSchedulesAsync(
                x => x.BibleReadingSchedule != null,
                false,
                true,
                cancellationTokenSource.Token);

            //bible gateway is not supported anymore due to copyright issues
            var toRemove = alarmSchedules.Where(x =>
                BgSourceHelper.PublicationCodeToNameMappings.Any(y => y.Key == x.BibleReadingSchedule!.PublicationCode)).ToList();

            if (toRemove.Any())
            {
                foreach (var item in toRemove)
                {
                    await alarmScheduleService.UpdateScheduleByIdAsync(
                        item.Id,
                        schedule =>
                        {
                            if (schedule.BibleReadingSchedule != null)
                            {
                                schedule.BibleReadingSchedule.PublicationCode = "nwt"; // NWT 2013 (not 1984)
                                schedule.BibleReadingSchedule.FinishedDuration = TimeSpan.Zero;
                            }
                        },
                        cancellationTokenSource.Token);
                }

                logger.Information("Migrated {Count} Bible Gateway schedules to default publication code", toRemove.Count);
            }
        }
        catch (Exception e)
        {
            logger.Error(e, "An error happened while migrating Bible Gateway schedules.");
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // Cancel and dispose cancellation token source
        try
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            logger.Warning(ex, "Error during cancellation token source disposal");
        }

        // IServiceScopeFactory is a singleton, so don't dispose it
        // No event handlers to unsubscribe
    }
}

