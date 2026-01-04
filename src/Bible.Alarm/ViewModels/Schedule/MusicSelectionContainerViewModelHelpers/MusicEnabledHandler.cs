#nullable enable

using AutoMapper;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Schedule.MusicSelectionContainerViewModelHelpers;

/// <summary>
/// Handles MusicEnabled property logic for MusicSelectionContainerViewModel.
/// </summary>
public class MusicEnabledHandler
{
    private readonly ILogger logger;
    private readonly IMapper mapper;
    private readonly IDispatcher dispatcher;
    private readonly IServiceProvider serviceProvider;
    private readonly IState<ApplicationState> state;

    public MusicEnabledHandler(
        ILogger logger,
        IMapper mapper,
        IDispatcher dispatcher,
        IServiceProvider serviceProvider,
        IState<ApplicationState> state)
    {
        this.logger = logger;
        this.mapper = mapper;
        this.dispatcher = dispatcher;
        this.serviceProvider = serviceProvider;
        this.state = state;
    }

    public bool HandleSetMusicEnabled(
        bool value,
        bool currentValue,
        bool isUpdatingFromState,
        bool? initialMusicEnabledOnPageLoad,
        Action<bool> setPendingMusicEnabled,
        Action<bool> setShouldScrollToBottom,
        Action onPropertyChanged)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            logger.Warning("MusicEnabled setter: CurrentSchedule is null, cannot update");
            return false;
        }

        // Check if the value is actually different from the current state
        if (currentValue == value)
        {
            // Value hasn't changed, don't dispatch
            logger.Debug("MusicEnabled setter: Value unchanged ({Value}), skipping", value);
            return false;
        }

        // Prevent dispatching if this update is coming from state (not user interaction)
        if (isUpdatingFromState)
        {
            logger.Debug("MusicEnabled setter: Update from state, skipping dispatch");
            setPendingMusicEnabled(false); // Clear pending when updating from state
            onPropertyChanged();
            return false;
        }

        logger.Debug("MusicEnabled setter: Setting to {Value} (was {CurrentValue})", value, currentValue);

        // Set optimistic update value immediately
        setPendingMusicEnabled(value);

        // Trigger PropertyChanged immediately to update UI (must be on UI thread for MAUI)
        MainThread.BeginInvokeOnMainThread(() =>
        {
            logger.Debug("MusicEnabled setter: Calling OnPropertyChanged on UI thread");
            onPropertyChanged();
        });

        // Signal to scroll to bottom when user enables music
        if (value && !currentValue)
        {
            setShouldScrollToBottom(true);
        }

        // Update state (will clear pendingMusicEnabled when state updates)
        // Run DeepClone and mapping on background thread to avoid blocking UI
        _ = Task.Run(() =>
        {
            var clonedSchedule = currentSchedule.DeepClone();
            var scheduleStateItem = mapper.Map<ScheduleStateItem>(clonedSchedule);
            scheduleStateItem.MusicEnabled = value;
            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(scheduleStateItem, false, false, shouldSave: false));
        });

        // If enabling music, only reset to default music if:
        // 1. Music was disabled when schedule page was first opened (initialMusicEnabledOnPageLoad == false)
        // 2. This is the first time enabling it on this page load (value && !currentValue)
        // On subsequent enable/disable cycles, preserve whatever music was selected
        if (value && !currentValue)
        {
            var shouldResetToDefault = initialMusicEnabledOnPageLoad.HasValue &&
                                      initialMusicEnabledOnPageLoad.Value == false;

            if (shouldResetToDefault)
            {
                // This is the first enable after opening a schedule with music disabled
                // Reset to default music (same as new schedule)
                // This is the ONLY place we query DB when enabling music on existing schedule
                Task.Run(async () =>
                {
                    try
                    {
                        const string DefaultPublicationCode = "iam";
                        var melodyMusicService = serviceProvider.GetRequiredService<IMelodyMusicService>();

                        logger.Information("MusicEnabled: First enable after opening schedule with music disabled, resetting to default music (same as new schedule)");

                        // Get default music from DB (same as sample schedule)
                        var melodyMusic = await melodyMusicService.GetByCodeWithTracksAsync(DefaultPublicationCode);

                        if (melodyMusic != null && melodyMusic.Tracks != null && melodyMusic.Tracks.Count > 0)
                        {
                            // Select a random track (same as sample schedule)
                            var randomTrack = melodyMusic.Tracks[Random.Shared.Next(melodyMusic.Tracks.Count)];

                            // Get the latest state to ensure MusicEnabled is preserved
                            var latestSchedule = state.Value.CurrentSchedule;
                            if (latestSchedule == null)
                            {
                                logger.Warning("MusicEnabled: CurrentSchedule is null when resetting to default music");
                                return;
                            }

                            // Check if music properties are already set to what we want to set
                            // This prevents redundant dispatches if the state was already updated
                            if (latestSchedule.MusicType == MusicType.Melodies &&
                                latestSchedule.MusicPublicationCode == DefaultPublicationCode &&
                                latestSchedule.MusicTrackNumber == randomTrack.Number &&
                                latestSchedule.MusicEnabled == true)
                            {
                                logger.Debug("MusicEnabled: Music properties already match default, skipping redundant update");
                                return;
                            }

                            // Update state with default music properties (reset to default)
                            // IMPORTANT: Preserve MusicEnabled from the latest state (should already be true from first dispatch)
                            // Run DeepClone and mapping on background thread to avoid blocking UI
                            var clonedSchedule = latestSchedule.DeepClone();
                            var scheduleStateItem = mapper.Map<ScheduleStateItem>(clonedSchedule);
                            // MusicEnabled should already be true from the first dispatch, but ensure it's set
                            scheduleStateItem.MusicEnabled = true;
                            scheduleStateItem.MusicType = MusicType.Melodies;
                            scheduleStateItem.MusicPublicationCode = DefaultPublicationCode;
                            scheduleStateItem.MusicLanguageCode = null;
                            scheduleStateItem.MusicTrackNumber = randomTrack.Number;
                            scheduleStateItem.MusicRepeat = false;
                            scheduleStateItem.MusicTrackName = $"Melody Number(s) {randomTrack.Title}";

                            logger.Information("MusicEnabled: Resetting to default music. MusicType={MusicType}, TrackNumber={TrackNumber}",
                                scheduleStateItem.MusicType, scheduleStateItem.MusicTrackNumber);

                            // Update state with music properties (MusicEnabled should already be true from first dispatch)
                            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(scheduleStateItem, false, false, shouldSave: false));

                            logger.Information("MusicEnabled: Reset to default music. MusicType=Melodies, TrackNumber={TrackNumber}, TrackName={TrackName}",
                                randomTrack.Number, scheduleStateItem.MusicTrackName);
                        }
                        else
                        {
                            logger.Warning("MusicEnabled: Could not load default music from DB. Melody music or tracks not found.");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "MusicEnabled: Error loading default music from DB");
                    }
                });
            }
            else
            {
                // Subsequent enable or music was already enabled on page load
                // Preserve existing music selection
                logger.Information("MusicEnabled: Re-enabling music, preserving existing music selection. InitialMusicEnabledOnPageLoad={InitialMusicEnabled}",
                    initialMusicEnabledOnPageLoad?.ToString() ?? "null");
            }
        }

        return true;
    }
}

