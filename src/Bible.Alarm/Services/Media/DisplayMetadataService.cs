#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;
using System.Net;
using File = TagLib.File;
using IPicture = TagLib.IPicture;
using ReadStyle = TagLib.ReadStyle;

namespace Bible.Alarm.Services.Media;

public sealed class DisplayMetadataService(
    ILogger logger,
    IMediaService mediaService,
    IBiblePublicationService? biblePublicationService = null,
    IVocalMusicService? vocalMusicService = null)
    : IDisplayMetadataService
{
    private readonly IVocalMusicService? vocalMusicService = vocalMusicService;

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
        // Melody disc-style publications (e.g. "iam") are stored as sectioned BiblePublications,
        // but their tracks are conceptually "melody music".
        // When these are used as the MAIN schedule content (Music category), their TrackMetadata.PlayType is still Bible
        // (because schedule progress/navigation uses BiblePublicationSchedule fields).
        // Ensure the alarm modal / now-playing metadata is still populated correctly from the melody catalog.
        if (await TrySetDiscStyleMelodyMetadataAsync(trackMetadata, meta))
        {
            await TryExtractArtworkFromFileAsync(meta, uri, "Melody disc file");
            return;
        }

        // Try to get section info first (for sectioned publications)
        var sectionCode = SectionCodeHelper.Normalize(trackMetadata.SectionCode);
        var section = !string.IsNullOrWhiteSpace(sectionCode)
            ? await mediaService.GetBiblePublicationSection(
                trackMetadata.LanguageCode,
                trackMetadata.PublicationCode,
                sectionCode)
            : null;

        if (section != null)
        {
            await SetBiblePublicationSectionMetadataAsync(trackMetadata, meta, section);
        }
        else
        {
            // No section found - try to get track info directly from publication
            await SetBiblePublicationTrackMetadataAsync(trackMetadata, meta);
        }

        // Try to extract artwork from file - works for both MP3 and MP4
        await TryExtractArtworkFromFileAsync(meta, uri, "Bible file");
    }
    
    private async Task SetBiblePublicationTrackMetadataAsync(TrackMetadata trackMetadata, MetaData meta)
    {
        // Get track info directly from publication (for publications without sections or when section lookup fails)
        if (biblePublicationService != null)
        {
            var publication = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(
                trackMetadata.LanguageCode,
                trackMetadata.PublicationCode);
            
            if (publication != null)
            {
                // Title: Track title from database
                var track = publication.Tracks?.FirstOrDefault(t => !string.IsNullOrWhiteSpace(trackMetadata.TrackCode) &&
                    t.TrackCode == trackMetadata.TrackCode);
                if (track != null && !string.IsNullOrWhiteSpace(track.Title))
                {
                    meta.Title = track.Title;
                }
                else
                {
                    meta.Title = $"Track {trackMetadata.TrackCode}";
                }
                
                // Artist: Publication name + (jw.org)
                meta.Artist = $"{publication.Name} (jw.org)";
                
                // Album: Language name
                var languages = await mediaService.GetBiblePublicationLanguages();
                if (languages.TryGetValue(trackMetadata.LanguageCode, out var language))
                {
                    meta.Album = language.Name;
                }
                
                return;
            }
        }
        
        // Fallback if no publication found
        meta.Title = $"Track {trackMetadata.TrackCode}";
    }

    private async Task SetBiblePublicationSectionMetadataAsync(TrackMetadata trackMetadata, MetaData meta, BiblePublicationSection section)
    {
        // Title: Section name + Track number
        meta.Title = $"{section.Name} {trackMetadata.TrackCode}";

        // Description: Language name
        var languages = await mediaService.GetBiblePublicationLanguages();
        if (languages.TryGetValue(trackMetadata.LanguageCode, out var language))
        {
            meta.Album = language.Name;
        }

        // SubTitle: Publication name + (jw.org)
        if (biblePublicationService != null)
        {
            var publication = await biblePublicationService.GetByLanguageAndCodeWithSectionsAsync(
                trackMetadata.LanguageCode,
                trackMetadata.PublicationCode);

            if (publication != null)
            {
                meta.Artist = $"{publication.Name} (jw.org)";
                return;
            }
        }

        // Fallback: keep behavior without pulling full publication list
        meta.Artist = "jw.org";
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
        // Melody music (e.g. "iam")
        // IMPORTANT: Sectioned melody publications (e.g., "iam") have duplicate track numbers across discs.
        // TrackMetadata.DownloadCode holds the disc code (e.g. "iam-2"), so resolve title from that disc.
        SortedDictionary<int, MusicTrack> tracks;
        if (PublicationTypeHelper.HasSectionStructure(trackMetadata.PublicationCode) &&
            !string.IsNullOrWhiteSpace(trackMetadata.DownloadCode))
        {
            tracks = await mediaService.GetMelodyMusicTracksBySection(trackMetadata.PublicationCode, trackMetadata.DownloadCode);
        }
        else
        {
            tracks = await mediaService.GetMelodyMusicTracks(trackMetadata.PublicationCode);
        }

        var trackCode = trackMetadata.OriginalTrackCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? trackMetadata.TrackCode;
        if (!string.IsNullOrWhiteSpace(trackCode) &&
            int.TryParse(trackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var trackNum) &&
            tracks.TryGetValue(trackNum, out var melodyTrack))
        {
            meta.Title = NormalizeTitle(melodyTrack.Title);
        }

        // Prefer a stable publication name for subtitle.
        try
        {
            var releases = await mediaService.GetMelodyMusicReleases();
            if (releases.TryGetValue(trackMetadata.PublicationCode, out var release) &&
                !string.IsNullOrWhiteSpace(release?.Name))
            {
                meta.Artist = $"{release.Name} (jw.org)";
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to resolve melody release name for {PublicationCode}", trackMetadata.PublicationCode);
        }

        // For disc-style melody music, show the disc/section name as the album/description line.
        if (PublicationTypeHelper.HasSectionStructure(trackMetadata.PublicationCode) &&
            !string.IsNullOrWhiteSpace(trackMetadata.DownloadCode))
        {
            try
            {
                var sections = await mediaService.GetSectionsForPublicationWithoutLanguage(trackMetadata.PublicationCode);
                if (sections.TryGetValue(trackMetadata.DownloadCode, out var section) &&
                    !string.IsNullOrWhiteSpace(section?.Name))
                {
                    meta.Album = section.Name;
                }
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "Failed to resolve melody disc name for {PublicationCode}/{DiscCode}",
                    trackMetadata.PublicationCode, trackMetadata.DownloadCode);
            }
        }
    }

    private async Task<bool> TrySetDiscStyleMelodyMetadataAsync(TrackMetadata trackMetadata, MetaData meta)
    {
        // We rely on DownloadCode being set by PlaylistBiblePublicationTrackBuilder.TryApplyDiscMusicLookUpPath.
        if (string.IsNullOrWhiteSpace(trackMetadata.DownloadCode))
        {
            return false;
        }

        // Additional guard: disc code should look like "{pubCode}-{digits}".
        if (!trackMetadata.DownloadCode.StartsWith(trackMetadata.PublicationCode + "-", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = trackMetadata.DownloadCode.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
        if (string.IsNullOrWhiteSpace(suffix) || !suffix.All(char.IsDigit))
        {
            return false;
        }

        try
        {
            var tracks = await mediaService.GetMelodyMusicTracksBySection(trackMetadata.PublicationCode, trackMetadata.DownloadCode);
            var trackCode = trackMetadata.OriginalTrackCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? trackMetadata.TrackCode;
            if (!string.IsNullOrWhiteSpace(trackCode) &&
                int.TryParse(trackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsedTrackNum) &&
                tracks.TryGetValue(parsedTrackNum, out var melodyTrack))
            {
                meta.Title = NormalizeTitle(melodyTrack.Title);
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to resolve melody disc track title for {PublicationCode}/{DiscCode}/{TrackCode}",
                trackMetadata.PublicationCode, trackMetadata.DownloadCode, trackMetadata.TrackCode);
        }

        try
        {
            var releases = await mediaService.GetMelodyMusicReleases();
            if (releases.TryGetValue(trackMetadata.PublicationCode, out var release) &&
                !string.IsNullOrWhiteSpace(release?.Name))
            {
                meta.Artist = $"{release.Name} (jw.org)";
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to resolve melody release name for {PublicationCode}", trackMetadata.PublicationCode);
        }

        try
        {
            var sections = await mediaService.GetSectionsForPublicationWithoutLanguage(trackMetadata.PublicationCode);
            if (sections.TryGetValue(trackMetadata.DownloadCode, out var section) &&
                !string.IsNullOrWhiteSpace(section?.Name))
            {
                meta.Album = section.Name;
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to resolve melody disc name for {PublicationCode}/{DiscCode}",
                trackMetadata.PublicationCode, trackMetadata.DownloadCode);
        }

        // Ensure we at least have a title.
        meta.Title ??= $"Track {trackMetadata.TrackCode}";
        meta.Artist ??= "jw.org";

        return true;
    }

    private static string? NormalizeTitle(string? rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
        {
            return null;
        }

        return WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ').Trim();
    }

    private async Task SetVocalMusicMetadataAsync(TrackMetadata trackMetadata, MetaData meta)
    {
        // Vocal music
        if (vocalMusicService != null)
        {
            // Avoid loading the full releases list just to get one name.
            var release = await vocalMusicService.GetByLanguageAndCodeAsync(
                trackMetadata.LanguageCode,
                trackMetadata.PublicationCode);

            if (release != null)
            {
                meta.Album = release.Name;
            }
        }
        else
        {
            var releases = await mediaService.GetVocalMusicReleases(trackMetadata.LanguageCode);
            if (releases.TryGetValue(trackMetadata.PublicationCode, out var vocalRelease))
            {
                // Description: Publication name
                meta.Album = vocalRelease.Name;
            }
        }

        var tracks = await mediaService.GetVocalMusicTracks(
            trackMetadata.LanguageCode,
            trackMetadata.PublicationCode);
        if (!string.IsNullOrWhiteSpace(trackMetadata.TrackCode) &&
            int.TryParse(trackMetadata.TrackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var trackNum) &&
            tracks.TryGetValue(trackNum, out var vocalTrack))
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

                if (!System.IO.File.Exists(filePath))
                {
                    logger.Debug("File does not exist for metadata extraction: {FilePath} (from URI: {Uri})", filePath, uri);
                    return CreateFallbackMetadata();
                }

                File? file = null;
                try
                {
                    file = File.Create(filePath);
                }
                catch (Exception ex) when (filePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                {
                    // Cached file may have .mp4 extension but contain MP3 (e.g. when API returned MP3 for video publication).
                    logger.Debug(ex, "TagLib failed for .mp4 path, trying as audio/mpeg: {FilePath}", filePath);
                    try
                    {
                        file = File.Create(filePath, "audio/mpeg", ReadStyle.None);
                    }
                    catch
                    {
                        logger.Warning(ex, "Failed to extract metadata from {Uri}", uri);
                        return CreateFallbackMetadata();
                    }
                }

                if (file == null)
                {
                    return CreateFallbackMetadata();
                }

                using (file)
                {
                    var tag = file.Tag;
                    var meta = ExtractBasicMetadata(tag);
                    ExtractArtworkIfAvailable(tag, meta, uri);
                    return meta;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to extract metadata from {Uri}", uri);
                return CreateFallbackMetadata();
            }
        });
    }

    private static string ConvertUriToFilePath(string uri)
    {
        // Convert file:// URI to local path, or use URI as-is if already a file path
        if (!uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            return uri;
        }

        // Use Uri class to properly decode the file path
        // This handles URL-encoded characters and platform-specific path separators
        try
        {
            var fileUri = new Uri(uri);
            return fileUri.LocalPath;
        }
        catch
        {
            // If URI parsing fails, try to extract path manually
            // Remove "file://" prefix (or "file:///" on Unix)
            var path = uri.Substring(7);
            if (path.StartsWith("//"))
            {
                // UNC path or extra slashes
                path = path.TrimStart('/');
            }
            else if (!path.StartsWith("/") && OperatingSystem.IsWindows())
            {
                // Windows path without leading slash
                return path;
            }
            return "/" + path.TrimStart('/');
        }
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

