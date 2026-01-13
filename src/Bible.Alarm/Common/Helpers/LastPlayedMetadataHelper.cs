#nullable enable
using Bible.Alarm.Common.Interfaces.Storage;
using Serilog;

namespace Bible.Alarm.Common.Helpers;

/// <summary>
/// Static helper for saving and retrieving the last played track metadata to/from Preferences.
/// Used to persist metadata across app restarts for all platforms.
/// Uses IThreadSafePreferencesService for centralized, thread-safe Preferences access.
/// </summary>
public static class LastPlayedMetadataHelper
{
    private static readonly ILogger logger = Log.ForContext(typeof(LastPlayedMetadataHelper));
    
    private static IThreadSafePreferencesService GetPreferencesService()
    {
        try
        {
            return Common.ServiceProviderManager.GetService<IThreadSafePreferencesService>();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to get IThreadSafePreferencesService, falling back to direct Preferences access");
            // Fallback to direct Preferences access if service is not available (shouldn't happen in normal operation)
            throw;
        }
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
    /// </summary>
    /// <returns>A tuple containing (Title, Artist, Album, ArtworkUrl, ScheduleId) or null if not found</returns>
    public static (string Title, string Artist, string Album, string ArtworkUrl, int? ScheduleId)? GetLastPlayedMetadata()
    {
        try
        {
            var prefs = GetPreferencesService();
            var title = prefs.Get(LastPlayedTitleKey, "");
            var artist = prefs.Get(LastPlayedArtistKey, "");

            // Return null if no valid metadata exists
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(artist))
            {
                return null;
            }

            var album = prefs.Get(LastPlayedAlbumKey, "");
            var artworkUrl = prefs.Get(LastPlayedArtworkUrlKey, "");
            var scheduleId = prefs.Get(LastPlayedScheduleIdKey, -1);

            return (
                Title: title,
                Artist: artist,
                Album: album,
                ArtworkUrl: artworkUrl,
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

