#nullable enable

using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Shared.Constants;
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
        var publicationChanged = !Bible.Alarm.Shared.Helpers.PublicationCodeHelper.CodeEquals(currentPublicationCode, publicationCode);
        var sectionChanged = !Bible.Alarm.Shared.Helpers.SectionCodeHelper.CodeEquals(currentSectionCode, sectionCode);
        var trackChanged = !Bible.Alarm.Shared.Helpers.CodeComparisonHelper.Equals(currentTrackCode, trackCode);
        var publicationModalCountChanged = currentSchedule.MusicPublicationModalItemCount != publicationModalItemCount;
        var sectionModalCountChanged = currentSchedule.MusicSectionModalItemCount != sectionModalItemCount;

        if (Bible.Alarm.Shared.Helpers.PublicationCodeHelper.CodeEquals(currentSchedule.MusicPublicationCode, publicationCode) &&
            Bible.Alarm.Shared.Helpers.SectionCodeHelper.CodeEquals(currentSectionCode, sectionCode) &&
            Bible.Alarm.Shared.Helpers.CodeComparisonHelper.Equals(currentTrackCode, trackCode) &&
            currentTrackTitle == trackTitle &&
            !publicationModalCountChanged &&
            !sectionModalCountChanged)
        {
            logger.Debug(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.ValuesUnchangedSkippingDispatchCycle,
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
