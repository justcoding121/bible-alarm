#nullable enable
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.Scheduler.Models;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
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
    IDisplayMetadataService displayMetadataService,
    IInternetConnectivityChecker? internetConnectivityChecker = null) : IDefaultScheduleService, IDisposable
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
            if (applicationState.Value.Schedules?.Any(s => s.Id == lastPlayedScheduleId) is true)
            {
                scheduleId = lastPlayedScheduleId;
                logger.Debug(AppConstants.Logging.DefaultScheduleServiceDiagnosticsLog.GetNextScheduleUsingLastPlayedVerifiedInState, scheduleId);
            }
            else
            {
                // State may not be loaded yet (early bootstrap after process restart).
                // Verify the last played schedule directly against the DB before falling
                // back to the first schedule, so we don't lose the user's context.
                try
                {
                    if (await alarmScheduleService.ScheduleExistsAsync(lastPlayedScheduleId, cancellationTokenSource.Token))
                    {
                        scheduleId = lastPlayedScheduleId;
                        logger.Debug(AppConstants.Logging.DefaultScheduleServiceDiagnosticsLog.GetNextScheduleUsingLastPlayedVerifiedInDbStateNotLoaded, scheduleId);
                    }
                    else
                    {
                        logger.Debug(AppConstants.Logging.DefaultScheduleServiceDiagnosticsLog.GetNextScheduleLastPlayedNoLongerInDbQueryingFirst, lastPlayedScheduleId);
                        var firstSchedule = await alarmScheduleService.GetFirstScheduleOrDefaultAsync(
                            includeMusic: false,
                            includeBiblePublication: false,
                            cancellationTokenSource.Token);
                        if (firstSchedule != null)
                        {
                            scheduleId = firstSchedule.Id;
                            logger.Debug(AppConstants.Logging.DefaultScheduleServiceDiagnosticsLog.GetNextScheduleFoundFirstScheduleFromDatabase, scheduleId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, AppConstants.Logging.DefaultScheduleServiceDiagnosticsLog.GetNextScheduleTrackMetaDataFailedVerifyLastPlayedInDb);
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
            logger.Warning(AppConstants.Logging.DefaultScheduleServiceDiagnosticsLog.GetNextScheduleTrackMetaDataNoSchedulesInStateReturningFallback);
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
        logger.Debug(AppConstants.Logging.DefaultScheduleServiceDiagnosticsLog.SavedDefaultScheduleMetadataToPreferences,
            metadata.Title, metadata.Artist, metadata.ScheduleId);

        return metadata;
    }

    public async Task<ScheduleTrackMetadata> GetNextScheduleInRotationMetadataAsync()
    {
        var schedules = applicationState.Value.Schedules;
        if (schedules == null || schedules.Count == 0)
        {
            logger.Warning(AppConstants.Logging.DefaultScheduleServiceDiagnosticsLog.GetNextScheduleInRotationNoSchedulesInStateReturningFallback);
            return new ScheduleTrackMetadata
            {
                ScheduleId = 0,
                Title = string.Empty,
                Artist = string.Empty,
                Album = null,
                ArtworkUrl = null
            };
        }

        // Exclude Music-category schedules so we don't get stuck in a music-only schedule during rotation
        var rotatable = schedules.Where(s => !s.BiblePublicationIsMusic).OrderBy(s => s.Id).ToList();
        if (rotatable.Count == 0)
        {
            logger.Warning(AppConstants.Logging.DefaultScheduleServiceDiagnosticsLog.GetNextScheduleInRotationNoNonMusicSchedulesReturningFallback);
            return new ScheduleTrackMetadata
            {
                ScheduleId = 0,
                Title = string.Empty,
                Artist = string.Empty,
                Album = null,
                ArtworkUrl = null
            };
        }

        var lastId = AndroidAutoRotationHelper.GetLastRotationScheduleId();
        var index = lastId.HasValue
            ? rotatable.FindIndex(s => s.Id == lastId.Value)
            : -1;
        if (index < 0)
        {
            index = 0;
        }
        else
        {
            index = (index + 1) % rotatable.Count;
        }

        var scheduleId = rotatable[index].Id;
        var metadata = await GetTrackMetadataForScheduleAsync(scheduleId);
        AndroidAutoRotationHelper.SetLastRotationScheduleId(scheduleId);
        logger.Debug(AppConstants.Logging.DefaultScheduleServiceDiagnosticsLog.GetNextScheduleInRotationRotatedToScheduleIndex, index, scheduleId);
        return metadata;
    }

    private async Task<ScheduleTrackMetadata> GetTrackMetadataForScheduleAsync(int scheduleId)
    {
        // Prefer the Bible publication track for default metadata so the car display / lock
        // screen always shows the Bible reading artwork, even when music is enabled.
        // Fall back to NextTrack (which returns the music track) when no Bible publication exists.
        var firstPlayItem = await playlistService.NextBiblePublicationTrack(scheduleId)
            ?? await playlistService.NextTrack(scheduleId);

        if (internetConnectivityChecker != null && !await internetConnectivityChecker.IsInternetAvailableAsync())
        {
            logger.Debug(AppConstants.Logging.DefaultScheduleServiceDiagnosticsLog.GetTrackMetadataNoInternetReturningFallbackForSchedule, scheduleId);
            return CreateFallbackMetadata(scheduleId, firstPlayItem);
        }

        // Prepare the first track using PreparePlaybackService (downloads and creates AudioPlayerTrack)
        var audioPlayerTrack = await preparePlaybackService.PrepareSingleTrackAsync(firstPlayItem);

        if (audioPlayerTrack == null)
        {
            logger.Warning(AppConstants.Logging.DefaultScheduleServiceDiagnosticsLog.FailedToPrepareFirstTrackUsingFallbackMetadata, scheduleId);
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
                var artworkDir = Path.Combine(FileSystem.AppDataDirectory, "Artwork");
                
                // Use timestamp for unique filename to avoid file locking issues
                // This prevents conflicts when multiple threads try to write simultaneously
                // or when the file is locked by the OS/media system
                var artworkPath = Path.Combine(artworkDir, $"default_schedule_artwork_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.jpg");
                
                // Use concurrency helper to prevent concurrent access to artwork directory operations
                await ConcurrencyHelper.ExecuteAsync(ArtworkHelper.ArtworkLock, async () =>
                {
                    // Ensure directory exists
                    Directory.CreateDirectory(artworkDir);
                    // Clean up old default schedule artwork files to prevent accumulation
                    CleanupOldDefaultScheduleArtworkFiles(artworkDir);
                    await File.WriteAllBytesAsync(artworkPath, metadata.ArtworkBytes);
                });
                
                artworkUrl = artworkPath;
                logger.Debug(AppConstants.Logging.DefaultScheduleServiceDiagnosticsLog.SavedDefaultScheduleArtworkToPathAndSize, artworkPath, metadata.ArtworkBytes.Length);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, AppConstants.Logging.DefaultScheduleServiceDiagnosticsLog.FailedToSaveDefaultScheduleArtworkToFile);
            }
        }

        var title = metadata.Title ?? string.Empty;
        var artist = metadata.Artist ?? string.Empty;

        var scheduleItem = applicationState.Value.Schedules?.FirstOrDefault(s => s.Id == scheduleId);
        if (scheduleItem != null)
        {
#if ANDROID || IOS
            const string musicSymbol = "\u266B";
#else
            string? musicSymbol = null;
#endif
            title = ScheduleDisplayMetadataHelper.BuildScheduleTitle(scheduleItem, musicSymbol);
            artist = ScheduleDisplayMetadataHelper.BuildScheduleSubtitle(scheduleItem, musicSymbol);
            logger.Debug(AppConstants.Logging.DefaultScheduleServiceDiagnosticsLog.UsingListingFormatMetadataForSchedule,
                scheduleId, title, artist);
        }

        logger.Debug(AppConstants.Logging.DefaultScheduleServiceDiagnosticsLog.ReturningTrackMetadataForSchedule,
            scheduleId, title, artist, metadata.Album, !string.IsNullOrEmpty(artworkUrl));

        return new ScheduleTrackMetadata
        {
            ScheduleId = scheduleId,
            Title = title,
            Artist = artist,
            Album = metadata.Album,
            ArtworkUrl = artworkUrl
        };
    }

    private ScheduleTrackMetadata CreateFallbackMetadata(int scheduleId, PlayItem firstPlayItem)
    {
        var scheduleItem = applicationState.Value.Schedules?.FirstOrDefault(s => s.Id == scheduleId);
        if (scheduleItem != null)
        {
#if ANDROID || IOS
            const string musicSymbol = "\u266B";
#else
            string? musicSymbol = null;
#endif
            return new ScheduleTrackMetadata
            {
                ScheduleId = scheduleId,
                Title = ScheduleDisplayMetadataHelper.BuildScheduleTitle(scheduleItem, musicSymbol),
                Artist = ScheduleDisplayMetadataHelper.BuildScheduleSubtitle(scheduleItem, musicSymbol),
                Album = null,
                ArtworkUrl = null
            };
        }

        string title;
        if (firstPlayItem.Metadata?.PlayType == PlayType.Bible)
        {
            title = $"Section {firstPlayItem.Metadata.SectionCode} Track {firstPlayItem.Metadata.TrackCode}";
        }
        else if (firstPlayItem.Metadata?.TrackCode != null)
        {
            title = $"Track {firstPlayItem.Metadata.TrackCode}";
        }
        else
        {
            title = string.Empty;
        }

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
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            logger.Warning(ex, AppConstants.Logging.DisposableLifetimeLog.ErrorDuringCancellationTokenSourceDisposal);
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
            return schedules.ElementAt(0).Id;
        }

        return null;
    }

    /// <summary>
    /// Cleans up old default schedule artwork files to prevent accumulation.
    /// Removes files older than 1 minute.
    /// </summary>
    private void CleanupOldDefaultScheduleArtworkFiles(string artworkDir)
    {
        try
        {
            var artworkFiles = Directory.GetFiles(artworkDir, "default_schedule_artwork_*.jpg")
                .Select(f => new FileInfo(f))
                .Where(f => f.LastWriteTimeUtc < DateTime.UtcNow.AddMinutes(-1))
                .ToList();

            foreach (var file in artworkFiles)
            {
                try
                {
                    file.Delete();
                }
                catch (Exception)
                {
                    // Ignore deletion errors - file might be in use
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.DefaultScheduleServiceDiagnosticsLog.FailedToCleanupOldDefaultScheduleArtworkFiles);
        }
    }
}

