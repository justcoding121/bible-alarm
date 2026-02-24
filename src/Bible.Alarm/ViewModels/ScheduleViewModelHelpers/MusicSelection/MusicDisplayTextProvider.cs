#nullable enable
using System.Linq;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores;
using Fluxor;
using Microsoft.Maui;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers.MusicSelection;

public sealed class MusicDisplayTextProvider
{
    private readonly IState<ApplicationState> state;
    private readonly IMediaService mediaService;
    private readonly ILanguageNameService? languageNameService;

    private string? cachedSongPublicationName;
    private string? lastMusicPublicationCode;
    private string? cachedTrackName;
    private string? lastMusicTrackCode;
    private string? lastTrackPublicationCode;
    private string? lastTrackLanguageCode;
    private string? cachedDefaultLanguageName;
    private bool isLoadingDefaultLanguageName;
    private Action<string>? propertyChangeNotifier;

    public MusicDisplayTextProvider(IState<ApplicationState> state, IMediaService mediaService, ILanguageNameService? languageNameService = null)
    {
        this.state = state;
        this.mediaService = mediaService;
        this.languageNameService = languageNameService;
    }

    /// <summary>
    /// Sets a callback to notify when properties change (e.g., when language name loads asynchronously).
    /// </summary>
    public void SetPropertyChangeNotifier(Action<string>? notifier)
    {
        propertyChangeNotifier = notifier;
    }

    public void ClearCaches()
    {
        cachedSongPublicationName = null;
        cachedTrackName = null;
        lastMusicPublicationCode = null;
        lastMusicTrackCode = null;
        lastTrackPublicationCode = null;
        lastTrackLanguageCode = null;
        // Note: Don't clear cachedDefaultLanguageName - it's a static lookup for "E" = "English"
    }

    public void ClearSongPublicationCache()
    {
        cachedSongPublicationName = null;
        lastMusicPublicationCode = null;
    }

    public void ClearTrackCache()
    {
        cachedTrackName = null;
        lastMusicTrackCode = null;
        lastTrackPublicationCode = null;
        lastTrackLanguageCode = null;
    }

    public void UpdateTrackCache(string? trackName, string? trackCode, string? publicationCode, string? languageCode)
    {
        cachedTrackName = trackName;
        lastMusicTrackCode = trackCode;
        lastTrackPublicationCode = publicationCode;
        lastTrackLanguageCode = languageCode;
    }

    public void UpdateSongPublicationCache(string? songPublicationName, string? publicationCode)
    {
        cachedSongPublicationName = songPublicationName;
        lastMusicPublicationCode = publicationCode;
    }

    public bool GetIsSongPublicationVisible()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        // Publication row is always visible when music is enabled
        return currentSchedule != null && currentSchedule.MusicEnabled;
    }

    public bool GetIsMusicLanguageVisible()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        // Language row is always visible when music is enabled
        return currentSchedule != null && currentSchedule.MusicEnabled;
    }

    public string GetMusicLanguageDisplayText()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            return string.Empty;
        }

        // Prefer display name (populated during bootstrap/effects), otherwise fall back to the language code.
        if (!string.IsNullOrWhiteSpace(currentSchedule.MusicLanguageName))
        {
            return currentSchedule.MusicLanguageName;
        }

        // When LanguageCode is "E" (default) but LanguageName is missing, load the name to show "English" instead of "E"
        // This handles cases where instrumental music (non-language publications like "iam") has LanguageCode="E" but no name
        if (!string.IsNullOrWhiteSpace(currentSchedule.MusicLanguageCode) &&
            currentSchedule.MusicLanguageCode.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            // Return cached value if available
            if (!string.IsNullOrEmpty(cachedDefaultLanguageName))
            {
                return cachedDefaultLanguageName;
            }

            // Trigger async load if not already loading
            if (!isLoadingDefaultLanguageName)
            {
                _ = LoadDefaultLanguageNameAsync(propertyChangeNotifier != null 
                    ? (propName) => propertyChangeNotifier(propName) 
                    : null);
            }

            // Return default language code as fallback while loading (will be replaced with "English" once loaded)
            return AppConstants.Media.DefaultLanguageCode;
        }

        // For other language codes, return the code if name is not available
        if (!string.IsNullOrWhiteSpace(currentSchedule.MusicLanguageCode))
        {
            return currentSchedule.MusicLanguageCode!;
        }

        // When music is enabled but LanguageCode is null (no-language publications like "iam"),
        // use the schedule's Bible/main language for display if set (e.g. MY -> Malayalam), else default to English.
        if (currentSchedule.MusicEnabled)
        {
            if (!string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationLanguageName))
            {
                return currentSchedule.BiblePublicationLanguageName;
            }

            if (!string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationLanguageCode))
            {
                return currentSchedule.BiblePublicationLanguageCode;
            }

            // Return cached value if available
            if (!string.IsNullOrEmpty(cachedDefaultLanguageName))
            {
                return cachedDefaultLanguageName;
            }

            // Trigger async load if not already loading
            if (!isLoadingDefaultLanguageName)
            {
                _ = LoadDefaultLanguageNameAsync(propertyChangeNotifier != null 
                    ? (propName) => propertyChangeNotifier(propName) 
                    : null);
            }

            // Return default language code as fallback while loading
            return AppConstants.Media.DefaultLanguageCode;
        }

        return string.Empty;
    }

    private async Task LoadDefaultLanguageNameAsync(Action<string>? onPropertyChanged = null)
    {
        if (isLoadingDefaultLanguageName || !string.IsNullOrEmpty(cachedDefaultLanguageName))
        {
            return;
        }

        isLoadingDefaultLanguageName = true;
        try
        {
            cachedDefaultLanguageName = languageNameService != null
                ? await languageNameService.GetNameByLanguageCodeAsync("E", AppConstants.Media.DefaultLanguageCode)
                : null;
            cachedDefaultLanguageName ??= "English";

            // Notify UI to update if callback provided
            if (onPropertyChanged != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    onPropertyChanged("MusicLanguageDisplayText");
                });
            }
        }
        catch
        {
            // Fallback on error
            cachedDefaultLanguageName = "English";
        }
        finally
        {
            isLoadingDefaultLanguageName = false;
        }
    }

    /// <summary>
    /// Ensures the default language name (English) is loaded from DB.
    /// Call this when music is enabled to pre-load and notify UI.
    /// </summary>
    public async Task EnsureDefaultLanguageNameLoadedAsync(Action<string>? onPropertyChanged = null)
    {
        if (string.IsNullOrEmpty(cachedDefaultLanguageName))
        {
            await LoadDefaultLanguageNameAsync(onPropertyChanged);
        }
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

        // Fall back to code (matches Bible container behavior).
        if (currentSchedule != null && !string.IsNullOrWhiteSpace(currentSchedule.MusicPublicationCode))
        {
            return currentSchedule.MusicPublicationCode!;
        }

        return string.Empty;
    }

    public async Task<string> GetSongPublicationDisplayTextAsync()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null ||
            string.IsNullOrWhiteSpace(currentSchedule.MusicPublicationCode))
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

        // Fall back to code (matches Bible container behavior).
        return currentSchedule.MusicPublicationCode!;
    }

    public string GetTrackDisplayText()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null ||
            string.IsNullOrWhiteSpace(currentSchedule.MusicTrackCode))
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
                lastMusicTrackCode = currentSchedule.MusicTrackCode;
                lastTrackPublicationCode = currentSchedule.MusicPublicationCode;
                lastTrackLanguageCode = currentSchedule.MusicLanguageCode;
            }
            return currentSchedule.MusicTrackName;
        }

        // Return cached value if available
        if (!string.IsNullOrEmpty(cachedTrackName))
        {
            return cachedTrackName;
        }

        // Fall back to track code (matches Bible container behavior of showing *something*).
        return currentSchedule.MusicTrackCode;
    }

    public async Task<string> GetTrackDisplayTextAsync()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null ||
            string.IsNullOrWhiteSpace(currentSchedule.MusicTrackCode))
        {
            return string.Empty;
        }

        // First check if track name is already in state (populated during bootstrap)
        if (!string.IsNullOrWhiteSpace(currentSchedule.MusicTrackName))
        {
            cachedTrackName = currentSchedule.MusicTrackName;
            lastMusicTrackCode = currentSchedule.MusicTrackCode;
            lastTrackPublicationCode = currentSchedule.MusicPublicationCode;
            lastTrackLanguageCode = currentSchedule.MusicLanguageCode;
            return currentSchedule.MusicTrackName;
        }

        // Return cached value if nothing changed
        if (!string.IsNullOrEmpty(cachedTrackName) &&
            lastMusicTrackCode == currentSchedule.MusicTrackCode &&
            lastTrackPublicationCode == currentSchedule.MusicPublicationCode &&
            lastTrackLanguageCode == currentSchedule.MusicLanguageCode)
        {
            return cachedTrackName;
        }

        return currentSchedule.MusicTrackCode;
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
               !string.IsNullOrWhiteSpace(currentSchedule.MusicTrackCode);
    }

    public FlowDirection GetFlowDirection()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        var direction = currentSchedule?.MusicLanguageDirection ?? AppConstants.Media.TextDirectionLeftToRight;
        return string.Equals(direction, AppConstants.Media.TextDirectionRightToLeft, StringComparison.OrdinalIgnoreCase)
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }

    /// <summary>
    /// Determines if the music section row should be visible.
    /// Section row is visible when:
    /// - Publication code is set
    /// - Publication has sections (checked via PublicationTypeHelper.HasSectionStructure)
    /// </summary>
    public bool GetIsMusicSectionVisible()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null || string.IsNullOrEmpty(currentSchedule.MusicPublicationCode))
        {
            return false;
        }

        // Only show section row if the publication actually has sections
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

        // Fall back to section code (matches Bible container behavior).
        if (currentSchedule != null && !string.IsNullOrWhiteSpace(currentSchedule.MusicSectionCode))
        {
            return currentSchedule.MusicSectionCode!;
        }

        return string.Empty;
    }

    /// <summary>
    /// Checks if there are multiple languages available for music.
    /// Returns true if there are 2 or more languages, false if only 1 or 0.
    /// </summary>
    public async Task<bool> GetIsMusicLanguageSelectableAsync()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            return false;
        }

        try
        {
            var languages = await mediaService.GetBiblePublicationLanguages("Music");
            return languages.Count > 1;
        }
        catch
        {
            // On error, default to not selectable
            return false;
        }
    }

    /// <summary>
    /// Checks if there are multiple publications available for the current language.
    /// Returns true if there are 2 or more publications, false if only 1 or 0.
    /// </summary>
    public Task<bool> GetIsSongPublicationSelectableAsync()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            return Task.FromResult(false);
        }

        // Use discovery-based expected modal count from state (set on initial load + cascades).
        var expectedCount = currentSchedule.MusicPublicationModalItemCount;
        return Task.FromResult(expectedCount.HasValue && expectedCount.Value > 1);
    }

    /// <summary>
    /// Checks if there are multiple sections available for the current music publication.
    /// Returns true if there are 2 or more sections, false if only 1 or 0.
    /// </summary>
    public Task<bool> GetIsMusicSectionSelectableAsync()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null || string.IsNullOrWhiteSpace(currentSchedule.MusicPublicationCode))
        {
            return Task.FromResult(false);
        }

        // If publication doesn't have sections, it's not selectable
        if (!GetIsMusicSectionVisible())
        {
            return Task.FromResult(false);
        }

        // Use discovery-based expected modal count from state (set on initial load + cascades).
        var expectedCount = currentSchedule.MusicSectionModalItemCount;
        return Task.FromResult(expectedCount.HasValue && expectedCount.Value > 1);
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

        // Use language code if available, otherwise empty string for no-language publications
        var languageCode = currentSchedule.MusicLanguageCode ?? string.Empty;
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
            // On error, default to not selectable
            return false;
        }
    }
}
