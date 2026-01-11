#nullable enable
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;

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
    private string? lastKnownBiblePublicationLanguageName;
    private string? lastKnownSectionName;

    public AlarmSchedule? LastKnownSchedule
    {
        get => lastKnownSchedule;
        set => lastKnownSchedule = value;
    }

    public string? LastKnownBiblePublicationLanguageName
    {
        get => lastKnownBiblePublicationLanguageName;
        set => lastKnownBiblePublicationLanguageName = value;
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
        var oldSectionNumber = currentSchedule.BiblePublicationSchedule?.SectionNumber;
        var oldTrackNumber = currentSchedule.BiblePublicationSchedule?.TrackNumber;
        var oldDaysOfWeek = currentSchedule.DaysOfWeek;
        var oldIsEnabled = currentSchedule.IsEnabled;
        var oldName = currentSchedule.Name ?? string.Empty;
        var oldHour = currentSchedule.Hour;
        var oldMinute = currentSchedule.Minute;
        var oldMusicEnabled = currentSchedule.MusicEnabled;

        var updatedSchedule = mapper.Map<AlarmSchedule>(updatedScheduleItem);
        var trackChanged = DetectTrackChange(updatedSchedule, currentSchedule);
        var newBiblePublicationLanguageName = updatedScheduleItem.BiblePublicationLanguageName;
        var newSectionName = updatedScheduleItem.BiblePublicationSectionName;

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
            SectionNumberChanged = oldSectionNumber != updatedSchedule.BiblePublicationSchedule?.SectionNumber,
            TrackNumberChanged = oldTrackNumber != updatedSchedule.BiblePublicationSchedule?.TrackNumber,
            BiblePublicationLanguageNameChanged = lastKnownBiblePublicationLanguageName != newBiblePublicationLanguageName,
            SectionNameChanged = lastKnownSectionName != newSectionName,
            DaysOfWeekChanged = daysOfWeekChanged,
            IsEnabledChanged = oldIsEnabled != updatedSchedule.IsEnabled,
            NameChanged = oldName != updatedSchedule.Name,
            TimeChanged = oldHour != updatedSchedule.Hour || oldMinute != updatedSchedule.Minute,
            MusicEnabledChanged = oldMusicEnabled != updatedSchedule.MusicEnabled,
            NewBiblePublicationLanguageName = newBiblePublicationLanguageName,
            NewSectionName = newSectionName
        };
    }

    private bool DetectTrackChange(AlarmSchedule updatedSchedule, AlarmSchedule currentSchedule)
    {
        if (currentSchedule.BiblePublicationSchedule != null && updatedSchedule.BiblePublicationSchedule != null)
        {
            return currentSchedule.BiblePublicationSchedule.SectionNumber != updatedSchedule.BiblePublicationSchedule.SectionNumber ||
                   currentSchedule.BiblePublicationSchedule.TrackNumber != updatedSchedule.BiblePublicationSchedule.TrackNumber;
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
        public bool TrackNumberChanged { get; init; }
        public bool BiblePublicationLanguageNameChanged { get; init; }
        public bool SectionNameChanged { get; init; }
        public bool DaysOfWeekChanged { get; init; }
        public bool IsEnabledChanged { get; init; }
        public bool NameChanged { get; init; }
        public bool TimeChanged { get; init; }
        public bool MusicEnabledChanged { get; init; }
        public string? NewBiblePublicationLanguageName { get; init; }
        public string? NewSectionName { get; init; }
    }
}
