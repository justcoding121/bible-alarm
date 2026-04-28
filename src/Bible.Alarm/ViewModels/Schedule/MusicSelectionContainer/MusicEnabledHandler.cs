#nullable enable

using System.Net;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Schedule.MusicSelectionContainer;

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

        if (value && !currentValue)
        {
            var shouldResetToDefault = initialMusicEnabledOnPageLoad is false;

            if (shouldResetToDefault)
            {
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
                            return;
                        }

                        var melodyMusicService = serviceProvider.GetRequiredService<IMelodyMusicService>();

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

                        var melodyMusic = await melodyMusicService.GetByCodeWithTracksAsync(defaultPublicationCode);

                        if (melodyMusic != null && melodyMusic.Tracks != null && melodyMusic.Tracks.Count > 0)
                        {
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

                            chosenTrack ??= melodyMusic.Tracks[Random.Shared.Next(melodyMusic.Tracks.Count)];

                            var latestSchedule = state.Value.CurrentSchedule;
                            if (latestSchedule == null)
                            {
                                return;
                            }

                            if (latestSchedule.MusicLanguageCode == null &&
                                latestSchedule.MusicPublicationCode == defaultPublicationCode &&
                                latestSchedule.MusicSectionCode == chosenSection?.SectionCode &&
                                latestSchedule.MusicTrackCode == TrackCodeHelper.GetFromTrack(chosenTrack) &&
                                latestSchedule.MusicEnabled)
                            {
                                return;
                            }

                            var clonedSchedule = latestSchedule.DeepClone();
                            clonedSchedule.MusicEnabled = true;
                            clonedSchedule.MusicPublicationCode = defaultPublicationCode;
                            clonedSchedule.MusicPublicationName = defaultPublicationName;
                            clonedSchedule.MusicLanguageCode = null;
                            clonedSchedule.MusicSectionCode = chosenSection?.SectionCode;
                            clonedSchedule.MusicSectionName = chosenSection?.Name;
                            clonedSchedule.MusicTrackCode = TrackCodeHelper.GetFromTrack(chosenTrack);
                            clonedSchedule.MusicRepeat = false;
                            clonedSchedule.MusicTrackName = WebUtility.HtmlDecode(chosenTrack.Title).Replace('\u00A0', ' ');

                            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(clonedSchedule, musicUpdated: true, biblePublicationUpdated: false, shouldSave: false));
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "MusicEnabled: Error loading default music from DB");
                    }
                });
            }
        }

        return true;
    }
}
