#nullable enable
using Bible;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using CommunityToolkit.Maui.Views;
using Fluxor;
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
            SendMetadataMessage(metadata);
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
            SendMetadataMessage(fallbackMeta);
        }
    }

    private async Task ApplyMetadataToMediaElement(MetaData meta, MediaElement mediaElement)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            mediaElement.MetadataTitle = meta.Title ?? "";
            mediaElement.MetadataArtist = meta.Artist ?? "";

            if (meta.ArtworkBytes != null && meta.ArtworkBytes.Length > 0)
            {
                // Use AppDataDirectory instead of CacheDirectory for artwork
                // CacheDirectory can be cleared by iOS when storage is low, which would break lock screen artwork
                // AppDataDirectory is more persistent and won't be cleared by the OS
                var artworkDir = FileSystem.AppDataDirectory;
                var artworkPath = Path.Combine(artworkDir, "playing_track_artwork.jpg");
                File.WriteAllBytes(artworkPath, meta.ArtworkBytes);
                mediaElement.MetadataArtworkUrl = artworkPath;
            }
            else if (!string.IsNullOrEmpty(meta.ArtworkUrl))
            {
                mediaElement.MetadataArtworkUrl = meta.ArtworkUrl;
            }
        });
    }

    private void SendMetadataMessage(MetaData meta)
    {
        string? artworkUrl = meta.ArtworkUrl;
        if (meta.ArtworkBytes != null && meta.ArtworkBytes.Length > 0 && string.IsNullOrEmpty(artworkUrl))
        {
            try
            {
                // Use AppDataDirectory instead of CacheDirectory for artwork
                // CacheDirectory can be cleared by iOS when storage is low, which would break lock screen artwork
                // AppDataDirectory is more persistent and won't be cleared by the OS
                var artworkDir = FileSystem.AppDataDirectory;
                var artworkPath = Path.Combine(artworkDir, "playing_track_artwork.jpg");
                File.WriteAllBytes(artworkPath, meta.ArtworkBytes);
                artworkUrl = artworkPath;
                logger.Debug($"Saved playing track artwork to {artworkPath}, size: {meta.ArtworkBytes.Length} bytes");
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to save playing track artwork to file");
            }
        }

        dispatcher.Dispatch(new PlaybackMetadataChangedAction
        {
            Title = meta.Title,
            Artist = meta.Artist,
            Album = meta.Album,
            ArtworkUrl = artworkUrl
        });

        if (!string.IsNullOrEmpty(artworkUrl))
        {
            logger.Debug($"Dispatched metadata with ArtworkUrl: {artworkUrl}");
        }
        else
        {
            logger.Debug("Dispatched metadata without ArtworkUrl");
        }
    }
}

