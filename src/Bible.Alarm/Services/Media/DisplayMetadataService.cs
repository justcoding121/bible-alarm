#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class DisplayMetadataService(ILogger logger, IMediaService mediaService) : IDisplayMetadataService, IDisposable
{
    private readonly ILogger logger = logger;
    private readonly IMediaService mediaService = mediaService;
    private bool isDisposed;

    public async Task<MetaData> GetDisplayMetadataAsync(AudioPlayerTrack track)
    {
        var trackMetadata = track.PlayItem.Metadata;
        var meta = new MetaData();

        try
        {
            if (trackMetadata.PlayType == PlayType.Bible)
            {
                // For Bible tracks: Get book name and chapter number
                var book = await mediaService.GetBibleBook(
                    trackMetadata.LanguageCode,
                    trackMetadata.PublicationCode,
                    trackMetadata.BookNumber);

                if (book != null)
                {
                    // Title: Book name + Chapter number
                    meta.Title = $"{book.Name} {trackMetadata.ChapterNumber}";

                    // Description: Language name
                    var languages = await mediaService.GetBibleLanguages();
                    if (languages.TryGetValue(trackMetadata.LanguageCode, out var language))
                    {
                        meta.Album = language.Name;
                    }

                    // SubTitle: Translation name + (jw.org)
                    var translations = await mediaService.GetBibleTranslations(trackMetadata.LanguageCode);
                    if (translations.TryGetValue(trackMetadata.PublicationCode, out var translation))
                    {
                        meta.Artist = $"{translation.Name} (jw.org)";
                    }
                    else
                    {
                        meta.Artist = "jw.org";
                    }
                }
                else
                {
                    meta.Title = $"Book {trackMetadata.BookNumber} Chapter {trackMetadata.ChapterNumber}";
                }

                // Try to extract artwork from Bible audio files as well
                try
                {
                    var fileMeta = await ExtractMetadataFromFileAsync(track.Uri);
                    if (fileMeta.ArtworkBytes != null && fileMeta.ArtworkBytes.Length > 0)
                    {
                        meta.ArtworkBytes = fileMeta.ArtworkBytes;
                    }
                }
                catch (Exception ex)
                {
                    // Ignore file metadata extraction errors
                    logger.Debug(ex, "Error extracting artwork from Bible file, ignoring");
                }
            }
            else
            {
                // For music tracks: Get track title from database
                if (string.IsNullOrEmpty(trackMetadata.LanguageCode))
                {
                    // Melody music (Kingdom Melodies) - prefix title with "Kingdom Melodies "
                    var tracks = await mediaService.GetMelodyMusicTracks(trackMetadata.PublicationCode);
                    if (tracks.TryGetValue(trackMetadata.TrackNumber, out var melodyTrack))
                    {
                        meta.Title = $"Kingdom Melodies {melodyTrack.Title}";
                    }
                    // Description left empty for kingdom melodies
                }
                else
                {
                    // Vocal music
                    var releases = await mediaService.GetVocalMusicReleases(trackMetadata.LanguageCode);
                    if (releases.TryGetValue(trackMetadata.PublicationCode, out var vocalRelease))
                    {
                        // Description: Publication name
                        meta.Album = vocalRelease.Name;
                    }

                    var tracks = await mediaService.GetVocalMusicTracks(
                        trackMetadata.LanguageCode,
                        trackMetadata.PublicationCode);
                    if (tracks.TryGetValue(trackMetadata.TrackNumber, out var vocalTrack))
                    {
                        meta.Title = vocalTrack.Title;
                    }
                }

                // Try to get artist from audio file metadata
                try
                {
                    var fileMeta = await ExtractMetadataFromFileAsync(track.Uri);
                    if (!string.IsNullOrEmpty(fileMeta.Artist))
                    {
                        // SubTitle: Artist
                        meta.Artist = fileMeta.Artist;
                    }
                    // If Album not set from database, use from file metadata
                    if (string.IsNullOrEmpty(meta.Album) && !string.IsNullOrEmpty(fileMeta.Album))
                    {
                        meta.Album = fileMeta.Album;
                    }
                    if (fileMeta.ArtworkBytes != null && fileMeta.ArtworkBytes.Length > 0)
                    {
                        meta.ArtworkBytes = fileMeta.ArtworkBytes;
                    }
                }
                catch (Exception ex)
                {
                    // Ignore file metadata extraction errors
                    logger.Debug(ex, "Error extracting file metadata, ignoring");
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, $"Failed to get display metadata for track");
        }

        // Fallback to file metadata if title is still empty
        if (string.IsNullOrEmpty(meta.Title))
        {
            try
            {
                var fileMeta = await ExtractMetadataFromFileAsync(track.Uri);
                meta.Title = fileMeta.Title ?? "Unknown Title";
                if (string.IsNullOrEmpty(meta.Artist))
                {
                    meta.Artist = fileMeta.Artist;
                }
                if (fileMeta.ArtworkBytes != null && fileMeta.ArtworkBytes.Length > 0)
                {
                    meta.ArtworkBytes = fileMeta.ArtworkBytes;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, $"Failed to extract metadata from file {track.Uri}");
                meta.Title = "Unknown Title";
            }
        }

        return meta;
    }

    private async Task<MetaData> ExtractMetadataFromFileAsync(string uri)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Convert file:// URI to local path, or use URI as-is if already a file path
                string filePath = uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                    ? new Uri(uri).LocalPath
                    : uri;

                // Extract metadata using TagLibSharp
                using var file = TagLib.File.Create(filePath);
                var tag = file.Tag;

                var meta = new MetaData
                {
                    Title = !string.IsNullOrEmpty(tag.Title) ? tag.Title : "Unknown Title",
                    Artist = !string.IsNullOrEmpty(tag.FirstPerformer) ? tag.FirstPerformer :
                             !string.IsNullOrEmpty(tag.FirstAlbumArtist) ? tag.FirstAlbumArtist :
                             null,
                    Album = !string.IsNullOrEmpty(tag.Album) ? tag.Album : null
                };

                // Extract artwork if available - find the largest picture
                if (tag.Pictures != null && tag.Pictures.Length > 0)
                {
                    TagLib.IPicture? largestPicture = null;
                    int largestSize = 0;

                    logger.Debug($"Found {tag.Pictures.Length} picture(s) in {uri}");

                    // Find the picture with the largest data size
                    foreach (TagLib.IPicture picture in tag.Pictures)
                    {
                        if (picture != null && picture.Data != null && picture.Data.Data != null)
                        {
                            var size = picture.Data.Data.Length;
                            logger.Debug($"  Picture Size={size} bytes");

                            // Select the largest picture
                            if (largestPicture == null || size > largestSize)
                            {
                                largestPicture = picture;
                                largestSize = size;
                            }
                        }
                    }

                    if (largestPicture != null && largestPicture.Data != null && largestPicture.Data.Data != null)
                    {
                        meta.ArtworkBytes = largestPicture.Data.Data;
                        logger.Information($"Extracted artwork from {uri}: Size={largestSize} bytes");
                    }
                    else
                    {
                        logger.Debug($"No valid artwork found in {uri} (checked {tag.Pictures.Length} pictures)");
                    }
                }
                else
                {
                    logger.Debug($"No pictures found in {uri}");
                }

                return meta;
            }
            catch (Exception ex)
            {
                logger.Warning(ex, $"Failed to extract metadata from {uri}");

                // Return fallback metadata
                return new MetaData
                {
                    Title = "Unknown Title",
                    Artist = null
                };
            }
        });
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // All injected services are singletons, so don't dispose them
        // No event handlers to unsubscribe
    }
}

