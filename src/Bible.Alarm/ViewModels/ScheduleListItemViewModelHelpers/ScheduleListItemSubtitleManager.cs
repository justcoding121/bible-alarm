#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
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
    IScheduleDisplayService displayService,
    IState<ApplicationState> applicationState)
{
    private string subtitle = string.Empty;
    private string language = string.Empty;
    private FlowDirection flowDirection = FlowDirection.LeftToRight;

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

    public FlowDirection FlowDirection
    {
        get => flowDirection;
        set => flowDirection = value;
    }

    /// <summary>
    /// Refreshes subtitle from ScheduleStateItem in state (uses pre-populated SectionName).
    /// Falls back to async database lookup if SectionName is not available in state.
    /// </summary>
    public void RefreshSubTitleFromState(int scheduleId, ScheduleStateItem? providedScheduleStateItem, Action<string> setSubTitle, Action<string> setLanguage, Action<FlowDirection> setFlowDirection, Action<string> onPropertyChanged)
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
                UpdateLanguageFromState(scheduleStateItem, setLanguage, setFlowDirection, onPropertyChanged);
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
                ClearLanguage(setLanguage, setFlowDirection, onPropertyChanged);
            }

            // Only fall back to async lookup if display names are not expected to be populated
            logger.Debug("ScheduleListItemSubtitleManager: RefreshSubTitleFromState - Falling back to async lookup for schedule {ScheduleId}", scheduleId);
            _ = RefreshTrackNameAsync(scheduleId, force: false, setSubTitle, setLanguage, setFlowDirection, onPropertyChanged);
        }
        catch (Exception e)
        {
            logger.Error(e, "An error happened while refreshing subtitle from state for schedule {ScheduleId}", scheduleId);
            _ = RefreshTrackNameAsync(scheduleId, force: false, setSubTitle, setLanguage, setFlowDirection, onPropertyChanged);
        }
    }

    private static void UpdateLanguageFromState(ScheduleStateItem scheduleStateItem, Action<string> setLanguage, Action<FlowDirection> setFlowDirection, Action<string> onPropertyChanged)
    {
        if (!string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationLanguageName))
        {
            setLanguage(scheduleStateItem.BiblePublicationLanguageName);
        }
        else if (!string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationLanguageCode))
        {
            setLanguage(scheduleStateItem.BiblePublicationLanguageCode);
        }
        else
        {
            setLanguage(string.Empty);
        }
        onPropertyChanged("Language");

        // Update flow direction based on language direction
        var direction = scheduleStateItem.BiblePublicationLanguageDirection ?? "ltr";
        var flowDirection = string.Equals(direction, "rtl", StringComparison.OrdinalIgnoreCase)
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
        setFlowDirection(flowDirection);
        onPropertyChanged("ContentFlowDirection");
    }

    private static string BuildSubtitleFromState(ScheduleStateItem scheduleStateItem)
    {
        var hasSectionStructure = PublicationTypeHelper.HasSectionStructure(scheduleStateItem.BiblePublicationCode);

        if (hasSectionStructure)
        {
            // Traditional Bible: Show "Book Name Chapter Number" (e.g., "Genesis 1")
            var sectionName = !string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationSectionName)
                ? scheduleStateItem.BiblePublicationSectionName
                : null;

            var trackNumber = scheduleStateItem.BiblePublicationTrackNumber.HasValue && scheduleStateItem.BiblePublicationTrackNumber.Value > 0
                ? scheduleStateItem.BiblePublicationTrackNumber.Value.ToString()
                : null;

            if (sectionName != null && trackNumber != null)
            {
                return $"{sectionName} {trackNumber}";
            }
            else if (sectionName != null)
            {
                return sectionName;
            }
            else if (trackNumber != null)
            {
                return trackNumber;
            }
            return string.Empty;
        }
        else
        {
            // Drama/Video: Show just the track title
            var trackTitle = scheduleStateItem.BiblePublicationTrackTitle;
            if (!string.IsNullOrWhiteSpace(trackTitle))
            {
                return trackTitle;
            }
            return string.Empty;
        }
    }

    private static void ClearLanguage(Action<string> setLanguage, Action<FlowDirection> setFlowDirection, Action<string> onPropertyChanged)
    {
        setLanguage(string.Empty);
        onPropertyChanged("Language");
        setFlowDirection(FlowDirection.LeftToRight);
        onPropertyChanged("ContentFlowDirection");
    }

    /// <summary>
    /// Async fallback method for refreshing track name from database.
    /// Only used if SectionName is not available in state.
    /// </summary>
    public async Task RefreshTrackNameAsync(int scheduleId, bool force, Action<string> setSubTitle, Action<string> setLanguage, Action<FlowDirection> setFlowDirection, Action<string> onPropertyChanged)
    {
        if (scheduleId <= 0)
        {
            return;
        }

        try
        {
            // Try to get Language from state first (synchronous)
            var scheduleStateItem = applicationState.Value.Schedules
                .FirstOrDefault(s => s.Id == scheduleId);

            string language = string.Empty;
            string direction = "ltr";
            if (scheduleStateItem != null)
            {
                if (!string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationLanguageName))
                {
                    language = scheduleStateItem.BiblePublicationLanguageName;
                }
                else if (!string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationLanguageCode))
                {
                    language = scheduleStateItem.BiblePublicationLanguageCode;
                }
                direction = scheduleStateItem.BiblePublicationLanguageDirection ?? "ltr";
            }

            // Run database operations off UI thread
            var displayName = await Task.Run(async () =>
                await displayService.GetTrackDisplayNameAsync(scheduleId, force));

            var flowDirection = string.Equals(direction, "rtl", StringComparison.OrdinalIgnoreCase)
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;

            // Update UI on main thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!string.IsNullOrEmpty(displayName))
                {
                    setSubTitle(displayName);
                    onPropertyChanged("SubTitle");
                }

                // Update Language property
                setLanguage(language);
                onPropertyChanged("Language");

                // Update FlowDirection property
                setFlowDirection(flowDirection);
                onPropertyChanged("ContentFlowDirection");
            });
        }
        catch (Exception e)
        {
            logger.Error(e, "An error happened while refreshing track name for schedule {ScheduleId}", scheduleId);
        }
    }
}
