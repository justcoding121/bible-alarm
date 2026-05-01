#nullable enable
using System.Collections.Generic;
using System.Net.Http;
using Bible.Alarm.Common;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Network.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;

/// <summary>
/// Handles command execution for MusicPublicationSelectionViewModel.
/// </summary>
public sealed class MusicPublicationSelectionCommandHandler(
    INavigationService navigationService,
    IState<ApplicationState> state,
    IDispatcher dispatcher,
    IMediaService mediaService,
    ILanguageNameService languageNameService)
{
    public async Task HandleTrackSelectionAsync(HandleMusicPublicationTrackSelectionArgs args)
    {
        var songPublication = args.SongPublication;
        var currentLanguage = args.CurrentLanguage;
        var dataProvider = args.DataProvider;
        var setShowProgress = args.Progress.SetShowProgress;

        if (songPublication == null)
        {
            return;
        }

        // Progress will be set by ModalOverlayFetchProgressReporter only when a fetch actually happens.
        // No DB probing: the tapped row already knows if it has LanguageId or not.
        // Publications without language FK (LanguageId == null) are melody/instrumental.
        var isMelodyMusic = songPublication.IsPublicationWithoutLanguage;

        // For melody music, language code is not needed.
        // For vocal music, use selected language or the publication's language (e.g. when opening from melody mode we show E + melody list; tapping osg uses publication language "E").
        var languageCode = currentLanguage?.Code ?? songPublication.PublicationLanguageCode ?? string.Empty;
        if (!isMelodyMusic && string.IsNullOrEmpty(languageCode))
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                setShowProgress(false);
            });
            return;
        }

        var currentSchedule = state.Value.CurrentSchedule;

        // Do NOT check internet upfront - melody/instrumental and English vocal are often local.
        // If a fetch is needed and network is down, the fetch will throw and we catch below.

        try
        {
            var progressReporter = new Bible.Alarm.Common.Helpers.ListItemFetchProgressReporter("MusicPublication", songPublication.Code);

            string trackCode;
            string trackName;
            string? sectionCode = null;
            string? sectionName = null;
            if (isMelodyMusic)
            {
                var melody = await TryResolveMelodyMusicSelectionAsync(songPublication, currentSchedule);
                if (melody == null)
                {
                    return;
                }

                trackCode = melody.TrackCode;
                trackName = melody.TrackName;
                sectionCode = melody.SectionCode;
                sectionName = melody.SectionName;
            }
            else
            {
                progressReporter.UpdateProgress(0.3);
                var result = await dataProvider.GetTrackForSongPublicationAsync(songPublication, languageCode, currentSchedule, progressReporter);
                if (string.IsNullOrWhiteSpace(result.TrackCode))
                {
                    await MainThread.InvokeOnMainThreadAsync(() => songPublication.DownloadProgress = 0.0);
                    return;
                }

                trackCode = result.TrackCode;
                trackName = result.TrackName;
            }

            progressReporter.UpdateProgress(0.7);

            var (resolvedLanguageName, resolvedLanguageDirection) =
                await ResolveVocalLanguageDisplayOverridesAsync(isMelodyMusic, languageCode, currentLanguage);

            // For melody music, LanguageCode is null (stored as null in DB)
            // For vocal music, LanguageCode is the selected language
            var trackSelectedItem = CreateMusicStateItemForSongPublication(
                new SongPublicationMusicStateCore(
                    songPublication,
                    isMelodyMusic ? null : languageCode,
                    trackCode,
                    trackName,
                    isMelodyMusic ? null : currentLanguage,
                    currentSchedule),
                new SongPublicationMusicStateExtras(
                    sectionCode,
                    sectionName,
                    resolvedLanguageName,
                    resolvedLanguageDirection));
            dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));

            await WaitForMusicPublicationCascadeAsync(songPublication.Code);
        }
        catch (Exception ex) when (ModalScrollHelper.IsFetchFailure(ex))
        {
            Log.Warning(ex, AppConstants.Logging.MusicPublicationSelectionCommandHandlerDiagnosticsLog.NetworkErrorPublicationSelectionPublicationCode, songPublication.Code);
            await MainThread.InvokeOnMainThreadAsync(() => songPublication.DownloadProgress = 0.0);
            var toastService = ServiceProviderManager.GetService<IToastService>();
            await Task.Delay(500);
            await navigationService.PopModalAsync();
            await toastService.ShowMessage(ModalScrollHelper.GetFetchErrorMessage(ex));
            return;
        }
        catch (Exception ex)
        {
            Log.Error(ex, AppConstants.Logging.MusicPublicationSelectionCommandHandlerDiagnosticsLog.ErrorPublicationSelectionPublicationCode, songPublication.Code);
            await MainThread.InvokeOnMainThreadAsync(() => songPublication.DownloadProgress = 0.0);
            var toastService = ServiceProviderManager.GetService<IToastService>();
            await Task.Delay(500);
            await navigationService.PopModalAsync();
            await toastService.ShowMessage(ModalScrollHelper.GetFetchErrorMessage(ex));
            return;
        }
        
        await navigationService.PopModalAsync();
    }

    private sealed record MelodyMusicSelectionResult(string TrackCode, string TrackName, string? SectionCode, string? SectionName);

    private async Task<MelodyMusicSelectionResult?> TryResolveMelodyMusicSelectionAsync(
        PublicationListViewItemModel songPublication,
        ScheduleStateItem? currentSchedule)
    {
        if (PublicationTypeHelper.HasSectionStructure(songPublication.Code))
        {
            return await TryResolveSectionedMelodyMusicAsync(songPublication, currentSchedule);
        }

        return await TryResolveFlatMelodyMusicAsync(songPublication, currentSchedule);
    }

    private async Task<MelodyMusicSelectionResult?> TryResolveSectionedMelodyMusicAsync(
        PublicationListViewItemModel songPublication,
        ScheduleStateItem? currentSchedule)
    {
        var sections = await mediaService.GetSectionsForPublicationWithoutLanguage(songPublication.Code);
        if (sections == null || sections.Count == 0)
        {
            await MainThread.InvokeOnMainThreadAsync(() => songPublication.DownloadProgress = 0.0);
            return null;
        }

        using var sectionsEnumerator = sections.GetEnumerator();
        _ = sectionsEnumerator.MoveNext();
        var selectedSection = sectionsEnumerator.Current;
        var isCurrentMelody = IsCurrentScheduleMelodyPublication(currentSchedule);
        if (isCurrentMelody &&
            currentSchedule?.MusicPublicationCode == songPublication.Code &&
            !string.IsNullOrWhiteSpace(currentSchedule.MusicSectionCode))
        {
            var match = sections.FirstOrDefault(kvp =>
                kvp.Value != null &&
                SectionCodeHelper.CodeEquals(kvp.Value.SectionCode, currentSchedule.MusicSectionCode));
            if (!EqualityComparer<KeyValuePair<string, BiblePublicationSection>>.Default.Equals(match, default))
            {
                selectedSection = match;
            }
        }

        var selectedSectionCode = selectedSection.Value.SectionCode;
        var resolvedSectionName = selectedSection.Value.Name;

        var sectionTracks = await mediaService.GetBiblePublicationTracks(string.Empty, songPublication.Code, selectedSectionCode);
        if (sectionTracks == null || sectionTracks.Count == 0)
        {
            await MainThread.InvokeOnMainThreadAsync(() => songPublication.DownloadProgress = 0.0);
            return null;
        }

        string trackCode;
        string trackName;
        if (isCurrentMelody &&
            currentSchedule?.MusicPublicationCode == songPublication.Code &&
            SectionCodeHelper.CodeEquals(currentSchedule.MusicSectionCode, selectedSectionCode) &&
            !string.IsNullOrWhiteSpace(currentSchedule.MusicTrackCode) &&
            sectionTracks.TryGetValue(currentSchedule.MusicTrackCode, out var existingTrack))
        {
            trackCode = TrackCodeHelper.GetFromTrack(existingTrack);
            trackName = existingTrack.Title ?? string.Empty;
        }
        else
        {
            var tracksList = sectionTracks.Values.ToList();
            var randomTrack = tracksList[Random.Shared.Next(tracksList.Count)];
            trackCode = TrackCodeHelper.GetFromTrack(randomTrack);
            trackName = randomTrack.Title ?? string.Empty;
        }

        return new MelodyMusicSelectionResult(trackCode, trackName, selectedSectionCode, resolvedSectionName);
    }

    private async Task<MelodyMusicSelectionResult?> TryResolveFlatMelodyMusicAsync(
        PublicationListViewItemModel songPublication,
        ScheduleStateItem? currentSchedule)
    {
        var tracks = await mediaService.GetMelodyMusicTracks(songPublication.Code);
        if (tracks == null || tracks.Count == 0)
        {
            await MainThread.InvokeOnMainThreadAsync(() => songPublication.DownloadProgress = 0.0);
            return null;
        }

        var isCurrentMelodyFlat = IsCurrentScheduleMelodyPublication(currentSchedule);
        string trackCode;
        string trackName;
        if (isCurrentMelodyFlat &&
            currentSchedule?.MusicPublicationCode == songPublication.Code &&
            !string.IsNullOrWhiteSpace(currentSchedule.MusicTrackCode) &&
            MusicTrackLookupHelper.TryGetByCode(tracks, currentSchedule.MusicTrackCode, out var flatPair))
        {
            trackCode = TrackCodeHelper.GetFromTrack(flatPair.Track);
            trackName = flatPair.Track.Title;
        }
        else
        {
            var tracksList = tracks.Values.ToList();
            var randomTrack = tracksList[Random.Shared.Next(tracksList.Count)];
            trackCode = TrackCodeHelper.GetFromTrack(randomTrack);
            trackName = randomTrack.Title;
        }

        return new MelodyMusicSelectionResult(trackCode, trackName, null, null);
    }

    private static bool IsCurrentScheduleMelodyPublication(ScheduleStateItem? currentSchedule)
    {
        return !string.IsNullOrWhiteSpace(currentSchedule?.MusicPublicationCode)
               && JwSourceHelper.MelodyMusicPublicationCodes.Contains(currentSchedule.MusicPublicationCode);
    }

    private async Task<(string? Name, string? Direction)> ResolveVocalLanguageDisplayOverridesAsync(
        bool isMelodyMusic,
        string languageCode,
        LanguageListViewItemModel? currentLanguage)
    {
        if (isMelodyMusic || string.IsNullOrEmpty(languageCode) || currentLanguage != null)
        {
            return (null, null);
        }

        var languages = await mediaService.GetVocalMusicLanguages();
        if (!languages.TryGetValue(languageCode, out var lang))
        {
            return (null, null);
        }

        var resolvedLanguageName = languageNameService.GetNameCached(lang.Id) ?? languageCode;
        return (resolvedLanguageName, lang.Direction);
    }

    private async Task WaitForMusicPublicationCascadeAsync(string publicationCode)
    {
        const int maxWaitAttempts = 30;
        const int delayMs = 200;
        for (var i = 0; i < maxWaitAttempts; i++)
        {
            var currentState = state.Value.CurrentSchedule;
            if (currentState != null &&
                currentState.MusicPublicationCode == publicationCode &&
                !string.IsNullOrWhiteSpace(currentState.MusicTrackCode))
            {
                break;
            }

            await Task.Delay(delayMs);
        }
    }


    public async Task HandleLanguageSelectionAsync(
        LanguageListViewItemModel language,
        MusicPublicationSelectionDataProvider dataProvider,
        HandleMusicLanguageSelectionUiCallbacks ui)
    {
        var updateSelectedLanguage = ui.UpdateSelectedLanguage;

        if (language == null)
        {
            return;
        }

        // Do NOT check internet upfront - English is pre-packaged and needs no fetch.
        // If a fetch is needed and network is down, the selector will throw and we catch below.

        // Track if progress was set (fetch happened) - only show completion if fetch occurred
        bool fetchOccurred = false;
        try
        {
            var currentSchedule = state.Value.CurrentSchedule;

            var progressReporter = new Bible.Alarm.Common.Helpers.ListItemFetchProgressReporter(
                "MusicLanguage", language.Code, () => fetchOccurred = true);

            (string? publicationCode, string trackCode, string trackName, string publicationName) result;
            try
            {
                result = await dataProvider.GetFirstSongPublicationAndTrackForLanguageAsync(language, currentSchedule, progressReporter);
            }
            catch (Exception ex) when (ModalScrollHelper.IsFetchFailure(ex))
            {
                Log.Warning(ex, AppConstants.Logging.MusicPublicationSelectionCommandHandlerDiagnosticsLog.NetworkErrorLanguageSelectionLanguageCode, language.Code);
                await MainThread.InvokeOnMainThreadAsync(() => language.DownloadProgress = 0.0);
                var toastService = ServiceProviderManager.GetService<IToastService>();
                await Task.Delay(500);
                await navigationService.PopModalAsync();
                await toastService.ShowMessage(ModalScrollHelper.GetFetchErrorMessage(ex));
                return;
            }
            var (publicationCode, trackCode, trackName, publicationName) = result;
            if (publicationCode == null || string.IsNullOrWhiteSpace(trackCode))
            {
                await MainThread.InvokeOnMainThreadAsync(() => language.DownloadProgress = 0.0);
                return;
            }

            // Only update the UI selection after we know we have valid content.
            // If fetching/cataloging fails, we must keep the previous language selection (and schedule state) unchanged.
            updateSelectedLanguage(language);

            var trackSelectedItem = CreateMusicStateItemForLanguage(language, publicationCode, trackCode, trackName, publicationName, currentSchedule);
            dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));

            const int maxWaitAttempts = 30;
            const int delayMs = 200;
            for (int i = 0; i < maxWaitAttempts; i++)
            {
                var currentState = state.Value.CurrentSchedule;
                if (currentState?.MusicLanguageCode == language.Code &&
                    !string.IsNullOrEmpty(currentState.MusicPublicationCode) &&
                    !string.IsNullOrWhiteSpace(currentState.MusicTrackCode))
                {
                    break;
                }
                await Task.Delay(delayMs);
            }
        }
        finally
        {
            if (fetchOccurred)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    language.DownloadProgress = 1.0;
                });
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    language.DownloadProgress = -1.0;
                });
            }
        }

        await navigationService.PopModalAsync();
    }

    private sealed record SongPublicationMusicStateCore(
        PublicationListViewItemModel SongPublication,
        string? LanguageCode,
        string TrackCode,
        string TrackName,
        LanguageListViewItemModel? CurrentLanguage,
        ScheduleStateItem? CurrentSchedule);

    private sealed record SongPublicationMusicStateExtras(
        string? SectionCode,
        string? SectionName,
        string? LanguageNameOverride,
        string? LanguageDirectionOverride);

    private static MusicStateItem CreateMusicStateItemForSongPublication(
        SongPublicationMusicStateCore core,
        SongPublicationMusicStateExtras extras)
    {
        string? languageName = extras.LanguageNameOverride;
        if (core.CurrentLanguage != null)
        {
            languageName = core.CurrentLanguage.Name;
        }

        var languageDirection = core.CurrentLanguage?.Direction ?? extras.LanguageDirectionOverride;
        return new MusicStateItem
        {
            Repeat = core.CurrentSchedule?.MusicRepeat ?? false,
            LanguageCode = core.LanguageCode,
            PublicationCode = core.SongPublication.Code,
            SectionCode = extras.SectionCode,
            TrackCode = core.TrackCode,
            LanguageName = languageName,
            LanguageDirection = languageDirection,
            PublicationName = core.SongPublication.Name,
            SectionName = extras.SectionName,
            TrackName = core.TrackName
        };
    }

    private static MusicStateItem CreateMusicStateItemForLanguage(
        LanguageListViewItemModel language,
        string publicationCode,
        string trackCode,
        string trackName,
        string publicationName,
        ScheduleStateItem? currentSchedule)
    {
        return new MusicStateItem
        {
            Repeat = currentSchedule?.MusicRepeat ?? false,
            LanguageCode = language.Code,
            PublicationCode = publicationCode,
            SectionCode = null,
            TrackCode = trackCode,
            LanguageName = language.Name,
            LanguageDirection = language.Direction,
            PublicationName = publicationName,
            SectionName = null,
            TrackName = trackName
        };
    }
}

