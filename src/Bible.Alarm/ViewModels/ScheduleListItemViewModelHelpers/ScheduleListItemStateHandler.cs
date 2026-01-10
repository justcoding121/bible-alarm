#nullable enable
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers;

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
    private string? lastKnownSectionName;

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

    public string? LastKnownSectionName
    {
        get => lastKnownSectionName;
        set => lastKnownSectionName = value;
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
        var oldSectionNumber = currentSchedule.BibleReadingSchedule?.SectionNumber;
        var oldChapterNumber = currentSchedule.BibleReadingSchedule?.ChapterNumber;
        var oldDaysOfWeek = currentSchedule.DaysOfWeek;
        var oldIsEnabled = currentSchedule.IsEnabled;
        var oldName = currentSchedule.Name ?? string.Empty;
        var oldHour = currentSchedule.Hour;
        var oldMinute = currentSchedule.Minute;
        var oldMusicEnabled = currentSchedule.MusicEnabled;

        var updatedSchedule = mapper.Map<AlarmSchedule>(updatedScheduleItem);
        var trackChanged = DetectTrackChange(updatedSchedule, currentSchedule);
        var newBibleReadingLanguageName = updatedScheduleItem.BibleReadingLanguageName;
        var newSectionName = updatedScheduleItem.BibleReadingSectionName;

        var daysOfWeekChanged = oldDaysOfWeek != updatedSchedule.DaysOfWeek;
        
        // Log DaysOfWeek changes for debugging
        if (daysOfWeekChanged)
        {
            logger.Debug("ScheduleListItemStateHandler: DaysOfWeek changed for schedule {ScheduleId}. Old: {OldDaysOfWeek}, New: {NewDaysOfWeek}",
                updatedScheduleItem.Id, oldDaysOfWeek, updatedSchedule.DaysOfWeek);
        }

        return new ScheduleChangeInfo
        {
            UpdatedSchedule = updatedSchedule,
            TrackChanged = trackChanged,
            SectionNumberChanged = oldSectionNumber != updatedSchedule.BibleReadingSchedule?.SectionNumber,
            ChapterNumberChanged = oldChapterNumber != updatedSchedule.BibleReadingSchedule?.ChapterNumber,
            BibleReadingLanguageNameChanged = lastKnownBibleReadingLanguageName != newBibleReadingLanguageName,
            SectionNameChanged = lastKnownSectionName != newSectionName,
            DaysOfWeekChanged = daysOfWeekChanged,
            IsEnabledChanged = oldIsEnabled != updatedSchedule.IsEnabled,
            NameChanged = oldName != updatedSchedule.Name,
            TimeChanged = oldHour != updatedSchedule.Hour || oldMinute != updatedSchedule.Minute,
            MusicEnabledChanged = oldMusicEnabled != updatedSchedule.MusicEnabled,
            NewBibleReadingLanguageName = newBibleReadingLanguageName,
            NewSectionName = newSectionName
        };
    }

    private bool DetectTrackChange(AlarmSchedule updatedSchedule, AlarmSchedule currentSchedule)
    {
        if (currentSchedule.BibleReadingSchedule != null && updatedSchedule.BibleReadingSchedule != null)
        {
            return currentSchedule.BibleReadingSchedule.SectionNumber != updatedSchedule.BibleReadingSchedule.SectionNumber ||
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
        public bool SectionNumberChanged { get; init; }
        public bool ChapterNumberChanged { get; init; }
        public bool BibleReadingLanguageNameChanged { get; init; }
        public bool SectionNameChanged { get; init; }
        public bool DaysOfWeekChanged { get; init; }
        public bool IsEnabledChanged { get; init; }
        public bool NameChanged { get; init; }
        public bool TimeChanged { get; init; }
        public bool MusicEnabledChanged { get; init; }
        public string? NewBibleReadingLanguageName { get; init; }
        public string? NewSectionName { get; init; }
    }
}
