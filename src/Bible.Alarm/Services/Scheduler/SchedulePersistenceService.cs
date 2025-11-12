using Bible.Alarm.Common.Interfaces.Scheduler;
using Bible.Alarm.Database;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Stores.Actions.Schedule;
using Microsoft.EntityFrameworkCore;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Scheduler;

public class SchedulePersistenceService(
    ILogger logger,
    IServiceScopeFactory scopeFactory,
    IAlarmService alarmService,
    IDispatcher dispatcher)
    : ISchedulePersistenceService
{
    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IAlarmService _alarmService = alarmService;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<bool> SaveScheduleAsync(AlarmSchedule schedule, bool isNewSchedule, bool musicUpdated = true, bool bibleReadingUpdated = true)
    {
        try
        {
            AlarmSchedule savedSchedule = null;

            if (isNewSchedule)
            {
                await Task.Run(async () =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
                    await scheduleDbContext.AlarmSchedules.AddAsync(schedule);
                    await scheduleDbContext.SaveChangesAsync();
                    if (schedule.IsEnabled) await _alarmService.Create(schedule);
                });

                // Load the complete schedule with all includes after save
                using var scope = _scopeFactory.CreateScope();
                var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
                savedSchedule = await scheduleDbContext.AlarmSchedules
                    .Include(x => x.Music)
                    .Include(x => x.BibleReadingSchedule)
                    .FirstAsync(x => x.Id == schedule.Id);

                _dispatcher.Dispatch(new AddScheduleAction(savedSchedule));
            }
            else
            {
                await Task.Run(async () =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
                    
                    var existing = await scheduleDbContext.AlarmSchedules
                        .Include(x => x.Music)
                        .Include(x => x.BibleReadingSchedule)
                        .FirstAsync(x => x.Id == schedule.Id);

                    existing.Hour = schedule.Hour;
                    existing.Minute = schedule.Minute;
                    existing.DaysOfWeek = schedule.DaysOfWeek;
                    existing.IsEnabled = schedule.IsEnabled;

                    // Only update music if it was changed
                    if (musicUpdated && schedule.Music != null && existing.Music != null)
                    {
                        existing.Music.Repeat = schedule.Music.Repeat;
                        existing.Music.LanguageCode = schedule.Music.LanguageCode;
                        existing.Music.MusicType = schedule.Music.MusicType;
                        existing.Music.PublicationCode = schedule.Music.PublicationCode;
                        existing.Music.TrackNumber = schedule.Music.TrackNumber;
                    }

                    if (schedule.BibleReadingSchedule != null && existing.BibleReadingSchedule != null)
                    {
                        existing.BibleReadingSchedule.BookNumber = schedule.BibleReadingSchedule.BookNumber;
                        existing.BibleReadingSchedule.ChapterNumber = schedule.BibleReadingSchedule.ChapterNumber;
                        existing.BibleReadingSchedule.LanguageCode = schedule.BibleReadingSchedule.LanguageCode;
                        existing.BibleReadingSchedule.PublicationCode = schedule.BibleReadingSchedule.PublicationCode;
                        // Only reset duration if bible reading was changed
                        if (bibleReadingUpdated)
                        {
                            existing.BibleReadingSchedule.FinishedDuration = TimeSpan.Zero;
                        }
                    }

                    existing.MusicEnabled = schedule.MusicEnabled;
                    existing.NotificationEnabled = schedule.NotificationEnabled;
                    existing.AlwaysPlayFromStart = schedule.AlwaysPlayFromStart;
                    existing.NumberOfChaptersToRead = schedule.NumberOfChaptersToRead;
                    existing.Name = schedule.Name;
                    existing.Second = schedule.Second;
                    existing.SnoozeMinutes = schedule.SnoozeMinutes;

                    await scheduleDbContext.SaveChangesAsync();
                    _alarmService.Update(existing);
                });

                // Load the complete updated schedule with all includes
                using var scope = _scopeFactory.CreateScope();
                var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
                savedSchedule = await scheduleDbContext.AlarmSchedules
                    .Include(x => x.Music)
                    .Include(x => x.BibleReadingSchedule)
                    .FirstAsync(x => x.Id == schedule.Id);

                _dispatcher.Dispatch(new UpdateScheduleAction(savedSchedule));
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error saving schedule {ScheduleId}", schedule?.Id);
            return false;
        }
    }

    public async Task DeleteScheduleAsync(long scheduleId)
    {
        try
        {
            await Task.Run(async () =>
            {
                _alarmService.Delete(scheduleId);
                using var scope = _scopeFactory.CreateScope();
                var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
                var model = await scheduleDbContext.AlarmSchedules.FirstOrDefaultAsync(x => x.Id == scheduleId);
                if (model != null)
                {
                    scheduleDbContext.AlarmSchedules.Remove(model);
                    await scheduleDbContext.SaveChangesAsync();
                }
            });

            using var scope = _scopeFactory.CreateScope();
            var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
            var schedule = await scheduleDbContext.AlarmSchedules.FirstOrDefaultAsync(x => x.Id == scheduleId);
            if (schedule != null)
            {
                _dispatcher.Dispatch(new RemoveScheduleAction(schedule));
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error deleting schedule {ScheduleId}", scheduleId);
        }
    }

    public async Task<AlarmSchedule> GetSampleScheduleAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var mediaDbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        return await AlarmSchedule.GetSampleSchedule(true, mediaDbContext);
    }
}

