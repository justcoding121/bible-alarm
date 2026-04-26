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
    private static readonly TimeSpan ArtworkKeyRegexTimeout = TimeSpan.FromMilliseconds(250);

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
            // Use core metadata (DB-only) to avoid redundant remote artwork extraction.
            // DispatchArtworkWhenReadyAsync already handles artwork asynchronously.
            // Using the full GetDisplayMetadataAsync here would compete for metadataFetchLock
            // and waste bandwidth on slow networks with duplicate HTTP Range requests.
            var metadata = await displayMetadataService.GetCoreDisplayMetadataAsync(currentTrack);
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
    /// Uses core metadata (DB only) first so playback is not blocked by slow remote artwork extraction on 4G.
    /// Artwork is fetched asynchronously and dispatched when ready (player, notification, CarPlay/Android Auto).
    /// </summary>
    public async Task SyncMetadataForTrackAsync(AudioPlayerTrack track)
    {
        lastDispatchedArtworkUrl = null;

        try
        {
            var metadata = await displayMetadataService.GetCoreDisplayMetadataAsync(track);
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

        DispatchArtworkWhenReadyAsync(track);
    }

    private const int ArtworkFetchTimeoutSeconds = 15;

    private async void DispatchArtworkWhenReadyAsync(AudioPlayerTrack track)
    {
        try
        {
            var metadataTask = displayMetadataService.GetDisplayMetadataAsync(track);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(ArtworkFetchTimeoutSeconds));
            var completed = await Task.WhenAny(metadataTask, timeoutTask);
            if (completed != metadataTask)
            {
                return;
            }
            var metadata = await metadataTask;
            if (metadata.ArtworkBytes != null && metadata.ArtworkBytes.Length > 0 || !string.IsNullOrEmpty(metadata.ArtworkUrl))
            {
                await SendMetadataMessageAsync(metadata, track);
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "AudioPlayerMetadataHandler: DispatchArtworkWhenReadyAsync failed for track");
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
        return Regex.Replace(raw, @"[\<\>\:\""\/\\\|\?\*]", "_", RegexOptions.None, ArtworkKeyRegexTimeout);
    }

    private async Task SendMetadataMessageAsync(MetaData meta, AudioPlayerTrack? track)
    {
        string? artworkUrl = meta.ArtworkUrl;

        // ALWAYS save artwork bytes to a local file if available
        // This ensures iOSNowPlayingInfoManager can load artwork synchronously from a file path
        // rather than trying to load from HTTP URLs which would require async loading
        if (meta.ArtworkBytes != null && meta.ArtworkBytes.Length > 0)
        {
            var artworkDir = Path.Combine(FileSystem.AppDataDirectory, "Artwork");
            var key = GetStableArtworkKey(track);
            var fileName = key != null
                ? $"playing_track_artwork_{key}.jpg"
                : $"playing_track_artwork_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.jpg";
            var artworkPath = Path.Combine(artworkDir, fileName);

            try
            {
                await ConcurrencyHelper.ExecuteAsync(ArtworkHelper.ArtworkLock, async () =>
                {
                    Directory.CreateDirectory(artworkDir);
                    CleanupOldArtworkFiles(artworkDir);
                    await File.WriteAllBytesAsync(artworkPath, meta.ArtworkBytes);
                });

                artworkUrl = artworkPath;
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to save playing track artwork to file");

                // Stable key guarantees same content for same track; use the existing file
                if (File.Exists(artworkPath))
                {
                    artworkUrl = artworkPath;
                }
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

