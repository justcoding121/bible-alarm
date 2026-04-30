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

            if (category.TryGetProperty(AppConstants.Media.PubMediaJson.Name, out var nameElement))
            {
                var rawName = nameElement.GetString();
                var categoryName = MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawName);
                if (category.TryGetProperty(AppConstants.Media.PubMediaJson.ParentCategory, out var parentCategoryElement) &&
                    parentCategoryElement.TryGetProperty(AppConstants.Media.PubMediaJson.Name, out var parentNameElement))
                {
                    var rawParent = parentNameElement.GetString();
                    var parentName = MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawParent);
                    if (!string.IsNullOrEmpty(parentName) && !string.IsNullOrEmpty(categoryName))
                        localizedPublicationName = $"{parentName} {categoryName}";
                    else if (!string.IsNullOrEmpty(categoryName))
                        localizedPublicationName = categoryName;
                }
                else if (!string.IsNullOrEmpty(categoryName))
                {
                    localizedPublicationName = categoryName;
                }
            }

            if (!category.TryGetProperty(AppConstants.Media.PubMediaJson.Media, out var mediaArray))
            {
                return (tracks, localizedPublicationName);
            }

            var categoryKey = publicationCode;
            var lookUpPathBase = $"?{AppConstants.Media.MediatorQueryParamName.Category}={categoryKey}&{AppConstants.Media.MediatorQueryParamName.Lang}={languageCode}";
            var index = 0;

            foreach (var mediaItem in mediaArray.EnumerateArray())
            {
                if (!mediaItem.TryGetProperty(AppConstants.Media.PubMediaJson.NaturalKey, out var _))
                    continue;

                if (!mediaItem.TryGetProperty(AppConstants.Media.PubMediaJson.Files, out var filesElement) || filesElement.ValueKind != JsonValueKind.Array)
                    continue;

                string? url = null;
                foreach (var file in filesElement.EnumerateArray())
                {
                    if (file.TryGetProperty(AppConstants.Media.PubMediaJson.ProgressiveDownloadUrl, out var urlEl))
                    {
                        url = urlEl.GetString();
                        break;
                    }
                }

                if (string.IsNullOrEmpty(url))
                    continue;

                var title = MediaTrackTitleHelper.UnknownTitle;
                if (mediaItem.TryGetProperty(AppConstants.Media.PubMediaJson.Title, out var titleElement))
                {
                    title = MediaTrackTitleHelper.DecodeHtmlTitle(titleElement.GetString());
                }

                index++;
                var trackCode = index.ToString();
                tracks.Add(new MediatorTrack
                {
                    TrackCode = trackCode,
                    Title = title,
                    Url = url,
                    LookUpPath = lookUpPathBase
                });
            }
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

            string? categoryName = null;
            string? parentCategoryName = null;

            if (category.TryGetProperty(AppConstants.Media.PubMediaJson.Name, out var nameElement))
            {
                var rawName = nameElement.GetString();
                categoryName = MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawName);
            }

            if (category.TryGetProperty(AppConstants.Media.PubMediaJson.ParentCategory, out var parentCategoryElement) &&
                parentCategoryElement.TryGetProperty(AppConstants.Media.PubMediaJson.Name, out var parentNameElement))
            {
                var rawParentName = parentNameElement.GetString();
                parentCategoryName = MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawParentName);
            }

            if (!string.IsNullOrEmpty(parentCategoryName) && !string.IsNullOrEmpty(categoryName))
            {
                localizedPublicationName = $"{parentCategoryName} {categoryName}";
            }
            else if (!string.IsNullOrEmpty(categoryName))
            {
                localizedPublicationName = categoryName;
            }

            if (!category.TryGetProperty(AppConstants.Media.PubMediaJson.Media, out var mediaArray))
            {
                return (mediaItems, localizedPublicationName);
            }

            foreach (var mediaItem in mediaArray.EnumerateArray())
            {
                if (!mediaItem.TryGetProperty(AppConstants.Media.PubMediaJson.NaturalKey, out var naturalKeyElement))
                {
                    continue;
                }

                var naturalKey = naturalKeyElement.GetString() ?? "";
                if (naturalKey.StartsWith(AppConstants.Media.MediatorIdentifiers.DocIdNaturalKeyPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var parts = naturalKey.Split('_');
                    if (parts.Length < 3)
                    {
                        continue;
                    }

                    var docidValue = parts[0][AppConstants.Media.MediatorIdentifiers.DocIdNaturalKeyPrefix.Length..];
                    if (string.IsNullOrEmpty(docidValue) || !int.TryParse(docidValue, out _))
                    {
                        continue;
                    }

                    if (!int.TryParse(parts[2], out var docidTrack) || docidTrack < 1)
                    {
                        continue;
                    }

                    mediaItems.Add(($"{AppConstants.Media.MediatorIdentifiers.DocIdSectionPrefix}{docidValue}", docidTrack));
                    continue;
                }

                if (!naturalKey.StartsWith(AppConstants.Media.MediatorIdentifiers.PubNaturalKeyPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var pubParts = naturalKey.Split('_');
                if (pubParts.Length < 3)
                {
                    continue;
                }

                var sectionCode = pubParts[0][AppConstants.Media.MediatorIdentifiers.PubNaturalKeyPrefix.Length..];
                if (string.IsNullOrEmpty(sectionCode))
                {
                    continue;
                }

                var trackPart = pubParts[2];
                if (!int.TryParse(trackPart, out var trackNumber) || trackNumber < 1)
                {
                    if (string.Equals(trackPart, "x", StringComparison.OrdinalIgnoreCase))
                    {
                        trackNumber = 1;
                    }
                    else
                    {
                        logger.Warning("Could not parse track number from naturalKey in publication {PublicationCode} for language {LanguageCode}. Skipping.", publicationCode, languageCode);
                        continue;
                    }
                }

                mediaItems.Add((sectionCode, trackNumber));
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to extract media items from category JSON");
        }

        return (mediaItems, localizedPublicationName);
    }
}

