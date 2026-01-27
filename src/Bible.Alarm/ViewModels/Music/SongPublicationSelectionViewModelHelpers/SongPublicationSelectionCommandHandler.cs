#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music.SongPublicationSelectionViewModelHelpers;

/// <summary>
/// Handles command execution for SongPublicationSelectionViewModel.
/// </summary>
public sealed class SongPublicationSelectionCommandHandler(
    INavigationService navigationService,
    IState<ApplicationState> state,
    IDispatcher dispatcher,
    IServiceScopeFactory scopeFactory,
    IMediaService mediaService)
{
    public async Task HandleTrackSelectionAsync(
        PublicationListViewItemModel songPublication,
        LanguageListViewItemModel? currentLanguage,
        SongPublicationSelectionDataProvider dataProvider,
        AlarmMusic? current,
        Action<bool> setShowProgress,
        Action<double> setProgressPercent,
        Action<string> setProgressText)
    {
        if (songPublication == null)
        {
            return;
        }

        // Determine if this is a melody music publication (LanguageId == null) or vocal music (LanguageId != null)
        // This is data-driven, not hard-coded
        bool isMelodyMusic = false;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            var publication = await db.BiblePublications
                .AsNoTracking()
                .Where(bp => bp.PublicationCode == songPublication.Code &&
                             bp.Category != null &&
                             bp.Category.CategoryName == "Music")
                .FirstOrDefaultAsync();
            
            isMelodyMusic = publication?.LanguageId == null;
        }

        // For melody music, language code is not needed
        // For vocal music, language code is required
        var languageCode = currentLanguage?.Code ?? string.Empty;
        if (!isMelodyMusic && string.IsNullOrEmpty(languageCode))
        {
            return;
        }

        var currentSchedule = state.Value.CurrentSchedule;
        
        // Show progress immediately on UI thread before any async work
        MainThread.BeginInvokeOnMainThread(() =>
        {
            setShowProgress(true);
            setProgressPercent(0.0);
            setProgressText("0%");
        });
        
        // Give UI thread a chance to render the progress
        await Task.Delay(50);
        
        try
        {
            // Create progress tracker
            var progressTracker = new Bible.Alarm.Common.Helpers.FetchProgressTracker(
                progress => MainThread.BeginInvokeOnMainThread(() => setProgressPercent(progress)),
                text => MainThread.BeginInvokeOnMainThread(() => setProgressText(text)),
                isVisible => MainThread.BeginInvokeOnMainThread(() => setShowProgress(isVisible)));
            
            // Get track based on music type
            int trackNumber;
            string trackName;
            if (isMelodyMusic)
            {
                // For melody music, get tracks directly (no language needed)
                progressTracker.UpdateProgress(0.3);
                var tracks = await mediaService.GetMelodyMusicTracks(songPublication.Code);
                if (tracks == null || tracks.Count == 0)
                {
                    return;
                }
                
                // Use current track if same publication, otherwise random
                if (currentSchedule?.MusicType == MusicType.Music &&
                    currentSchedule.MusicPublicationCode == songPublication.Code &&
                    currentSchedule.MusicTrackNumber.HasValue &&
                    tracks.TryGetValue(currentSchedule.MusicTrackNumber.Value, out var currentTrack))
                {
                    trackNumber = currentSchedule.MusicTrackNumber.Value;
                    trackName = currentTrack.Title;
                }
                else
                {
                    var tracksList = tracks.Values.ToList();
                    var randomTrack = tracksList[Random.Shared.Next(tracksList.Count)];
                    trackNumber = randomTrack.Number;
                    trackName = randomTrack.Title;
                }
            }
            else
            {
                // For vocal music, use existing logic
                progressTracker.UpdateProgress(0.3);
                var result = await dataProvider.GetTrackForSongPublicationAsync(songPublication, languageCode, currentSchedule, progressTracker);
                if (result.TrackNumber == 0)
                {
                    return;
                }
                trackNumber = result.TrackNumber;
                trackName = result.TrackName;
            }

            progressTracker.UpdateProgress(0.7);

            // Check if publication is sectioned - if not, ensure SectionCode is null
            bool isSectioned = Bible.Alarm.Shared.Helpers.PublicationTypeHelper.HasSectionStructure(songPublication.Code);
            string? sectionCode = null;
            string? sectionName = null;
            
            // Only set section code/name if publication is sectioned
            if (isSectioned)
            {
                // For sectioned publications, we need to get the section from the track
                // But since we're selecting a publication and getting a random track,
                // we don't know which section it belongs to yet
                // The cascade handler will handle setting the correct section
                // For now, preserve any existing section if the publication hasn't changed
                if (currentSchedule?.MusicPublicationCode == songPublication.Code && 
                    !string.IsNullOrWhiteSpace(currentSchedule.MusicSectionCode))
                {
                    sectionCode = currentSchedule.MusicSectionCode;
                    sectionName = currentSchedule.MusicSectionName;
                }
            }
            // For non-sectioned publications, explicitly set SectionCode to null to clear it
            else
            {
                sectionCode = null;
                sectionName = null;
            }

            var trackSelectedItem = CreateMusicStateItemForSongPublication(
                songPublication, 
                isMelodyMusic ? string.Empty : languageCode, 
                trackNumber, 
                trackName, 
                isMelodyMusic ? null : currentLanguage, 
                currentSchedule,
                isMelodyMusic ? MusicType.Music : MusicType.VocalMusic,
                sectionCode,
                sectionName);
            dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));
            
            // Wait for cascade to complete by checking state
            progressTracker.UpdateProgress(0.9);
            
            // Wait for state to be updated (cascade effect)
            const int maxWaitAttempts = 30;
            const int delayMs = 200;
            for (int i = 0; i < maxWaitAttempts; i++)
            {
                var currentState = state.Value.CurrentSchedule;
                if (currentState != null && 
                    !string.IsNullOrEmpty(currentState.MusicPublicationCode) &&
                    currentState.MusicTrackNumber.HasValue &&
                    currentState.MusicTrackNumber.Value > 0)
                {
                    // Cascade complete
                    break;
                }
                // Update progress gradually while waiting
                var waitProgress = 0.9 + (i / (double)maxWaitAttempts) * 0.1;
                progressTracker.UpdateProgress(waitProgress);
                await Task.Delay(delayMs);
            }
            
            progressTracker.UpdateProgress(1.0);
            await Task.Delay(200); // Brief delay to show completion
        }
        finally
        {
            setShowProgress(false);
        }
        
        await navigationService.PopModalAsync();
    }


    public async Task HandleLanguageSelectionAsync(
        LanguageListViewItemModel language,
        SongPublicationSelectionDataProvider dataProvider,
        Action<LanguageListViewItemModel?> setCurrentLanguage,
        Action<LanguageListViewItemModel> updateSelectedLanguage,
        Action<bool> setShowProgress,
        Action<double> setProgressPercent,
        Action<string> setProgressText,
        Action<bool> setIsBusy)
    {
        if (language == null)
        {
            return;
        }

        updateSelectedLanguage(language);

        // Show progress immediately on UI thread before any async work
        MainThread.BeginInvokeOnMainThread(() =>
        {
            setIsBusy(true);
            setShowProgress(true);
            setProgressPercent(0.0);
            setProgressText("0%");
        });
        
        // Give UI thread a chance to render the progress
        await Task.Delay(50);

        try
        {
            var currentSchedule = state.Value.CurrentSchedule;
            
            // Create progress tracker
            var progressTracker = new Bible.Alarm.Common.Helpers.FetchProgressTracker(
                progress => MainThread.BeginInvokeOnMainThread(() => setProgressPercent(progress)),
                text => MainThread.BeginInvokeOnMainThread(() => setProgressText(text)),
                isVisible => MainThread.BeginInvokeOnMainThread(() => setShowProgress(isVisible)));
            
            var (publicationCode, trackNumber, trackName, publicationName) = await dataProvider.GetFirstSongPublicationAndTrackForLanguageAsync(language, currentSchedule, progressTracker);
            if (publicationCode == null)
            {
                return;
            }

            var trackSelectedItem = CreateMusicStateItemForLanguage(language, publicationCode, trackNumber, trackName, publicationName, currentSchedule);
            dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));
            
            // Wait for cascade to complete by checking state
            progressTracker.UpdateProgress(0.9);
            
            // Wait for state to be updated (cascade effect)
            const int maxWaitAttempts = 30;
            const int delayMs = 200;
            for (int i = 0; i < maxWaitAttempts; i++)
            {
                var currentState = state.Value.CurrentSchedule;
                if (currentState != null && 
                    !string.IsNullOrEmpty(currentState.MusicPublicationCode) &&
                    currentState.MusicTrackNumber.HasValue &&
                    currentState.MusicTrackNumber.Value > 0)
                {
                    // Cascade complete
                    break;
                }
                // Update progress gradually while waiting
                var waitProgress = 0.9 + (i / (double)maxWaitAttempts) * 0.1;
                progressTracker.UpdateProgress(waitProgress);
                await Task.Delay(delayMs);
            }
            
            progressTracker.UpdateProgress(1.0);
            await Task.Delay(200); // Brief delay to show completion
        }
        finally
        {
            setIsBusy(false);
            setShowProgress(false);
        }
        
        await navigationService.PopModalAsync();
    }

    private MusicStateItem CreateMusicStateItemForSongPublication(
        PublicationListViewItemModel songPublication,
        string languageCode,
        int trackNumber,
        string trackName,
        LanguageListViewItemModel? currentLanguage,
        ScheduleStateItem? currentSchedule,
        MusicType musicType,
        string? sectionCode = null,
        string? sectionName = null)
    {
        return new MusicStateItem
        {
            Repeat = currentSchedule?.MusicRepeat ?? false,
            MusicType = musicType,
            LanguageCode = languageCode,
            PublicationCode = songPublication.Code,
            SectionCode = sectionCode, // Explicitly set SectionCode (null for non-sectioned publications)
            TrackNumber = trackNumber,
            LanguageName = currentLanguage?.Name,
            LanguageDirection = currentLanguage?.Direction,
            PublicationName = songPublication.Name,
            SectionName = sectionName, // Explicitly set SectionName (null for non-sectioned publications)
            TrackName = trackName
        };
    }

    private MusicStateItem CreateMusicStateItemForLanguage(
        LanguageListViewItemModel language,
        string publicationCode,
        int trackNumber,
        string trackName,
        string publicationName,
        ScheduleStateItem? currentSchedule)
    {
        return new MusicStateItem
        {
            Repeat = currentSchedule?.MusicRepeat ?? false,
            MusicType = MusicType.VocalMusic,
            LanguageCode = language.Code,
            PublicationCode = publicationCode,
            TrackNumber = trackNumber,
            LanguageName = language.Name,
            LanguageDirection = language.Direction,
            PublicationName = publicationName,
            TrackName = trackName
        };
    }
}

