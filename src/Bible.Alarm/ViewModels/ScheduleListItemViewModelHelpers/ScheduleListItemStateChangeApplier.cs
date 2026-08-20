#nullable enable

using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;

namespace Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers;

internal sealed class ScheduleListItemStateChangeApplier
{
    private readonly ILogger logger;
    private readonly IState<ApplicationState> applicationState;
    private readonly ScheduleListItemStateHandler stateHandler;
    private readonly Action<bool> setIsEnabled;

    public ScheduleListItemStateChangeApplier(
        ILogger logger,
        IState<ApplicationState> applicationState,
        ScheduleListItemStateHandler stateHandler,
        Action<bool> setIsEnabled)
    {
        this.logger = logger;
        this.applicationState = applicationState;
        this.stateHandler = stateHandler;
        this.setIsEnabled = setIsEnabled;
    }

    public void UpdateScheduleFromState(
        ScheduleListItemStateHandler.ScheduleChangeInfo changeInfo,
        Action<AlarmSchedule?> setSchedule,
        Action<ScheduleStateItem?> refreshSubTitleFromState,
        Action<string> onPropertyChanged,
        Func<int> getScheduleId)
    {
        var updatedSchedule = changeInfo.UpdatedSchedule;
        setSchedule(updatedSchedule);
        stateHandler.LastKnownSchedule = updatedSchedule;
        setIsEnabled(updatedSchedule.IsEnabled);

        // Refresh subtitle if ANY bible schedule property changed
        var subtitleChanged = changeInfo.AnyBibleSchedulePropertyChanged;

        logger.Debug("ScheduleListItemViewModel: UpdateScheduleFromState - ScheduleId: {ScheduleId}, SubtitleChanged: {SubtitleChanged}, AnyBibleSchedulePropertyChanged: {AnyBibleSchedulePropertyChanged}, NewSectionName: '{NewSectionName}', NewTrackTitle: '{NewTrackTitle}', BiblePublicationCodeChanged: {BiblePublicationCodeChanged}",
            updatedSchedule.Id, subtitleChanged, changeInfo.AnyBibleSchedulePropertyChanged,
            changeInfo.NewSectionName ?? "null", changeInfo.NewTrackTitle ?? "null",
            changeInfo.BiblePublicationCodeChanged);

        if (!subtitleChanged)
        {
            return;
        }

        stateHandler.LastKnownBiblePublicationLanguageName = changeInfo.NewBiblePublicationLanguageName;
        stateHandler.LastKnownSectionName = changeInfo.NewSectionName;
        stateHandler.LastKnownTrackTitle = changeInfo.NewTrackTitle;

        // Refresh subtitle from state on UI thread to ensure proper updates
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var updatedScheduleItem = applicationState.Value.Schedules?.FirstOrDefault(s => s.Id == updatedSchedule.Id);

            logger.Debug("ScheduleListItemViewModel: UpdateScheduleFromState - OnMainThread - ScheduleId: {ScheduleId}, FoundScheduleItem: {FoundScheduleItem}, PublicationCode: {PublicationCode}, SectionName: '{SectionName}', TrackTitle: '{TrackTitle}'",
                updatedSchedule.Id, updatedScheduleItem != null,
                updatedScheduleItem?.BiblePublicationCode ?? "null",
                updatedScheduleItem?.BiblePublicationSectionName ?? "null",
                updatedScheduleItem?.BiblePublicationTrackTitle ?? "null");

            if (changeInfo.BiblePublicationCodeChanged && updatedScheduleItem != null)
            {
                stateHandler.LastKnownBiblePublicationCode = updatedScheduleItem.BiblePublicationCode;
                onPropertyChanged(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.BiblePublicationName));
            }

            refreshSubTitleFromState(updatedScheduleItem);
        });
    }

    public void NotifyPropertyChanges(
        ScheduleListItemStateHandler.ScheduleChangeInfo changeInfo,
        Action<string> onPropertyChanged,
        Func<int> getScheduleId,
        Func<AlarmSchedule?> getSchedule,
        Action raisePropertiesChangedEvent)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Always notify 'This' first to trigger converters that bind to the entire ViewModel
            onPropertyChanged(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.This));

            if (changeInfo.DaysOfWeekChanged)
            {
                logger.Debug("ScheduleListItemViewModel: NotifyPropertyChanges - WeekDays changed for schedule {ScheduleId}. New value: {NewDaysOfWeek}",
                    getScheduleId(), getSchedule()?.DaysOfWeek ?? 0);
                onPropertyChanged(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.DaysOfWeek));
            }
            if (changeInfo.IsEnabledChanged)
            {
                onPropertyChanged(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.IsEnabled));

                // Hide progress bar when IsEnabled is updated (indicates toggle operation is complete)
                WeakReferenceMessenger.Default.Send(new HideProgressBarMessage());
            }
            if (changeInfo.NameChanged)
            {
                onPropertyChanged(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.Name));
            }
            if (changeInfo.TimeChanged)
            {
                onPropertyChanged(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.TimeText));
                onPropertyChanged(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.Hour));
                onPropertyChanged(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.Minute));
                onPropertyChanged(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.Meridian));
                onPropertyChanged(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.MeridianText));
            }
            if (changeInfo.MusicEnabledChanged)
            {
                onPropertyChanged(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.MusicEnabled));
            }
            // Always notify SubTitle if any bible schedule property changed to ensure UI updates
            if (changeInfo.AnyBibleSchedulePropertyChanged)
            {
                logger.Debug("ScheduleListItemViewModel: NotifyPropertyChanges - Bible schedule property changed, notifying display properties for schedule {ScheduleId}",
                    getScheduleId());
                onPropertyChanged(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.SubTitle));
                onPropertyChanged(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.Language));
                onPropertyChanged(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.BiblePublicationName));
                onPropertyChanged(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.BiblePublicationSectionName));
                onPropertyChanged(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.BiblePublicationTrackName));
                onPropertyChanged(nameof(Bible.Alarm.ViewModels.ScheduleListItemViewModel.BiblePublicationSectionAndTrackOneLine));

                // Hide progress bar when subtitle is updated (indicates track change is complete)
                WeakReferenceMessenger.Default.Send(new HideProgressBarMessage());
            }

            // Preserve existing behavior: expensive reflection-based update
            // (kept in one place so it can be removed later if desired)
            raisePropertiesChangedEvent();
        });
    }
}
