#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Serilog;
using System.IO;
using TagLib;

namespace Bible.Alarm.Services.Media;

public class DisplayMetadataService(ILogger logger, MediaService mediaService) : IDisplayMetadataService
{
    private readonly ILogger _logger = logger;
    private readonly MediaService _mediaService = mediaService;

    public async Task<MetaData> GetDisplayMetadataAsync(AudioPlayerTrack track)
    {
        var trackMetadata = track.PlayItem.Metadata;
        var meta = new MetaData();

        try
        {
            if (trackMetadata.PlayType == PlayType.Bible)
            {
                // For Bible tracks: Get book name and chapter number
                var book = await _mediaService.GetBibleBook(
                    trackMetadata.LanguageCode,
                    trackMetadata.PublicationCode,
                    trackMetadata.BookNumber);

                if (book != null)
                {
                    // Title: Book name + Chapter number
                    meta.Title = $"{book.Name} {trackMetadata.ChapterNumber}";
                    
                    // Description: Language name
                    var languages = await _mediaService.GetBibleLanguages();
                    if (languages.TryGetValue(trackMetadata.LanguageCode, out var language))
                    {
                        meta.Album = language.Name;
                    }
                    
                    // SubTitle: Translation name + (jw.org)
                    var translations = await _mediaService.GetBibleTranslations(trackMetadata.LanguageCode);
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
            }
            else
            {
                // For music tracks: Get track title from database
                if (string.IsNullOrEmpty(trackMetadata.LanguageCode))
                {
                    // Melody music (Kingdom Melodies) - prefix title with "Kingdom Melodies "
                    var tracks = await _mediaService.GetMelodyMusicTracks(trackMetadata.PublicationCode);
                    if (tracks.TryGetValue(trackMetadata.TrackNumber, out var melodyTrack))
                    {
                        meta.Title = $"Kingdom Melodies {melodyTrack.Title}";
                    }
                    // Description left empty for kingdom melodies
                }
                else
                {
                    // Vocal music
                    var releases = await _mediaService.GetVocalMusicReleases(trackMetadata.LanguageCode);
                    if (releases.TryGetValue(trackMetadata.PublicationCode, out var vocalRelease))
                    {
                        // Description: Publication name
                        meta.Album = vocalRelease.Name;
                    }
                    
                    var tracks = await _mediaService.GetVocalMusicTracks(
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
                    _logger.Debug(ex, "Error extracting file metadata, ignoring");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, $"Failed to get display metadata for track");
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
                _logger.Warning(ex, $"Failed to extract metadata from file {track.Uri}");
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

                // Extract artwork if available
                if (tag.Pictures != null && tag.Pictures.Length > 0)
                {
                    var picture = tag.Pictures[0];
                    if (picture?.Data?.Data != null)
                    {
                        meta.ArtworkBytes = picture.Data.Data;
                    }
                }

                return meta;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, $"Failed to extract metadata from {uri}");
                
                // Return fallback metadata
                return new MetaData
                {
                    Title = "Unknown Title",
                    Artist = null
                };
            }
        });
    }
}

