#nullable enable
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Playlist;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media.PlaylistServiceHelpers;

/// <summary>
/// Handles marking tracks as played or finished.
/// </summary>
public sealed class TrackMarker(
    IAlarmScheduleService alarmScheduleService,
    IDiskCacheService? diskCacheService,
    IDispatcher dispatcher,
    CancellationToken cancellationToken)
{
    public record NextTrackInfo(int? NextTrackNumber, KeyValuePair<BiblePublicationSection, BiblePublicationTrack>? NextTrack);

    /// <summary>
    /// Marks a track as played.
    /// </summary>
    public async Task MarkTrackAsPlayed(TrackMetadata trackMetadata, Func<TrackMetadata, Task<bool>> checkIfTrackChanged, Func<TrackMetadata, Task<int?>> getNextTrackNumber)
    {
        var trackChanged = await checkIfTrackChanged(trackMetadata);
        var nextTrackNumber = await getNextTrackNumber(trackMetadata);

        var updatedSchedule = await UpdateScheduleForPlayedTrack(trackMetadata, nextTrackNumber);

        if (trackChanged)
        {
            dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
        }
    }

    /// <summary>
    /// Marks a track as finished.
    /// </summary>
    public async Task MarkTrackAsFinished(
        TrackMetadata trackMetadata,
        Func<TrackMetadata, Task<NextTrackInfo>> getNextTrackInfo,
        Func<AlarmSchedule, TrackMetadata, NextTrackInfo, AlarmSchedule> updateScheduleForFinishedTrack)
    {
        // Get next track/track before updating
        var nextTrackInfo = await getNextTrackInfo(trackMetadata);

        // Clear cache BEFORE save to prevent stale cache if process crashes
        const string CacheKey = "ScheduleList";
        diskCacheService?.Remove(CacheKey);

        // Update schedule using service
        var updatedSchedule = await alarmScheduleService.UpdateScheduleByIdAsync(
            (int)trackMetadata.ScheduleId,
            schedule => updateScheduleForFinishedTrack(schedule, trackMetadata, nextTrackInfo),
            cancellationToken);

        // Update the Fluxor store to trigger state change and UI refresh
        dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
    }

    private async Task<AlarmSchedule> UpdateScheduleForPlayedTrack(
        TrackMetadata trackMetadata,
        int? nextTrackNumber)
    {
        // Clear cache BEFORE save to prevent stale cache if process crashes
        const string CacheKey = "ScheduleList";
        diskCacheService?.Remove(CacheKey);

        return await alarmScheduleService.UpdateScheduleByIdAsync(
            (int)trackMetadata.ScheduleId,
            schedule => PlaylistTrackUpdater.UpdateScheduleForPlayedTrackInternal(schedule, trackMetadata, nextTrackNumber),
            cancellationToken);
    }
}
