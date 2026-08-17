#nullable enable
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Stores.Reducers.Services;

public static class DisplayNamePreservationHelper
{
    public static void PreserveDisplayNamesFromExisting(ScheduleStateItem actionSchedule, ScheduleStateItem existingScheduleItem)
    {
        Log.Debug(AppConstants.Logging.ApplicationReducerDiagnosticsLog.PreserveDisplayNamesFoundExistingSchedule,
            existingScheduleItem.BiblePublicationLanguageName ?? "null",
            actionSchedule.BiblePublicationLanguageName ?? "null");

        PreserveBiblePublicationDisplayNames(actionSchedule, existingScheduleItem);
        PreserveMusicDisplayNames(actionSchedule, existingScheduleItem);
    }

    public static void PreserveBiblePublicationDisplayNames(ScheduleStateItem actionSchedule, ScheduleStateItem existingScheduleItem)
    {
        PreserveBiblePublicationCategory(actionSchedule, existingScheduleItem);
        PreserveBiblePublicationLanguage(actionSchedule, existingScheduleItem);

        if (string.IsNullOrWhiteSpace(actionSchedule.BiblePublicationName) && !string.IsNullOrWhiteSpace(existingScheduleItem.BiblePublicationName))
        {
            actionSchedule.BiblePublicationName = existingScheduleItem.BiblePublicationName;
        }

        CopyMissingBibleModalCounts(actionSchedule, existingScheduleItem);

        var actionHasSectionStructure = !string.IsNullOrWhiteSpace(actionSchedule.BiblePublicationCode) &&
            PublicationTypeHelper.HasSectionStructure(actionSchedule.BiblePublicationCode);
        var existingHasSectionStructure = !string.IsNullOrWhiteSpace(existingScheduleItem.BiblePublicationCode) &&
            PublicationTypeHelper.HasSectionStructure(existingScheduleItem.BiblePublicationCode);

        PreserveEligibleBibleSectionName(actionSchedule, existingScheduleItem, actionHasSectionStructure, existingHasSectionStructure);
        PreserveEligibleBibleNonSectionTrackTitle(actionSchedule, existingScheduleItem, actionHasSectionStructure, existingHasSectionStructure);
    }

    public static void PreserveMusicDisplayNames(ScheduleStateItem actionSchedule, ScheduleStateItem existingScheduleItem)
    {
        PreserveMusicPublicationStrings(actionSchedule, existingScheduleItem);
        PreserveMusicSectionTitleIfEligible(actionSchedule, existingScheduleItem);
        CopyMusicTrackAndModalCounts(actionSchedule, existingScheduleItem);
    }

    private static void PreserveBiblePublicationCategory(ScheduleStateItem actionSchedule, ScheduleStateItem existingScheduleItem)
    {
        if (string.IsNullOrWhiteSpace(actionSchedule.BiblePublicationCategoryName) && !string.IsNullOrWhiteSpace(existingScheduleItem.BiblePublicationCategoryName))
        {
            Log.Debug(AppConstants.Logging.ApplicationReducerDiagnosticsLog.PreserveDisplayNamesPreservingExistingCategoryName,
                existingScheduleItem.BiblePublicationCategoryName);
            actionSchedule.BiblePublicationCategoryId = existingScheduleItem.BiblePublicationCategoryId;
            actionSchedule.BiblePublicationCategoryName = existingScheduleItem.BiblePublicationCategoryName;
            return;
        }

        if (!string.IsNullOrWhiteSpace(actionSchedule.BiblePublicationCategoryName))
        {
            Log.Debug(AppConstants.Logging.ApplicationReducerDiagnosticsLog.PreserveDisplayNamesUsingActionCategoryName,
                actionSchedule.BiblePublicationCategoryName);
            return;
        }

        Log.Warning(AppConstants.Logging.ApplicationReducerDiagnosticsLog.PreserveDisplayNamesCategoryNullInBothWarning);
    }

    private static void PreserveBiblePublicationLanguage(ScheduleStateItem actionSchedule, ScheduleStateItem existingScheduleItem)
    {
        if (string.IsNullOrWhiteSpace(actionSchedule.BiblePublicationLanguageName) && !string.IsNullOrWhiteSpace(existingScheduleItem.BiblePublicationLanguageName))
        {
            Log.Debug(AppConstants.Logging.ApplicationReducerDiagnosticsLog.PreserveDisplayNamesPreservingExistingLanguageName,
                existingScheduleItem.BiblePublicationLanguageName);
            actionSchedule.BiblePublicationLanguageName = existingScheduleItem.BiblePublicationLanguageName;
            return;
        }

        Log.Debug(AppConstants.Logging.ApplicationReducerDiagnosticsLog.PreserveDisplayNamesUsingActionLanguageName,
            actionSchedule.BiblePublicationLanguageName ?? "null");
    }

    private static void CopyMissingBibleModalCounts(ScheduleStateItem actionSchedule, ScheduleStateItem existingScheduleItem)
    {
        if (!actionSchedule.BiblePublicationModalItemCount.HasValue && existingScheduleItem.BiblePublicationModalItemCount.HasValue)
        {
            actionSchedule.BiblePublicationModalItemCount = existingScheduleItem.BiblePublicationModalItemCount;
        }

        if (!actionSchedule.BiblePublicationSectionModalItemCount.HasValue && existingScheduleItem.BiblePublicationSectionModalItemCount.HasValue)
        {
            actionSchedule.BiblePublicationSectionModalItemCount = existingScheduleItem.BiblePublicationSectionModalItemCount;
        }

        if (!actionSchedule.BiblePublicationTrackModalItemCount.HasValue && existingScheduleItem.BiblePublicationTrackModalItemCount.HasValue)
        {
            actionSchedule.BiblePublicationTrackModalItemCount = existingScheduleItem.BiblePublicationTrackModalItemCount;
        }
    }

    private static void PreserveEligibleBibleSectionName(
        ScheduleStateItem actionSchedule,
        ScheduleStateItem existingScheduleItem,
        bool actionHasSectionStructure,
        bool existingHasSectionStructure)
    {
        var sectionCodeUnchanged = string.Equals(actionSchedule.BiblePublicationSectionCode, existingScheduleItem.BiblePublicationSectionCode, StringComparison.OrdinalIgnoreCase);
        if (actionHasSectionStructure == existingHasSectionStructure && actionHasSectionStructure && sectionCodeUnchanged
            && string.IsNullOrWhiteSpace(actionSchedule.BiblePublicationSectionName) && !string.IsNullOrWhiteSpace(existingScheduleItem.BiblePublicationSectionName))
        {
            Log.Debug(AppConstants.Logging.ApplicationReducerDiagnosticsLog.PreserveDisplayNamesPreservingExistingBibleSectionName,
                existingScheduleItem.BiblePublicationSectionName);
            actionSchedule.BiblePublicationSectionName = existingScheduleItem.BiblePublicationSectionName;
        }
    }

    private static void PreserveEligibleBibleNonSectionTrackTitle(
        ScheduleStateItem actionSchedule,
        ScheduleStateItem existingScheduleItem,
        bool actionHasSectionStructure,
        bool existingHasSectionStructure)
    {
        var trackCodeUnchanged = string.Equals(actionSchedule.BiblePublicationTrackCode, existingScheduleItem.BiblePublicationTrackCode, StringComparison.OrdinalIgnoreCase);
        if (actionHasSectionStructure == existingHasSectionStructure && !actionHasSectionStructure && trackCodeUnchanged)
        {
            if (string.IsNullOrWhiteSpace(actionSchedule.BiblePublicationTrackTitle) && !string.IsNullOrWhiteSpace(existingScheduleItem.BiblePublicationTrackTitle))
            {
                Log.Debug(AppConstants.Logging.ApplicationReducerDiagnosticsLog.PreserveDisplayNamesPreservingExistingBibleTrackTitle,
                    existingScheduleItem.BiblePublicationTrackTitle);
                actionSchedule.BiblePublicationTrackTitle = existingScheduleItem.BiblePublicationTrackTitle;
            }
            else if (!string.IsNullOrWhiteSpace(actionSchedule.BiblePublicationTrackTitle))
            {
                Log.Debug(AppConstants.Logging.ApplicationReducerDiagnosticsLog.PreserveDisplayNamesUsingActionBibleTrackTitle,
                    actionSchedule.BiblePublicationTrackTitle);
            }

            return;
        }

        Log.Debug(AppConstants.Logging.ApplicationReducerDiagnosticsLog.PreserveDisplayNamesPublicationTypeChanged,
            actionHasSectionStructure, existingHasSectionStructure);
    }

    private static void PreserveMusicPublicationStrings(ScheduleStateItem actionSchedule, ScheduleStateItem existingScheduleItem)
    {
        if (string.IsNullOrWhiteSpace(actionSchedule.MusicLanguageName) && !string.IsNullOrWhiteSpace(existingScheduleItem.MusicLanguageName))
        {
            actionSchedule.MusicLanguageName = existingScheduleItem.MusicLanguageName;
        }

        if (string.IsNullOrWhiteSpace(actionSchedule.MusicLanguageDirection) && !string.IsNullOrWhiteSpace(existingScheduleItem.MusicLanguageDirection))
        {
            actionSchedule.MusicLanguageDirection = existingScheduleItem.MusicLanguageDirection;
        }

        if (string.IsNullOrWhiteSpace(actionSchedule.MusicPublicationName) && !string.IsNullOrWhiteSpace(existingScheduleItem.MusicPublicationName))
        {
            actionSchedule.MusicPublicationName = existingScheduleItem.MusicPublicationName;
        }
    }

    private static void PreserveMusicSectionTitleIfEligible(ScheduleStateItem actionSchedule, ScheduleStateItem existingScheduleItem)
    {
        var actionMusicSectioned = !string.IsNullOrWhiteSpace(actionSchedule.MusicPublicationCode) &&
            PublicationTypeHelper.HasSectionStructure(actionSchedule.MusicPublicationCode);
        var existingMusicSectioned = !string.IsNullOrWhiteSpace(existingScheduleItem.MusicPublicationCode) &&
            PublicationTypeHelper.HasSectionStructure(existingScheduleItem.MusicPublicationCode);

        if (actionMusicSectioned == existingMusicSectioned && actionMusicSectioned
            && string.IsNullOrWhiteSpace(actionSchedule.MusicSectionName) && !string.IsNullOrWhiteSpace(existingScheduleItem.MusicSectionName))
        {
            Log.Debug(AppConstants.Logging.ApplicationReducerDiagnosticsLog.PreserveDisplayNamesPreservingExistingMusicSectionName,
                existingScheduleItem.MusicSectionName);
            actionSchedule.MusicSectionName = existingScheduleItem.MusicSectionName;
        }
    }

    private static void CopyMusicTrackAndModalCounts(ScheduleStateItem actionSchedule, ScheduleStateItem existingScheduleItem)
    {
        if (string.IsNullOrWhiteSpace(actionSchedule.MusicTrackName) && !string.IsNullOrWhiteSpace(existingScheduleItem.MusicTrackName))
        {
            actionSchedule.MusicTrackName = existingScheduleItem.MusicTrackName;
        }

        if (!actionSchedule.MusicPublicationModalItemCount.HasValue && existingScheduleItem.MusicPublicationModalItemCount.HasValue)
        {
            actionSchedule.MusicPublicationModalItemCount = existingScheduleItem.MusicPublicationModalItemCount;
        }

        if (!actionSchedule.MusicSectionModalItemCount.HasValue && existingScheduleItem.MusicSectionModalItemCount.HasValue)
        {
            actionSchedule.MusicSectionModalItemCount = existingScheduleItem.MusicSectionModalItemCount;
        }
    }
}

