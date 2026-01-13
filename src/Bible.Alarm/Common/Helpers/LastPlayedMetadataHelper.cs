#nullable enable
using Bible.Alarm.Common.Interfaces.Storage;
using Serilog;

namespace Bible.Alarm.Common.Helpers;

/// <summary>
/// Static helper for saving and retrieving the last played track metadata to/from Preferences.
/// Used to persist metadata across app restarts for all platforms.
/// Uses IThreadSafePreferencesService for centralized, thread-safe Preferences access when available.
/// Falls back to direct Preferences access when DI is not available (e.g., during early Android Auto startup).
/// </summary>
public static class LastPlayedMetadataHelper
{
    private static readonly ILogger logger = Log.ForContext(typeof(LastPlayedMetadataHelper));
    
    /// <summary>
    /// Tries to get the DI-managed preferences service. Returns null if DI is not available yet.
    /// </summary>
    private static IThreadSafePreferencesService? TryGetPreferencesService()
    {
        try
        {
            return Common.ServiceProviderManager.GetService<IThreadSafePreferencesService>();
        }
        catch
        {
            // DI not available yet (e.g., during early Android Auto startup before MauiApp is created)
            return null;
        }
    }

    /// <summary>
    /// Gets the DI-managed preferences service. Throws if DI is not available.
    /// Use TryGetPreferencesService() for pre-DI scenarios.
    /// </summary>
    private static IThreadSafePreferencesService GetPreferencesService()
    {
        return TryGetPreferencesService() 
            ?? throw new InvalidOperationException("IThreadSafePreferencesService not available - DI may not be initialized yet");
    }

    // Preference keys
    private const string LastPlayedTitleKey = "LastPlayedTitle";
    private const string LastPlayedArtistKey = "LastPlayedArtist";
    private const string LastPlayedAlbumKey = "LastPlayedAlbum";
    private const string LastPlayedArtworkUrlKey = "LastPlayedArtworkUrl";
    private const string LastPlayedScheduleIdKey = "LastPlayedScheduleId";

    /// <summary>
    /// Saves the last played track metadata to Preferences.
    /// </summary>
    /// <param name="title">Track title</param>
    /// <param name="artist">Artist name</param>
    /// <param name="album">Album name (optional)</param>
    /// <param name="artworkUrl">Artwork URL (optional)</param>
    /// <param name="scheduleId">Schedule ID (optional)</param>
    public static void SaveLastPlayedMetadata(
        string? title,
        string? artist,
        string? album = null,
        string? artworkUrl = null,
        int? scheduleId = null)
    {
        try
        {
            // Only save if we have valid metadata (title or artist)
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(artist))
            {
                logger.Debug("Skipping save to Preferences - no valid metadata (title or artist required)");
                return;
            }

            var prefs = GetPreferencesService();
            prefs.Set(LastPlayedTitleKey, title ?? "");
            prefs.Set(LastPlayedArtistKey, artist ?? "");
            prefs.Set(LastPlayedAlbumKey, album ?? "");
            prefs.Set(LastPlayedArtworkUrlKey, artworkUrl ?? "");

            // Save schedule ID if available, otherwise remove it
            if (scheduleId.HasValue)
            {
                prefs.Set(LastPlayedScheduleIdKey, scheduleId.Value);
            }
            else
            {
                prefs.Remove(LastPlayedScheduleIdKey);
            }

            logger.Debug("Saved last played metadata to Preferences - Title: {Title}, Artist: {Artist}, ScheduleId: {ScheduleId}",
                title, artist, scheduleId);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to save last played metadata to Preferences");
        }
    }

    /// <summary>
    /// Retrieves the last played track metadata from Preferences.
    /// Uses DI-managed service when available, falls back to direct Preferences access for pre-DI scenarios.
    /// </summary>
    /// <returns>A tuple containing (Title, Artist, Album, ArtworkUrl, ScheduleId) or null if not found</returns>
    public static (string Title, string Artist, string Album, string ArtworkUrl, int? ScheduleId)? GetLastPlayedMetadata()
    {
        try
        {
            var prefs = TryGetPreferencesService();
            
            // Use DI service if available, otherwise fall back to direct Preferences access
            // Direct access is needed during early Android Auto startup before MauiApp is created
            string title, artist, album, artworkUrl;
            int scheduleId;
            
            if (prefs != null)
            {
                title = prefs.Get(LastPlayedTitleKey, "");
                artist = prefs.Get(LastPlayedArtistKey, "");
                album = prefs.Get(LastPlayedAlbumKey, "");
                artworkUrl = prefs.Get(LastPlayedArtworkUrlKey, "");
                scheduleId = prefs.Get(LastPlayedScheduleIdKey, -1);
            }
            else
            {
                // Fallback to direct Preferences access (pre-DI scenario)
                // This is safe for reads - MAUI Preferences is thread-safe for simple operations
                logger.Debug("Using direct Preferences access (DI not available yet)");
                title = Preferences.Get(LastPlayedTitleKey, "");
                artist = Preferences.Get(LastPlayedArtistKey, "");
                album = Preferences.Get(LastPlayedAlbumKey, "");
                artworkUrl = Preferences.Get(LastPlayedArtworkUrlKey, "");
                scheduleId = Preferences.Get(LastPlayedScheduleIdKey, -1);
            }

            // Return null if no valid metadata exists
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(artist))
            {
                return null;
            }

            return (
                Title: title ?? "",
                Artist: artist ?? "",
                Album: album ?? "",
                ArtworkUrl: artworkUrl ?? "",
                ScheduleId: scheduleId >= 0 ? scheduleId : null
            );
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to retrieve last played metadata from Preferences");
            return null;
        }
    }

    /// <summary>
    /// Clears the last played metadata from Preferences.
    /// </summary>
    public static void ClearLastPlayedMetadata()
    {
        try
        {
            var prefs = GetPreferencesService();
            prefs.Remove(LastPlayedTitleKey);
            prefs.Remove(LastPlayedArtistKey);
            prefs.Remove(LastPlayedAlbumKey);
            prefs.Remove(LastPlayedArtworkUrlKey);
            prefs.Remove(LastPlayedScheduleIdKey);

            logger.Debug("Cleared last played metadata from Preferences");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to clear last played metadata from Preferences");
        }
    }

    /// <summary>
    /// Gets all preference metadata for last played item in a format suitable for MediaSession.
    /// Returns null if no valid metadata exists.
    /// </summary>
    /// <returns>Metadata tuple or null</returns>
    public static (string Title, string Artist, string Album, string ArtworkUrl, int? ScheduleId)? GetAllPreferenceMetadata()
    {
        return GetLastPlayedMetadata();
    }
}

