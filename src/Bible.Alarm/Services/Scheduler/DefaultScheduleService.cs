#nullable enable
using System;
using System.Threading.Tasks;
using Bible.Alarm.Models;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.Scheduler.Models;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
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
/// Downloads the first track and uses DisplayMetadataService to get full metadata (same as PreparePlaybackService).
/// </summary>
public class DefaultScheduleService(
    ILogger logger,
    IServiceScopeFactory scopeFactory,
    IDatabaseSeedService databaseSeedService,
    IAlarmScheduleService alarmScheduleService,
    IPlaylistService playlistService,
    IPreparePlaybackService preparePlaybackService,
    IDisplayMetadataService displayMetadataService,
    IDispatcher dispatcher) : IDefaultScheduleService, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private bool _isDisposed;

    public async Task<ScheduleTrackMetadata> GetNextScheduleTrackMetaDataAsync()
    {
        // Check if schedules exist before seeding
        var schedulesExistedBeforeSeed = await alarmScheduleService.AnySchedulesExistAsync(_cancellationTokenSource.Token);
        
        // First, ensure initial seeding has occurred (for legacy app support)
        // This will create a sample schedule if none exists and seeding hasn't happened
        await databaseSeedService.SeedDefaultAlarmAsync();

        using var scope = scopeFactory.CreateScope();
        var mediaDbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        // Try to get a valid schedule: last played -> first existing -> create sample
        var schedule = await TryGetLastPlayedScheduleAsync(schedulesExistedBeforeSeed) 
            ?? await TryGetFirstExistingScheduleAsync(schedulesExistedBeforeSeed)
            ?? await CreateSampleScheduleAsync(mediaDbContext);

        var scheduleId = schedule.Id;

        // Get track metadata for the schedule
        return await GetTrackMetadataForScheduleAsync(scheduleId);
    }

    private async Task<AlarmSchedule?> TryGetLastPlayedScheduleAsync(bool schedulesExistedBeforeSeed)
    {
        var lastPlayedSetting = await alarmScheduleService.GetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.LastPlayedScheduleId, 
            _cancellationTokenSource.Token);

        if (string.IsNullOrEmpty(lastPlayedSetting?.Value))
        {
            logger.Debug("No last played schedule found in general settings, will create sample schedule");
            return null;
        }

        if (!int.TryParse(lastPlayedSetting.Value, out int lastPlayedScheduleId))
        {
            logger.Warning("Invalid last played schedule ID format in general settings: {Value}, will create sample schedule", 
                lastPlayedSetting.Value);
            return null;
        }

        logger.Debug("Found last played schedule ID in general settings: {ScheduleId}", lastPlayedScheduleId);
        
        var schedule = await alarmScheduleService.GetScheduleByIdAsync(
            lastPlayedScheduleId, 
            includeMusic: true, 
            includeBibleReading: true, 
            _cancellationTokenSource.Token);

        if (schedule == null)
        {
            logger.Warning("Last played schedule ID {ScheduleId} not found in database, will create sample schedule", 
                lastPlayedScheduleId);
            return null;
        }

        logger.Information("Using most recently played schedule: {ScheduleId}, Name: {Name}", 
            schedule.Id, schedule.Name);
        
        DispatchScheduleIfNeeded(schedule, schedulesExistedBeforeSeed, "schedule created by seed service");
        return schedule;
    }

    private async Task<AlarmSchedule?> TryGetFirstExistingScheduleAsync(bool schedulesExistedBeforeSeed)
    {
        var schedule = await alarmScheduleService.GetFirstScheduleOrDefaultAsync(
            includeMusic: true, 
            includeBibleReading: true, 
            _cancellationTokenSource.Token);

        if (schedule == null)
        {
            return null;
        }

        logger.Information("Using first existing schedule: {ScheduleId}, Name: {Name}", 
            schedule.Id, schedule.Name);
        
        DispatchScheduleIfNeeded(schedule, schedulesExistedBeforeSeed, "schedule created by seed service");
        return schedule;
    }

    private async Task<AlarmSchedule> CreateSampleScheduleAsync(MediaDbContext mediaDbContext)
    {
        // Note: GetSampleSchedule creates schedule with IsEnabled = false (disabled by default)
        logger.Information("No schedules found, creating sample schedule as default schedule");
        var sampleSchedule = await AlarmSchedule.GetSampleSchedule(false, mediaDbContext);

        // Save the sample schedule to database using AlarmScheduleService
        var schedule = await alarmScheduleService.AddScheduleAsync(sampleSchedule, _cancellationTokenSource.Token);

        logger.Information("Sample schedule created and saved: {ScheduleId}, Name: {Name}", 
            schedule.Id, schedule.Name);

        // Dispatch the newly created schedule
        logger.Debug("Dispatching AddScheduleAction for newly created schedule");
        dispatcher.Dispatch(new AddScheduleAction(schedule));
        
        return schedule;
    }

    private async Task<ScheduleTrackMetadata> GetTrackMetadataForScheduleAsync(int scheduleId)
    {
        // Get the first track from the schedule
        var firstPlayItem = await playlistService.NextTrack(scheduleId);
        
        // Prepare the first track using PreparePlaybackService (downloads and creates AudioPlayerTrack)
        var audioPlayerTrack = await preparePlaybackService.PrepareSingleTrackAsync(firstPlayItem);
        
        if (audioPlayerTrack == null)
        {
            logger.Warning("Failed to prepare first track for schedule {ScheduleId}, using fallback metadata", scheduleId);
            return CreateFallbackMetadata(scheduleId, firstPlayItem);
        }

        // Use DisplayMetadataService to get full metadata (same as AudioPlayer does)
        var metadata = await displayMetadataService.GetDisplayMetadataAsync(audioPlayerTrack);

        logger.Debug("Returning track metadata for schedule {ScheduleId}: Title={Title}, Artist={Artist}, Album={Album}", 
            scheduleId, metadata.Title, metadata.Artist, metadata.Album);

        return new ScheduleTrackMetadata
        {
            ScheduleId = scheduleId,
            Title = metadata.Title ?? "",
            Artist = metadata.Artist ?? "",
            Album = metadata.Album
        };
    }

    private ScheduleTrackMetadata CreateFallbackMetadata(int scheduleId, PlayItem firstPlayItem)
    {
        return new ScheduleTrackMetadata
        {
            ScheduleId = scheduleId,
            Title = firstPlayItem.Metadata?.PlayType == PlayType.Bible
                ? $"Book {firstPlayItem.Metadata.BookNumber} Chapter {firstPlayItem.Metadata.ChapterNumber}"
                : $"Track {firstPlayItem.Metadata?.TrackNumber ?? 1}",
            Artist = firstPlayItem.Metadata?.PublicationCode ?? "",
            Album = firstPlayItem.Metadata?.LanguageCode
        };
    }

    private void DispatchScheduleIfNeeded(AlarmSchedule schedule, bool schedulesExistedBeforeSeed, string reason)
    {
        if (!schedulesExistedBeforeSeed)
        {
            logger.Debug("Dispatching AddScheduleAction for {Reason}", reason);
            dispatcher.Dispatch(new AddScheduleAction(schedule));
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
            logger.Warning(ex, "Error during cancellation token source disposal");
        }
        
        // IServiceScopeFactory is a singleton, so don't dispose it
    }
}

