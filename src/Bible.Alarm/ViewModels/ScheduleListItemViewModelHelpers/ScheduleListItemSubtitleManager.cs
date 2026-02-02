#nullable enable
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;

namespace Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers;

/// <summary>
/// Handles subtitle and language management for ScheduleListItemViewModel.
/// </summary>
public sealed class ScheduleListItemSubtitleManager(
    ILogger logger,
    IState<ApplicationState> applicationState)
{
    private string subtitle = string.Empty;
    private string language = string.Empty;

    public string SubTitle
    {
        get => subtitle;
        set => subtitle = value;
    }

    public string Language
    {
        get => language;
        set => language = value;
    }

    /// <summary>
    /// Refreshes subtitle from ScheduleStateItem in state (uses pre-populated SectionName).
    /// Intentionally state-only: Home list rendering should not trigger ad-hoc DB calls.
    /// </summary>
    public void RefreshSubTitleFromState(int scheduleId, ScheduleStateItem? providedScheduleStateItem, Action<string> setSubTitle, Action<string> setLanguage, Action<string> onPropertyChanged)
    {
        if (scheduleId <= 0)
        {
            logger.Debug("ScheduleListItemSubtitleManager: RefreshSubTitleFromState - Invalid scheduleId: {ScheduleId}", scheduleId);
            return;
        }

        try
        {
            // Use provided scheduleStateItem if available, otherwise look it up from state
            var scheduleStateItem = providedScheduleStateItem ??
                applicationState.Value.Schedules?.FirstOrDefault(s => s.Id == scheduleId);

            logger.Debug("ScheduleListItemSubtitleManager: RefreshSubTitleFromState - ScheduleId: {ScheduleId}, ProvidedItem: {HasProvidedItem}, FoundInState: {FoundInState}, PublicationCode: {PublicationCode}, SectionName: {SectionName}, TrackTitle: {TrackTitle}",
                scheduleId,
                providedScheduleStateItem != null,
                scheduleStateItem != null,
                scheduleStateItem?.BiblePublicationCode ?? "null",
                scheduleStateItem?.BiblePublicationSectionName ?? "null",
                scheduleStateItem?.BiblePublicationTrackTitle ?? "null");

            if (scheduleStateItem?.BiblePublicationScheduleId.HasValue == true)
            {
                UpdateLanguageFromState(scheduleStateItem, setLanguage, onPropertyChanged);
                var subtitle = BuildSubtitleFromState(scheduleStateItem);
                
                logger.Debug("ScheduleListItemSubtitleManager: RefreshSubTitleFromState - Built subtitle: '{Subtitle}' for schedule {ScheduleId}", subtitle, scheduleId);
                
                if (!string.IsNullOrEmpty(subtitle))
                {
                    logger.Debug("ScheduleListItemSubtitleManager: RefreshSubTitleFromState - Setting subtitle to '{Subtitle}' for schedule {ScheduleId}", subtitle, scheduleId);
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
                
                logger.Debug("ScheduleListItemSubtitleManager: RefreshSubTitleFromState - ScheduleId: {ScheduleId}, HasSectionStructure: {HasSectionStructure}, WaitingForDisplayNames: {WaitingForDisplayNames}, SectionName: '{SectionName}', TrackTitle: '{TrackTitle}'",
                    scheduleId, hasSectionStructure, waitingForDisplayNames,
                    scheduleStateItem.BiblePublicationSectionName ?? "null",
                    scheduleStateItem.BiblePublicationTrackTitle ?? "null");
                
                // If we're waiting for display names, don't fall back to async lookup yet
                // The delayed refresh will catch it when display names are populated
                if (waitingForDisplayNames)
                {
                    logger.Debug("ScheduleListItemSubtitleManager: Waiting for display names to be populated for schedule {ScheduleId}. HasSectionStructure: {HasSectionStructure}", 
                        scheduleId, hasSectionStructure);
                    return;
                }
            }
            else
            {
                logger.Debug("ScheduleListItemSubtitleManager: RefreshSubTitleFromState - No BiblePublicationScheduleId for schedule {ScheduleId}, clearing language", scheduleId);
                ClearLanguage(setLanguage, onPropertyChanged);
            }

            // No DB fallback: state will be enriched by bootstrap / schedule effects.
        }
        catch (Exception e)
        {
            logger.Error(e, "An error happened while refreshing subtitle from state for schedule {ScheduleId}", scheduleId);
            // No DB fallback.
        }
    }

    private static void UpdateLanguageFromState(ScheduleStateItem scheduleStateItem, Action<string> setLanguage, Action<string> onPropertyChanged)
    {
        if (!string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationLanguageName))
        {
            setLanguage(DisplayTextHelper.NormalizeSingleLine(scheduleStateItem.BiblePublicationLanguageName));
        }
        else if (!string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationLanguageCode))
        {
            setLanguage(DisplayTextHelper.NormalizeSingleLine(scheduleStateItem.BiblePublicationLanguageCode));
        }
        else
        {
            setLanguage(string.Empty);
        }
        onPropertyChanged("Language");
    }

    private static string BuildSubtitleFromState(ScheduleStateItem scheduleStateItem)
    {
        var parts = new List<string>();
        static void AddPart(List<string> parts, string? value)
        {
            var normalized = DisplayTextHelper.NormalizeSingleLine(value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                parts.Add(normalized);
            }
        }

        // Add Category
        AddPart(parts, scheduleStateItem.BiblePublicationCategoryName);

        // Add Publication Name
        AddPart(parts, scheduleStateItem.BiblePublicationName);

        // Add Subsection (if exists) - only for sectioned publications
        var hasSectionStructure = PublicationTypeHelper.HasSectionStructure(scheduleStateItem.BiblePublicationCode);
        if (hasSectionStructure)
        {
            AddPart(parts, scheduleStateItem.BiblePublicationSectionName);
        }

        // Add Track Name
        var hasSectionStructureForTrack = PublicationTypeHelper.HasSectionStructure(scheduleStateItem.BiblePublicationCode);
        if (hasSectionStructureForTrack)
        {
            // For sectioned publications:
            // - Bible: show the track number (e.g., "9")
            // - Music (e.g., "iam"): prefer the track title (e.g., "Melody Number(s) 195, 224") when available
            var categoryName = scheduleStateItem.BiblePublicationCategoryName;
            var isMusicCategory = string.Equals(categoryName, "Music", StringComparison.OrdinalIgnoreCase);

            if (isMusicCategory && !string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationTrackTitle))
            {
                AddPart(parts, scheduleStateItem.BiblePublicationTrackTitle);
            }
            else
            {
                var trackNumber = scheduleStateItem.BiblePublicationTrackNumber.HasValue && scheduleStateItem.BiblePublicationTrackNumber.Value > 0
                    ? scheduleStateItem.BiblePublicationTrackNumber.Value.ToString()
                    : null;
                if (trackNumber != null)
                {
                    AddPart(parts, trackNumber);
                }
            }
        }
        else
        {
            // For non-sectioned publications (dramas/videos), show track title
            if (!string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationTrackTitle))
            {
                AddPart(parts, scheduleStateItem.BiblePublicationTrackTitle);
            }
        }

        // Add Language (at the end)
        if (!string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationLanguageName))
        {
            AddPart(parts, scheduleStateItem.BiblePublicationLanguageName);
        }
        else if (!string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationLanguageCode))
        {
            AddPart(parts, scheduleStateItem.BiblePublicationLanguageCode);
        }

        return string.Join(" • ", parts);
    }

    private static void ClearLanguage(Action<string> setLanguage, Action<string> onPropertyChanged)
    {
        setLanguage(string.Empty);
        onPropertyChanged("Language");
    }
}
