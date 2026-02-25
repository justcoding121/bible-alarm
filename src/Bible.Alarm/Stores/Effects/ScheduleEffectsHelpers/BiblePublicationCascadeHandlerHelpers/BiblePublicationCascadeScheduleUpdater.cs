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
        string publicationCode,
        string? publicationName,
        string? sectionCode,
        string sectionName,
        string trackCode,
        string trackTitle,
        int? publicationModalItemCount,
        int? sectionModalItemCount,
        int? trackModalItemCount,
        IDispatcher dispatcher,
        bool publicationWithoutLanguage = false)
    {
        var normalizedSectionCode = SectionCodeHelper.Normalize(sectionCode);
        var currentSectionCode = currentSchedule.BiblePublicationSectionCode;
        var currentTrackCode = currentSchedule.BiblePublicationTrackCode;
        var currentTrackTitle = currentSchedule.BiblePublicationTrackTitle;
        var currentPublicationCode = currentSchedule.BiblePublicationCode;
        var publicationChanged = !PublicationCodeHelper.CodeEquals(currentPublicationCode, publicationCode);
        var sectionChanged = !SectionCodeHelper.CodeEquals(currentSectionCode, normalizedSectionCode);
        var trackChanged = !CodeComparisonHelper.Equals(currentTrackCode, trackCode);
        var publicationModalCountChanged = currentSchedule.BiblePublicationModalItemCount != publicationModalItemCount;
        var sectionModalCountChanged = currentSchedule.BiblePublicationSectionModalItemCount != sectionModalItemCount;
        var trackModalCountChanged = currentSchedule.BiblePublicationTrackModalItemCount != trackModalItemCount;

        if (PublicationCodeHelper.CodeEquals(currentSchedule.BiblePublicationCode, publicationCode) &&
            SectionCodeHelper.CodeEquals(currentSectionCode, normalizedSectionCode) &&
            CodeComparisonHelper.Equals(currentTrackCode, trackCode) &&
            currentTrackTitle == trackTitle &&
            !publicationModalCountChanged &&
            !sectionModalCountChanged &&
            !trackModalCountChanged)
        {
            logger.Debug("BiblePublicationCascadeHandler: Values unchanged, skipping dispatch. publication={PublicationCode}, sectionCode={SectionCode}, track={TrackCode}",
                publicationCode, normalizedSectionCode ?? "(none)", trackCode);
            return;
        }

        var updatedSchedule = currentSchedule.DeepClone();
        updatedSchedule.BiblePublicationCode = publicationCode;
        if (publicationChanged)
        {
            updatedSchedule.BiblePublicationName = !string.IsNullOrWhiteSpace(publicationName) ? publicationName : publicationCode;
        }
        else if (!string.IsNullOrWhiteSpace(publicationName))
        {
            updatedSchedule.BiblePublicationName = publicationName;
        }
        updatedSchedule.BiblePublicationSectionCode = normalizedSectionCode;
        if (publicationChanged || sectionChanged)
        {
            updatedSchedule.BiblePublicationSectionName = !string.IsNullOrWhiteSpace(sectionName) ? sectionName : null;
        }
        else if (!string.IsNullOrWhiteSpace(sectionName))
        {
            updatedSchedule.BiblePublicationSectionName = sectionName;
        }
        updatedSchedule.BiblePublicationTrackCode = trackCode;
        if (publicationChanged || sectionChanged || trackChanged)
        {
            updatedSchedule.BiblePublicationTrackTitle = !string.IsNullOrWhiteSpace(trackTitle) ? trackTitle : null;
        }
        else if (!string.IsNullOrWhiteSpace(trackTitle))
        {
            updatedSchedule.BiblePublicationTrackTitle = trackTitle;
        }
        updatedSchedule.BiblePublicationModalItemCount = publicationModalItemCount;
        updatedSchedule.BiblePublicationSectionModalItemCount = sectionModalItemCount;
        updatedSchedule.BiblePublicationTrackModalItemCount = trackModalItemCount;

        if (string.IsNullOrWhiteSpace(updatedSchedule.BiblePublicationCategoryName) && !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationCategoryName))
        {
            updatedSchedule.BiblePublicationCategoryId = currentSchedule.BiblePublicationCategoryId;
            updatedSchedule.BiblePublicationCategoryName = currentSchedule.BiblePublicationCategoryName;
            logger.Debug("BiblePublicationCascadeHandler: Preserving category={CategoryName}", currentSchedule.BiblePublicationCategoryName);
        }

        if (publicationWithoutLanguage)
        {
            updatedSchedule.BiblePublicationLanguageCode = !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationLanguageCode)
                ? currentSchedule.BiblePublicationLanguageCode
                : AppConstants.Media.DefaultLanguageCode;
            updatedSchedule.BiblePublicationLanguageName = currentSchedule.BiblePublicationLanguageName;
            updatedSchedule.BiblePublicationLanguageDirection = !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationLanguageDirection)
                ? currentSchedule.BiblePublicationLanguageDirection
                : AppConstants.Media.TextDirectionLeftToRight;
            logger.Debug("BiblePublicationCascadeHandler: {Action} language for no-language publication={PublicationCode} (LanguageCode: {LanguageCode})",
                string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationLanguageCode) ? "Setting default" : "Preserving",
                publicationCode,
                updatedSchedule.BiblePublicationLanguageCode ?? "null");
        }
        else if (string.IsNullOrWhiteSpace(updatedSchedule.BiblePublicationLanguageCode) && !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationLanguageCode))
        {
            updatedSchedule.BiblePublicationLanguageCode = currentSchedule.BiblePublicationLanguageCode;
            updatedSchedule.BiblePublicationLanguageName = currentSchedule.BiblePublicationLanguageName;
            updatedSchedule.BiblePublicationLanguageDirection = currentSchedule.BiblePublicationLanguageDirection;
            logger.Debug("BiblePublicationCascadeHandler: Preserving language={LanguageCode}", currentSchedule.BiblePublicationLanguageCode);
        }

        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
    }
}
