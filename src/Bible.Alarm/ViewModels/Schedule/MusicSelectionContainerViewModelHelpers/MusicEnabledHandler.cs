#nullable enable

using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Fluxor;
using Serilog;
using System.Net;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Schedule.MusicSelectionContainerViewModelHelpers;

/// <summary>
/// Handles MusicEnabled property logic for MusicSelectionContainerViewModel.
/// </summary>
public class MusicEnabledHandler
{
    private readonly ILogger logger;
    private readonly IDispatcher dispatcher;
    private readonly IServiceProvider serviceProvider;
    private readonly IState<ApplicationState> state;

    public MusicEnabledHandler(
        ILogger logger,
        IDispatcher dispatcher,
        IServiceProvider serviceProvider,
        IState<ApplicationState> state)
    {
        this.logger = logger;
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
            return false;
        }

        // Check if the value is actually different from the current state
        if (currentValue == value)
        {
            // Value hasn't changed, don't dispatch
            return false;
        }

        // Prevent dispatching if this update is coming from state (not user interaction)
        if (isUpdatingFromState)
        {
            // Clear pending when updating from state
            setPendingMusicEnabled(false);
            onPropertyChanged();
            return false;
        }

        // Set optimistic update value immediately
        setPendingMusicEnabled(value);

        // Trigger PropertyChanged immediately to update UI
        MainThread.BeginInvokeOnMainThread(() =>
        {
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
            // DeepClone already returns ScheduleStateItem, no need to map again
            clonedSchedule.MusicEnabled = value;

            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(clonedSchedule, false, false, shouldSave: false));
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
                // Only set default music (iam) when state has no publication code. Otherwise retain current selection (e.g. osg).
                Task.Run(async () =>
                {
                    try
                    {
                        var schedule = state.Value.CurrentSchedule;
                        if (schedule == null)
                        {
                            return;
                        }
                        if (!string.IsNullOrEmpty(schedule.MusicPublicationCode))
                        {
                            // Schedule already has a music publication (e.g. osg) – retain it, do not reset to iam
                            return;
                        }

                        var melodyMusicService = serviceProvider.GetRequiredService<IMelodyMusicService>();

                        // Prefer "iam" (Kingdom Melodies) as default, else first available (same as sample schedule)
                        const string PreferredMelodyPublicationCode = "iam";
                        var melodyReleases = await melodyMusicService.GetAllAsync();
                        if (melodyReleases == null || melodyReleases.Count == 0)
                        {
                            logger.Warning("MusicEnabled: No melody music publications found in database");
                            return;
                        }

                        string defaultPublicationCode;
                        string defaultPublicationName;
                        if (melodyReleases.TryGetValue(PreferredMelodyPublicationCode, out var preferred) && preferred != null)
                        {
                            defaultPublicationCode = PreferredMelodyPublicationCode;
                            defaultPublicationName = preferred.Name;
                        }
                        else
                        {
                            var firstMelody = melodyReleases.FirstOrDefault();
                            if (firstMelody.Value == null)
                            {
                                return;
                            }
                            defaultPublicationCode = firstMelody.Key;
                            defaultPublicationName = firstMelody.Value.Name;
                        }

                        // Get default music from DB (same as sample schedule)
                        var melodyMusic = await melodyMusicService.GetByCodeWithTracksAsync(defaultPublicationCode);

                        if (melodyMusic != null && melodyMusic.Tracks != null && melodyMusic.Tracks.Count > 0)
                        {
                            // For sectioned melody publications (e.g., "iam"), pick a random disc/section first,
                            // then pick a random track within that section.
                            BiblePublicationSection? chosenSection = null;
                            BiblePublicationTrack? chosenTrack = null;

                            var sectionsWithTracks = melodyMusic.Sections?
                                .Where(s => s.Tracks != null && s.Tracks.Count > 0)
                                .ToList();

                            if (sectionsWithTracks != null && sectionsWithTracks.Count > 0)
                            {
                                chosenSection = sectionsWithTracks[Random.Shared.Next(sectionsWithTracks.Count)];
                                if (chosenSection.Tracks.Count > 0)
                                {
                                    chosenTrack = chosenSection.Tracks[Random.Shared.Next(chosenSection.Tracks.Count)];
                                }
                            }

                            // Fallback: if no section could be selected (non-sectioned melody or partial harvest),
                            // select a random track from the publication-level list.
                            chosenTrack ??= melodyMusic.Tracks[Random.Shared.Next(melodyMusic.Tracks.Count)];

                            // Get the latest state to ensure MusicEnabled is preserved
                            var latestSchedule = state.Value.CurrentSchedule;
                            if (latestSchedule == null)
                            {
                                return;
                            }

                            // Check if music properties are already set to what we want to set
                            // This prevents redundant dispatches if the state was already updated
                            // For melody music, LanguageCode is null
                            if (latestSchedule.MusicLanguageCode == null &&
                                latestSchedule.MusicPublicationCode == defaultPublicationCode &&
                                latestSchedule.MusicSectionCode == chosenSection?.SectionCode &&
                                latestSchedule.MusicTrackCode == chosenTrack.Number.ToString(System.Globalization.CultureInfo.InvariantCulture) &&
                                latestSchedule.MusicEnabled == true)
                            {
                                return;
                            }

                            // Update state with default music properties (reset to default)
                            // IMPORTANT: Preserve MusicEnabled from the latest state (should already be true from first dispatch)
                            // DeepClone already returns ScheduleStateItem, no need to map again
                            var clonedSchedule = latestSchedule.DeepClone();
                            // MusicEnabled should already be true from the first dispatch, but ensure it's set
                            clonedSchedule.MusicEnabled = true;
                            clonedSchedule.MusicPublicationCode = defaultPublicationCode;
                            clonedSchedule.MusicPublicationName = defaultPublicationName;
                            clonedSchedule.MusicLanguageCode = null; // Melody music has no language
                            clonedSchedule.MusicSectionCode = chosenSection?.SectionCode;
                            clonedSchedule.MusicSectionName = chosenSection?.Name;
                            clonedSchedule.MusicTrackCode = chosenTrack.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                            clonedSchedule.MusicRepeat = false;
                            // Titles for melody tracks should come from harvested track titles as-is.
                            clonedSchedule.MusicTrackName = WebUtility.HtmlDecode(chosenTrack.Title).Replace('\u00A0', ' ');

                            // Update state with music properties (MusicEnabled should already be true from first dispatch)
                            // Mark as music-updated so:
                            // - music cascade can validate/normalize (it should be a no-op when track is already set)
                            // - modal expected-counts are refreshed so section-row arrow can show immediately (e.g., "iam")
                            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(clonedSchedule, musicUpdated: true, biblePublicationUpdated: false, shouldSave: false));
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
            }
        }

        return true;
    }
}
