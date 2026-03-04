#nullable enable
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Stores.Reducers.Services;

/// <summary>
/// Handles preservation of display names when updating schedules.
/// Separated from ApplicationReducer for better modularity.
/// </summary>
public static class DisplayNamePreservationHelper
{
    public static void PreserveDisplayNamesFromExisting(ScheduleStateItem actionSchedule, ScheduleStateItem existingScheduleItem)
    {
        Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Found existing schedule. Existing LanguageName: {ExistingLanguageName}, Action LanguageName: {ActionLanguageName}",
            existingScheduleItem.BiblePublicationLanguageName ?? "null",
            actionSchedule.BiblePublicationLanguageName ?? "null");

        PreserveBiblePublicationDisplayNames(actionSchedule, existingScheduleItem);
        PreserveMusicDisplayNames(actionSchedule, existingScheduleItem);
    }

    public static void PreserveBiblePublicationDisplayNames(ScheduleStateItem actionSchedule, ScheduleStateItem existingScheduleItem)
    {
        // ALWAYS preserve category - category can only be changed via CategorySelectionAction
        // If category is null in action schedule, preserve it from existing schedule
        if (string.IsNullOrWhiteSpace(actionSchedule.BiblePublicationCategoryName) && !string.IsNullOrWhiteSpace(existingScheduleItem.BiblePublicationCategoryName))
        {
            Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Preserving existing BiblePublicationCategoryName: {CategoryName}",
                existingScheduleItem.BiblePublicationCategoryName);
            actionSchedule.BiblePublicationCategoryId = existingScheduleItem.BiblePublicationCategoryId;
            actionSchedule.BiblePublicationCategoryName = existingScheduleItem.BiblePublicationCategoryName;
        }
        else if (!string.IsNullOrWhiteSpace(actionSchedule.BiblePublicationCategoryName))
        {
            Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Using action's BiblePublicationCategoryName: {CategoryName}",
                actionSchedule.BiblePublicationCategoryName);
        }
        else
        {
            Log.Warning("ApplicationReducer: OnUpdateScheduleFromViewModel - Category is null in both action and existing schedule. This should not happen - category must always be selected.");
        }

        if (string.IsNullOrWhiteSpace(actionSchedule.BiblePublicationLanguageName) && !string.IsNullOrWhiteSpace(existingScheduleItem.BiblePublicationLanguageName))
        {
            Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Preserving existing BiblePublicationLanguageName: {LanguageName}",
                existingScheduleItem.BiblePublicationLanguageName);
            actionSchedule.BiblePublicationLanguageName = existingScheduleItem.BiblePublicationLanguageName;
        }
        else
        {
            Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Using action's BiblePublicationLanguageName: {LanguageName}",
                actionSchedule.BiblePublicationLanguageName ?? "null");
        }

        if (string.IsNullOrWhiteSpace(actionSchedule.BiblePublicationName) && !string.IsNullOrWhiteSpace(existingScheduleItem.BiblePublicationName))
        {
            actionSchedule.BiblePublicationName = existingScheduleItem.BiblePublicationName;
        }

        // Preserve discovery-based "expected modal item counts" so unrelated updates don't wipe them.
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

        // Check if publication type changed (sectioned <-> non-sectioned)
        var actionHasSectionStructure = !string.IsNullOrWhiteSpace(actionSchedule.BiblePublicationCode) &&
            PublicationTypeHelper.HasSectionStructure(actionSchedule.BiblePublicationCode);
        var existingHasSectionStructure = !string.IsNullOrWhiteSpace(existingScheduleItem.BiblePublicationCode) &&
            PublicationTypeHelper.HasSectionStructure(existingScheduleItem.BiblePublicationCode);

        // Only preserve section name if publication type hasn't changed, both are sectioned,
        // and section CODE hasn't changed (playback track advance changes section/track; don't preserve stale names).
        var sectionCodeUnchanged = string.Equals(actionSchedule.BiblePublicationSectionCode, existingScheduleItem.BiblePublicationSectionCode, StringComparison.OrdinalIgnoreCase);
        if (actionHasSectionStructure == existingHasSectionStructure && actionHasSectionStructure && sectionCodeUnchanged)
        {
            if (string.IsNullOrWhiteSpace(actionSchedule.BiblePublicationSectionName) && !string.IsNullOrWhiteSpace(existingScheduleItem.BiblePublicationSectionName))
            {
                Log.Debug("ApplicationReducer: Preserving existing BiblePublicationSectionName: {SectionName}",
                    existingScheduleItem.BiblePublicationSectionName);
                actionSchedule.BiblePublicationSectionName = existingScheduleItem.BiblePublicationSectionName;
            }
        }
        // Only preserve track title if publication type hasn't changed, both are non-sectioned,
        // and track CODE hasn't changed (playback track advance; don't preserve stale titles).
        var trackCodeUnchanged = string.Equals(actionSchedule.BiblePublicationTrackCode, existingScheduleItem.BiblePublicationTrackCode, StringComparison.OrdinalIgnoreCase);
        if (actionHasSectionStructure == existingHasSectionStructure && !actionHasSectionStructure && trackCodeUnchanged)
        {
            if (string.IsNullOrWhiteSpace(actionSchedule.BiblePublicationTrackTitle) && !string.IsNullOrWhiteSpace(existingScheduleItem.BiblePublicationTrackTitle))
            {
                Log.Debug("ApplicationReducer: Preserving existing BiblePublicationTrackTitle: {TrackTitle}",
                    existingScheduleItem.BiblePublicationTrackTitle);
                actionSchedule.BiblePublicationTrackTitle = existingScheduleItem.BiblePublicationTrackTitle;
            }
            else if (!string.IsNullOrWhiteSpace(actionSchedule.BiblePublicationTrackTitle))
            {
                Log.Debug("ApplicationReducer: Using action's BiblePublicationTrackTitle (not preserving): {TrackTitle}",
                    actionSchedule.BiblePublicationTrackTitle);
            }
        }
        // If publication type changed, don't preserve incompatible display names
        else
        {
            Log.Debug("ApplicationReducer: Publication type changed (sectioned: {ActionSectioned} -> {ExistingSectioned}), not preserving incompatible display names",
                actionHasSectionStructure, existingHasSectionStructure);
        }
    }

    public static void PreserveMusicDisplayNames(ScheduleStateItem actionSchedule, ScheduleStateItem existingScheduleItem)
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

        // Only preserve music section name when both have sectioned music (mirrors Bible section preservation).
        var actionMusicSectioned = !string.IsNullOrWhiteSpace(actionSchedule.MusicPublicationCode) &&
            PublicationTypeHelper.HasSectionStructure(actionSchedule.MusicPublicationCode);
        var existingMusicSectioned = !string.IsNullOrWhiteSpace(existingScheduleItem.MusicPublicationCode) &&
            PublicationTypeHelper.HasSectionStructure(existingScheduleItem.MusicPublicationCode);
        if (actionMusicSectioned == existingMusicSectioned && actionMusicSectioned)
        {
            if (string.IsNullOrWhiteSpace(actionSchedule.MusicSectionName) && !string.IsNullOrWhiteSpace(existingScheduleItem.MusicSectionName))
            {
                Log.Debug("ApplicationReducer: Preserving existing MusicSectionName: {SectionName}",
                    existingScheduleItem.MusicSectionName);
                actionSchedule.MusicSectionName = existingScheduleItem.MusicSectionName;
            }
        }

        if (string.IsNullOrWhiteSpace(actionSchedule.MusicTrackName) && !string.IsNullOrWhiteSpace(existingScheduleItem.MusicTrackName))
        {
            actionSchedule.MusicTrackName = existingScheduleItem.MusicTrackName;
        }

        // Preserve discovery-based "expected modal item counts" so unrelated updates don't wipe them.
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

