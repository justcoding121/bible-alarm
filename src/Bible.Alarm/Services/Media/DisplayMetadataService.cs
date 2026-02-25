#nullable enable
using Bible.Alarm.Services.Media.DisplayMetadataServiceHelpers;
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
    HttpMessageHandler httpHandler,
    IBiblePublicationService? biblePublicationService = null,
    IVocalMusicService? vocalMusicService = null,
    ILanguageNameService? languageNameService = null)
    : IDisplayMetadataService
{
    private readonly IVocalMusicService? vocalMusicService = vocalMusicService;
    private readonly ILanguageNameService? languageNameService = languageNameService;
    private readonly DisplayMetadataServiceMusicHelper musicHelper = new(logger, mediaService, vocalMusicService);
    private readonly RemoteId3ArtworkExtractor remoteId3ArtworkExtractor = new(httpHandler, logger);
    private readonly RemoteMp4ArtworkExtractor remoteMp4ArtworkExtractor = new(httpHandler, logger);
    private readonly SemaphoreSlim metadataFetchLock = new(1, 1);

    public async Task<MetaData> GetDisplayMetadataAsync(AudioPlayerTrack track)
    {
        await metadataFetchLock.WaitAsync();
        try
        {
            return await GetDisplayMetadataCoreAsync(track);
        }
        finally
        {
            metadataFetchLock.Release();
        }
    }

    private async Task<MetaData> GetDisplayMetadataCoreAsync(AudioPlayerTrack track)
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
                
                // Album: Language name (from LanguageNamesByLanguage for "E")
                var languages = await mediaService.GetBiblePublicationLanguages();
                if (languages.TryGetValue(trackMetadata.LanguageCode, out var language))
                {
                    meta.Album = languageNameService != null
                        ? await languageNameService.GetNameAsync(language.Id, Bible.Alarm.Shared.Constants.AppConstants.Media.DefaultLanguageCode) ?? language.LanguageCode
                        : language.LanguageCode;
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

        // Description: Language name (from LanguageNamesByLanguage for "E")
        var languages = await mediaService.GetBiblePublicationLanguages();
        if (languages.TryGetValue(trackMetadata.LanguageCode, out var language))
        {
            meta.Album = languageNameService != null
                ? await languageNameService.GetNameAsync(language.Id, Bible.Alarm.Shared.Constants.AppConstants.Media.DefaultLanguageCode) ?? language.LanguageCode
                : language.LanguageCode;
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

    private Task SetMusicMetadataAsync(TrackMetadata trackMetadata, MetaData meta, string uri)
    {
        return musicHelper.SetMusicMetadataAsync(trackMetadata, meta, uri, () => TryExtractFileMetadataAsync(meta, uri));
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
                Bible.Alarm.Shared.Helpers.MusicTrackLookupHelper.TryGetByCode(tracks, trackCode, out var melodyPair))
            {
                meta.Title = NormalizeTitle(melodyPair.Track.Title);
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
            return null;
        return WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ').Trim();
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
        // Preferred path: extract from local cached file.
        var localMeta = await TryExtractFromLocalFileAsync(uri);
        if (localMeta != null)
        {
            return localMeta;
        }

        // Fallback: for HTTPS streaming URLs with no local file, use HTTP Range requests
        // to fetch tag/metadata (ID3v2 for MP3, moov for MP4) without downloading the full file.
        if (uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var remoteMeta = await remoteId3ArtworkExtractor.TryExtractMetadataAsync(uri);
            if (remoteMeta == null)
            {
                remoteMeta = await remoteMp4ArtworkExtractor.TryExtractMetadataAsync(uri);
            }

            if (remoteMeta != null)
            {
                return remoteMeta;
            }
        }

        return CreateFallbackMetadata();
    }

    private async Task<MetaData?> TryExtractFromLocalFileAsync(string uri)
    {
        // HTTPS URLs are not local files -- skip the file system check entirely.
        if (uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await Task.Run(() =>
        {
            try
            {
                var filePath = ConvertUriToFilePath(uri);

                if (!System.IO.File.Exists(filePath))
                {
                    logger.Debug("File does not exist for metadata extraction: {FilePath} (from URI: {Uri})", filePath, uri);
                    return null;
                }

                File? file = null;
                try
                {
                    file = File.Create(filePath);
                }
                catch (Exception ex)
                {
                    // Cache filenames may not match content (e.g. .mp3 path with MP4 content). Try explicit mimetypes.
                    logger.Debug(ex, "TagLib auto-detect failed for {FilePath}, trying video/mp4 then audio/mpeg", filePath);
                    try
                    {
                        file = File.Create(filePath, "video/mp4", ReadStyle.None);
                    }
                    catch
                    {
                        try
                        {
                            file = File.Create(filePath, "audio/mpeg", ReadStyle.None);
                        }
                        catch
                        {
                            logger.Warning(ex, "Failed to extract metadata from {Uri}", uri);
                            return null;
                        }
                    }
                }

                if (file == null)
                {
                    return null;
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
                logger.Warning(ex, "Failed to extract metadata from local file {Uri}", uri);
                return null;
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

