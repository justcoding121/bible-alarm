#nullable enable
using System.Collections.Generic;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;

namespace Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers;

/// <summary>
/// Handles subtitle and language management for ScheduleListItemViewModel.
/// When a schedule is currently playing, uses PlaybackState.Title so the list shows the actual track (e.g. "Jacob—A Man who loved peace...") instead of the saved selection.
/// </summary>
public sealed class ScheduleListItemSubtitleManager(
    ILogger logger,
    IState<ApplicationState> applicationState,
    IState<PlaybackState> playbackState)
{
    public string SubTitle { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Refreshes subtitle from ScheduleStateItem in state (uses pre-populated SectionName).
    /// Intentionally state-only: Home list rendering should not trigger ad-hoc DB calls.
    /// </summary>
    public void RefreshSubTitleFromState(int scheduleId, ScheduleStateItem? providedScheduleStateItem, Action<string> setSubTitle, Action<string> setLanguage, Action<string> onPropertyChanged)
    {
        if (scheduleId <= 0)
        {
            logger.Debug(AppConstants.Logging.ScheduleListItemSubtitleManagerDiagnosticsLog.RefreshInvalidScheduleId, scheduleId);
            return;
        }

        try
        {
            // Use provided scheduleStateItem if available, otherwise look it up from state
            var scheduleStateItem = providedScheduleStateItem ??
                applicationState.Value.Schedules?.FirstOrDefault(s => s.Id == scheduleId);

            logger.Debug(AppConstants.Logging.ScheduleListItemSubtitleManagerDiagnosticsLog.RefreshScheduleIdProvidedFoundPublicationSectionTrack,
                scheduleId,
                providedScheduleStateItem != null,
                scheduleStateItem != null,
                scheduleStateItem?.BiblePublicationCode ?? "null",
                scheduleStateItem?.BiblePublicationSectionName ?? "null",
                scheduleStateItem?.BiblePublicationTrackTitle ?? "null");

            if (scheduleStateItem?.BiblePublicationScheduleId.HasValue is true)
            {
                UpdateLanguageFromState(scheduleStateItem, setLanguage, onPropertyChanged);
                var playingTrackTitle = (playbackState.Value.CurrentScheduleId == scheduleId && playbackState.Value.IsPreparingOrPlaying && !string.IsNullOrWhiteSpace(playbackState.Value.Title))
                    ? playbackState.Value.Title
                    : null;
                var subtitle = BuildSubtitleFromState(scheduleStateItem, playingTrackTitle);
                
                logger.Debug(AppConstants.Logging.ScheduleListItemSubtitleManagerDiagnosticsLog.RefreshBuiltSubtitleForSchedule, subtitle, scheduleId);
                
                if (!string.IsNullOrEmpty(subtitle))
                {
                    logger.Debug(AppConstants.Logging.ScheduleListItemSubtitleManagerDiagnosticsLog.RefreshSettingSubtitleForSchedule, subtitle, scheduleId);
                    setSubTitle(subtitle);
                    onPropertyChanged("SubTitle");
                    return;
                }
                
                // If subtitle is empty, check if we're waiting for display names to be populated
                // For sectioned publications, we need BiblePublicationSectionName
                // For non-sectioned publications, we need BiblePublicationTrackTitle
                var hasSectionStructure = PublicationTypeHelper.HasSectionStructure(scheduleStateItem.BiblePublicationCode);
                var waitingForDisplayNames = (hasSectionStructure && string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationSectionName)) ||
                                            (!hasSectionStructure && string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationTrackTitle));
                
                logger.Debug(AppConstants.Logging.ScheduleListItemSubtitleManagerDiagnosticsLog.RefreshSectionStructureWaitingSectionNameTrackTitle,
                    scheduleId, hasSectionStructure, waitingForDisplayNames,
                    scheduleStateItem.BiblePublicationSectionName ?? "null",
                    scheduleStateItem.BiblePublicationTrackTitle ?? "null");
                
                // If we're waiting for display names, don't fall back to async lookup yet
                // The delayed refresh will catch it when display names are populated
                if (waitingForDisplayNames)
                {
                    logger.Debug(AppConstants.Logging.ScheduleListItemSubtitleManagerDiagnosticsLog.WaitingForDisplayNamesToPopulate,
                        scheduleId, hasSectionStructure);
                    return;
                }
            }
            else
            {
                logger.Debug(AppConstants.Logging.ScheduleListItemSubtitleManagerDiagnosticsLog.RefreshNoBiblePublicationScheduleIdClearingLanguage, scheduleId);
                ClearLanguage(setLanguage, onPropertyChanged);
            }

            // No DB fallback: state will be enriched by bootstrap / schedule effects.
        }
        catch (Exception e)
        {
            logger.Error(e, AppConstants.Logging.ScheduleListItemSubtitleManagerDiagnosticsLog.ErrorWhileRefreshingSubtitleFromStateForSchedule, scheduleId);
            // No DB fallback.
        }
    }

    private static void UpdateLanguageFromState(ScheduleStateItem scheduleStateItem, Action<string> setLanguage, Action<string> onPropertyChanged)
    {
        // Only show language if BiblePublicationLanguageName is set.
        // Publications without language (like "iam" instrumental music) don't have a language name in the media index.
        // Don't fall back to BiblePublicationLanguageCode as it might be stale from cascade logic.
        if (!string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationLanguageName))
        {
            setLanguage(DisplayTextHelper.NormalizeSingleLine(scheduleStateItem.BiblePublicationLanguageName));
        }
        else
        {
            setLanguage(string.Empty);
        }
        onPropertyChanged("Language");
    }

    private static string BuildSubtitleFromState(ScheduleStateItem scheduleStateItem, string? overrideTrackTitle = null)
    {
        var parts = new List<string>();
        AddSubtitlePart(parts, scheduleStateItem.BiblePublicationCategoryName);
        AddSubtitlePart(parts, scheduleStateItem.BiblePublicationName);

        var hasSectionStructure = PublicationTypeHelper.HasSectionStructure(scheduleStateItem.BiblePublicationCode);
        if (hasSectionStructure)
        {
            AddSubtitlePart(parts, scheduleStateItem.BiblePublicationSectionName);
        }

        var trackTitleForDisplay = !string.IsNullOrWhiteSpace(overrideTrackTitle) ? overrideTrackTitle : scheduleStateItem.BiblePublicationTrackTitle;
        AppendTrackSubtitleParts(parts, scheduleStateItem, trackTitleForDisplay);

        if (!string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationLanguageName))
        {
            AddSubtitlePart(parts, scheduleStateItem.BiblePublicationLanguageName);
        }

        return string.Join(" • ", parts);
    }

    private static void AddSubtitlePart(List<string> parts, string? value)
    {
        var normalized = DisplayTextHelper.NormalizeSingleLine(value);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            parts.Add(normalized);
        }
    }

    private static void AppendTrackSubtitleParts(List<string> parts, ScheduleStateItem scheduleStateItem, string? trackTitleForDisplay)
    {
        var hasSectionStructureForTrack = PublicationTypeHelper.HasSectionStructure(scheduleStateItem.BiblePublicationCode);
        if (hasSectionStructureForTrack)
        {
            var categoryName = scheduleStateItem.BiblePublicationCategoryName;
            var isMusicCategory = string.Equals(categoryName, AppConstants.Media.BiblePublicationCategoryMusic, StringComparison.OrdinalIgnoreCase);

            if (isMusicCategory && !string.IsNullOrWhiteSpace(trackTitleForDisplay))
            {
                AddSubtitlePart(parts, trackTitleForDisplay);
            }
            else
            {
                var trackCode = !string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationTrackCode)
                    ? scheduleStateItem.BiblePublicationTrackCode
                    : null;
                if (trackCode != null)
                {
                    AddSubtitlePart(parts, trackCode);
                }
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(trackTitleForDisplay))
        {
            AddSubtitlePart(parts, trackTitleForDisplay);
        }
    }

    private static void ClearLanguage(Action<string> setLanguage, Action<string> onPropertyChanged)
    {
        setLanguage(string.Empty);
        onPropertyChanged("Language");
    }
}
