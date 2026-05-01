#nullable enable
using System.Net.Http;
using Bible.Alarm.Common;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Network.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
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
        var setProgressPercent = args.Progress.SetProgressPercent;
        var setProgressText = args.Progress.SetProgressText;

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
            
            // Get track based on music type
            string trackCode;
            string trackName;
            string? sectionCode = null;
            string? sectionName = null;
            if (isMelodyMusic)
            {
                // Melody publications are usually flat, but some (e.g. "iam") are sectioned (discs).
                // Progress will be set by fetch methods if a fetch is needed (DB queries don't set progress).
                // For sectioned publications we MUST pick section + track so Schedule always has section data.
                var isSectionedMelody = Bible.Alarm.Shared.Helpers.PublicationTypeHelper.HasSectionStructure(songPublication.Code);
                if (isSectionedMelody)
                {
                    var sections = await mediaService.GetSectionsForPublicationWithoutLanguage(songPublication.Code);
                    if (sections == null || sections.Count == 0)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() => songPublication.DownloadProgress = 0.0);
                        return;
                    }

                    // Prefer preserving current section if same publication; otherwise take first section.
                    using var sectionsEnumerator = sections.GetEnumerator();
                    _ = sectionsEnumerator.MoveNext();
                    var selectedSection = sectionsEnumerator.Current;
                    var isCurrentMelody = !string.IsNullOrWhiteSpace(currentSchedule?.MusicPublicationCode)
                        && Bible.Alarm.Shared.Helpers.JwSourceHelper.MelodyMusicPublicationCodes.Contains(currentSchedule.MusicPublicationCode);
                    if (isCurrentMelody &&
                        currentSchedule?.MusicPublicationCode == songPublication.Code &&
                        !string.IsNullOrWhiteSpace(currentSchedule.MusicSectionCode))
                    {
                        var match = sections.FirstOrDefault(kvp =>
                            kvp.Value != null &&
                            Bible.Alarm.Shared.Helpers.SectionCodeHelper.CodeEquals(kvp.Value.SectionCode, currentSchedule.MusicSectionCode));
                        if (!EqualityComparer<KeyValuePair<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>>.Default.Equals(match, default))
                        {
                            selectedSection = match;
                        }
                    }

                    var selectedSectionCode = selectedSection.Value.SectionCode;
                    sectionName = selectedSection.Value.Name;
                    sectionCode = selectedSectionCode;

                    var sectionTracks = await mediaService.GetBiblePublicationTracks(string.Empty, songPublication.Code, selectedSectionCode);
                    if (sectionTracks == null || sectionTracks.Count == 0)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() => songPublication.DownloadProgress = 0.0);
                        return;
                    }

                    // Preserve current track if it's valid for this section; otherwise pick random track within the section.
                    if (isCurrentMelody &&
                        currentSchedule?.MusicPublicationCode == songPublication.Code &&
                        Bible.Alarm.Shared.Helpers.SectionCodeHelper.CodeEquals(currentSchedule.MusicSectionCode, selectedSectionCode) &&
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
                }
                else
                {
                    // Flat melody music
                    var tracks = await mediaService.GetMelodyMusicTracks(songPublication.Code);
                    if (tracks == null || tracks.Count == 0)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() => songPublication.DownloadProgress = 0.0);
                        return;
                    }

                    var isCurrentMelodyFlat = !string.IsNullOrWhiteSpace(currentSchedule?.MusicPublicationCode)
                        && Bible.Alarm.Shared.Helpers.JwSourceHelper.MelodyMusicPublicationCodes.Contains(currentSchedule.MusicPublicationCode);
                    if (isCurrentMelodyFlat &&
                        currentSchedule?.MusicPublicationCode == songPublication.Code &&
                        !string.IsNullOrWhiteSpace(currentSchedule.MusicTrackCode) &&
                        Bible.Alarm.Shared.Helpers.MusicTrackLookupHelper.TryGetByCode(tracks, currentSchedule.MusicTrackCode, out var flatPair))
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
                }
            }
            else
            {
                // For vocal music, use existing logic
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

            // For vocal music when currentLanguage is null (e.g. tapped osg from merged list in melody mode),
            // resolve language name/direction so the reducer can set them and the UI shows "English" not "E".
            string? resolvedLanguageName = null;
            string? resolvedLanguageDirection = null;
            if (!isMelodyMusic && !string.IsNullOrEmpty(languageCode) && currentLanguage == null)
            {
                var languages = await mediaService.GetVocalMusicLanguages();
                if (languages.TryGetValue(languageCode, out var lang))
                {
                    resolvedLanguageName = languageNameService.GetNameCached(lang.Id) ?? languageCode;
                    resolvedLanguageDirection = lang.Direction;
                }
            }

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
            
            // Wait for cascade to complete - check for the SPECIFIC publication we just dispatched
            const int maxWaitAttempts = 30;
            const int delayMs = 200;
            for (int i = 0; i < maxWaitAttempts; i++)
            {
                var currentState = state.Value.CurrentSchedule;
                if (currentState != null && 
                    currentState.MusicPublicationCode == songPublication.Code &&
                    !string.IsNullOrWhiteSpace(currentState.MusicTrackCode))
                {
                    break;
                }
                await Task.Delay(delayMs);
            }
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


    public async Task HandleLanguageSelectionAsync(
        LanguageListViewItemModel language,
        MusicPublicationSelectionDataProvider dataProvider,
        HandleMusicLanguageSelectionUiCallbacks ui)
    {
        var setCurrentLanguage = ui.SetCurrentLanguage;
        var updateSelectedLanguage = ui.UpdateSelectedLanguage;
        var setShowProgress = ui.SetShowProgress;
        var setProgressPercent = ui.SetProgressPercent;
        var setProgressText = ui.SetProgressText;
        var setIsBusy = ui.SetIsBusy;

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

