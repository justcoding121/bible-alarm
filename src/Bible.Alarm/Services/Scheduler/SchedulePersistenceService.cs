using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Scheduler;

public sealed class SchedulePersistenceService(
    ILogger logger,
    IAlarmService alarmService,
    IDispatcher dispatcher,
    IMediaCacheService mediaCacheService,
    IAlarmScheduleService alarmScheduleService)
    : ISchedulePersistenceService
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    public async Task<bool> SaveScheduleAsync(AlarmSchedule schedule, bool isNewSchedule, bool musicUpdated = true, bool biblePublicationUpdated = true)
    {
        try
        {
            LogSaveStart(schedule, isNewSchedule);

            AlarmSchedule savedSchedule = isNewSchedule
                ? await SaveNewScheduleAsync(schedule)
                : await UpdateExistingScheduleAsync(schedule, musicUpdated, biblePublicationUpdated);

            logger.Information(AppConstants.Logging.SchedulePersistenceDiagnosticsLog.SaveScheduleAsyncSaveCompletedSuccessfully, savedSchedule?.Id ?? schedule?.Id);
            return true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.SchedulePersistenceDiagnosticsLog.SaveScheduleAsyncErrorSaving,
                schedule?.Id, isNewSchedule, schedule?.Name);
            return false;
        }
    }

    private void LogSaveStart(AlarmSchedule schedule, bool isNewSchedule)
    {
        logger.Information(AppConstants.Logging.SchedulePersistenceDiagnosticsLog.SaveScheduleAsyncStarting,
            isNewSchedule, schedule?.Id, schedule?.Name, schedule?.Music != null, schedule?.BiblePublicationSchedule != null);
    }

    private async Task<AlarmSchedule> SaveNewScheduleAsync(AlarmSchedule schedule)
    {
        logger.Debug(AppConstants.Logging.SchedulePersistenceDiagnosticsLog.SaveScheduleAsyncSavingNewScheduleToDatabase);
        logger.Debug(AppConstants.Logging.SchedulePersistenceDiagnosticsLog.SaveScheduleAsyncAddingScheduleToDbContext,
            schedule.Id, schedule.Name);

        var savedSchedule = await alarmScheduleService.AddScheduleAsync(schedule, cancellationTokenSource.Token);
        logger.Information(AppConstants.Logging.SchedulePersistenceDiagnosticsLog.SaveScheduleAsyncSaveChangesAsyncCompletedNewScheduleId, savedSchedule.Id);

        if (savedSchedule.IsEnabled)
        {
            await alarmService.Create(savedSchedule);
        }

        logger.Information(AppConstants.Logging.SchedulePersistenceDiagnosticsLog.SaveScheduleAsyncReloadedSchedule,
            savedSchedule.Id, savedSchedule.Name, savedSchedule.Music != null, savedSchedule.BiblePublicationSchedule != null);

        logger.Debug(AppConstants.Logging.SchedulePersistenceDiagnosticsLog.SaveScheduleAsyncDispatchingAddScheduleAction);
        dispatcher.Dispatch(new AddScheduleAction(savedSchedule));
        logger.Debug(AppConstants.Logging.SchedulePersistenceDiagnosticsLog.SaveScheduleAsyncAddScheduleActionDispatchedSuccessfully);

        return savedSchedule;
    }

    private async Task<AlarmSchedule> UpdateExistingScheduleAsync(AlarmSchedule schedule, bool musicUpdated, bool biblePublicationUpdated)
    {
        var savedSchedule = await alarmScheduleService.UpdateScheduleByIdAsync(
            schedule.Id,
            existing => UpdateScheduleProperties(existing, schedule, musicUpdated, biblePublicationUpdated),
            cancellationTokenSource.Token);

        await alarmService.Update(savedSchedule);
        dispatcher.Dispatch(new UpdateScheduleAction(savedSchedule));

        return savedSchedule;
    }

    private static void UpdateScheduleProperties(AlarmSchedule existing, AlarmSchedule schedule, bool musicUpdated, bool biblePublicationUpdated)
    {
        UpdateBasicScheduleProperties(existing, schedule);
        UpdateMusicIfChanged(existing, schedule, musicUpdated);
        UpdateBiblePublicationIfChanged(existing, schedule, biblePublicationUpdated);
        UpdateAdditionalScheduleProperties(existing, schedule);
    }

    private static void UpdateBasicScheduleProperties(AlarmSchedule existing, AlarmSchedule schedule)
    {
        existing.Hour = schedule.Hour;
        existing.Minute = schedule.Minute;
        existing.DaysOfWeek = schedule.DaysOfWeek;
        existing.IsEnabled = schedule.IsEnabled;
    }

    private static void UpdateMusicIfChanged(AlarmSchedule existing, AlarmSchedule schedule, bool musicUpdated)
    {
        // Only update music if it was changed
        if (musicUpdated && schedule.Music != null && existing.Music != null)
        {
            existing.Music.Repeat = schedule.Music.Repeat;
            existing.Music.LanguageCode = schedule.Music.LanguageCode;
            existing.Music.PublicationCode = schedule.Music.PublicationCode;
            existing.Music.SectionCode = schedule.Music.SectionCode;
            existing.Music.TrackCode = schedule.Music.TrackCode;
        }
    }

    private static void UpdateBiblePublicationIfChanged(AlarmSchedule existing, AlarmSchedule schedule, bool biblePublicationUpdated)
    {
        if (schedule.BiblePublicationSchedule != null && existing.BiblePublicationSchedule != null)
        {
            existing.BiblePublicationSchedule.SectionCode = schedule.BiblePublicationSchedule.SectionCode;
            existing.BiblePublicationSchedule.TrackCode = schedule.BiblePublicationSchedule.TrackCode;
            existing.BiblePublicationSchedule.LanguageCode = schedule.BiblePublicationSchedule.LanguageCode;
            existing.BiblePublicationSchedule.PublicationCode = schedule.BiblePublicationSchedule.PublicationCode;
            // Only reset duration if bible reading was changed
            if (biblePublicationUpdated)
            {
                existing.BiblePublicationSchedule.FinishedDuration = TimeSpan.Zero;
            }
        }
    }

    private static void UpdateAdditionalScheduleProperties(AlarmSchedule existing, AlarmSchedule schedule)
    {
        existing.MusicEnabled = schedule.MusicEnabled;
        existing.NotificationEnabled = schedule.NotificationEnabled;
        existing.AlwaysPlayFromStart = schedule.AlwaysPlayFromStart;
        existing.NumberOfTracksToPlay = schedule.NumberOfTracksToPlay;
        existing.Name = schedule.Name;
        existing.Second = schedule.Second;
        existing.SnoozeMinutes = schedule.SnoozeMinutes;
    }

    public async Task DeleteScheduleAsync(int scheduleId)
    {
        try
        {
            // Check if this is the last schedule - prevent deletion if it is
            var allSchedules = await alarmScheduleService.GetAllSchedulesAsync(
                includeMusic: false,
                includeBiblePublication: false,
                cancellationTokenSource.Token);

            if (allSchedules.Count <= 1)
            {
                logger.Warning(AppConstants.Logging.SchedulePersistenceDiagnosticsLog.CannotDeleteScheduleLastInDatabase, scheduleId);
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("Cannot delete last schedule"));
                return;
            }

            // Load the schedule BEFORE deleting it, so we can dispatch the action
            var scheduleToRemove = await alarmScheduleService.GetScheduleByIdAsync(
                scheduleId, true, true, cancellationTokenSource.Token);

            if (scheduleToRemove == null)
            {
                logger.Warning(AppConstants.Logging.ScheduleLookupDiagnosticsLog.NotFoundForDeletion, scheduleId);
                return;
            }

            // Delete cached media files for this schedule
            await mediaCacheService.DeleteScheduleCacheAsync(scheduleId);

            // Delete from database
            await alarmService.Delete(scheduleId);
            await alarmScheduleService.DeleteScheduleAsync(scheduleId, cancellationTokenSource.Token);

            // Dispatch action to remove from state (this will trigger HomeViewModel to update)
            dispatcher.Dispatch(new RemoveScheduleAction(scheduleToRemove));
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.SchedulePersistenceDiagnosticsLog.ErrorDeletingSchedule, scheduleId);
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
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            logger.Warning(ex, AppConstants.Logging.DisposableLifetimeLog.ErrorDuringCancellationTokenSourceDisposal);
        }

        // All injected services are singletons, so don't dispose them
        // No event handlers to unsubscribe
    }
}

