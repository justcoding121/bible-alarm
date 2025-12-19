#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.Scheduler.Models;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Services.Scheduler;

/// <summary>
/// Service for getting the next schedule track metadata for Android Auto MediaSession.
/// Returns track metadata and scheduleId for the next schedule to be played.
/// Downloads the first track and uses DisplayMetadataService to get full metadata (same as PreparePlaybackService).
/// </summary>
public class DefaultScheduleService(
    ILogger logger,
    IState<ApplicationState> applicationState,
    IState<PlaybackState> playbackState,
    IPlaylistService playlistService,
    IPreparePlaybackService preparePlaybackService,
    IDisplayMetadataService displayMetadataService) : IDefaultScheduleService, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _isDisposed;

    public async Task<ScheduleTrackMetadata> GetNextScheduleTrackMetaDataAsync()
    {
        // IMPORTANT:
        // This method must NOT call database seeding or create schedules.
        // Bootstrap guarantees schedules are loaded into Fluxor state, and UI enforces at least one schedule.
        //
        // Pick a scheduleId deterministically from state:
        // - Prefer the currently/most-recently active playback schedule (if available)
        // - Else prefer the currently selected schedule in state
        // - Else fall back to the first schedule in the schedule list (guaranteed post-bootstrap)
        var scheduleId =
            playbackState.Value.CurrentScheduleId
            ?? applicationState.Value.CurrentSchedule?.Id
            ?? applicationState.Value.Schedules.FirstOrDefault()?.Id;

        if (!scheduleId.HasValue || scheduleId.Value <= 0)
        {
            logger.Warning("GetNextScheduleTrackMetaDataAsync: No schedules available in state; returning fallback metadata");
            // Fallback: create "empty" metadata with an invalid schedule id
            return new ScheduleTrackMetadata
            {
                ScheduleId = scheduleId ?? 0,
                Title = "",
                Artist = "",
                Album = ""
            };
        }

        // Get track metadata for the schedule
        return await GetTrackMetadataForScheduleAsync(scheduleId.Value);
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

