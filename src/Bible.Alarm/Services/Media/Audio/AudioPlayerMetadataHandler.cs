#nullable enable
using System.Text.RegularExpressions;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using CommunityToolkit.Maui;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media.Audio;

/// <summary>
/// Handles metadata operations for AudioPlayer.
/// Separated from AudioPlayer for better modularity.
/// </summary>
public class AudioPlayerMetadataHandler
{
    private readonly ILogger logger;
    private readonly IDisplayMetadataService displayMetadataService;
    private readonly IDispatcher dispatcher;

    // Tracks the last artwork URL dispatched to Fluxor so HandleMediaOpenedAsync
    // does not clear artwork that SyncMetadataForTrackAsync already set.
    // On some platforms the media player locks the cached file, causing TagLib
    // extraction to fail during HandleMediaOpenedAsync (after PrepareAsync).
    private string? lastDispatchedArtworkUrl;

    public AudioPlayerMetadataHandler(
        ILogger logger,
        IDisplayMetadataService displayMetadataService,
        IDispatcher dispatcher)
    {
        this.logger = logger;
        this.displayMetadataService = displayMetadataService;
        this.dispatcher = dispatcher;
    }

    public async Task HandleMediaOpenedAsync(AudioPlayerTrack currentTrack, MediaElement mediaElement)
    {
        try
        {
            var metadata = await displayMetadataService.GetDisplayMetadataAsync(currentTrack);
            await ApplyMetadataToMediaElement(metadata, mediaElement);
            await SendMetadataMessageAsync(metadata, currentTrack);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Metadata extraction failed");
            var fallbackMeta = new MetaData
            {
                Title = "Unknown Title",
                Artist = "Unknown Artist"
            };
            await ApplyMetadataToMediaElement(fallbackMeta, mediaElement);
            await SendMetadataMessageAsync(fallbackMeta, null);
        }
    }

    /// <summary>
    /// Syncs playback metadata (title, artist, album, artwork) to Fluxor and MediaSession for the given track.
    /// Use when MediaOpened may not fire (e.g. Android queue-based track change) so Android Auto Now Playing shows the correct title.
    /// </summary>
    public async Task SyncMetadataForTrackAsync(AudioPlayerTrack track)
    {
        // Reset so stale artwork from a previous track does not leak when
        // neither SyncMetadata nor HandleMediaOpened can extract artwork.
        lastDispatchedArtworkUrl = null;

        try
        {
            var metadata = await displayMetadataService.GetDisplayMetadataAsync(track);
            await SendMetadataMessageAsync(metadata, track);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "SyncMetadataForTrack failed for track");
            var fallbackMeta = new MetaData
            {
                Title = "Unknown Title",
                Artist = "Unknown Artist"
            };
            await SendMetadataMessageAsync(fallbackMeta, null);
        }
    }

    private async Task ApplyMetadataToMediaElement(MetaData meta, MediaElement mediaElement)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            mediaElement.MetadataTitle = meta.Title ?? "";
            mediaElement.MetadataArtist = meta.Artist ?? "";

            if (meta.ArtworkBytes != null && meta.ArtworkBytes.Length > 0)
            {
                // Use AppDataDirectory instead of CacheDirectory for artwork
                // CacheDirectory can be cleared by iOS when storage is low, which would break lock screen artwork
                // AppDataDirectory is more persistent and won't be cleared by the OS
                var artworkDir = Path.Combine(FileSystem.AppDataDirectory, "Artwork");
                // Use timestamp for unique filename - ensures artwork updates are detected
                var artworkPath = Path.Combine(artworkDir, $"media_element_artwork_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.jpg");
                
                // Use concurrency helper to prevent concurrent access to artwork directory operations
                await ConcurrencyHelper.ExecuteAsync(ArtworkHelper.ArtworkLock, async () =>
                {
                    Directory.CreateDirectory(artworkDir); // Ensure directory exists
                    CleanupOldMediaElementArtworkFiles(artworkDir);
                    await File.WriteAllBytesAsync(artworkPath, meta.ArtworkBytes);
                });
                
                mediaElement.MetadataArtworkUrl = artworkPath;
            }
            else if (!string.IsNullOrEmpty(meta.ArtworkUrl))
            {
                mediaElement.MetadataArtworkUrl = meta.ArtworkUrl;
            }
        });
    }

    /// <summary>
    /// Cleans up old media element artwork files.
    /// </summary>
    private void CleanupOldMediaElementArtworkFiles(string artworkDir)
    {
        try
        {
            var artworkFiles = Directory.GetFiles(artworkDir, "media_element_artwork_*.jpg")
                .Select(f => new FileInfo(f))
                .Where(f => f.LastWriteTimeUtc < DateTime.UtcNow.AddMinutes(-1))
                .ToList();

            foreach (var file in artworkFiles)
            {
                try
                {
                    file.Delete();
                }
                catch
                {
                    // Ignore deletion errors
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Builds a stable filename-safe key for the track so Sync and HandleMediaOpened both use the same artwork path (avoids modal blink).
    /// </summary>
    private static string? GetStableArtworkKey(AudioPlayerTrack? track)
    {
        var m = track?.PlayItem?.Metadata;
        if (m == null) return null;
        var raw = $"{m.ScheduleId}_{m.LanguageCode}_{m.PublicationCode}_{m.SectionCode ?? "n"}_{m.TrackCode}";
        return Regex.Replace(raw, @"[\<\>\:\""\/\\\|\?\*]", "_");
    }

    private async Task SendMetadataMessageAsync(MetaData meta, AudioPlayerTrack? track)
    {
        string? artworkUrl = meta.ArtworkUrl;

        // ALWAYS save artwork bytes to a local file if available
        // This ensures iOSNowPlayingInfoManager can load artwork synchronously from a file path
        // rather than trying to load from HTTP URLs which would require async loading
        if (meta.ArtworkBytes != null && meta.ArtworkBytes.Length > 0)
        {
            try
            {
                var artworkDir = Path.Combine(FileSystem.AppDataDirectory, "Artwork");
                var key = GetStableArtworkKey(track);
                var fileName = key != null
                    ? $"playing_track_artwork_{key}.jpg"
                    : $"playing_track_artwork_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.jpg";
                var artworkPath = Path.Combine(artworkDir, fileName);

                // Use concurrency helper to prevent concurrent access to artwork directory operations
                await ConcurrencyHelper.ExecuteAsync(ArtworkHelper.ArtworkLock, async () =>
                {
                    Directory.CreateDirectory(artworkDir); // Ensure directory exists
                    CleanupOldArtworkFiles(artworkDir);
                    await File.WriteAllBytesAsync(artworkPath, meta.ArtworkBytes);
                });
                
                artworkUrl = artworkPath; // Always use local file path for iOS lock screen
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to save playing track artwork to file");
                // Fall back to URL if file save failed
            }
        }

        // Preserve the previously dispatched artwork when extraction produced nothing.
        // SyncMetadataForTrackAsync runs before PrepareAsync (file not locked) and sets
        // artwork successfully.  HandleMediaOpenedAsync runs after (file may be locked
        // by the media player on some platforms), so TagLib extraction can fail.
        // Without this guard the second dispatch would clear the artwork.
        if (artworkUrl != null)
        {
            lastDispatchedArtworkUrl = artworkUrl;
        }
        else if (lastDispatchedArtworkUrl != null)
        {
            logger.Debug("Artwork extraction produced no URL; preserving previously dispatched artwork: {ArtworkUrl}",
                lastDispatchedArtworkUrl);
            artworkUrl = lastDispatchedArtworkUrl;
        }

        dispatcher.Dispatch(new PlaybackMetadataChangedAction
        {
            Title = meta.Title,
            Artist = meta.Artist,
            Album = meta.Album,
            ArtworkUrl = artworkUrl
        });
    }

    /// <summary>
    /// Cleans up old artwork files to prevent accumulation.
    /// Keeps only the most recent file or removes files older than 1 minute.
    /// </summary>
    private void CleanupOldArtworkFiles(string artworkDir)
    {
        try
        {
            var artworkFiles = Directory.GetFiles(artworkDir, "playing_track_artwork_*.jpg")
                .Select(f => new FileInfo(f))
                .Where(f => f.LastWriteTimeUtc < DateTime.UtcNow.AddMinutes(-1))
                .ToList();

            foreach (var file in artworkFiles)
            {
                try
                {
                    file.Delete();
                }
                catch
                {
                    // Ignore deletion errors - file might be in use
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to cleanup old artwork files");
        }
    }
}

