#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.ViewModels.ScheduleListItemHelpers;

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
    /// Refreshes subtitle from ScheduleStateItem in state (uses pre-populated BookName).
    /// Falls back to async database lookup if BookName is not available in state.
    /// </summary>
    public void RefreshSubTitleFromState(int scheduleId, ScheduleStateItem? providedScheduleStateItem, Action<string> setSubTitle, Action<string> setLanguage, Action<string> onPropertyChanged)
    {
        if (scheduleId <= 0)
        {
            return;
        }

        try
        {
            // Use provided scheduleStateItem if available, otherwise look it up from state
            var scheduleStateItem = providedScheduleStateItem ??
                applicationState.Value.Schedules?.FirstOrDefault(s => s.Id == scheduleId);

            if (scheduleStateItem?.BibleReadingScheduleId.HasValue == true)
            {
                UpdateLanguageFromState(scheduleStateItem, setLanguage, onPropertyChanged);
                var subtitle = BuildSubtitleFromState(scheduleStateItem);
                if (!string.IsNullOrEmpty(subtitle))
                {
                    setSubTitle(subtitle);
                    onPropertyChanged("SubTitle");
                    return;
                }
            }
            else
            {
                ClearLanguage(setLanguage, onPropertyChanged);
            }

            _ = RefreshChapterNameAsync(scheduleId, force: false, setSubTitle, setLanguage, onPropertyChanged);
        }
        catch (Exception e)
        {
            logger.Error(e, "An error happened while refreshing subtitle from state for schedule {ScheduleId}", scheduleId);
            _ = RefreshChapterNameAsync(scheduleId, force: false, setSubTitle, setLanguage, onPropertyChanged);
        }
    }

    private void UpdateLanguageFromState(ScheduleStateItem scheduleStateItem, Action<string> setLanguage, Action<string> onPropertyChanged)
    {
        if (!string.IsNullOrWhiteSpace(scheduleStateItem.BibleReadingLanguageName))
        {
            setLanguage(scheduleStateItem.BibleReadingLanguageName);
        }
        else if (!string.IsNullOrWhiteSpace(scheduleStateItem.BibleReadingLanguageCode))
        {
            setLanguage(scheduleStateItem.BibleReadingLanguageCode);
        }
        else
        {
            setLanguage(string.Empty);
        }
        onPropertyChanged("Language");
    }

    private static string BuildSubtitleFromState(ScheduleStateItem scheduleStateItem)
    {
        var subtitleParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(scheduleStateItem.BibleReadingBookName))
        {
            subtitleParts.Add(scheduleStateItem.BibleReadingBookName);
        }
        else if (scheduleStateItem.BibleReadingBookNumber.HasValue && scheduleStateItem.BibleReadingBookNumber.Value > 0)
        {
            subtitleParts.Add($"Book {scheduleStateItem.BibleReadingBookNumber.Value}");
        }

        if (scheduleStateItem.BibleReadingChapterNumber.HasValue && scheduleStateItem.BibleReadingChapterNumber.Value > 0)
        {
            subtitleParts.Add(scheduleStateItem.BibleReadingChapterNumber.Value.ToString());
        }

        return subtitleParts.Count > 0 ? string.Join(" ", subtitleParts) : string.Empty;
    }

    private void ClearLanguage(Action<string> setLanguage, Action<string> onPropertyChanged)
    {
        setLanguage(string.Empty);
        onPropertyChanged("Language");
    }

    /// <summary>
    /// Async fallback method for refreshing chapter name from database.
    /// Only used if BookName is not available in state.
    /// </summary>
    public async Task RefreshChapterNameAsync(int scheduleId, bool force, Action<string> setSubTitle, Action<string> setLanguage, Action<string> onPropertyChanged)
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
            if (scheduleStateItem != null)
            {
                if (!string.IsNullOrWhiteSpace(scheduleStateItem.BibleReadingLanguageName))
                {
                    language = scheduleStateItem.BibleReadingLanguageName;
                }
                else if (!string.IsNullOrWhiteSpace(scheduleStateItem.BibleReadingLanguageCode))
                {
                    language = scheduleStateItem.BibleReadingLanguageCode;
                }
            }

            // Run database operations off UI thread
            var displayName = await Task.Run(async () =>
                await displayService.GetChapterDisplayNameAsync(scheduleId, force));

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
            });
        }
        catch (Exception e)
        {
            logger.Error(e, "An error happened while refreshing chapter name for schedule {ScheduleId}", scheduleId);
        }
    }
}
