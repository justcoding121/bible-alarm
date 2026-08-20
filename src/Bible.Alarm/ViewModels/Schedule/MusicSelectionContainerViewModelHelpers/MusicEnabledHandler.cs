#nullable enable

using System;
using System.Linq;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
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

        if (currentValue == value)
        {
            return false;
        }

        if (isUpdatingFromState)
        {
            setPendingMusicEnabled(false);
            onPropertyChanged();
            return false;
        }

        setPendingMusicEnabled(value);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            onPropertyChanged();
        });

        if (value && !currentValue)
        {
            setShouldScrollToBottom(true);
        }

        DispatchMusicEnabledScheduleUpdate(currentSchedule, value);

        if (value && !currentValue && initialMusicEnabledOnPageLoad is false)
        {
            EnqueueDefaultMelodyPublicationWhenEmpty();
        }

        return true;
    }

    private void DispatchMusicEnabledScheduleUpdate(ScheduleStateItem currentSchedule, bool value)
    {
        _ = Task.Run(() =>
        {
            var clonedSchedule = currentSchedule.DeepClone();
            clonedSchedule.MusicEnabled = value;

            var shouldTriggerMusicCascade = value &&
                                           !string.IsNullOrWhiteSpace(clonedSchedule.MusicPublicationCode) &&
                                           !string.IsNullOrWhiteSpace(clonedSchedule.MusicTrackCode);

            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(
                clonedSchedule,
                musicUpdated: shouldTriggerMusicCascade,
                biblePublicationUpdated: false,
                shouldSave: false));
        });
    }

    private void EnqueueDefaultMelodyPublicationWhenEmpty()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await TryPopulateDefaultMelodyWhenScheduleEmptyAsync();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "MusicEnabled: Error loading default music from DB");
            }
        });
    }

    private async Task TryPopulateDefaultMelodyWhenScheduleEmptyAsync()
    {
        var schedule = state.Value.CurrentSchedule;
        if (schedule == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(schedule.MusicPublicationCode))
        {
            return;
        }

        var melodyPick = await TryLoadDefaultMelodyPublicationWithTracksAsync().ConfigureAwait(false);
        if (melodyPick == null)
        {
            return;
        }

        var (defaultPublicationCode, defaultPublicationName, melodyMusic) = melodyPick.Value;

        ChooseRandomMelodyTrack(melodyMusic, out var chosenSection, out var chosenTrack);

        var latestSchedule = state.Value.CurrentSchedule;
        if (latestSchedule == null)
        {
            return;
        }

        if (IsScheduleAlreadyPointingAtChosenMelody(
                latestSchedule,
                defaultPublicationCode,
                chosenSection,
                chosenTrack))
        {
            return;
        }

        DispatchChosenMelodyOnSchedule(latestSchedule, defaultPublicationCode, defaultPublicationName, chosenSection,
            chosenTrack);
    }

    private async Task<(string Code, string Name, MelodyMusic Music)?> TryLoadDefaultMelodyPublicationWithTracksAsync()
    {
        var melodyMusicService = serviceProvider.GetRequiredService<IMelodyMusicService>();

        var melodyReleases = await melodyMusicService.GetAllAsync();
        if (melodyReleases == null || melodyReleases.Count == 0)
        {
            logger.Warning("MusicEnabled: No melody music publications found in database");
            return null;
        }

        if (!TryResolvePreferredOrFirstMelodyPublication(
                melodyReleases,
                AppConstants.Media.MelodyMusicPublicationCodeIam,
                out var defaultPublicationCode,
                out var defaultPublicationName))
        {
            return null;
        }

        var melodyMusic = await melodyMusicService.GetByCodeWithTracksAsync(defaultPublicationCode);

        if (melodyMusic?.Tracks == null || melodyMusic.Tracks.Count == 0)
        {
            return null;
        }

        return (defaultPublicationCode, defaultPublicationName, melodyMusic);
    }

    private static bool TryResolvePreferredOrFirstMelodyPublication(
        Dictionary<string, MelodyMusic> melodyReleases,
        string preferredPublicationCode,
        out string defaultPublicationCode,
        out string defaultPublicationName)
    {
        if (melodyReleases.TryGetValue(preferredPublicationCode, out var preferred) && preferred != null)
        {
            defaultPublicationCode = preferredPublicationCode;
            defaultPublicationName = preferred.Name;
            return true;
        }

        foreach (var pair in melodyReleases)
        {
            if (pair.Value != null)
            {
                defaultPublicationCode = pair.Key;
                defaultPublicationName = pair.Value.Name;
                return true;
            }
        }

        defaultPublicationCode = string.Empty;
        defaultPublicationName = string.Empty;
        return false;
    }

    private static void ChooseRandomMelodyTrack(
        MelodyMusic melodyMusic,
        out BiblePublicationSection? chosenSection,
        out BiblePublicationTrack chosenTrack)
    {
        chosenSection = null;
        BiblePublicationTrack? track = null;

        var sectionsWithTracks = melodyMusic.Sections?
            .Where(static s => s.Tracks != null && s.Tracks.Count > 0)
            .ToList();

        if (sectionsWithTracks is { Count: > 0 })
        {
            chosenSection = sectionsWithTracks[Random.Shared.Next(sectionsWithTracks.Count)];
            if (chosenSection.Tracks.Count > 0)
            {
                track = chosenSection.Tracks[Random.Shared.Next(chosenSection.Tracks.Count)];
            }
        }

        chosenTrack = track ?? melodyMusic.Tracks[Random.Shared.Next(melodyMusic.Tracks.Count)];
    }

    private static bool IsScheduleAlreadyPointingAtChosenMelody(
        ScheduleStateItem latestSchedule,
        string defaultPublicationCode,
        BiblePublicationSection? chosenSection,
        BiblePublicationTrack chosenTrack) =>
        latestSchedule.MusicLanguageCode == null &&
        string.Equals(latestSchedule.MusicPublicationCode, defaultPublicationCode, StringComparison.OrdinalIgnoreCase) &&
        SectionCodeHelper.CodeEquals(latestSchedule.MusicSectionCode, chosenSection?.SectionCode) &&
        CodeComparisonHelper.Equals(latestSchedule.MusicTrackCode, TrackCodeHelper.GetFromTrack(chosenTrack)) &&
        latestSchedule.MusicEnabled;

    private void DispatchChosenMelodyOnSchedule(
        ScheduleStateItem latestSchedule,
        string defaultPublicationCode,
        string defaultPublicationName,
        BiblePublicationSection? chosenSection,
        BiblePublicationTrack chosenTrack)
    {
        var clonedSchedule = latestSchedule.DeepClone();
        clonedSchedule.MusicEnabled = true;
        clonedSchedule.MusicPublicationCode = defaultPublicationCode;
        clonedSchedule.MusicPublicationName = defaultPublicationName;
        clonedSchedule.MusicLanguageCode = null;
        clonedSchedule.MusicSectionCode = chosenSection?.SectionCode;
        clonedSchedule.MusicSectionName = chosenSection?.Name;
        clonedSchedule.MusicTrackCode = TrackCodeHelper.GetFromTrack(chosenTrack);
        clonedSchedule.MusicRepeat = false;
        clonedSchedule.MusicTrackName = MediaTrackTitleHelper.DecodeHtmlTitle(chosenTrack.Title);

        dispatcher.Dispatch(
            new UpdateScheduleFromViewModelAction(clonedSchedule, musicUpdated: true, biblePublicationUpdated: false,
                shouldSave: false));
    }
}
