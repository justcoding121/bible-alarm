#nullable enable
using Bible.Alarm.Services.Media.DisplayMetadataServiceHelpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Constants;
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

    private const string FallbackUnknownTitle = "Unknown Title";
    private const string FallbackTrackTitlePrefix = "Track ";
    private const string ArtworkExtractionContextMelodyDiscFile = "Melody disc file";
    private const string ArtworkExtractionContextBibleFile = "Bible file";

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
            logger.Warning(ex, AppConstants.Logging.DisplayMetadataServiceDiagnosticsLog.FailedToGetDisplayMetadataForTrack);
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
            if (!skipRemoteArtwork || !uri.StartsWith(MediaUriSchemeConstants.HttpsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                await TryExtractArtworkFromFileAsync(meta, uri, ArtworkExtractionContextMelodyDiscFile);
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

        if (!skipRemoteArtwork || !uri.StartsWith(MediaUriSchemeConstants.HttpsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await TryExtractArtworkFromFileAsync(meta, uri, ArtworkExtractionContextBibleFile);
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
                    CodeComparisonHelper.Equals(t.TrackCode, trackMetadata.TrackCode));
                if (track != null && !string.IsNullOrWhiteSpace(track.Title))
                {
                    meta.Title = track.Title;
                }
                else
                {
                    meta.Title = $"{FallbackTrackTitlePrefix}{trackMetadata.TrackCode}";
                }
                
                // Artist: Publication name + JW.org qualifier
                meta.Artist = $"{publication.Name}{DisplayMetadataPublisherStrings.JwOrgArtistQualifier}";

                return;
            }
        }
        
        // Fallback if no publication found
        meta.Title = $"{FallbackTrackTitlePrefix}{trackMetadata.TrackCode}";
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
        Func<Task> extractFileMetadata = (skipRemoteArtwork && uri.StartsWith(MediaUriSchemeConstants.HttpsPrefix, StringComparison.OrdinalIgnoreCase))
            ? () => Task.CompletedTask
            : () => TryExtractFileMetadataAsync(meta, uri);
        return musicHelper.SetMusicMetadataAsync(trackMetadata, meta, uri, extractFileMetadata);
    }

    private async Task<bool> TrySetDiscStyleMelodyMetadataAsync(TrackMetadata trackMetadata, MetaData meta)
    {
        if (!TryValidateDiscStyleMelodyInputs(trackMetadata))
            return false;

        var trackTitle = await TryGetMelodyTrackTitleAsync(trackMetadata).ConfigureAwait(false);
        var releaseName = await TryGetMelodyReleaseNameAsync(trackMetadata).ConfigureAwait(false);
        var sectionName = await TryGetMelodySectionNameAsync(trackMetadata).ConfigureAwait(false);

        ApplyDiscStyleMelodyMetadata(meta, trackTitle, releaseName, sectionName);
        return true;
    }

    private static bool TryValidateDiscStyleMelodyInputs(TrackMetadata trackMetadata)
    {
        if (string.IsNullOrWhiteSpace(trackMetadata.DownloadCode))
            return false;

        if (!trackMetadata.DownloadCode.StartsWith(trackMetadata.PublicationCode + "-", StringComparison.OrdinalIgnoreCase))
            return false;

        var discParts = trackMetadata.DownloadCode.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var suffix = discParts.Length > 0 ? discParts[^1] : null;
        return !string.IsNullOrWhiteSpace(suffix) && suffix.All(char.IsDigit);
    }

    private async Task<string?> TryGetMelodyTrackTitleAsync(TrackMetadata trackMetadata)
    {
        try
        {
            var tracks = await mediaService.GetMelodyMusicTracksBySection(trackMetadata.PublicationCode, trackMetadata.DownloadCode).ConfigureAwait(false);
            var trackCode = trackMetadata.TrackCode;
            if (!string.IsNullOrWhiteSpace(trackCode) &&
                MusicTrackLookupHelper.TryGetByCode(tracks, trackCode, out var melodyPair))
            {
                return NormalizeTitle(melodyPair.Track.Title);
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, AppConstants.Logging.DisplayMetadataServiceDiagnosticsLog.FailedToGetMelodyTrackTitleFromMediaService);
        }

        return null;
    }

    private async Task<string?> TryGetMelodyReleaseNameAsync(TrackMetadata trackMetadata)
    {
        try
        {
            var releases = await mediaService.GetMelodyMusicReleases().ConfigureAwait(false);
            if (releases.TryGetValue(trackMetadata.PublicationCode, out var release) &&
                !string.IsNullOrWhiteSpace(release?.Name))
            {
                return release.Name;
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, AppConstants.Logging.DisplayMetadataServiceDiagnosticsLog.FailedToGetMelodyReleaseNameFromMediaService);
        }

        return null;
    }

    private async Task<string?> TryGetMelodySectionNameAsync(TrackMetadata trackMetadata)
    {
        try
        {
            var sections = await mediaService.GetSectionsForPublicationWithoutLanguage(trackMetadata.PublicationCode).ConfigureAwait(false);
            if (sections.TryGetValue(trackMetadata.DownloadCode, out var section) &&
                !string.IsNullOrWhiteSpace(section?.Name))
            {
                return section.Name;
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, AppConstants.Logging.DisplayMetadataServiceDiagnosticsLog.FailedToGetMelodySectionNameFromMediaService);
        }

        return null;
    }

    private static void ApplyDiscStyleMelodyMetadata(MetaData meta, string? trackTitle, string? releaseName, string? sectionName)
    {
        meta.Title = !string.IsNullOrWhiteSpace(releaseName) ? releaseName : sectionName;
        meta.Artist = trackTitle;
        meta.Album = sectionName;
        if (string.IsNullOrWhiteSpace(meta.Artist))
            meta.Artist = DisplayMetadataPublisherStrings.JwOrgLabel;
        if (string.IsNullOrWhiteSpace(meta.Title))
            meta.Title = "Melody";
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
            logger.Debug(ex, AppConstants.Logging.DisplayMetadataServiceDiagnosticsLog.FailedToExtractFileMetadataForArtworkArtistAlbum, uri);
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
            logger.Debug(ex, AppConstants.Logging.DisplayMetadataServiceDiagnosticsLog.FailedToExtractArtworkFromFileWithContext, uri, context);
        }
    }

    private Task ApplyFallbackMetadataIfNeededAsync(MetaData meta, string uri, bool skipRemoteArtwork = false)
    {
        if (!string.IsNullOrEmpty(meta.Title))
        {
            return Task.CompletedTask;
        }

        if (skipRemoteArtwork && uri.StartsWith(MediaUriSchemeConstants.HttpsPrefix, StringComparison.OrdinalIgnoreCase))
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
            logger.Warning(ex, AppConstants.Logging.DisplayMetadataServiceDiagnosticsLog.FailedToExtractMetadataFromFile, uri);
            meta.Title = FallbackUnknownTitle;
        }
    }

    private async Task<MetaData> ExtractMetadataFromFileAsync(string uri)
    {
        var localMeta = await TryExtractFromLocalFileAsync(uri).ConfigureAwait(false);
        if (localMeta != null)
            return localMeta;

        var remoteMeta = await TryExtractRemoteStreamingMetadataAsync(uri).ConfigureAwait(false);
        return remoteMeta ?? CreateFallbackMetadata();
    }

    private async Task<MetaData?> TryExtractRemoteStreamingMetadataAsync(string uri)
    {
        if (!uri.StartsWith(MediaUriSchemeConstants.HttpsPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var remoteMeta = await remoteId3ArtworkExtractor.TryExtractMetadataAsync(uri).ConfigureAwait(false);
        remoteMeta ??= await remoteMp4ArtworkExtractor.TryExtractMetadataAsync(uri).ConfigureAwait(false);
        return remoteMeta;
    }

    private async Task<MetaData?> TryExtractFromLocalFileAsync(string uri)
    {
        if (uri.StartsWith(MediaUriSchemeConstants.HttpsPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        return await Task.Run(() =>
            ReadMetadataFromLocalFileCore(ConvertUriToFilePath(uri), uri)).ConfigureAwait(false);
    }

    private MetaData? ReadMetadataFromLocalFileCore(string filePath, string uri)
    {
        try
        {
            if (!System.IO.File.Exists(filePath))
                return null;

            using var tagFile = TryOpenTagLibForLocalMetadata(filePath, uri);
            if (tagFile == null)
                return null;

            var tag = tagFile.Tag;
            var meta = ExtractBasicMetadata(tag);
            ExtractArtworkIfAvailable(tag, meta);
            return meta;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.DisplayMetadataServiceDiagnosticsLog.FailedToExtractMetadataFromLocalFile, uri);
            return null;
        }
    }

    private File? TryOpenTagLibForLocalMetadata(string filePath, string uri)
    {
        try
        {
            return File.Create(filePath);
        }
        catch (Exception ex)
        {
            try
            {
                return File.Create(filePath, TagLibMimeConstants.VideoMp4, ReadStyle.None);
            }
            catch (Exception ex2)
            {
                try
                {
                    return File.Create(filePath, TagLibMimeConstants.AudioMpeg, ReadStyle.None);
                }
                catch (Exception ex3)
                {
                    logger.Warning(ex, AppConstants.Logging.DisplayMetadataServiceDiagnosticsLog.FailedToExtractMetadataDefaultCreateFailedForUri, uri);
                    logger.Debug(ex2, AppConstants.Logging.DisplayMetadataServiceDiagnosticsLog.VideoMp4CreateAlsoFailedForUri, uri);
                    logger.Debug(ex3, AppConstants.Logging.DisplayMetadataServiceDiagnosticsLog.AudioMpegCreateAlsoFailedForUri, uri);
                    return null;
                }
            }
        }
    }

    private static string ConvertUriToFilePath(string uri)
    {
        // Convert file:// URI to local path, or use URI as-is if already a file path
        if (!uri.StartsWith(MediaUriSchemeConstants.FilePrefix, StringComparison.OrdinalIgnoreCase))
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
        catch (Exception)
        {
            // If URI parsing fails, try to extract path manually
            // Remove "file://" prefix (or "file:///" on Unix)
            var path = uri[MediaUriSchemeConstants.FilePrefix.Length..];
            if (path.StartsWith("//", StringComparison.Ordinal))
            {
                // UNC path or extra slashes
                path = path.TrimStart('/');
            }
            else if (!path.StartsWith('/') && OperatingSystem.IsWindows())
            {
                // Windows path without leading slash
                return path;
            }
            return "/" + path.TrimStart('/');
        }
    }

    private static MetaData ExtractBasicMetadata(TagLib.Tag tag)
    {
        string? artistMeta = null;
        if (!string.IsNullOrEmpty(tag.FirstPerformer))
        {
            artistMeta = tag.FirstPerformer;
        }
        else if (!string.IsNullOrEmpty(tag.FirstAlbumArtist))
        {
            artistMeta = tag.FirstAlbumArtist;
        }

        string? albumMeta = null;
        if (!string.IsNullOrEmpty(tag.Album))
        {
            albumMeta = tag.Album;
        }

        string titleMeta;
        if (!string.IsNullOrEmpty(tag.Title))
        {
            titleMeta = tag.Title;
        }
        else
        {
            titleMeta = FallbackUnknownTitle;
        }

        return new MetaData
        {
            Title = titleMeta,
            Artist = artistMeta,
            Album = albumMeta
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

