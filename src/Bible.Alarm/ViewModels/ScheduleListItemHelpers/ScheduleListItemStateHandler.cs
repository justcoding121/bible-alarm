#nullable enable
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.ScheduleListItemHelpers;

/// <summary>
/// Handles state change management for ScheduleListItemViewModel.
/// </summary>
public sealed class ScheduleListItemStateHandler(
    ILogger logger,
    IMapper mapper,
    IState<ApplicationState> applicationState)
{
    private AlarmSchedule? lastKnownSchedule;
    private string? lastKnownBibleReadingLanguageName;
    private string? lastKnownBookName;

    public AlarmSchedule? LastKnownSchedule
    {
        get => lastKnownSchedule;
        set => lastKnownSchedule = value;
    }

    public string? LastKnownBibleReadingLanguageName
    {
        get => lastKnownBibleReadingLanguageName;
        set => lastKnownBibleReadingLanguageName = value;
    }

    public string? LastKnownBookName
    {
        get => lastKnownBookName;
        set => lastKnownBookName = value;
    }

    /// <summary>
    /// Handles application state changes.
    /// </summary>
    public ScheduleChangeInfo? HandleApplicationStateChanged(int scheduleId, AlarmSchedule? currentSchedule)
    {
        if (scheduleId <= 0 || currentSchedule == null)
        {
            return null;
        }

        var updatedScheduleItem = applicationState.Value.Schedules
            .FirstOrDefault(s => s.Id == scheduleId);

        if (updatedScheduleItem == null)
        {
            return null;
        }

        return DetectScheduleChanges(updatedScheduleItem, currentSchedule);
    }

    private ScheduleChangeInfo DetectScheduleChanges(ScheduleStateItem updatedScheduleItem, AlarmSchedule currentSchedule)
    {
        var oldBookNumber = currentSchedule.BibleReadingSchedule?.BookNumber;
        var oldChapterNumber = currentSchedule.BibleReadingSchedule?.ChapterNumber;
        var oldDaysOfWeek = currentSchedule.DaysOfWeek ?? 0;
        var oldIsEnabled = currentSchedule.IsEnabled;
        var oldName = currentSchedule.Name ?? string.Empty;
        var oldHour = currentSchedule.Hour ?? 0;
        var oldMinute = currentSchedule.Minute ?? 0;
        var oldMusicEnabled = currentSchedule.MusicEnabled ?? false;

        var updatedSchedule = mapper.Map<AlarmSchedule>(updatedScheduleItem);
        var trackChanged = DetectTrackChange(updatedSchedule, currentSchedule);
        var newBibleReadingLanguageName = updatedScheduleItem.BibleReadingLanguageName;
        var newBookName = updatedScheduleItem.BibleReadingBookName;

        return new ScheduleChangeInfo
        {
            UpdatedSchedule = updatedSchedule,
            TrackChanged = trackChanged,
            BookNumberChanged = oldBookNumber != updatedSchedule.BibleReadingSchedule?.BookNumber,
            ChapterNumberChanged = oldChapterNumber != updatedSchedule.BibleReadingSchedule?.ChapterNumber,
            BibleReadingLanguageNameChanged = lastKnownBibleReadingLanguageName != newBibleReadingLanguageName,
            BookNameChanged = lastKnownBookName != newBookName,
            DaysOfWeekChanged = oldDaysOfWeek != updatedSchedule.DaysOfWeek,
            IsEnabledChanged = oldIsEnabled != updatedSchedule.IsEnabled,
            NameChanged = oldName != updatedSchedule.Name,
            TimeChanged = oldHour != updatedSchedule.Hour || oldMinute != updatedSchedule.Minute,
            MusicEnabledChanged = oldMusicEnabled != updatedSchedule.MusicEnabled,
            NewBibleReadingLanguageName = newBibleReadingLanguageName,
            NewBookName = newBookName
        };
    }

    private bool DetectTrackChange(AlarmSchedule updatedSchedule, AlarmSchedule currentSchedule)
    {
        if (currentSchedule.BibleReadingSchedule != null && updatedSchedule.BibleReadingSchedule != null)
        {
            return currentSchedule.BibleReadingSchedule.BookNumber != updatedSchedule.BibleReadingSchedule.BookNumber ||
                   currentSchedule.BibleReadingSchedule.ChapterNumber != updatedSchedule.BibleReadingSchedule.ChapterNumber;
        }

        if (currentSchedule.Music != null && updatedSchedule.Music != null)
        {
            return currentSchedule.Music.TrackNumber != updatedSchedule.Music.TrackNumber;
        }

        return false;
    }

    public record ScheduleChangeInfo
    {
        public AlarmSchedule UpdatedSchedule { get; init; } = null!;
        public bool TrackChanged { get; init; }
        public bool BookNumberChanged { get; init; }
        public bool ChapterNumberChanged { get; init; }
        public bool BibleReadingLanguageNameChanged { get; init; }
        public bool BookNameChanged { get; init; }
        public bool DaysOfWeekChanged { get; init; }
        public bool IsEnabledChanged { get; init; }
        public bool NameChanged { get; init; }
        public bool TimeChanged { get; init; }
        public bool MusicEnabledChanged { get; init; }
        public string? NewBibleReadingLanguageName { get; init; }
        public string? NewBookName { get; init; }
    }
}
