#nullable enable
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Fluxor;
using Microsoft.Maui;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers.MusicSelection;

/// <summary>
/// Provides display text for music-related properties.
/// Separated from MusicSelectionContainerViewModel for better modularity.
/// </summary>
public sealed class MusicDisplayTextProvider
{
    private readonly IState<ApplicationState> state;

    // Cache fields
    private string? cachedSongPublicationName;
    private string? lastMusicPublicationCode;
    private string? cachedTrackName;
    private int? lastMusicTrackNumber;
    private string? lastTrackPublicationCode;
    private string? lastTrackLanguageCode;
    private MusicType? lastTrackMusicType;

    public MusicDisplayTextProvider(IState<ApplicationState> state)
    {
        this.state = state;
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
            return "Orchestral Melodies";
        }

        return currentSchedule.MusicType.Value switch
        {
            MusicType.Melodies => "Orchestral Melodies",
            MusicType.Vocals => "Vocals",
            _ => "Orchestral Melodies"
        };
    }

    public bool GetIsSongPublicationVisible()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        return currentSchedule != null &&
               currentSchedule.MusicType.HasValue &&
               currentSchedule.MusicType.Value == MusicType.Vocals;
    }

    public bool GetIsMusicLanguageVisible()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        return currentSchedule != null &&
               currentSchedule.MusicType.HasValue &&
               currentSchedule.MusicType.Value == MusicType.Vocals;
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
            currentSchedule.MusicType.Value != MusicType.Vocals ||
            string.IsNullOrWhiteSpace(currentSchedule.MusicPublicationCode) ||
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

        // NOTE: Do NOT query database here - publication names should be in state from bootstrap
        // If publication name is missing, it means bootstrap didn't populate it, which is an error
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

        // NOTE: Do NOT query database here - track names should be in state from bootstrap
        // If track name is missing, it means bootstrap didn't populate it, which is an error
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

    /// <summary>
    /// Gets the FlowDirection based on the Music's selected language direction.
    /// Used for song publication and track rows which display RTL content.
    /// Returns RightToLeft for RTL languages, LeftToRight otherwise.
    /// </summary>
    public FlowDirection GetFlowDirection()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        var direction = currentSchedule?.MusicLanguageDirection ?? "ltr";
        return string.Equals(direction, "rtl", StringComparison.OrdinalIgnoreCase)
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }
}

