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
        IDispatcher dispatcher,
        bool publicationWithoutLanguage = false)
    {
        var normalizedSectionCode = SectionCodeHelper.Normalize(sectionCode);
        var currentSectionCode = currentSchedule.BiblePublicationSectionCode;
        var currentTrackCode = currentSchedule.BiblePublicationTrackCode;
        var currentTrackTitle = currentSchedule.BiblePublicationTrackTitle;
        var currentPublicationCode = currentSchedule.BiblePublicationCode;
        var publicationChanged = !string.Equals(currentPublicationCode, publicationCode, StringComparison.OrdinalIgnoreCase);
        var sectionChanged = !string.Equals(currentSectionCode, normalizedSectionCode, StringComparison.OrdinalIgnoreCase);
        var trackChanged = currentTrackCode != trackCode;
        var publicationModalCountChanged = currentSchedule.BiblePublicationModalItemCount != publicationModalItemCount;
        var sectionModalCountChanged = currentSchedule.BiblePublicationSectionModalItemCount != sectionModalItemCount;

        if (currentSchedule.BiblePublicationCode == publicationCode &&
            string.Equals(currentSectionCode, normalizedSectionCode, StringComparison.OrdinalIgnoreCase) &&
            currentTrackCode == trackCode &&
            currentTrackTitle == trackTitle &&
            !publicationModalCountChanged &&
            !sectionModalCountChanged)
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

        if (string.IsNullOrWhiteSpace(updatedSchedule.BiblePublicationCategoryName) && !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationCategoryName))
        {
            updatedSchedule.BiblePublicationCategoryId = currentSchedule.BiblePublicationCategoryId;
            updatedSchedule.BiblePublicationCategoryName = currentSchedule.BiblePublicationCategoryName;
            logger.Debug("BiblePublicationCascadeHandler: Preserving category={CategoryName}", currentSchedule.BiblePublicationCategoryName);
        }

        if (publicationWithoutLanguage)
        {
            updatedSchedule.BiblePublicationLanguageCode = AppConstants.Media.DefaultLanguageCode;
            updatedSchedule.BiblePublicationLanguageName = null;
            updatedSchedule.BiblePublicationLanguageDirection = AppConstants.Media.TextDirectionLeftToRight;
            logger.Debug("BiblePublicationCascadeHandler: Setting language to English default for no-language publication={PublicationCode}", publicationCode);
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
