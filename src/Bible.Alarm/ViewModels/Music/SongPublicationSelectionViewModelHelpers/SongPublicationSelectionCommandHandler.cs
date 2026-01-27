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
        AlarmMusic? current)
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
        
        // Get track based on music type
        int trackNumber;
        string trackName;
        if (isMelodyMusic)
        {
            // For melody music, get tracks directly (no language needed)
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
            var result = await dataProvider.GetTrackForSongPublicationAsync(songPublication, languageCode, currentSchedule);
            if (result.TrackNumber == 0)
            {
                return;
            }
            trackNumber = result.TrackNumber;
            trackName = result.TrackName;
        }

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
        await navigationService.PopModalAsync();
    }


    public async Task HandleLanguageSelectionAsync(
        LanguageListViewItemModel language,
        SongPublicationSelectionDataProvider dataProvider,
        Action<LanguageListViewItemModel?> setCurrentLanguage,
        Action<LanguageListViewItemModel> updateSelectedLanguage)
    {
        if (language == null)
        {
            return;
        }

        updateSelectedLanguage(language);

        var currentSchedule = state.Value.CurrentSchedule;
        var (publicationCode, trackNumber, trackName, publicationName) = await dataProvider.GetFirstSongPublicationAndTrackForLanguageAsync(language, currentSchedule);
        if (publicationCode == null)
        {
            return;
        }

        var trackSelectedItem = CreateMusicStateItemForLanguage(language, publicationCode, trackNumber, trackName, publicationName, currentSchedule);
        dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));
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

