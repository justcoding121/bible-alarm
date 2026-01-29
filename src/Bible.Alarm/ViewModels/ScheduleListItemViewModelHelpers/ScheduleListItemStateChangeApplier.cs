#nullable enable

using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers;

internal sealed class ScheduleListItemStateChangeApplier
{
    private readonly ILogger logger;
    private readonly IState<ApplicationState> applicationState;
    private readonly ScheduleListItemStateHandler stateHandler;
    private readonly ScheduleListItemPropertyManager propertyManager;

    public ScheduleListItemStateChangeApplier(
        ILogger logger,
        IState<ApplicationState> applicationState,
        ScheduleListItemStateHandler stateHandler,
        ScheduleListItemPropertyManager propertyManager)
    {
        this.logger = logger;
        this.applicationState = applicationState;
        this.stateHandler = stateHandler;
        this.propertyManager = propertyManager;
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
        propertyManager.IsEnabled = updatedSchedule.IsEnabled;

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

            // Update tracked publication code if it changed
            if (changeInfo.BiblePublicationCodeChanged && updatedScheduleItem != null)
            {
                stateHandler.LastKnownBiblePublicationCode = updatedScheduleItem.BiblePublicationCode;
                onPropertyChanged(nameof(BiblePublicationName));
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
            onPropertyChanged(nameof(This));

            if (changeInfo.DaysOfWeekChanged)
            {
                logger.Debug("ScheduleListItemViewModel: NotifyPropertyChanges - DaysOfWeek changed for schedule {ScheduleId}. New value: {NewDaysOfWeek}",
                    getScheduleId(), getSchedule()?.DaysOfWeek ?? 0);
                onPropertyChanged(nameof(DaysOfWeek));
            }
            if (changeInfo.IsEnabledChanged)
            {
                onPropertyChanged(nameof(IsEnabled));

                // Hide progress bar when IsEnabled is updated (indicates toggle operation is complete)
                WeakReferenceMessenger.Default.Send(new HideProgressBarMessage());
            }
            if (changeInfo.NameChanged)
            {
                onPropertyChanged(nameof(Name));
            }
            if (changeInfo.TimeChanged)
            {
                onPropertyChanged(nameof(TimeText));
                onPropertyChanged(nameof(Hour));
                onPropertyChanged(nameof(Minute));
                onPropertyChanged(nameof(Meridian));
                onPropertyChanged(nameof(MeridianText));
            }
            if (changeInfo.MusicEnabledChanged)
            {
                onPropertyChanged(nameof(MusicEnabled));
            }
            // Always notify SubTitle if any bible schedule property changed to ensure UI updates
            if (changeInfo.AnyBibleSchedulePropertyChanged)
            {
                logger.Debug("ScheduleListItemViewModel: NotifyPropertyChanges - Bible schedule property changed, notifying SubTitle for schedule {ScheduleId}",
                    getScheduleId());
                onPropertyChanged(nameof(SubTitle));
                onPropertyChanged(nameof(Language));
                onPropertyChanged(nameof(BiblePublicationName));

                // Hide progress bar when subtitle is updated (indicates track change is complete)
                WeakReferenceMessenger.Default.Send(new HideProgressBarMessage());
            }

            // Preserve existing behavior: expensive reflection-based update
            // (kept in one place so it can be removed later if desired)
            raisePropertiesChangedEvent();
        });
    }
}

