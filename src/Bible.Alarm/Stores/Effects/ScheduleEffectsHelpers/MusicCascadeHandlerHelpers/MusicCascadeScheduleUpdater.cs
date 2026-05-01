#nullable enable

using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
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
        MusicCascadeScheduleMutation mutation,
        IDispatcher dispatcher)
    {
        var m = mutation;
        var currentSectionCode = currentSchedule.MusicSectionCode;
        var currentTrackCode = currentSchedule.MusicTrackCode;
        var currentTrackTitle = currentSchedule.MusicTrackName;
        var currentPublicationCode = currentSchedule.MusicPublicationCode;
        var publicationChanged = !PublicationCodeHelper.CodeEquals(currentPublicationCode, m.PublicationCode);
        var sectionChanged = !SectionCodeHelper.CodeEquals(currentSectionCode, m.SectionCode);
        var trackChanged = !CodeComparisonHelper.Equals(currentTrackCode, m.TrackCode);
        var publicationModalCountChanged = currentSchedule.MusicPublicationModalItemCount != m.PublicationModalItemCount;
        var sectionModalCountChanged = currentSchedule.MusicSectionModalItemCount != m.SectionModalItemCount;

        if (PublicationCodeHelper.CodeEquals(currentSchedule.MusicPublicationCode, m.PublicationCode) &&
            SectionCodeHelper.CodeEquals(currentSectionCode, m.SectionCode) &&
            CodeComparisonHelper.Equals(currentTrackCode, m.TrackCode) &&
            currentTrackTitle == m.TrackTitle &&
            !publicationModalCountChanged &&
            !sectionModalCountChanged)
        {
            logger.Debug(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.ValuesUnchangedSkippingDispatchCycle,
                m.PublicationCode, m.SectionCode ?? "null", m.TrackCode);
            return;
        }

        var updatedSchedule = currentSchedule.DeepClone();
        updatedSchedule.MusicPublicationCode = m.PublicationCode;
        AssignMusicPublicationDisplayName(updatedSchedule, m, publicationChanged);
        AssignMusicSectionName(updatedSchedule, m, publicationChanged, sectionChanged);
        updatedSchedule.MusicTrackCode = m.TrackCode;
        AssignMusicTrackTitle(updatedSchedule, m, publicationChanged, sectionChanged, trackChanged);

        updatedSchedule.MusicPublicationModalItemCount = m.PublicationModalItemCount;
        updatedSchedule.MusicSectionModalItemCount = m.SectionModalItemCount;

        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, musicUpdated: true, biblePublicationUpdated: false, shouldSave: false));
    }

    private static void AssignMusicPublicationDisplayName(ScheduleStateItem updatedSchedule, MusicCascadeScheduleMutation m, bool publicationChanged)
    {
        if (publicationChanged)
        {
            updatedSchedule.MusicPublicationName = !string.IsNullOrWhiteSpace(m.PublicationName)
                ? m.PublicationName
                : m.PublicationCode;
        }
        else if (!string.IsNullOrWhiteSpace(m.PublicationName))
        {
            updatedSchedule.MusicPublicationName = m.PublicationName;
        }
    }

    private static void AssignMusicSectionName(ScheduleStateItem updatedSchedule, MusicCascadeScheduleMutation m, bool publicationChanged, bool sectionChanged)
    {
        updatedSchedule.MusicSectionCode = m.SectionCode;
        if (publicationChanged || sectionChanged)
        {
            updatedSchedule.MusicSectionName = !string.IsNullOrWhiteSpace(m.SectionName)
                ? m.SectionName
                : null;
        }
        else if (!string.IsNullOrWhiteSpace(m.SectionName))
        {
            updatedSchedule.MusicSectionName = m.SectionName;
        }
    }

    private static void AssignMusicTrackTitle(
        ScheduleStateItem updatedSchedule,
        MusicCascadeScheduleMutation m,
        bool publicationChanged,
        bool sectionChanged,
        bool trackChanged)
    {
        if (publicationChanged || sectionChanged || trackChanged)
        {
            updatedSchedule.MusicTrackName = !string.IsNullOrWhiteSpace(m.TrackTitle)
                ? m.TrackTitle
                : null;
        }
        else if (!string.IsNullOrWhiteSpace(m.TrackTitle))
        {
            updatedSchedule.MusicTrackName = m.TrackTitle;
        }
    }
}
