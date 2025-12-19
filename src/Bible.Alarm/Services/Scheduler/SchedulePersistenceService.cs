using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Scheduler;

public class SchedulePersistenceService(
    ILogger logger,
    IAlarmService alarmService,
    IDispatcher dispatcher,
    IMediaCacheService mediaCacheService,
    IAlarmScheduleService alarmScheduleService,
    IBibleTranslationService bibleTranslationService,
    IMelodyMusicService melodyMusicService)
    : ISchedulePersistenceService, IDisposable
{
    private readonly ILogger _logger = logger;
    private readonly IAlarmService _alarmService = alarmService;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly IMediaCacheService _mediaCacheService = mediaCacheService;
    private readonly IAlarmScheduleService _alarmScheduleService = alarmScheduleService;
    private readonly IBibleTranslationService _bibleTranslationService = bibleTranslationService;
    private readonly IMelodyMusicService _melodyMusicService = melodyMusicService;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _isDisposed;

    public async Task<bool> SaveScheduleAsync(AlarmSchedule schedule, bool isNewSchedule, bool musicUpdated = true, bool bibleReadingUpdated = true)
    {
        try
        {
            _logger.Information("SaveScheduleAsync: Starting. IsNewSchedule={IsNewSchedule}, ScheduleId={ScheduleId}, Name={Name}, HasMusic={HasMusic}, HasBibleReading={HasBibleReading}",
                isNewSchedule, schedule?.Id, schedule?.Name, schedule?.Music != null, schedule?.BibleReadingSchedule != null);

            AlarmSchedule savedSchedule = null;

            if (isNewSchedule)
            {
                _logger.Debug("SaveScheduleAsync: Saving new schedule to database");
                _logger.Debug("SaveScheduleAsync: Adding schedule to DbContext. ScheduleId={ScheduleId}, Name={Name}",
                    schedule.Id, schedule.Name);

                savedSchedule = await _alarmScheduleService.AddScheduleAsync(schedule, _cancellationTokenSource.Token);

                _logger.Information("SaveScheduleAsync: SaveChangesAsync completed. New ScheduleId={ScheduleId}", savedSchedule.Id);

                if (savedSchedule.IsEnabled)
                {
                    await _alarmService.Create(savedSchedule);
                }

                _logger.Information("SaveScheduleAsync: Reloaded schedule. ScheduleId={ScheduleId}, Name={Name}, HasMusic={HasMusic}, HasBibleReading={HasBibleReading}",
                    savedSchedule.Id, savedSchedule.Name, savedSchedule.Music != null, savedSchedule.BibleReadingSchedule != null);

                _logger.Debug("SaveScheduleAsync: Dispatching AddScheduleAction");
                _dispatcher.Dispatch(new AddScheduleAction(savedSchedule));
                _logger.Information("SaveScheduleAsync: AddScheduleAction dispatched successfully");
            }
            else
            {
                savedSchedule = await _alarmScheduleService.UpdateScheduleByIdAsync(
                    schedule.Id,
                    existing =>
                    {
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
                    },
                    _cancellationTokenSource.Token);

                await Task.Run(async () =>
                {
                    _alarmService.Update(savedSchedule);
                });

                _dispatcher.Dispatch(new UpdateScheduleAction(savedSchedule));
            }

            _logger.Information("SaveScheduleAsync: Save completed successfully. ScheduleId={ScheduleId}", savedSchedule?.Id ?? schedule?.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "SaveScheduleAsync: Error saving schedule. ScheduleId={ScheduleId}, IsNewSchedule={IsNewSchedule}, Name={Name}",
                schedule?.Id, isNewSchedule, schedule?.Name);
            return false;
        }
    }

    public async Task DeleteScheduleAsync(int scheduleId)
    {
        try
        {
            // Check if this is the last schedule - prevent deletion if it is
            var allSchedules = await _alarmScheduleService.GetAllSchedulesAsync(
                includeMusic: false,
                includeBibleReading: false,
                _cancellationTokenSource.Token);

            if (allSchedules.Count <= 1)
            {
                _logger.Warning("Cannot delete schedule {ScheduleId} - it is the last schedule in the database", scheduleId);
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("Cannot delete last schedule"));
                return;
            }

            // Load the schedule BEFORE deleting it, so we can dispatch the action
            var scheduleToRemove = await _alarmScheduleService.GetScheduleByIdAsync(
                scheduleId, true, true, _cancellationTokenSource.Token);

            if (scheduleToRemove == null)
            {
                _logger.Warning("Schedule {ScheduleId} not found for deletion", scheduleId);
                return;
            }

            // Delete cached media files for this schedule
            await _mediaCacheService.DeleteScheduleCacheAsync(scheduleId);

            // Delete from database
            await Task.Run(async () =>
            {
                _alarmService.Delete(scheduleId);
                await _alarmScheduleService.DeleteScheduleAsync(scheduleId, _cancellationTokenSource.Token);
            });

            // Dispatch action to remove from state (this will trigger HomeViewModel to update)
            _dispatcher.Dispatch(new RemoveScheduleAction(scheduleToRemove));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error deleting schedule {ScheduleId}", scheduleId);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        // Cancel and dispose cancellation token source
        try
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            _logger.Warning(ex, "Error during cancellation token source disposal");
        }

        // All injected services are singletons, so don't dispose them
        // No event handlers to unsubscribe
    }
}

