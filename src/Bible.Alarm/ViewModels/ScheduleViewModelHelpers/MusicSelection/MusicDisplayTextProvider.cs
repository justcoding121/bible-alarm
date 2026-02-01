#nullable enable
using System.Linq;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Fluxor;
using Microsoft.Maui;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers.MusicSelection;

public sealed class MusicDisplayTextProvider
{
    private readonly IState<ApplicationState> state;
    private readonly IMediaService mediaService;

    private string? cachedSongPublicationName;
    private string? lastMusicPublicationCode;
    private string? cachedTrackName;
    private int? lastMusicTrackNumber;
    private string? lastTrackPublicationCode;
    private string? lastTrackLanguageCode;
    private MusicType? lastTrackMusicType;

    public MusicDisplayTextProvider(IState<ApplicationState> state, IMediaService mediaService)
    {
        this.state = state;
        this.mediaService = mediaService;
    }

    public void ClearCaches()
    {
        cachedSongPublicationName = null;
        cachedTrackName = null;
        lastMusicPublicationCode = null;
        lastMusicTrackNumber = null;
        lastTrackPublicationCode = null;
        lastTrackLanguageCode = null;
        lastTrackMusicType = null;
    }

    public void ClearSongPublicationCache()
    {
        cachedSongPublicationName = null;
        lastMusicPublicationCode = null;
    }

    public void ClearTrackCache()
    {
        cachedTrackName = null;
        lastMusicTrackNumber = null;
        lastTrackPublicationCode = null;
        lastTrackLanguageCode = null;
        lastTrackMusicType = null;
    }

    public void UpdateTrackCache(string? trackName, int? trackNumber, string? publicationCode, string? languageCode, MusicType? musicType)
    {
        cachedTrackName = trackName;
        lastMusicTrackNumber = trackNumber;
        lastTrackPublicationCode = publicationCode;
        lastTrackLanguageCode = languageCode;
        lastTrackMusicType = musicType;
    }

    public void UpdateSongPublicationCache(string? songPublicationName, string? publicationCode)
    {
        cachedSongPublicationName = songPublicationName;
        lastMusicPublicationCode = publicationCode;
    }

    public string GetMusicTypeDisplayText()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null || !currentSchedule.MusicType.HasValue)
        {
            return "Instrumental Music";
        }

        return currentSchedule.MusicType.Value switch
        {
            MusicType.Music => "Instrumental Music",
            MusicType.VocalMusic => "Vocal Music",
            _ => "Instrumental Music"
        };
    }

    public bool GetIsSongPublicationVisible()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        return currentSchedule != null && currentSchedule.MusicType.HasValue;
    }

    public bool GetIsMusicLanguageVisible()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null || !currentSchedule.MusicType.HasValue)
        {
            return false;
        }

        return currentSchedule.MusicType.Value == MusicType.VocalMusic;
    }

    public string GetMusicLanguageDisplayText()
    {
        if (!GetIsMusicLanguageVisible())
        {
            return string.Empty;
        }

        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null || string.IsNullOrWhiteSpace(currentSchedule.MusicLanguageName))
        {
            return string.Empty;
        }

        return currentSchedule.MusicLanguageName;
    }

    public string GetSongPublicationDisplayText()
    {
        if (!GetIsSongPublicationVisible())
        {
            return string.Empty;
        }

        var currentSchedule = state.Value.CurrentSchedule;

        // First, check if MusicPublicationName is already available in state (populated during bootstrap or from effect)
        if (currentSchedule != null && !string.IsNullOrWhiteSpace(currentSchedule.MusicPublicationName))
        {
            // Update cache if it's different or empty
            if (string.IsNullOrEmpty(cachedSongPublicationName) || cachedSongPublicationName != currentSchedule.MusicPublicationName)
            {
                cachedSongPublicationName = currentSchedule.MusicPublicationName;
                lastMusicPublicationCode = currentSchedule.MusicPublicationCode;
            }
            return currentSchedule.MusicPublicationName;
        }

        // Return cached value if available
        if (!string.IsNullOrEmpty(cachedSongPublicationName))
        {
            return cachedSongPublicationName;
        }

        // Return empty if not loaded yet
        return string.Empty;
    }

    public async Task<string> GetSongPublicationDisplayTextAsync()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null ||
            !currentSchedule.MusicType.HasValue ||
            currentSchedule.MusicType.Value != MusicType.VocalMusic ||
            string.IsNullOrWhiteSpace(currentSchedule.MusicPublicationCode) ||
            string.IsNullOrWhiteSpace(currentSchedule.MusicLanguageCode))
        {
            return string.Empty;
        }

        // For VocalMusic, we also need language code
        if (currentSchedule.MusicType.Value == MusicType.VocalMusic &&
            string.IsNullOrWhiteSpace(currentSchedule.MusicLanguageCode))
        {
            return string.Empty;
        }

        // First check if publication name is already in state (populated during bootstrap or from effect)
        if (!string.IsNullOrWhiteSpace(currentSchedule.MusicPublicationName))
        {
            cachedSongPublicationName = currentSchedule.MusicPublicationName;
            lastMusicPublicationCode = currentSchedule.MusicPublicationCode;
            return currentSchedule.MusicPublicationName;
        }

        // Return cached value if publication code hasn't changed
        if (!string.IsNullOrEmpty(cachedSongPublicationName) &&
            lastMusicPublicationCode == currentSchedule.MusicPublicationCode)
        {
            return cachedSongPublicationName;
        }

        return string.Empty;
    }

    public string GetTrackDisplayText()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null ||
            !currentSchedule.MusicType.HasValue ||
            !currentSchedule.MusicTrackNumber.HasValue ||
            currentSchedule.MusicTrackNumber.Value <= 0)
        {
            return string.Empty;
        }

        // First, check if MusicTrackName is already available in state (populated during bootstrap)
        if (!string.IsNullOrWhiteSpace(currentSchedule.MusicTrackName))
        {
            // Update cache if it's different or empty
            if (string.IsNullOrEmpty(cachedTrackName) || cachedTrackName != currentSchedule.MusicTrackName)
            {
                cachedTrackName = currentSchedule.MusicTrackName;
                lastMusicTrackNumber = currentSchedule.MusicTrackNumber;
                lastTrackPublicationCode = currentSchedule.MusicPublicationCode;
                lastTrackLanguageCode = currentSchedule.MusicLanguageCode;
                lastTrackMusicType = currentSchedule.MusicType.Value;
            }
            return currentSchedule.MusicTrackName;
        }

        // Return cached value if available
        if (!string.IsNullOrEmpty(cachedTrackName))
        {
            return cachedTrackName;
        }

        // Return empty if not loaded yet (will be loaded asynchronously)
        return string.Empty;
    }

    public async Task<string> GetTrackDisplayTextAsync()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null ||
            !currentSchedule.MusicType.HasValue ||
            !currentSchedule.MusicTrackNumber.HasValue ||
            currentSchedule.MusicTrackNumber.Value <= 0)
        {
            return string.Empty;
        }

        // First check if track name is already in state (populated during bootstrap)
        if (!string.IsNullOrWhiteSpace(currentSchedule.MusicTrackName))
        {
            cachedTrackName = currentSchedule.MusicTrackName;
            lastMusicTrackNumber = currentSchedule.MusicTrackNumber;
            lastTrackPublicationCode = currentSchedule.MusicPublicationCode;
            lastTrackLanguageCode = currentSchedule.MusicLanguageCode;
            lastTrackMusicType = currentSchedule.MusicType.Value;
            return currentSchedule.MusicTrackName;
        }

        // Return cached value if nothing changed
        if (!string.IsNullOrEmpty(cachedTrackName) &&
            lastMusicTrackNumber == currentSchedule.MusicTrackNumber.Value &&
            lastTrackPublicationCode == currentSchedule.MusicPublicationCode &&
            lastTrackLanguageCode == currentSchedule.MusicLanguageCode &&
            lastTrackMusicType == currentSchedule.MusicType.Value)
        {
            return cachedTrackName;
        }

        return string.Empty;
    }

    public bool GetIsRepeatEnabled()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        return currentSchedule?.MusicRepeat ?? false;
    }

    public bool GetHasTrackSelected()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        return currentSchedule != null &&
               currentSchedule.MusicType.HasValue &&
               currentSchedule.MusicTrackNumber.HasValue &&
               currentSchedule.MusicTrackNumber.Value > 0;
    }

    public FlowDirection GetFlowDirection()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        var direction = currentSchedule?.MusicLanguageDirection ?? "ltr";
        return string.Equals(direction, "rtl", StringComparison.OrdinalIgnoreCase)
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }

    /// <summary>
    /// Determines if the music section row should be visible.
    /// Section row is visible when:
    /// - Music type is set
    /// - Publication code is set
    /// - For VocalMusic: language code is set
    /// - Publication has sections (checked via PublicationTypeHelper.HasSectionStructure)
    /// 
    /// IMPORTANT: We only check PublicationTypeHelper.HasSectionStructure, NOT MusicSectionCode/MusicSectionName.
    /// This ensures that when switching from a sectioned publication to a non-sectioned one, the section row
    /// is hidden even if MusicSectionCode/MusicSectionName is still set (cascade handler should clear these,
    /// but this provides a safety check).
    /// </summary>
    public bool GetIsMusicSectionVisible()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null || !currentSchedule.MusicType.HasValue || string.IsNullOrEmpty(currentSchedule.MusicPublicationCode))
        {
            return false;
        }

        // For VocalMusic, we need language code
        if (currentSchedule.MusicType.Value == MusicType.VocalMusic && string.IsNullOrEmpty(currentSchedule.MusicLanguageCode))
        {
            return false;
        }

        // Only show section row if the publication actually has sections
        // This ensures section row is hidden for non-sectioned publications (e.g., "Sing out Joyfully")
        // even if MusicSectionCode/MusicSectionName is still set from a previous sectioned publication
        return PublicationTypeHelper.HasSectionStructure(currentSchedule.MusicPublicationCode);
    }

    /// <summary>
    /// Gets the display text for the music section.
    /// Returns the section name if available, otherwise placeholder text or empty string.
    /// Follows the same pattern as Bible container: shows placeholder when section is visible but not selected.
    /// </summary>
    public string GetMusicSectionDisplayText()
    {
        var currentSchedule = state.Value.CurrentSchedule;

        // Read from CurrentSchedule for section name (populated during bootstrap/effects)
        if (currentSchedule != null && !string.IsNullOrWhiteSpace(currentSchedule.MusicSectionName))
        {
            return currentSchedule.MusicSectionName;
        }

        return string.Empty;
    }

    /// <summary>
    /// Checks if there are multiple languages available for the current music type (VocalMusic only).
    /// Returns true if there are 2 or more languages, false if only 1 or 0.
    /// </summary>
    public async Task<bool> GetIsMusicLanguageSelectableAsync()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null || currentSchedule.MusicType != MusicType.VocalMusic)
        {
            return false; // Only vocal music has languages
        }

        try
        {
            var languages = await mediaService.GetBiblePublicationLanguages("Music");
            return languages.Count > 1;
        }
        catch
        {
            return false; // On error, default to not selectable
        }
    }

    /// <summary>
    /// Checks if there are multiple publications available for the current music type and language.
    /// Returns true if there are 2 or more publications, false if only 1 or 0.
    /// </summary>
    public async Task<bool> GetIsSongPublicationSelectableAsync()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null || !currentSchedule.MusicType.HasValue)
        {
            return false;
        }

        var musicType = currentSchedule.MusicType.Value;
        
        try
        {
            if (musicType == MusicType.Music)
            {
                // For Instrumental Music, only count downloaded publications without language (LanguageId == null)
                var allPublications = await mediaService.GetBiblePublications(string.Empty, "Music", downloadAll: false);
                var instrumentalPublications = allPublications.Values
                    .Where(p => p.LanguageId == null && p.Id > 0) // Only downloaded publications (Id > 0)
                    .ToList();
                return instrumentalPublications.Count > 1;
            }
            else
            {
                // For Vocal Music, only count downloaded publications for the selected language
                var languageCode = currentSchedule.MusicLanguageCode ?? string.Empty;
                var publications = await mediaService.GetBiblePublications(languageCode, "Music", downloadAll: false);
                // Only count downloaded publications (Id > 0), not placeholders (Id == 0)
                var downloadedCount = publications.Values.Count(p => p.Id > 0);
                return downloadedCount > 1;
            }
        }
        catch
        {
            return false; // On error, default to not selectable
        }
    }

    /// <summary>
    /// Checks if there are multiple sections available for the current music publication.
    /// Returns true if there are 2 or more sections, false if only 1 or 0.
    /// </summary>
    public async Task<bool> GetIsMusicSectionSelectableAsync()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null || string.IsNullOrWhiteSpace(currentSchedule.MusicPublicationCode))
        {
            return false;
        }

        // If publication doesn't have sections, it's not selectable
        if (!GetIsMusicSectionVisible())
        {
            return false;
        }

        var musicType = currentSchedule.MusicType ?? MusicType.Music;
        var languageCode = musicType == MusicType.VocalMusic 
            ? (currentSchedule.MusicLanguageCode ?? string.Empty)
            : string.Empty;
        var publicationCode = currentSchedule.MusicPublicationCode;

        try
        {
            // Check if publication has LanguageId == null by trying both methods
            SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection> sections;
            
            // First try with language
            sections = await mediaService.GetBiblePublicationSections(languageCode, publicationCode);
            
            // If no sections found with language, try without language
            if (sections.Count == 0)
            {
                sections = await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);
            }

            return sections.Count > 1;
        }
        catch
        {
            return false; // On error, default to not selectable
        }
    }

    /// <summary>
    /// Checks if there are multiple tracks available for the current music section/publication.
    /// Returns true if there are 2 or more tracks, false if only 1 or 0.
    /// </summary>
    public async Task<bool> GetIsMusicTrackSelectableAsync()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null || string.IsNullOrWhiteSpace(currentSchedule.MusicPublicationCode))
        {
            return false;
        }

        var musicType = currentSchedule.MusicType ?? MusicType.Music;
        var languageCode = musicType == MusicType.VocalMusic 
            ? (currentSchedule.MusicLanguageCode ?? string.Empty)
            : string.Empty;
        var publicationCode = currentSchedule.MusicPublicationCode;
        var sectionCode = Bible.Alarm.Shared.Helpers.SectionCodeHelper.Normalize(currentSchedule.MusicSectionCode);

        try
        {
            // Try with language first
            var tracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, sectionCode);
            
            // If no tracks found with language, try without language
            if (tracks.Count == 0)
            {
                tracks = await mediaService.GetBiblePublicationTracks(string.Empty, publicationCode, sectionCode);
            }

            return tracks.Count > 1;
        }
        catch
        {
            return false; // On error, default to not selectable
        }
    }
}

