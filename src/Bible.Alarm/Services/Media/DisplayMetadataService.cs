#nullable enable
using Bible.Alarm.Services.Media.DisplayMetadataServiceHelpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;
using File = TagLib.File;
using IPicture = TagLib.IPicture;
using ReadStyle = TagLib.ReadStyle;

namespace Bible.Alarm.Services.Media;

public sealed class DisplayMetadataService(
    ILogger logger,
    IMediaService mediaService,
    HttpMessageHandler httpHandler,
    IBiblePublicationService? biblePublicationService = null,
    IVocalMusicService? vocalMusicService = null)
    : IDisplayMetadataService
{
    private readonly DisplayMetadataServiceMusicHelper musicHelper = new(logger, mediaService, vocalMusicService);
    private readonly RemoteId3ArtworkExtractor remoteId3ArtworkExtractor = new(httpHandler, logger);
    private readonly RemoteMp4ArtworkExtractor remoteMp4ArtworkExtractor = new(httpHandler, logger);
    private readonly SemaphoreSlim metadataFetchLock = new(1, 1);

    private const string HttpsUriSchemePrefix = "https://";
    private const string FallbackUnknownTitle = "Unknown Title";

    public async Task<MetaData> GetDisplayMetadataAsync(AudioPlayerTrack track)
    {
        await metadataFetchLock.WaitAsync();
        try
        {
            return await GetDisplayMetadataCoreAsync(track, skipRemoteArtwork: false);
        }
        finally
        {
            metadataFetchLock.Release();
        }
    }

    public async Task<MetaData> GetCoreDisplayMetadataAsync(AudioPlayerTrack track)
    {
        // No lock needed: this path only does DB/catalog lookups (no TagLib file access,
        // no remote HTTP). Avoiding the lock prevents playback-start from being blocked
        // by a long-running background artwork fetch holding metadataFetchLock.
        return await GetDisplayMetadataCoreAsync(track, skipRemoteArtwork: true);
    }

    private async Task<MetaData> GetDisplayMetadataCoreAsync(AudioPlayerTrack track, bool skipRemoteArtwork = false)
    {
        var trackMetadata = track.PlayItem.Metadata;
        var meta = new MetaData();

        try
        {
            if (trackMetadata.PlayType == PlayType.Bible)
            {
                await SetBibleMetadataAsync(trackMetadata, meta, track.Uri, skipRemoteArtwork);
            }
            else
            {
                await SetMusicMetadataAsync(trackMetadata, meta, track.Uri, skipRemoteArtwork);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to get display metadata for track");
        }

        await ApplyFallbackMetadataIfNeededAsync(meta, track.Uri, skipRemoteArtwork);

        return meta;
    }

    private async Task SetBibleMetadataAsync(TrackMetadata trackMetadata, MetaData meta, string uri, bool skipRemoteArtwork = false)
    {
        // Melody disc-style publications (e.g. "iam") are stored as sectioned BiblePublications,
        // but their tracks are conceptually "melody music".
        // When these are used as the MAIN schedule content (Music category), their TrackMetadata.PlayType is still Bible
        // (because schedule progress/navigation uses BiblePublicationSchedule fields).
        // Ensure the alarm modal / now-playing metadata is still populated correctly from the melody catalog.
        if (await TrySetDiscStyleMelodyMetadataAsync(trackMetadata, meta))
        {
            if (!skipRemoteArtwork || !uri.StartsWith(HttpsUriSchemePrefix, StringComparison.OrdinalIgnoreCase))
            {
                await TryExtractArtworkFromFileAsync(meta, uri, "Melody disc file");
            }
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

        if (!skipRemoteArtwork || !uri.StartsWith(HttpsUriSchemePrefix, StringComparison.OrdinalIgnoreCase))
        {
            await TryExtractArtworkFromFileAsync(meta, uri, "Bible file");
        }
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
                
                // Artist: Publication name + JW.org qualifier
                meta.Artist = $"{publication.Name}{DisplayMetadataPublisherStrings.JwOrgArtistQualifier}";

                return;
            }
        }
        
        // Fallback if no publication found
        meta.Title = $"Track {trackMetadata.TrackCode}";
    }

    private async Task SetBiblePublicationSectionMetadataAsync(TrackMetadata trackMetadata, MetaData meta, BiblePublicationSection section)
    {
        if (MagazineHelper.IsMagazinePublicationCode(trackMetadata.PublicationCode))
        {
            await SetMagazineSectionMetadataAsync(trackMetadata, meta, section);
            return;
        }

        // Bible: "Genesis 1" style (section name + chapter number)
        meta.Title = $"{section.Name} {trackMetadata.TrackCode}";

        if (biblePublicationService != null)
        {
            var publication = await biblePublicationService.GetByLanguageAndCodeWithSectionsAsync(
                trackMetadata.LanguageCode,
                trackMetadata.PublicationCode);

            if (publication != null)
            {
                meta.Artist = $"{publication.Name}{DisplayMetadataPublisherStrings.JwOrgArtistQualifier}";
                return;
            }
        }

        meta.Artist = DisplayMetadataPublisherStrings.JwOrgLabel;
    }

    private async Task SetMagazineSectionMetadataAsync(TrackMetadata trackMetadata, MetaData meta, BiblePublicationSection section)
    {
        string? trackTitle = null;
        var sectionCode = SectionCodeHelper.Normalize(trackMetadata.SectionCode);
        if (!string.IsNullOrWhiteSpace(sectionCode))
        {
            var tracks = await mediaService.GetBiblePublicationTracks(
                trackMetadata.LanguageCode,
                trackMetadata.PublicationCode,
                sectionCode);

            if (tracks.TryGetValue(trackMetadata.TrackCode, out var track) && !string.IsNullOrWhiteSpace(track.Title))
            {
                trackTitle = track.Title;
            }
        }

        meta.Title = !string.IsNullOrWhiteSpace(trackTitle) ? trackTitle : section.Name;
        meta.Artist = $"{section.Name}{DisplayMetadataPublisherStrings.JwOrgArtistQualifier}";
    }

    private Task SetMusicMetadataAsync(TrackMetadata trackMetadata, MetaData meta, string uri, bool skipRemoteArtwork = false)
    {
        Func<Task> extractFileMetadata = (skipRemoteArtwork && uri.StartsWith(HttpsUriSchemePrefix, StringComparison.OrdinalIgnoreCase))
            ? () => Task.CompletedTask
            : () => TryExtractFileMetadataAsync(meta, uri);
        return musicHelper.SetMusicMetadataAsync(trackMetadata, meta, uri, extractFileMetadata);
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

        var discParts = trackMetadata.DownloadCode.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var suffix = discParts.Length > 0 ? discParts[^1] : null;
        if (string.IsNullOrWhiteSpace(suffix) || !suffix.All(char.IsDigit))
        {
            return false;
        }

        string? trackTitle = null;
        try
        {
            var tracks = await mediaService.GetMelodyMusicTracksBySection(trackMetadata.PublicationCode, trackMetadata.DownloadCode);
            var trackCode = trackMetadata.TrackCode;
            if (!string.IsNullOrWhiteSpace(trackCode) &&
                Bible.Alarm.Shared.Helpers.MusicTrackLookupHelper.TryGetByCode(tracks, trackCode, out var melodyPair))
            {
                trackTitle = NormalizeTitle(melodyPair.Track.Title);
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to get melody track title from media service");
        }

        string? releaseName = null;
        try
        {
            var releases = await mediaService.GetMelodyMusicReleases();
            if (releases.TryGetValue(trackMetadata.PublicationCode, out var release) &&
                !string.IsNullOrWhiteSpace(release?.Name))
            {
                releaseName = release.Name;
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to get melody release name from media service");
        }

        string? sectionName = null;
        try
        {
            var sections = await mediaService.GetSectionsForPublicationWithoutLanguage(trackMetadata.PublicationCode);
            if (sections.TryGetValue(trackMetadata.DownloadCode, out var section) &&
                !string.IsNullOrWhiteSpace(section?.Name))
            {
                sectionName = section.Name;
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to get melody section name from media service");
        }

        // CarPlay/lock screen: put short text in Title so it does not overlap the two-line subtitle.
        // (Behavior may differ between Simulator and real CarPlay; verify on device when possible.)
        // Title = publication or disc (short); Artist = track name; Album = disc/section.
        meta.Title = !string.IsNullOrWhiteSpace(releaseName) ? releaseName : sectionName;
        meta.Artist = trackTitle;
        meta.Album = sectionName;
        if (string.IsNullOrWhiteSpace(meta.Artist))
            meta.Artist = DisplayMetadataPublisherStrings.JwOrgLabel;
        if (string.IsNullOrWhiteSpace(meta.Title))
            meta.Title = "Melody";

        return true;
    }

    private static string? NormalizeTitle(string? rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
            return null;
        return MediaTrackTitleHelper.DecodeHtmlTitle(rawTitle).Trim();
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
            logger.Debug(ex, "Failed to extract file metadata for artwork/artist/album {Uri}", uri);
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
            logger.Debug(ex, "Failed to extract artwork from file {Uri} ({Context})", uri, context);
        }
    }

    private Task ApplyFallbackMetadataIfNeededAsync(MetaData meta, string uri, bool skipRemoteArtwork = false)
    {
        if (!string.IsNullOrEmpty(meta.Title))
        {
            return Task.CompletedTask;
        }

        if (skipRemoteArtwork && uri.StartsWith(HttpsUriSchemePrefix, StringComparison.OrdinalIgnoreCase))
        {
            meta.Title = FallbackUnknownTitle;
            meta.Artist ??= DisplayMetadataPublisherStrings.JwOrgLabel;
            return Task.CompletedTask;
        }

        return ApplyFallbackMetadataFromFileAsync(meta, uri);
    }

    private async Task ApplyFallbackMetadataFromFileAsync(MetaData meta, string uri)
    {
        try
        {
            var fileMeta = await ExtractMetadataFromFileAsync(uri);
            meta.Title = fileMeta.Title ?? FallbackUnknownTitle;
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
            logger.Warning(ex, "Failed to extract metadata from file {Uri}", uri);
            meta.Title = FallbackUnknownTitle;
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
        if (uri.StartsWith(HttpsUriSchemePrefix, StringComparison.OrdinalIgnoreCase))
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
        if (uri.StartsWith(HttpsUriSchemePrefix, StringComparison.OrdinalIgnoreCase))
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
                    return null;
                }

                File? file = null;
                try
                {
                    file = File.Create(filePath);
                }
                catch (Exception ex)
                {
                    try
                    {
                        file = File.Create(filePath, "video/mp4", ReadStyle.None);
                    }
                    catch (Exception ex2)
                    {
                        try
                        {
                            file = File.Create(filePath, "audio/mpeg", ReadStyle.None);
                        }
                        catch (Exception ex3)
                        {
                            logger.Warning(ex, "Failed to extract metadata from {Uri} (default create failed)", uri);
                            logger.Debug(ex2, "Video/mp4 create also failed for {Uri}", uri);
                            logger.Debug(ex3, "Audio/mpeg create also failed for {Uri}", uri);
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
                    ExtractArtworkIfAvailable(tag, meta);
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
            Title = !string.IsNullOrEmpty(tag.Title) ? tag.Title : FallbackUnknownTitle,
            Artist = !string.IsNullOrEmpty(tag.FirstPerformer) ? tag.FirstPerformer :
                     !string.IsNullOrEmpty(tag.FirstAlbumArtist) ? tag.FirstAlbumArtist :
                     null,
            Album = !string.IsNullOrEmpty(tag.Album) ? tag.Album : null
        };
    }

    private static void ExtractArtworkIfAvailable(TagLib.Tag tag, MetaData meta)
    {
        // Extract artwork if available - find the largest picture
        if (tag.Pictures != null && tag.Pictures.Length > 0)
        {
            var largestPicture = FindLargestPicture(tag.Pictures);
            if (largestPicture != null && largestPicture.Data != null && largestPicture.Data.Data != null)
            {
                meta.ArtworkBytes = largestPicture.Data.Data;
            }
        }
    }

    private static IPicture? FindLargestPicture(IPicture[] pictures)
    {
        IPicture? largestPicture = null;
        int largestSize = 0;

        foreach (IPicture picture in pictures)
        {
            if (picture != null && picture.Data != null && picture.Data.Data != null)
            {
                var size = picture.Data.Data.Length;

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
            Title = FallbackUnknownTitle,
            Artist = null
        };
    }

}

