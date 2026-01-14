#nullable enable
using System.Text;
using System.Text.Json;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Serilog;

namespace Bible.Alarm.Services.Media;

public sealed class MediaUrlRefreshService(ILogger logger, IDownloadService downloadService) : IMediaUrlRefreshService
{
    public async Task<string?> RefreshUrlAsync(TrackMetadata trackMetadata)
    {
        try
        {
            var lookUpPath = trackMetadata.LookUpPath;
            if (string.IsNullOrEmpty(lookUpPath))
            {
                return null;
            }

            var playType = trackMetadata.PlayType;
            if (playType == PlayType.Bible)
            {
                // Check if this is a drama (uses Mediator API)
                // Note: Videos like "gnj" are non-sectioned but use GETPUBMEDIALINKS, not Mediator API
                if (PublicationTypeHelper.IsDrama(trackMetadata.PublicationCode) && 
                    !PublicationTypeHelper.IsVideo(trackMetadata.PublicationCode))
                {
                    return await GetDramaTrackUrl(
                        trackMetadata.PublicationCode,
                        trackMetadata.LanguageCode,
                        trackMetadata.TrackNumber,
                        trackMetadata.NaturalKey);
                }
                
                // Traditional Bible publication or video (uses GETPUBMEDIALINKS)
                return await GetBiblePublicationTrackUrl(
                    trackMetadata.LanguageCode,
                    trackMetadata.PublicationCode,
                    trackMetadata.SectionNumber,
                    trackMetadata.TrackNumber,
                    lookUpPath);
            }

            // Music (uses GETPUBMEDIALINKS)
            return await GetMusicTrackUrl(
                trackMetadata.LanguageCode,
                lookUpPath);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to refresh URL using LookUpPath");
            return null;
        }
    }

    public async Task<string?> GetBiblePublicationTrackUrl(string languageCode, string pubCode, int sectionNumber, int track,
        string lookUpPath)
    {
        try
        {
            var harvestLink = $"{UrlHelper.JwOrgIndexServiceBaseUrl}{lookUpPath}";
            var tes = await downloadService.DownloadAsync(harvestLink);
            var jsonString = Encoding.Default.GetString(tes);
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            // Check if root is an object, not an array
            if (root.ValueKind != JsonValueKind.Object)
            {
                logger.Warning("Root element is not an object (type: {ValueKind}) for Bible track URL refresh", root.ValueKind);
                return null;
            }

            if (!root.TryGetProperty("files", out var files))
            {
                logger.Warning("'files' property not found in JSON response for Bible track URL refresh");
                return null;
            }

            if (!files.TryGetProperty(languageCode, out var languageFiles))
            {
                logger.Warning("Language '{LanguageCode}' not found in files for Bible track URL refresh", languageCode);
                return null;
            }

            // For videos (sectionNumber == 0), try MP4 first, then MP3 as fallback
            // For regular Bible publications, use MP3
            JsonElement? fileArray = null;
            string fileFormat = "MP3";
            
            if (sectionNumber == 0)
            {
                // Video - try MP4 first
                if (languageFiles.TryGetProperty("MP4", out var mp4Files) && mp4Files.ValueKind == JsonValueKind.Array)
                {
                    fileArray = mp4Files;
                    fileFormat = "MP4";
                }
                else if (languageFiles.TryGetProperty("MP3", out var mp3Files) && mp3Files.ValueKind == JsonValueKind.Array)
                {
                    fileArray = mp3Files;
                    fileFormat = "MP3";
                }
            }
            else
            {
                // Regular Bible publication - use MP3
                if (languageFiles.TryGetProperty("MP3", out var mp3Files) && mp3Files.ValueKind == JsonValueKind.Array)
                {
                    fileArray = mp3Files;
                    fileFormat = "MP3";
                }
            }

            if (!fileArray.HasValue)
            {
                logger.Warning("'{FileFormat}' property not found or not an array for language '{LanguageCode}' in Bible track URL refresh", fileFormat, languageCode);
                return null;
            }

            var fileArrayValue = fileArray.Value;
            var fileList = fileArrayValue.EnumerateArray().ToList();
            if (fileList.Count == 0)
            {
                logger.Warning("No {FileFormat} files found for language '{LanguageCode}' in Bible track URL refresh", fileFormat, languageCode);
                return null;
            }

            // Find the file with matching track number (harvester extracts track from "track" property)
            JsonElement? selectedFile = null;
            if (fileFormat == "MP4")
            {
                // For MP4 (videos), find by track number first, then prefer 240p quality
                foreach (var file in fileList)
                {
                    if (file.TryGetProperty("track", out var trackElement))
                    {
                        var fileTrackNumber = trackElement.GetInt32();
                        if (fileTrackNumber == track)
                        {
                            // Found matching track - check for preferred quality
                            if (file.TryGetProperty("label", out var labelElement))
                            {
                                var label = labelElement.GetString();
                                if (label == "240p")
                                {
                                    selectedFile = file;
                                    break;
                                }
                            }
                            // Store first matching track (will use if no 240p found)
                            selectedFile ??= file;
                        }
                    }
                }
                // If no track match found, fall back to first file with 240p quality
                if (!selectedFile.HasValue)
                {
                    foreach (var file in fileList)
                    {
                        if (file.TryGetProperty("label", out var labelElement))
                        {
                            var label = labelElement.GetString();
                            if (label == "240p")
                            {
                                selectedFile = file;
                                break;
                            }
                        }
                        selectedFile ??= file;
                    }
                }
            }
            else
            {
                // For MP3 (Bible publications), find by track number
                foreach (var file in fileList)
                {
                    if (file.TryGetProperty("track", out var trackElement))
                    {
                        var fileTrackNumber = trackElement.GetInt32();
                        if (fileTrackNumber == track)
                        {
                            selectedFile = file;
                            break;
                        }
                    }
                }
                // If no track match found, use first file (fallback)
                if (!selectedFile.HasValue)
                {
                    selectedFile = fileList[0];
                }
            }

            if (!selectedFile.HasValue)
            {
                logger.Warning("No suitable {FileFormat} file found for language '{LanguageCode}' in Bible track URL refresh", fileFormat, languageCode);
                return null;
            }

            var fileElement = selectedFile.Value;
            if (!fileElement.TryGetProperty("file", out var fileInfo) ||
                !fileInfo.TryGetProperty("url", out var urlElement))
            {
                logger.Warning("Missing 'file.url' property in {FileFormat} file for Bible track URL refresh", fileFormat);
                return null;
            }

            var url = urlElement.GetString();
            if (string.IsNullOrEmpty(url))
            {
                logger.Warning("URL is null or empty in {FileFormat} file for Bible track URL refresh", fileFormat);
                return null;
            }

            return url;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Exception in GetBiblePublicationTrackUrl for language '{LanguageCode}', section {SectionNumber}, track {Track}", languageCode, sectionNumber, track);
            return null;
        }
    }

    public async Task<string?> GetMusicTrackUrl(string languageCode, string lookUpPath)
    {
        try
        {
            var harvestLink = $"{UrlHelper.JwOrgIndexServiceBaseUrl}{lookUpPath}";
            var tes = await downloadService.DownloadAsync(harvestLink);
            var jsonString = Encoding.Default.GetString(tes);
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            // Check if root is an object, not an array
            if (root.ValueKind != JsonValueKind.Object)
            {
                logger.Warning("Root element is not an object (type: {ValueKind}) for music track URL refresh", root.ValueKind);
                return null;
            }

            // For melody music, languageCode may be null or empty - default to "E"
            var lc = string.IsNullOrEmpty(languageCode) ? AppConstants.Media.DefaultLanguageCode : languageCode;

            if (lc == AppConstants.Media.LanguageCodePatchFrom)
            {
                lc = AppConstants.Media.LanguageCodePatchTo;
            }

            if (!root.TryGetProperty("files", out var files))
            {
                logger.Warning("'files' property not found in JSON response for music track URL refresh");
                return null;
            }

            if (!files.TryGetProperty(lc, out var languageFiles))
            {
                logger.Warning("Language '{LanguageCode}' not found in files for music track URL refresh", lc);
                return null;
            }

            if (!languageFiles.TryGetProperty("MP3", out var mp3Files))
            {
                logger.Warning("'MP3' property not found for language '{LanguageCode}' in music track URL refresh", lc);
                return null;
            }

            // Check if MP3 is an array
            if (mp3Files.ValueKind != JsonValueKind.Array)
            {
                logger.Warning("'MP3' property is not an array (type: {ValueKind}) for language '{LanguageCode}' in music track URL refresh", mp3Files.ValueKind, lc);
                return null;
            }

            var mp3Array = mp3Files.EnumerateArray().ToList();
            if (mp3Array.Count == 0)
            {
                logger.Warning("No MP3 files found for language '{LanguageCode}' in music track URL refresh", lc);
                return null;
            }

            var firstMp3 = mp3Array[0];
            if (!firstMp3.TryGetProperty("file", out var fileElement) ||
                !fileElement.TryGetProperty("url", out var urlElement))
            {
                logger.Warning("Missing 'file.url' property in MP3 file for music track URL refresh");
                return null;
            }

            var url = urlElement.GetString();
            if (string.IsNullOrEmpty(url))
            {
                logger.Warning("URL is null or empty in MP3 file for music track URL refresh");
                return null;
            }

            return url;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Exception in GetMusicTrackUrl for language '{LanguageCode}'", languageCode ?? "null");
            return null;
        }
    }

    public async Task<string?> GetDramaTrackUrl(string categoryKey, string languageCode, int trackNumber, string? naturalKey = null)
    {
        try
        {
            // Use Mediator API for dramas
            var mediatorUrl = $"{AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrl}/categories/{languageCode}/{categoryKey}?detailed=1";
            
            var responseBytes = await downloadService.DownloadAsync(mediatorUrl);
            var jsonString = Encoding.Default.GetString(responseBytes);
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                logger.Warning("Root element is not an object (type: {ValueKind}) for drama track URL refresh", root.ValueKind);
                return null;
            }

            // The API response has category.media array, not items
            if (!root.TryGetProperty("category", out var categoryElement))
            {
                logger.Warning("'category' property not found in drama category response");
                return null;
            }

            if (!categoryElement.TryGetProperty("media", out var mediaArray) || mediaArray.ValueKind != JsonValueKind.Array)
            {
                logger.Warning("'category.media' property not found or not an array in drama category response");
                return null;
            }

            // Find the matching track
            // The harvester assigns track numbers sequentially (1, 2, 3...) as it iterates through the media array
            // So we need to match by position in the array, not by a "track" property
            int currentTrackNumber = 0;
            foreach (var mediaItem in mediaArray.EnumerateArray())
            {
                // Skip audio descriptions (same logic as harvester)
                if (mediaItem.TryGetProperty("title", out var titleElement))
                {
                    var title = titleElement.GetString();
                    if (!string.IsNullOrEmpty(title) && title.Contains("audio descriptions", StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // Skip this item, don't increment track number
                    }
                }

                currentTrackNumber++; // Increment track number for valid items

                // Try to get natural key for more precise matching
                string? itemNaturalKey = null;
                if (mediaItem.TryGetProperty("naturalKey", out var naturalKeyElement))
                {
                    itemNaturalKey = naturalKeyElement.GetString();
                }

                // Match by naturalKey if available, otherwise by sequential track number
                bool matches = false;
                if (!string.IsNullOrEmpty(naturalKey) && !string.IsNullOrEmpty(itemNaturalKey))
                {
                    matches = itemNaturalKey.Equals(naturalKey, StringComparison.OrdinalIgnoreCase);
                }
                else if (currentTrackNumber == trackNumber)
                {
                    matches = true;
                }

                if (!matches)
                {
                    continue;
                }

                // Get the first MP3 file URL
                if (!mediaItem.TryGetProperty("files", out var filesArray) || filesArray.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var file in filesArray.EnumerateArray())
                {
                    if (!file.TryGetProperty("mimetype", out var mimeType))
                    {
                        continue;
                    }

                    if (mimeType.GetString() == "audio/mpeg")
                    {
                        if (file.TryGetProperty("progressiveDownloadURL", out var urlElement))
                        {
                            var url = urlElement.GetString();
                            if (!string.IsNullOrEmpty(url))
                            {
                                return url;
                            }
                        }
                    }
                }
            }

            logger.Warning("No matching MP3 file found for drama track. Category: {CategoryKey}, Language: {LanguageCode}, Track: {TrackNumber}", 
                categoryKey, languageCode, trackNumber);
            return null;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Exception in GetDramaTrackUrl for category '{CategoryKey}', language '{LanguageCode}', track {TrackNumber}", 
                categoryKey, languageCode, trackNumber);
            return null;
        }
    }

}

