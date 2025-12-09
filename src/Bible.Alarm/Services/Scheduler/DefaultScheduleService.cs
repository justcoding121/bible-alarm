#nullable enable
using System;
using System.Threading.Tasks;
using Bible.Alarm.Models;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.Scheduler.Models;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Services;
using Bible.Alarm.Shared.Services.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Scheduler;

/// <summary>
/// Service for getting the next schedule track metadata for Android Auto MediaSession.
/// Returns track metadata and scheduleId for the next schedule to be played.
/// Uses DatabaseSeedService to ensure initial seeding has occurred.
/// </summary>
public class DefaultScheduleService(
    ILogger logger,
    IServiceScopeFactory scopeFactory,
    IDatabaseSeedService databaseSeedService,
    IAlarmScheduleService alarmScheduleService,
    IPlaylistService playlistService,
    IDispatcher dispatcher) : IDefaultScheduleService, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private bool _isDisposed;

    public async Task<AlarmSchedule> GetDefaultScheduleAsync()
    {
        // Check if schedules exist before seeding
        var schedulesExistedBeforeSeed = await alarmScheduleService.AnySchedulesExistAsync(_cancellationTokenSource.Token);
        
        // First, ensure initial seeding has occurred (for legacy app support)
        // This will create a sample schedule if none exists and seeding hasn't happened
        await databaseSeedService.SeedDefaultAlarmAsync();

        using var scope = scopeFactory.CreateScope();
        var mediaDbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        // Check for most recently played schedule in general settings
        var lastPlayedSetting = await alarmScheduleService.GetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.LastPlayedScheduleId, 
            _cancellationTokenSource.Token);

        if (!string.IsNullOrEmpty(lastPlayedSetting?.Value))
        {
            if (int.TryParse(lastPlayedSetting.Value, out int lastPlayedScheduleId))
            {
                logger.Debug("Found last played schedule ID in general settings: {ScheduleId}", lastPlayedScheduleId);
                
                // Check if the schedule exists in database and is valid
                var lastPlayedSchedule = await alarmScheduleService.GetScheduleByIdAsync(
                    lastPlayedScheduleId, 
                    includeMusic: true, 
                    includeBibleReading: true, 
                    _cancellationTokenSource.Token);

                if (lastPlayedSchedule != null)
                {
                    logger.Information("Returning most recently played schedule: {ScheduleId}, Name: {Name}", 
                        lastPlayedSchedule.Id, lastPlayedSchedule.Name);
                    
                    // If seed created this schedule, dispatch it
                    if (!schedulesExistedBeforeSeed)
                    {
                        logger.Debug("Dispatching AddScheduleAction for schedule created by seed service");
                        dispatcher.Dispatch(new AddScheduleAction(lastPlayedSchedule));
                    }
                    
                    return lastPlayedSchedule;
                }
                else
                {
                    logger.Warning("Last played schedule ID {ScheduleId} not found in database, will create sample schedule", 
                        lastPlayedScheduleId);
                }
            }
            else
            {
                logger.Warning("Invalid last played schedule ID format in general settings: {Value}, will create sample schedule", 
                    lastPlayedSetting.Value);
            }
        }
        else
        {
            logger.Debug("No last played schedule found in general settings, will create sample schedule");
        }

        // No valid last played schedule found
        // Check if any schedules exist (seed service may have created one)
        var existingSchedule = await alarmScheduleService.GetFirstScheduleOrDefaultAsync(
            includeMusic: true, 
            includeBibleReading: true, 
            _cancellationTokenSource.Token);

        if (existingSchedule != null)
        {
            // Return the first existing schedule (seed service may have created it)
            logger.Information("Returning first existing schedule: {ScheduleId}, Name: {Name}", 
                existingSchedule.Id, existingSchedule.Name);
            
            // If seed created this schedule, dispatch it
            if (!schedulesExistedBeforeSeed)
            {
                logger.Debug("Dispatching AddScheduleAction for schedule created by seed service");
                dispatcher.Dispatch(new AddScheduleAction(existingSchedule));
            }
            
            return existingSchedule;
        }

        // No schedules exist, create a sample schedule
        // Note: GetSampleSchedule creates schedule with IsEnabled = false (disabled by default)
        logger.Information("No schedules found, creating sample schedule as default schedule");
        var sampleSchedule = await AlarmSchedule.GetSampleSchedule(false, mediaDbContext);

        // Save the sample schedule to database using AlarmScheduleService
        var savedSchedule = await alarmScheduleService.AddScheduleAsync(sampleSchedule, _cancellationTokenSource.Token);

        logger.Information("Sample schedule created and saved: {ScheduleId}, Name: {Name}", 
            savedSchedule.Id, savedSchedule.Name);

        // Dispatch the newly created schedule
        logger.Debug("Dispatching AddScheduleAction for newly created schedule");
        dispatcher.Dispatch(new AddScheduleAction(savedSchedule));

        return savedSchedule;
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
            logger.Warning(ex, "Error during cancellation token source disposal");
        }
        
        // IServiceScopeFactory is a singleton, so don't dispose it
    }
}

