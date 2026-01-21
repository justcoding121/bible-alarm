#nullable enable
using AutoMapper;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.MusicSelection;
using Fluxor;
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

        // Check if Bible language direction changed (affects RTL/LTR layout)
        if (currentSchedule != null)
        {
            HandleBibleLanguageDirectionChange(currentSchedule);
        }

        // Check if Music language direction changed (affects RTL/LTR layout for music rows)
        if (currentSchedule != null)
        {
            HandleMusicLanguageDirectionChange(currentSchedule);
        }

        // Check if CurrentSchedule music changed (CurrentSchedule is the single source of truth)
        if (currentSchedule != null && currentSchedule.MusicType.HasValue)
        {
            // Create MusicStateItem from CurrentSchedule for the handler
            var musicStateItem = new MusicStateItem
            {
                MusicType = currentSchedule.MusicType.Value,
                LanguageCode = currentSchedule.MusicLanguageCode,
                PublicationCode = currentSchedule.MusicPublicationCode ?? string.Empty,
                TrackNumber = currentSchedule.MusicTrackNumber ?? 0,
                Repeat = currentSchedule.MusicRepeat ?? false
            };
            HandleCurrentMusicChange(
                musicStateItem,
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

    // Debounce flag to prevent multiple rapid notifications
    private bool isPropertyChangeScheduled;

    private void HandleMusicPropertyChanges(
        ScheduleStateItem currentSchedule,
        Action<bool> setShouldScrollToBottom,
        Action<string> onPropertyChanged)
    {
        var (musicTypeChanged, languageCodeChanged, publicationCodeChanged, sectionCodeChanged, trackNumberChanged, repeatChanged) =
            stateTracker.DetectChanges(currentSchedule);

        // Also check for display name changes (publication name, section name) that don't trigger code changes
        var publicationNameChanged = stateTracker.HasMusicPublicationNameChanged(currentSchedule);
        var sectionNameChanged = stateTracker.HasMusicSectionNameChanged(currentSchedule);

        if (musicTypeChanged || languageCodeChanged || publicationCodeChanged || sectionCodeChanged || trackNumberChanged || repeatChanged || publicationNameChanged || sectionNameChanged)
        {
            // Update last values immediately to prevent duplicate detection
            stateTracker.UpdateFromSchedule(currentSchedule);

            // Prevent duplicate scheduled notifications
            if (isPropertyChangeScheduled)
            {
                return;
            }
            isPropertyChangeScheduled = true;

            // Capture values for the closure
            var capturedMusicType = currentSchedule.MusicType;
            var capturedMusicEnabled = currentSchedule.MusicEnabled;

            // Trigger property change notifications with cascading logic (single batched call)
            MainThread.BeginInvokeOnMainThread(() =>
            {
                isPropertyChangeScheduled = false;
                propertyNotifier.NotifyPropertiesChanged(
                    musicTypeChanged,
                    languageCodeChanged,
                    publicationCodeChanged,
                    sectionCodeChanged,
                    trackNumberChanged,
                    repeatChanged,
                    capturedMusicType,
                    shouldScroll => { if (capturedMusicEnabled) setShouldScrollToBottom(shouldScroll); });

                // Also notify display text properties if only display names changed (not codes)
                if (!musicTypeChanged && !languageCodeChanged && !publicationCodeChanged && !sectionCodeChanged && !trackNumberChanged && !repeatChanged)
                {
                    if (publicationNameChanged)
                    {
                        onPropertyChanged(nameof(MusicSelectionContainerViewModel.SongPublicationDisplayText));
                    }
                    if (sectionNameChanged)
                    {
                        onPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicSectionDisplayText));
                    }
                }
            });
        }
    }

    private void HandleBibleLanguageDirectionChange(ScheduleStateItem currentSchedule)
    {
        if (stateTracker.HasBibleLanguageDirectionChanged(currentSchedule))
        {
            stateTracker.UpdateBibleLanguageDirection(currentSchedule.BiblePublicationLanguageDirection);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                propertyNotifier.NotifyFlowDirectionChanged();
            });
        }
    }

    private void HandleMusicLanguageDirectionChange(ScheduleStateItem currentSchedule)
    {
        if (stateTracker.HasMusicLanguageDirectionChanged(currentSchedule))
        {
            stateTracker.UpdateMusicLanguageDirection(currentSchedule.MusicLanguageDirection);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                propertyNotifier.NotifyFlowDirectionChanged();
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
        // Check section code from CurrentSchedule (now stored in AlarmMusic.SectionCode)
        var currentSchedule = state.Value.CurrentSchedule;
        var sectionCodeChanged = currentSchedule?.MusicSectionCode != stateTracker.LastMusicSectionCode;
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
                sectionCodeChanged,
                trackNumberChanged,
                false,
                newMusic.MusicType);
        });
    }
}
