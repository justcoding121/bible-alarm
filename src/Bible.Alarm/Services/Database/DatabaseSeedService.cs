using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Database;

public sealed class DatabaseSeedService(
    ILogger logger,
    IAlarmScheduleService alarmScheduleService,
    IBiblePublicationService BiblePublicationService,
    IMelodyMusicService melodyMusicService)
    : IDatabaseSeedService, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    public async Task<bool> SeedDefaultAlarmAsync()
    {
        // Seed if schedules are empty
        if (!await alarmScheduleService.AnySchedulesExistAsync(cancellationTokenSource.Token))
        {
            try
            {
                // Create sample schedule with IsEnabled = false (disabled by default)
                var schedule = await AlarmSchedule.GetSampleSchedule(false, BiblePublicationService, melodyMusicService);

                await alarmScheduleService.AddScheduleAsync(schedule, cancellationTokenSource.Token);

                logger.Information("Seeded default alarm schedule. ScheduleId={ScheduleId}, Name={Name}",
                    schedule.Id, schedule.Name);

                // Save basic metadata to Preferences for early MediaSession setup
                // This ensures Android Auto can show metadata even before full bootstrap completes
                // Full metadata (with publication names) will be set later by SetCarPlayScreenAction
                SaveSeedMetadataToPreferences(schedule);

                return true; // Schedule was seeded
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("No Bible publications found") || 
                                                       ex.Message.Contains("No sectioned Bible publication"))
            {
                // Bible publications not yet available (Media database may still be initializing)
                // This is expected during early bootstrap or when test data doesn't include sectioned publications
                // The schedule will be seeded later when publications are available
                logger.Warning("Cannot seed default alarm schedule yet - Bible publications not available. " +
                    "This is normal during early bootstrap or when using test data. Schedule will be created when publications are loaded.");
                return false; // No seeding occurred, will retry later
            }
        }
        else
        {
            // Database has schedules but Preferences might be empty (e.g., app data cleared but DB retained)
            // Ensure Preferences has metadata for Android Auto
            await EnsurePreferencesHasMetadataAsync();
        }

        return false; // No seeding occurred
    }

    /// <summary>
    /// Saves basic metadata from seeded schedule to Preferences for early MediaSession setup.
    /// </summary>
    private void SaveSeedMetadataToPreferences(AlarmSchedule schedule)
    {
        try
        {
            // Save basic metadata - Android Auto will show this until full bootstrap completes
            LastPlayedMetadataHelper.SaveLastPlayedMetadata(
                title: schedule.Name,
                artist: AppConstants.Media.NowPlayingPlaceholder.ArtistReadyToPlay,
                album: null,
                artworkUrl: null,
                scheduleId: schedule.Id);

            logger.Debug("Saved seeded schedule metadata to Preferences for Android Auto. ScheduleId={ScheduleId}, Title={Title}",
                schedule.Id, schedule.Name);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to save seeded schedule metadata to Preferences");
        }
    }

    /// <summary>
    /// Ensures Preferences has metadata if schedules exist but Preferences is empty.
    /// This handles the case where Preferences was cleared but database was retained.
    /// </summary>
    private async Task EnsurePreferencesHasMetadataAsync()
    {
        try
        {
            // Check if Preferences already has metadata
            var existingMetadata = LastPlayedMetadataHelper.GetLastPlayedMetadata();
            if (existingMetadata != null)
            {
                return; // Already has metadata
            }

            // Get first schedule from database
            var schedules = await alarmScheduleService.GetAllSchedulesAsync(
                includeMusic: false,
                includeBiblePublication: false,
                cancellationToken: cancellationTokenSource.Token);

            if (schedules == null || schedules.Count == 0)
            {
                return;
            }

            var firstSchedule = schedules[0];

            // Save basic metadata
            LastPlayedMetadataHelper.SaveLastPlayedMetadata(
                title: firstSchedule.Name,
                artist: AppConstants.Media.NowPlayingPlaceholder.ArtistReadyToPlay,
                album: null,
                artworkUrl: null,
                scheduleId: firstSchedule.Id);

            logger.Debug("Saved existing schedule metadata to Preferences for Android Auto. ScheduleId={ScheduleId}, Title={Title}",
                firstSchedule.Id, firstSchedule.Name);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to ensure Preferences has metadata");
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

