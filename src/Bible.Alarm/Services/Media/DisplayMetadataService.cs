#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Serilog;
using File = TagLib.File;
using IPicture = TagLib.IPicture;

namespace Bible.Alarm.Services.Media;

public sealed class DisplayMetadataService(ILogger logger, IMediaService mediaService) : IDisplayMetadataService
{

    public async Task<MetaData> GetDisplayMetadataAsync(AudioPlayerTrack track)
    {
        var trackMetadata = track.PlayItem.Metadata;
        var meta = new MetaData();

        try
        {
            if (trackMetadata.PlayType == PlayType.Bible)
            {
                await SetBibleMetadataAsync(trackMetadata, meta, track.Uri);
            }
            else
            {
                await SetMusicMetadataAsync(trackMetadata, meta, track.Uri);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to get display metadata for track");
        }

        await ApplyFallbackMetadataIfNeededAsync(meta, track.Uri);

        return meta;
    }

    private async Task SetBibleMetadataAsync(TrackMetadata trackMetadata, MetaData meta, string uri)
    {
        var section = await mediaService.GetBiblePublicationSection(
            trackMetadata.LanguageCode,
            trackMetadata.PublicationCode,
            trackMetadata.SectionNumber);

        if (section != null)
        {
            await SetBiblePublicationSectionMetadataAsync(trackMetadata, meta, section);
        }
        else
        {
            meta.Title = $"Section {trackMetadata.SectionNumber} Track {trackMetadata.TrackNumber}";
        }

        await TryExtractArtworkFromFileAsync(meta, uri, "Bible file");
    }

    private async Task SetBiblePublicationSectionMetadataAsync(TrackMetadata trackMetadata, MetaData meta, BiblePublicationSection section)
    {
        // Title: Section name + Track number
        meta.Title = $"{section.Name} {trackMetadata.TrackNumber}";

        // Description: Language name
        var languages = await mediaService.GetBiblePublicationLanguages();
        if (languages.TryGetValue(trackMetadata.LanguageCode, out var language))
        {
            meta.Album = language.Name;
        }

        // SubTitle: Publication name + (jw.org)
        var publications = await mediaService.GetBiblePublications(trackMetadata.LanguageCode);
        if (publications.TryGetValue(trackMetadata.PublicationCode, out var publication))
        {
            meta.Artist = $"{publication.Name} (jw.org)";
        }
        else
        {
            meta.Artist = "jw.org";
        }
    }

    private async Task SetMusicMetadataAsync(TrackMetadata trackMetadata, MetaData meta, string uri)
    {
        if (string.IsNullOrEmpty(trackMetadata.LanguageCode))
        {
            await SetMelodyMusicMetadataAsync(trackMetadata, meta);
        }
        else
        {
            await SetVocalMusicMetadataAsync(trackMetadata, meta);
        }

        await TryExtractFileMetadataAsync(meta, uri);
    }

    private async Task SetMelodyMusicMetadataAsync(TrackMetadata trackMetadata, MetaData meta)
    {
        // Melody music (Kingdom Melodies) - prefix title with "Kingdom Melodies "
        var tracks = await mediaService.GetMelodyMusicTracks(trackMetadata.PublicationCode);
        if (tracks.TryGetValue(trackMetadata.TrackNumber, out var melodyTrack))
        {
            meta.Title = $"Kingdom Melodies {melodyTrack.Title}";
        }
        // Description left empty for kingdom melodies
    }

    private async Task SetVocalMusicMetadataAsync(TrackMetadata trackMetadata, MetaData meta)
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

    private async Task TryExtractFileMetadataAsync(MetaData meta, string uri)
    {
        try
        {
            var fileMeta = await ExtractMetadataFromFileAsync(uri);
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

    private async Task TryExtractArtworkFromFileAsync(MetaData meta, string uri, string context)
    {
        try
        {
            var fileMeta = await ExtractMetadataFromFileAsync(uri);
            if (fileMeta.ArtworkBytes != null && fileMeta.ArtworkBytes.Length > 0)
            {
                meta.ArtworkBytes = fileMeta.ArtworkBytes;
            }
        }
        catch (Exception ex)
        {
            // Ignore file metadata extraction errors
            logger.Debug(ex, $"Error extracting artwork from {context}, ignoring");
        }
    }

    private async Task ApplyFallbackMetadataIfNeededAsync(MetaData meta, string uri)
    {
        // Fallback to file metadata if title is still empty
        if (string.IsNullOrEmpty(meta.Title))
        {
            try
            {
                var fileMeta = await ExtractMetadataFromFileAsync(uri);
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
                logger.Warning(ex, $"Failed to extract metadata from file {uri}");
                meta.Title = "Unknown Title";
            }
        }
    }

    private async Task<MetaData> ExtractMetadataFromFileAsync(string uri)
    {
        return await Task.Run(() =>
        {
            try
            {
                var filePath = ConvertUriToFilePath(uri);
                using var file = File.Create(filePath);
                var tag = file.Tag;

                var meta = ExtractBasicMetadata(tag);
                ExtractArtworkIfAvailable(tag, meta, uri);

                return meta;
            }
            catch (Exception ex)
            {
                logger.Warning(ex, $"Failed to extract metadata from {uri}");
                return CreateFallbackMetadata();
            }
        });
    }

    private static string ConvertUriToFilePath(string uri)
    {
        // Convert file:// URI to local path, or use URI as-is if already a file path
        return uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
            ? new Uri(uri).LocalPath
            : uri;
    }

    private static MetaData ExtractBasicMetadata(TagLib.Tag tag)
    {
        return new MetaData
        {
            Title = !string.IsNullOrEmpty(tag.Title) ? tag.Title : "Unknown Title",
            Artist = !string.IsNullOrEmpty(tag.FirstPerformer) ? tag.FirstPerformer :
                     !string.IsNullOrEmpty(tag.FirstAlbumArtist) ? tag.FirstAlbumArtist :
                     null,
            Album = !string.IsNullOrEmpty(tag.Album) ? tag.Album : null
        };
    }

    private void ExtractArtworkIfAvailable(TagLib.Tag tag, MetaData meta, string uri)
    {
        // Extract artwork if available - find the largest picture
        if (tag.Pictures != null && tag.Pictures.Length > 0)
        {
            var largestPicture = FindLargestPicture(tag.Pictures, uri);
            if (largestPicture != null && largestPicture.Data != null && largestPicture.Data.Data != null)
            {
                meta.ArtworkBytes = largestPicture.Data.Data;
                logger.Information($"Extracted artwork from {uri}: Size={largestPicture.Data.Data.Length} bytes");
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
    }

    private IPicture? FindLargestPicture(IPicture[] pictures, string uri)
    {
        IPicture? largestPicture = null;
        int largestSize = 0;

        logger.Debug($"Found {pictures.Length} picture(s) in {uri}");

        // Find the picture with the largest data size
        foreach (IPicture picture in pictures)
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

        return largestPicture;
    }

    private static MetaData CreateFallbackMetadata()
    {
        return new MetaData
        {
            Title = "Unknown Title",
            Artist = null
        };
    }

}

