#nullable enable
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.Scheduler.Models;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Services.Scheduler;

/// <summary>
/// Service for getting the next schedule track metadata for Android Auto MediaSession.
/// Returns track metadata and scheduleId for the next schedule to be played.
/// Downloads the first track and uses DisplayMetadataService to get full metadata (same as PreparePlaybackService).
/// </summary>
public sealed class DefaultScheduleService(
    ILogger logger,
    IState<ApplicationState> applicationState,
    IAlarmScheduleService alarmScheduleService,
    IPlaylistService playlistService,
    IPreparePlaybackService preparePlaybackService,
    IDisplayMetadataService displayMetadataService) : IDefaultScheduleService, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    public async Task<ScheduleTrackMetadata> GetNextScheduleTrackMetaDataAsync()
    {
        // IMPORTANT:
        // This method must NOT call database seeding or create schedules.
        // Bootstrap guarantees schedules are loaded into Fluxor state, and UI enforces at least one schedule.
        //
        // Pick a scheduleId deterministically:
        // - Prefer the last played schedule ID from preferences (if available and exists in state)
        // - Else fall back to the first schedule in the schedule list (guaranteed post-bootstrap)
        // Note: CurrentScheduleId is not used here as it should be cleared after playback ends/resets
        // Note: CurrentSchedule is intentionally not used here as it should be null after navigating back to home

        int? scheduleId = null;

        // Check preferences for last played schedule ID
        var lastPlayedMetadata = LastPlayedMetadataHelper.GetLastPlayedMetadata();
        if (lastPlayedMetadata.HasValue && lastPlayedMetadata.Value.ScheduleId.HasValue)
        {
            var lastPlayedScheduleId = lastPlayedMetadata.Value.ScheduleId.Value;
            // Verify the schedule still exists in state
            if (applicationState.Value.Schedules?.Any(s => s.Id == lastPlayedScheduleId) == true)
            {
                scheduleId = lastPlayedScheduleId;
                logger.Debug("GetNextScheduleTrackMetaDataAsync: Using last played schedule {ScheduleId} from preferences", scheduleId);
            }
            else
            {
                logger.Debug("GetNextScheduleTrackMetaDataAsync: Last played schedule {ScheduleId} from preferences not found in state, querying DB for first schedule", lastPlayedScheduleId);
                // Query database for first schedule when last played schedule doesn't exist in state
                try
                {
                    var firstSchedule = await alarmScheduleService.GetFirstScheduleOrDefaultAsync(
                        includeMusic: false,
                        includeBibleReading: false,
                        cancellationTokenSource.Token);
                    if (firstSchedule != null)
                    {
                        scheduleId = firstSchedule.Id;
                        logger.Debug("GetNextScheduleTrackMetaDataAsync: Found first schedule {ScheduleId} from database", scheduleId);
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "GetNextScheduleTrackMetaDataAsync: Failed to query database for first schedule, falling back to state");
                }
            }
        }

        // Final fallback to first schedule in state
        if (!scheduleId.HasValue)
        {
            scheduleId = applicationState.Value.Schedules?.FirstOrDefault()?.Id;
        }

        if (!scheduleId.HasValue || scheduleId.Value <= 0)
        {
            logger.Warning("GetNextScheduleTrackMetaDataAsync: No schedules available in state; returning fallback metadata");
            // Return empty values - builders will handle fallbacks
            return new ScheduleTrackMetadata
            {
                ScheduleId = scheduleId ?? 0,
                Title = string.Empty,
                Artist = string.Empty,
                Album = null,
                ArtworkUrl = null
            };
        }

        // Get track metadata for the schedule
        var metadata = await GetTrackMetadataForScheduleAsync(scheduleId.Value);

        // Save metadata to Preferences before returning (all platforms)
        // This ensures Preferences always has the latest default schedule metadata
        LastPlayedMetadataHelper.SaveLastPlayedMetadata(
            metadata.Title,
            metadata.Artist,
            metadata.Album,
            metadata.ArtworkUrl,
            metadata.ScheduleId > 0 ? metadata.ScheduleId : null);
        logger.Debug("Saved default schedule metadata to Preferences - Title: {Title}, Artist: {Artist}, ScheduleId: {ScheduleId}",
            metadata.Title, metadata.Artist, metadata.ScheduleId);

        return metadata;
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

        // Save artwork bytes to file and create ArtworkUrl
        // Uses different filename than regular track playback to avoid conflicts
        string? artworkUrl = metadata.ArtworkUrl;
        if (metadata.ArtworkBytes != null && metadata.ArtworkBytes.Length > 0 && string.IsNullOrEmpty(artworkUrl))
        {
            try
            {
                // Use AppDataDirectory instead of CacheDirectory for artwork
                // CacheDirectory can be cleared by iOS when storage is low, which would break lock screen artwork
                // AppDataDirectory is more persistent and won't be cleared by the OS
                var artworkPath = Path.Combine(FileSystem.AppDataDirectory, "default_schedule_artwork.jpg");
                await File.WriteAllBytesAsync(artworkPath, metadata.ArtworkBytes);
                artworkUrl = artworkPath;
                logger.Debug("Saved default schedule artwork to {ArtworkPath}, size: {Size} bytes", artworkPath, metadata.ArtworkBytes.Length);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to save default schedule artwork to file");
            }
        }

        logger.Debug("Returning track metadata for schedule {ScheduleId}: Title={Title}, Artist={Artist}, Album={Album}, HasArtwork={HasArtwork}",
            scheduleId, metadata.Title, metadata.Artist, metadata.Album, !string.IsNullOrEmpty(artworkUrl));

        // Pass raw metadata values (null becomes empty string for non-nullable properties)
        // Builders will handle empty string fallbacks
        return new ScheduleTrackMetadata
        {
            ScheduleId = scheduleId,
            Title = metadata.Title ?? string.Empty,
            Artist = metadata.Artist ?? string.Empty,
            Album = metadata.Album,
            ArtworkUrl = artworkUrl
        };
    }

    private ScheduleTrackMetadata CreateFallbackMetadata(int scheduleId, PlayItem firstPlayItem)
    {
        // Generate meaningful values from available metadata, but pass empty if not available
        // Builders will handle empty string fallbacks
        var title = firstPlayItem.Metadata?.PlayType == PlayType.Bible
            ? $"Section {firstPlayItem.Metadata.SectionNumber} Chapter {firstPlayItem.Metadata.ChapterNumber}"
            : firstPlayItem.Metadata?.TrackNumber != null
                ? $"Track {firstPlayItem.Metadata.TrackNumber}"
                : string.Empty;

        return new ScheduleTrackMetadata
        {
            ScheduleId = scheduleId,
            Title = title,
            Artist = firstPlayItem.Metadata?.PublicationCode ?? string.Empty,
            Album = firstPlayItem.Metadata?.LanguageCode,
            ArtworkUrl = null
        };
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
    }

    public bool ValidateScheduleIdExists(int scheduleId)
    {
        var schedules = applicationState.Value.Schedules;
        return schedules?.Any(s => s.Id == scheduleId) ?? false;
    }

    public int? GetFirstScheduleId()
    {
        var schedules = applicationState.Value.Schedules;
        if (schedules != null && schedules.Count > 0)
        {
            return schedules.First().Id;
        }

        return null;
    }
}

