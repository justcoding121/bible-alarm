#nullable enable
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.MusicSelection;
using Fluxor;
using Microsoft.Maui.ApplicationModel;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Schedule.MusicSelectionContainerViewModelHelpers;

/// <summary>
/// Handles state change logic for MusicSelectionContainerViewModel.
/// Separated from MusicSelectionContainerViewModel for better modularity.
/// </summary>
public sealed class MusicStateChangeHandler
{
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;
    private readonly MusicStateTracker stateTracker;
    private readonly MusicPropertyNotifier propertyNotifier;
    private readonly MusicDisplayTextProvider displayTextProvider;

    public MusicStateChangeHandler(
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        IMapper mapper,
        MusicStateTracker stateTracker,
        MusicPropertyNotifier propertyNotifier,
        MusicDisplayTextProvider displayTextProvider)
    {
        this.state = state;
        this.dispatcher = dispatcher;
        this.mapper = mapper;
        this.stateTracker = stateTracker;
        this.propertyNotifier = propertyNotifier;
        this.displayTextProvider = displayTextProvider;
    }

    /// <summary>
    /// Handles state changes, detecting and processing music property changes.
    /// </summary>
    public void HandleStateChanged(
        int scheduleId,
        MusicStateHolder stateHolder,
        bool? initialMusicEnabledOnPageLoad,
        Action<bool> setShouldScrollToBottom,
        Action<string> onPropertyChanged)
    {
        var stateValue = state.Value;
        var currentSchedule = stateValue.CurrentSchedule;

        // Check if schedule ID changed (new schedule opened)
        if (currentSchedule != null && currentSchedule.Id != scheduleId)
        {
            // Schedule changed - this will be handled by the ViewModel's initialization logic
            return;
        }

        // Check if MusicEnabled changed in state
        if (currentSchedule != null)
        {
            HandleMusicEnabledChange(
                currentSchedule,
                stateHolder,
                initialMusicEnabledOnPageLoad,
                setShouldScrollToBottom,
                onPropertyChanged);
        }

        // Check if CurrentSchedule.MusicType or MusicRepeat changed
        if (currentSchedule != null)
        {
            HandleMusicPropertyChanges(
                currentSchedule,
                setShouldScrollToBottom,
                onPropertyChanged);
        }

        // Check if CurrentMusic changed
        if (stateValue.CurrentMusic != null)
        {
            HandleCurrentMusicChange(
                stateValue.CurrentMusic,
                stateHolder,
                onPropertyChanged);
        }
    }

    private void HandleMusicEnabledChange(
        ScheduleStateItem currentSchedule,
        MusicStateHolder stateHolder,
        bool? initialMusicEnabledOnPageLoad,
        Action<bool> setShouldScrollToBottom,
        Action<string> onPropertyChanged)
    {
        var stateMusicEnabled = currentSchedule.MusicEnabled;
        var previousMusicEnabled = stateTracker.LastMusicEnabled ?? false;

        // Clear pending value since state has been updated
        if (stateHolder.PendingMusicEnabled.HasValue && stateHolder.PendingMusicEnabled.Value == stateMusicEnabled)
        {
            stateHolder.PendingMusicEnabled = null; // State now matches, clear pending
        }

        // Check if MusicEnabled changed by comparing with tracked previous value
        if (stateTracker.HasMusicEnabledChanged(currentSchedule))
        {
            // Check if notification is already queued to prevent duplicate queued notifications
            if (stateHolder.IsMusicEnabledNotificationQueued)
            {
                // Update tracked value but don't queue another notification
                stateTracker.UpdateMusicEnabled(stateMusicEnabled);
                stateHolder.PendingMusicEnabled = null;
                return;
            }

            // State has a different value, update tracked value and notify
            stateHolder.IsUpdatingFromState = true;
            try
            {
                stateTracker.UpdateMusicEnabled(stateMusicEnabled);
                stateHolder.PendingMusicEnabled = null; // Clear pending when updating from state
                stateHolder.IsMusicEnabledNotificationQueued = true; // Mark as queued
                // Marshal to UI thread to ensure PropertyChanged events are raised on the correct thread
                // This is important because OnStateChanged can be called from background threads
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    stateHolder.IsMusicEnabledNotificationQueued = false; // Reset flag when notification executes
                    onPropertyChanged("MusicEnabled");
                });

                // If music was just enabled, update cache from state
                // NOTE: Loading default music from DB is handled in MusicEnabled setter
                if (stateMusicEnabled && !previousMusicEnabled)
                {
                    // Update lastScheduleMusicType to ensure change detection works
                    if (currentSchedule.MusicType.HasValue)
                    {
                        stateTracker.InitializeFromSchedule(currentSchedule);
                    }

                    // Notify MusicTypeDisplayText on main thread with delay to ensure content is visible first
                    // This is critical for iOS - bindings are evaluated when content becomes visible
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        // Wait a bit to ensure CollapsibleContent is visible before notifying
                        onPropertyChanged("MusicTypeDisplayText");
                        onPropertyChanged("IsMusicLanguageVisible");
                        onPropertyChanged("IsSongBookVisible");
                    });

                    // Check if MusicTrackName is already in state (from bootstrap or from DB load in setter)
                    if (!string.IsNullOrWhiteSpace(currentSchedule.MusicTrackName))
                    {
                        // Update cache and notify
                        displayTextProvider.UpdateTrackCache(
                            currentSchedule.MusicTrackName,
                            currentSchedule.MusicTrackNumber,
                            currentSchedule.MusicPublicationCode,
                            currentSchedule.MusicLanguageCode,
                            currentSchedule.MusicType);
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            onPropertyChanged("TrackDisplayText");
                        });
                    }
                    else
                    {
                        // Track name not in state yet - wait for MusicEnabled setter to load it from DB
                        // Just notify property change to trigger UI update
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            onPropertyChanged("TrackDisplayText");
                        });
                    }
                }
            }
            finally
            {
                stateHolder.IsUpdatingFromState = false;
            }
        }
    }

    private void HandleMusicPropertyChanges(
        ScheduleStateItem currentSchedule,
        Action<bool> setShouldScrollToBottom,
        Action<string> onPropertyChanged)
    {
        var (musicTypeChanged, languageCodeChanged, publicationCodeChanged, trackNumberChanged, repeatChanged) =
            stateTracker.DetectChanges(currentSchedule);

        // Determine which properties need to be notified (cascading logic)
        var notifyMusicType = musicTypeChanged;
        var notifyLanguage = musicTypeChanged || languageCodeChanged;
        var notifySongBook = musicTypeChanged || languageCodeChanged || publicationCodeChanged;
        var notifyTrack = musicTypeChanged || languageCodeChanged || publicationCodeChanged || trackNumberChanged;

        if (notifyMusicType || notifyLanguage || notifySongBook || notifyTrack || repeatChanged)
        {
            // Update last values
            stateTracker.UpdateFromSchedule(currentSchedule);

            // Trigger property change notifications with cascading logic
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var currentSchedule = state.Value.CurrentSchedule;
                propertyNotifier.NotifyPropertiesChanged(
                    notifyMusicType,
                    notifyLanguage,
                    notifySongBook,
                    notifyTrack,
                    repeatChanged,
                    currentSchedule?.MusicType,
                    shouldScroll => { if (currentSchedule?.MusicEnabled == true) setShouldScrollToBottom(shouldScroll); });
            });
        }
    }

    private void HandleCurrentMusicChange(
        MusicStateItem newMusicItem,
        MusicStateHolder stateHolder,
        Action<string> onPropertyChanged)
    {
        // Check if the music actually changed by comparing properties
        var hasChanged = stateHolder.LastMusic == null ||
                        stateHolder.Music == null ||
                        stateHolder.LastMusic.LanguageCode != newMusicItem.LanguageCode ||
                        stateHolder.LastMusic.PublicationCode != newMusicItem.PublicationCode ||
                        stateHolder.LastMusic.MusicType != newMusicItem.MusicType ||
                        stateHolder.LastMusic.TrackNumber != newMusicItem.TrackNumber ||
                        (stateHolder.Music != null &&
                         (stateHolder.Music.TrackNumber != newMusicItem.TrackNumber ||
                          stateHolder.Music.MusicType != newMusicItem.MusicType ||
                          stateHolder.Music.LanguageCode != newMusicItem.LanguageCode ||
                          stateHolder.Music.PublicationCode != newMusicItem.PublicationCode));

        if (!hasChanged)
        {
            return;
        }

        // Map DTO to entity
        var newMusic = mapper.Map<AlarmMusic>(newMusicItem);

        // Determine what changed to trigger cascading notifications (compare BEFORE updating)
        var musicTypeChanged = stateHolder.Music?.MusicType != newMusic.MusicType;
        var languageCodeChanged = stateHolder.Music?.LanguageCode != newMusic.LanguageCode;
        var publicationCodeChanged = stateHolder.Music?.PublicationCode != newMusic.PublicationCode;
        var trackNumberChanged = stateHolder.Music?.TrackNumber != newMusic.TrackNumber;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            stateHolder.Music = newMusic;
            stateHolder.LastMusic = newMusic;
            stateHolder.MusicUpdated = true;

            // Trigger cascading property change notifications
            propertyNotifier.NotifyPropertiesChanged(
                musicTypeChanged,
                languageCodeChanged,
                publicationCodeChanged,
                trackNumberChanged,
                false,
                newMusic.MusicType);
        });
    }
}
