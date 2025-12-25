#nullable enable
using Microsoft.Maui.Storage;
using Serilog;

namespace Bible.Alarm.Common.Helpers;

/// <summary>
/// Static helper for saving and retrieving the last played track metadata to/from Preferences.
/// Used to persist metadata across app restarts for all platforms.
/// </summary>
public static class LastPlayedMetadataHelper
{
    private static readonly ILogger logger = Log.ForContext(typeof(LastPlayedMetadataHelper));

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

            Preferences.Set(LastPlayedTitleKey, title ?? "");
            Preferences.Set(LastPlayedArtistKey, artist ?? "");
            Preferences.Set(LastPlayedAlbumKey, album ?? "");
            Preferences.Set(LastPlayedArtworkUrlKey, artworkUrl ?? "");

            // Save schedule ID if available, otherwise remove it
            if (scheduleId.HasValue)
            {
                Preferences.Set(LastPlayedScheduleIdKey, scheduleId.Value);
            }
            else
            {
                Preferences.Remove(LastPlayedScheduleIdKey);
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
            var title = Preferences.Get(LastPlayedTitleKey, "");
            var artist = Preferences.Get(LastPlayedArtistKey, "");

            // Return null if no valid metadata exists
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(artist))
            {
                return null;
            }

            var album = Preferences.Get(LastPlayedAlbumKey, "");
            var artworkUrl = Preferences.Get(LastPlayedArtworkUrlKey, "");
            var scheduleId = Preferences.Get(LastPlayedScheduleIdKey, -1);

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
            Preferences.Remove(LastPlayedTitleKey);
            Preferences.Remove(LastPlayedArtistKey);
            Preferences.Remove(LastPlayedAlbumKey);
            Preferences.Remove(LastPlayedArtworkUrlKey);
            Preferences.Remove(LastPlayedScheduleIdKey);

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

