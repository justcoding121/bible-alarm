#nullable enable
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers.BiblePublicationCascadeHandlerHelpers;

/// <summary>
/// Updates schedule state from Bible publication cascade and dispatches action.
/// </summary>
public static class BiblePublicationCascadeScheduleUpdater
{
    public static void UpdateSchedule(
        ILogger logger,
        ScheduleStateItem currentSchedule,
        BiblePublicationCascadeScheduleMutation mutation,
        IDispatcher dispatcher)
    {
        var m = mutation;
        var normalizedSectionCode = SectionCodeHelper.Normalize(m.SectionCode);
        var currentSectionCode = currentSchedule.BiblePublicationSectionCode;
        var currentTrackCode = currentSchedule.BiblePublicationTrackCode;
        var currentTrackTitle = currentSchedule.BiblePublicationTrackTitle;
        var currentPublicationCode = currentSchedule.BiblePublicationCode;
        var publicationChanged = !PublicationCodeHelper.CodeEquals(currentPublicationCode, m.PublicationCode);
        var sectionChanged = !SectionCodeHelper.CodeEquals(currentSectionCode, normalizedSectionCode);
        var trackChanged = !CodeComparisonHelper.Equals(currentTrackCode, m.TrackCode);
        var publicationModalCountChanged = currentSchedule.BiblePublicationModalItemCount != m.PublicationModalItemCount;
        var sectionModalCountChanged = currentSchedule.BiblePublicationSectionModalItemCount != m.SectionModalItemCount;
        var trackModalCountChanged = currentSchedule.BiblePublicationTrackModalItemCount != m.TrackModalItemCount;

        if (PublicationCodeHelper.CodeEquals(currentSchedule.BiblePublicationCode, m.PublicationCode) &&
            SectionCodeHelper.CodeEquals(currentSectionCode, normalizedSectionCode) &&
            CodeComparisonHelper.Equals(currentTrackCode, m.TrackCode) &&
            currentTrackTitle == m.TrackTitle &&
            !publicationModalCountChanged &&
            !sectionModalCountChanged &&
            !trackModalCountChanged)
        {
            logger.Debug(AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.ValuesUnchangedSkippingDispatch,
                m.PublicationCode, normalizedSectionCode ?? "(none)", m.TrackCode);
            return;
        }

        var updatedSchedule = currentSchedule.DeepClone();
        updatedSchedule.BiblePublicationCode = m.PublicationCode;
        AssignBiblePublicationName(updatedSchedule, m, publicationChanged);
        AssignBibleSectionFields(updatedSchedule, m, normalizedSectionCode, publicationChanged, sectionChanged);
        AssignBibleTrackFields(updatedSchedule, m, publicationChanged, sectionChanged, trackChanged);

        updatedSchedule.BiblePublicationModalItemCount = m.PublicationModalItemCount;
        updatedSchedule.BiblePublicationSectionModalItemCount = m.SectionModalItemCount;
        updatedSchedule.BiblePublicationTrackModalItemCount = m.TrackModalItemCount;

        if (publicationChanged || sectionChanged || trackChanged)
        {
            updatedSchedule.BiblePublicationFinishedDuration = TimeSpan.Zero;
        }

        PreserveCategoryIfClear(updatedSchedule, currentSchedule, logger);
        AlignLanguageFields(updatedSchedule, currentSchedule, m, logger);

        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
    }

    private static void AssignBiblePublicationName(ScheduleStateItem updated, BiblePublicationCascadeScheduleMutation m, bool publicationChanged)
    {
        if (publicationChanged)
        {
            updated.BiblePublicationName = !string.IsNullOrWhiteSpace(m.PublicationName) ? m.PublicationName : m.PublicationCode;
        }
        else if (!string.IsNullOrWhiteSpace(m.PublicationName))
        {
            updated.BiblePublicationName = m.PublicationName;
        }
    }

    private static void AssignBibleSectionFields(
        ScheduleStateItem updated,
        BiblePublicationCascadeScheduleMutation m,
        string? normalizedSectionCode,
        bool publicationChanged,
        bool sectionChanged)
    {
        updated.BiblePublicationSectionCode = normalizedSectionCode;
        if (publicationChanged || sectionChanged)
        {
            updated.BiblePublicationSectionName = !string.IsNullOrWhiteSpace(m.SectionName) ? m.SectionName : null;
        }
        else if (!string.IsNullOrWhiteSpace(m.SectionName))
        {
            updated.BiblePublicationSectionName = m.SectionName;
        }
    }

    private static void AssignBibleTrackFields(
        ScheduleStateItem updated,
        BiblePublicationCascadeScheduleMutation m,
        bool publicationChanged,
        bool sectionChanged,
        bool trackChanged)
    {
        updated.BiblePublicationTrackCode = m.TrackCode;
        if (publicationChanged || sectionChanged || trackChanged)
        {
            updated.BiblePublicationTrackTitle = !string.IsNullOrWhiteSpace(m.TrackTitle) ? m.TrackTitle : null;
        }
        else if (!string.IsNullOrWhiteSpace(m.TrackTitle))
        {
            updated.BiblePublicationTrackTitle = m.TrackTitle;
        }
    }

    private static void PreserveCategoryIfClear(ScheduleStateItem updated, ScheduleStateItem current, ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(updated.BiblePublicationCategoryName) ||
            string.IsNullOrWhiteSpace(current.BiblePublicationCategoryName))
        {
            return;
        }

        updated.BiblePublicationCategoryId = current.BiblePublicationCategoryId;
        updated.BiblePublicationCategoryName = current.BiblePublicationCategoryName;
        logger.Debug(AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.PreservingCategory, current.BiblePublicationCategoryName);
    }

    private static void AlignLanguageFields(
        ScheduleStateItem updated,
        ScheduleStateItem current,
        BiblePublicationCascadeScheduleMutation m,
        ILogger logger)
    {
        if (m.PublicationWithoutLanguage)
        {
            updated.BiblePublicationLanguageCode = !string.IsNullOrWhiteSpace(current.BiblePublicationLanguageCode)
                ? current.BiblePublicationLanguageCode
                : AppConstants.Media.DefaultLanguageCode;
            updated.BiblePublicationLanguageName = current.BiblePublicationLanguageName;
            updated.BiblePublicationLanguageDirection = !string.IsNullOrWhiteSpace(current.BiblePublicationLanguageDirection)
                ? current.BiblePublicationLanguageDirection
                : AppConstants.Media.TextDirectionLeftToRight;
            logger.Debug(AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.ActionLanguageForNoLanguagePublication,
                string.IsNullOrWhiteSpace(current.BiblePublicationLanguageCode) ? "Setting default" : "Preserving",
                m.PublicationCode,
                updated.BiblePublicationLanguageCode ?? "null");
            return;
        }

        if (!string.IsNullOrWhiteSpace(updated.BiblePublicationLanguageCode) ||
            string.IsNullOrWhiteSpace(current.BiblePublicationLanguageCode))
        {
            return;
        }

        updated.BiblePublicationLanguageCode = current.BiblePublicationLanguageCode;
        updated.BiblePublicationLanguageName = current.BiblePublicationLanguageName;
        updated.BiblePublicationLanguageDirection = current.BiblePublicationLanguageDirection;
        logger.Debug(AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.PreservingLanguage, current.BiblePublicationLanguageCode);
    }
}
