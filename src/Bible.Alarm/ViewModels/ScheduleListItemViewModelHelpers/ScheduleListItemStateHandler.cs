#nullable enable
using AutoMapper;
using Bible.Alarm.Shared.Models.Schedule;
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
    private string? lastKnownTrackTitle;
    private string? lastKnownBiblePublicationCode;

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

    public string? LastKnownTrackTitle
    {
        get => lastKnownTrackTitle;
        set => lastKnownTrackTitle = value;
    }

    public string? LastKnownBiblePublicationCode
    {
        get => lastKnownBiblePublicationCode;
        set => lastKnownBiblePublicationCode = value;
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
        var oldSectionCode = currentSchedule.BiblePublicationSchedule?.SectionCode;
        var oldTrackCode = currentSchedule.BiblePublicationSchedule?.TrackCode;
        var oldDaysOfWeek = currentSchedule.DaysOfWeek;
        var oldIsEnabled = currentSchedule.IsEnabled;
        var oldName = currentSchedule.Name ?? string.Empty;
        var oldHour = currentSchedule.Hour;
        var oldMinute = currentSchedule.Minute;
        var oldMusicEnabled = currentSchedule.MusicEnabled;
        var oldBiblePublicationCode = currentSchedule.BiblePublicationSchedule?.PublicationCode;
        var oldBiblePublicationLanguageCode = currentSchedule.BiblePublicationSchedule?.LanguageCode;
        var oldBiblePublicationScheduleId = currentSchedule.BiblePublicationSchedule?.Id;

        var updatedSchedule = mapper.Map<AlarmSchedule>(updatedScheduleItem);
        var trackChanged = DetectTrackChange(updatedSchedule, currentSchedule);
        var newBiblePublicationLanguageName = updatedScheduleItem.BiblePublicationLanguageName;
        var newSectionName = updatedScheduleItem.BiblePublicationSectionName;
        var newTrackTitle = updatedScheduleItem.BiblePublicationTrackTitle;
        var newBiblePublicationCode = updatedScheduleItem.BiblePublicationCode;
        var newBiblePublicationLanguageCode = updatedScheduleItem.BiblePublicationLanguageCode;
        var newBiblePublicationScheduleId = updatedScheduleItem.BiblePublicationScheduleId;

        var daysOfWeekChanged = oldDaysOfWeek != updatedSchedule.DaysOfWeek;
        var biblePublicationCodeChanged = oldBiblePublicationCode != newBiblePublicationCode;
        var biblePublicationLanguageCodeChanged = oldBiblePublicationLanguageCode != newBiblePublicationLanguageCode;
        var biblePublicationScheduleIdChanged = oldBiblePublicationScheduleId != newBiblePublicationScheduleId;

        // Check if display names have been populated (even if other properties haven't changed)
        // This handles the case where display names are populated asynchronously by bootstrap service
        var displayNamesPopulated = (!string.IsNullOrWhiteSpace(newSectionName) && 
                                     string.IsNullOrWhiteSpace(lastKnownSectionName)) ||
                                    (!string.IsNullOrWhiteSpace(newTrackTitle) && 
                                     string.IsNullOrWhiteSpace(lastKnownTrackTitle));

        logger.Debug("ScheduleListItemStateHandler: DetectScheduleChanges - ScheduleId: {ScheduleId}, LastKnownSectionName: '{LastKnownSectionName}', NewSectionName: '{NewSectionName}', LastKnownTrackTitle: '{LastKnownTrackTitle}', NewTrackTitle: '{NewTrackTitle}', DisplayNamesPopulated: {DisplayNamesPopulated}",
            updatedScheduleItem.Id,
            lastKnownSectionName ?? "null", newSectionName ?? "null",
            lastKnownTrackTitle ?? "null", newTrackTitle ?? "null",
            displayNamesPopulated);

        // Check if ANY bible schedule property has changed
        var anyBibleSchedulePropertyChanged = biblePublicationCodeChanged ||
                                             biblePublicationLanguageCodeChanged ||
                                             biblePublicationScheduleIdChanged ||
                                             oldSectionCode != updatedSchedule.BiblePublicationSchedule?.SectionCode ||
                                             oldTrackCode != updatedSchedule.BiblePublicationSchedule?.TrackCode ||
                                             lastKnownBiblePublicationLanguageName != newBiblePublicationLanguageName ||
                                             lastKnownSectionName != newSectionName ||
                                             lastKnownTrackTitle != newTrackTitle ||
                                             displayNamesPopulated;

        logger.Debug("ScheduleListItemStateHandler: DetectScheduleChanges - ScheduleId: {ScheduleId}, AnyBibleSchedulePropertyChanged: {AnyBibleSchedulePropertyChanged}, TrackTitleChanged: {TrackTitleChanged}, SectionNameChanged: {SectionNameChanged}",
            updatedScheduleItem.Id, anyBibleSchedulePropertyChanged,
            lastKnownTrackTitle != newTrackTitle,
            lastKnownSectionName != newSectionName);

        // Log DaysOfWeek changes for debugging
        if (daysOfWeekChanged)
        {
            logger.Debug("ScheduleListItemStateHandler: DaysOfWeek changed for schedule {ScheduleId}. Old: {OldDaysOfWeek}, New: {NewDaysOfWeek}",
                updatedScheduleItem.Id, oldDaysOfWeek, updatedSchedule.DaysOfWeek);
        }

        // Log publication code changes (sectioned <-> non-sectioned)
        if (biblePublicationCodeChanged)
        {
            logger.Debug("ScheduleListItemStateHandler: BiblePublicationCode changed for schedule {ScheduleId}. Old: {OldCode}, New: {NewCode}",
                updatedScheduleItem.Id, oldBiblePublicationCode, newBiblePublicationCode);
        }

        // Log any bible schedule property changes
        if (anyBibleSchedulePropertyChanged)
        {
            logger.Debug("ScheduleListItemStateHandler: Bible schedule property changed for schedule {ScheduleId}. Will refresh subtitle.",
                updatedScheduleItem.Id);
        }

        return new ScheduleChangeInfo
        {
            UpdatedSchedule = updatedSchedule,
            TrackChanged = trackChanged,
            SectionCodeChanged = oldSectionCode != updatedSchedule.BiblePublicationSchedule?.SectionCode,
            TrackCodeChanged = oldTrackCode != updatedSchedule.BiblePublicationSchedule?.TrackCode,
            BiblePublicationLanguageNameChanged = lastKnownBiblePublicationLanguageName != newBiblePublicationLanguageName,
            SectionNameChanged = lastKnownSectionName != newSectionName,
            TrackTitleChanged = lastKnownTrackTitle != newTrackTitle,
            BiblePublicationCodeChanged = biblePublicationCodeChanged,
            AnyBibleSchedulePropertyChanged = anyBibleSchedulePropertyChanged,
            DaysOfWeekChanged = daysOfWeekChanged,
            IsEnabledChanged = oldIsEnabled != updatedSchedule.IsEnabled,
            NameChanged = oldName != updatedSchedule.Name,
            TimeChanged = oldHour != updatedSchedule.Hour || oldMinute != updatedSchedule.Minute,
            MusicEnabledChanged = oldMusicEnabled != updatedSchedule.MusicEnabled,
            NewBiblePublicationLanguageName = newBiblePublicationLanguageName,
            NewSectionName = newSectionName,
            NewTrackTitle = newTrackTitle
        };
    }

    private bool DetectTrackChange(AlarmSchedule updatedSchedule, AlarmSchedule currentSchedule)
    {
        if (currentSchedule.BiblePublicationSchedule != null && updatedSchedule.BiblePublicationSchedule != null)
        {
            return currentSchedule.BiblePublicationSchedule.SectionCode != updatedSchedule.BiblePublicationSchedule.SectionCode ||
                   currentSchedule.BiblePublicationSchedule.TrackCode != updatedSchedule.BiblePublicationSchedule.TrackCode;
        }

        if (currentSchedule.Music != null && updatedSchedule.Music != null)
        {
            return currentSchedule.Music.TrackCode != updatedSchedule.Music.TrackCode;
        }

        return false;
    }

    public record ScheduleChangeInfo
    {
        public AlarmSchedule UpdatedSchedule { get; init; } = null!;
        public bool TrackChanged { get; init; }
        public bool SectionCodeChanged { get; init; }
        public bool TrackCodeChanged { get; init; }
        public bool BiblePublicationLanguageNameChanged { get; init; }
        public bool SectionNameChanged { get; init; }
        public bool TrackTitleChanged { get; init; }
        public bool BiblePublicationCodeChanged { get; init; }
        public bool AnyBibleSchedulePropertyChanged { get; init; }
        public bool DaysOfWeekChanged { get; init; }
        public bool IsEnabledChanged { get; init; }
        public bool NameChanged { get; init; }
        public bool TimeChanged { get; init; }
        public bool MusicEnabledChanged { get; init; }
        public string? NewBiblePublicationLanguageName { get; init; }
        public string? NewSectionName { get; init; }
        public string? NewTrackTitle { get; init; }
    }
}
