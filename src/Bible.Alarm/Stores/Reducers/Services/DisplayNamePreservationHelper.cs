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

        // Check if publication type changed (sectioned <-> non-sectioned)
        var actionHasSectionStructure = !string.IsNullOrWhiteSpace(actionSchedule.BiblePublicationCode) &&
            PublicationTypeHelper.HasSectionStructure(actionSchedule.BiblePublicationCode);
        var existingHasSectionStructure = !string.IsNullOrWhiteSpace(existingScheduleItem.BiblePublicationCode) &&
            PublicationTypeHelper.HasSectionStructure(existingScheduleItem.BiblePublicationCode);

        // Only preserve section name if publication type hasn't changed and both are sectioned
        if (actionHasSectionStructure == existingHasSectionStructure && actionHasSectionStructure)
        {
            if (string.IsNullOrWhiteSpace(actionSchedule.BiblePublicationSectionName) && !string.IsNullOrWhiteSpace(existingScheduleItem.BiblePublicationSectionName))
            {
                Log.Debug("ApplicationReducer: Preserving existing BiblePublicationSectionName: {SectionName}",
                    existingScheduleItem.BiblePublicationSectionName);
                actionSchedule.BiblePublicationSectionName = existingScheduleItem.BiblePublicationSectionName;
            }
        }
        // Only preserve track title if publication type hasn't changed and both are non-sectioned
        else if (actionHasSectionStructure == existingHasSectionStructure && !actionHasSectionStructure)
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

        if (string.IsNullOrWhiteSpace(actionSchedule.MusicTrackName) && !string.IsNullOrWhiteSpace(existingScheduleItem.MusicTrackName))
        {
            actionSchedule.MusicTrackName = existingScheduleItem.MusicTrackName;
        }
    }
}

