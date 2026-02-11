#nullable enable

using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers.MusicCascadeHandlerHelpers;

/// <summary>
/// Updates schedule state from Music cascade and dispatches action.
/// </summary>
public static class MusicCascadeScheduleUpdater
{
    public static void UpdateSchedule(
        ILogger logger,
        ScheduleStateItem currentSchedule,
        string publicationCode,
        string? publicationName,
        string? sectionCode,
        string sectionName,
        string trackCode,
        string trackTitle,
        int? publicationModalItemCount,
        int? sectionModalItemCount,
        IDispatcher dispatcher)
    {
        var currentSectionCode = currentSchedule.MusicSectionCode;
        var currentTrackCode = currentSchedule.MusicTrackCode;
        var currentTrackTitle = currentSchedule.MusicTrackName;
        var currentPublicationCode = currentSchedule.MusicPublicationCode;
        var publicationChanged = !string.Equals(currentPublicationCode, publicationCode, StringComparison.OrdinalIgnoreCase);
        var sectionChanged = !string.Equals(currentSectionCode, sectionCode, StringComparison.OrdinalIgnoreCase);
        var trackChanged = currentTrackCode != trackCode;
        var publicationModalCountChanged = currentSchedule.MusicPublicationModalItemCount != publicationModalItemCount;
        var sectionModalCountChanged = currentSchedule.MusicSectionModalItemCount != sectionModalItemCount;

        if (currentSchedule.MusicPublicationCode == publicationCode &&
            string.Equals(currentSectionCode, sectionCode, StringComparison.OrdinalIgnoreCase) &&
            currentTrackCode == trackCode &&
            currentTrackTitle == trackTitle &&
            !publicationModalCountChanged &&
            !sectionModalCountChanged)
        {
            logger.Debug("MusicCascadeHandler: Values unchanged, skipping dispatch to prevent cycle. publication={PublicationCode}, section={SectionCode}, track={TrackCode}",
                publicationCode, sectionCode ?? "null", trackCode);
            return;
        }

        var updatedSchedule = currentSchedule.DeepClone();
        updatedSchedule.MusicPublicationCode = publicationCode;
        if (publicationChanged)
        {
            updatedSchedule.MusicPublicationName = !string.IsNullOrWhiteSpace(publicationName)
                ? publicationName
                : publicationCode;
        }
        else if (!string.IsNullOrWhiteSpace(publicationName))
        {
            updatedSchedule.MusicPublicationName = publicationName;
        }
        updatedSchedule.MusicSectionCode = sectionCode;
        if (publicationChanged || sectionChanged)
        {
            updatedSchedule.MusicSectionName = !string.IsNullOrWhiteSpace(sectionName)
                ? sectionName
                : null;
        }
        else if (!string.IsNullOrWhiteSpace(sectionName))
        {
            updatedSchedule.MusicSectionName = sectionName;
        }
        updatedSchedule.MusicTrackCode = trackCode;
        if (publicationChanged || sectionChanged || trackChanged)
        {
            updatedSchedule.MusicTrackName = !string.IsNullOrWhiteSpace(trackTitle)
                ? trackTitle
                : null;
        }
        else if (!string.IsNullOrWhiteSpace(trackTitle))
        {
            updatedSchedule.MusicTrackName = trackTitle;
        }

        updatedSchedule.MusicPublicationModalItemCount = publicationModalItemCount;
        updatedSchedule.MusicSectionModalItemCount = sectionModalItemCount;

        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, musicUpdated: true, biblePublicationUpdated: false, shouldSave: false));
    }
}
