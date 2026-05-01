#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Serilog;

namespace Bible.Alarm.Cataloger.Catalogers;

internal static class MediatorSectionCodeExtractor
{
    /// <summary>
    /// Extracts tracks with CDN URLs directly from the mediator response. Use this for mediator publications;
    /// do not call GETPUBMEDIALINKS when using mediator.
    /// </summary>
    internal static (List<MediatorTrack> Tracks, string? LocalizedPublicationName) ExtractTracksFromMediatorCategory(
        string jsonString,
        string publicationCode,
        string languageCode,
        ILogger logger)
    {
        var tracks = new List<MediatorTrack>();
        string? localizedPublicationName = null;

        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (!root.TryGetProperty(AppConstants.Media.PubMediaJson.Category, out var category))
            {
                return (tracks, null);
            }

            localizedPublicationName = ResolveLocalizedPublicationNameFromCategory(category);

            if (!category.TryGetProperty(AppConstants.Media.PubMediaJson.CategoryMedia, out var mediaArray))
            {
                return (tracks, localizedPublicationName);
            }

            var categoryKey = publicationCode;
            var lookUpPathBase = $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.MediatorQueryParamName.Category}={categoryKey}&{AppConstants.Media.MediatorQueryParamName.Lang}={languageCode}";
            AppendMediatorTracksFromCategoryMedia(mediaArray, lookUpPathBase, tracks);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to extract tracks from mediator category JSON");
        }

        return (tracks, localizedPublicationName);
    }

    internal static (List<(string SectionCode, int TrackNumber)> MediaItems, string? LocalizedPublicationName) ExtractMediaItemsFromCategory(
        string jsonString,
        string publicationCode,
        string languageCode,
        ILogger logger)
    {
        var mediaItems = new List<(string SectionCode, int TrackNumber)>();
        string? localizedPublicationName = null;

        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (!root.TryGetProperty(AppConstants.Media.PubMediaJson.Category, out var category))
            {
                return (mediaItems, null);
            }

            localizedPublicationName = ResolveLocalizedPublicationNameFromCategory(category);

            if (!category.TryGetProperty(AppConstants.Media.PubMediaJson.CategoryMedia, out var mediaArray))
            {
                return (mediaItems, localizedPublicationName);
            }

            AppendMediaItemsFromCategoryMedia(mediaArray, publicationCode, languageCode, logger, mediaItems);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to extract media items from category JSON");
        }

        return (mediaItems, localizedPublicationName);
    }

    private static string? ResolveLocalizedPublicationNameFromCategory(JsonElement category)
    {
        if (!category.TryGetProperty(AppConstants.Media.PubMediaJson.Name, out var nameElement))
        {
            return null;
        }

        var categoryName = MediaTrackTitleHelper.DecodeHtmlTitleNullable(nameElement.GetString());
        if (category.TryGetProperty(AppConstants.Media.PubMediaJson.ParentCategory, out var parentCategoryElement) &&
            parentCategoryElement.TryGetProperty(AppConstants.Media.PubMediaJson.Name, out var parentNameElement))
        {
            var parentName = MediaTrackTitleHelper.DecodeHtmlTitleNullable(parentNameElement.GetString());
            if (!string.IsNullOrEmpty(parentName) && !string.IsNullOrEmpty(categoryName))
            {
                return $"{parentName} {categoryName}";
            }

            if (!string.IsNullOrEmpty(categoryName))
            {
                return categoryName;
            }

            return null;
        }

        return string.IsNullOrEmpty(categoryName) ? null : categoryName;
    }

    private static void AppendMediatorTracksFromCategoryMedia(
        JsonElement mediaArray,
        string lookUpPathBase,
        List<MediatorTrack> tracks)
    {
        var index = 0;
        foreach (var mediaItem in mediaArray.EnumerateArray())
        {
            if (!mediaItem.TryGetProperty(AppConstants.Media.PubMediaJson.NaturalKey, out _))
            {
                continue;
            }

            if (!mediaItem.TryGetProperty(AppConstants.Media.PubMediaJson.Files, out var filesElement) ||
                filesElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            if (!TryGetFirstProgressiveDownloadUrl(filesElement, out var url) || string.IsNullOrEmpty(url))
            {
                continue;
            }

            var title = MediaTrackTitleHelper.UnknownTitle;
            if (mediaItem.TryGetProperty(AppConstants.Media.PubMediaJson.Title, out var titleElement))
            {
                title = MediaTrackTitleHelper.DecodeHtmlTitle(titleElement.GetString());
            }

            index++;
            tracks.Add(new MediatorTrack
            {
                TrackCode = index.ToString(),
                Title = title,
                Url = url,
                LookUpPath = lookUpPathBase
            });
        }
    }

    private static bool TryGetFirstProgressiveDownloadUrl(JsonElement filesElement, out string? url)
    {
        foreach (var file in filesElement.EnumerateArray())
        {
            if (file.TryGetProperty(AppConstants.Media.PubMediaJson.ProgressiveDownloadUrl, out var urlEl))
            {
                url = urlEl.GetString();
                return true;
            }
        }

        url = null;
        return false;
    }

    private static void AppendMediaItemsFromCategoryMedia(
        JsonElement mediaArray,
        string publicationCode,
        string languageCode,
        ILogger logger,
        List<(string SectionCode, int TrackNumber)> mediaItems)
    {
        foreach (var mediaItem in mediaArray.EnumerateArray())
        {
            if (!mediaItem.TryGetProperty(AppConstants.Media.PubMediaJson.NaturalKey, out var naturalKeyElement))
            {
                continue;
            }

            var naturalKey = naturalKeyElement.GetString() ?? "";
            if (TryAppendDocIdNaturalKeyMediaItem(naturalKey, mediaItems))
            {
                continue;
            }

            TryAppendPubNaturalKeyMediaItem(naturalKey, publicationCode, languageCode, logger, mediaItems);
        }
    }

    private static bool TryAppendDocIdNaturalKeyMediaItem(string naturalKey, List<(string SectionCode, int TrackNumber)> mediaItems)
    {
        if (!naturalKey.StartsWith(AppConstants.Media.MediatorIdentifiers.DocIdNaturalKeyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = naturalKey.Split('_', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return true;
        }

        var docidValue = parts[0][AppConstants.Media.MediatorIdentifiers.DocIdNaturalKeyPrefix.Length..];
        if (string.IsNullOrEmpty(docidValue) || !int.TryParse(docidValue, out _))
        {
            return true;
        }

        if (!int.TryParse(parts[2], out var docidTrack) || docidTrack < 1)
        {
            return true;
        }

        mediaItems.Add(($"{AppConstants.Media.MediatorIdentifiers.DocIdSectionPrefix}{docidValue}", docidTrack));
        return true;
    }

    private static void TryAppendPubNaturalKeyMediaItem(
        string naturalKey,
        string publicationCode,
        string languageCode,
        ILogger logger,
        List<(string SectionCode, int TrackNumber)> mediaItems)
    {
        if (!naturalKey.StartsWith(AppConstants.Media.MediatorIdentifiers.PubNaturalKeyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var pubParts = naturalKey.Split('_', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (pubParts.Length < 3)
        {
            return;
        }

        var sectionCode = pubParts[0][AppConstants.Media.MediatorIdentifiers.PubNaturalKeyPrefix.Length..];
        if (string.IsNullOrEmpty(sectionCode))
        {
            return;
        }

        var trackPart = pubParts[2];
        if (!TryResolvePubNaturalKeyTrackNumber(trackPart, publicationCode, languageCode, logger, out var trackNumber))
        {
            return;
        }

        mediaItems.Add((sectionCode, trackNumber));
    }

    private static bool TryResolvePubNaturalKeyTrackNumber(
        string trackPart,
        string publicationCode,
        string languageCode,
        ILogger logger,
        out int trackNumber)
    {
        if (int.TryParse(trackPart, out trackNumber) && trackNumber >= 1)
        {
            return true;
        }

        if (string.Equals(trackPart, "x", StringComparison.OrdinalIgnoreCase))
        {
            trackNumber = 1;
            return true;
        }

        logger.Warning("Could not parse track number from naturalKey in publication {PublicationCode} for language {LanguageCode}. Skipping.", publicationCode, languageCode);
        trackNumber = 0;
        return false;
    }
}

