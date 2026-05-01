#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Serilog;

namespace Bible.Alarm.Cataloger.Catalogers;

internal static class MediatorTrackParser
{
    internal static (List<MediatorTrack>? Tracks, string? SectionName) ParseTracksFromGetPubMediaLinks(
        string jsonString,
        string sectionCode,
        string languageCode,
        ILogger logger,
        bool isVideo = false,
        int? trackNumber = null,
        bool useDocidParam = false)
    {
        var tracks = new List<MediatorTrack>();
        string? sectionName = null;
        var formatKey = isVideo ? AppConstants.Media.MediaStreamFormatMp4 : AppConstants.Media.MediaStreamFormatMp3;

        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(AppConstants.Media.PubMediaJson.Files, out var filesElement))
            {
                return (null, null);
            }

            sectionName = TryResolveSectionNameFromPubMediaRoot(root);

            if (!filesElement.TryGetProperty(languageCode, out var languageFiles) ||
                !languageFiles.TryGetProperty(formatKey, out var formatFiles))
            {
                return (null, sectionName);
            }

            var fileFormat = isVideo ? AppConstants.Media.MediaStreamFormatMp4 : AppConstants.Media.MediaStreamFormatMp3;
            var lookUpPathBase = BuildLookUpPathBase(sectionCode, languageCode, fileFormat, useDocidParam);

            foreach (var trackFile in formatFiles.EnumerateArray())
            {
                if (!TryExtractTrackUrl(trackFile, out var url) || string.IsNullOrEmpty(url))
                {
                    continue;
                }

                var title = ResolveTrackTitle(trackFile);
                if (AudioDescriptionTitlePhrases.ContainsAudioDescriptionPhrase(languageCode, title))
                {
                    continue;
                }

                var trackCode = ResolveMediatorTrackCode(sectionCode, trackNumber, useDocidParam);

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
            logger.Error(ex, "Failed to parse tracks from GETPUBMEDIALINKS JSON for section {SectionCode}", sectionCode);
            return (null, null);
        }

        return tracks.Count > 0 ? (tracks, sectionName) : (null, sectionName);
    }

    private static string? TryResolveSectionNameFromPubMediaRoot(JsonElement root)
    {
        if (!root.TryGetProperty(AppConstants.Media.PubMediaJson.PubName, out var pubNameElement))
        {
            return null;
        }

        var rawName = pubNameElement.GetString();
        return MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawName);
    }

    private static string BuildLookUpPathBase(string sectionCode, string languageCode, string fileFormat, bool useDocidParam)
    {
        if (useDocidParam && sectionCode.StartsWith(AppConstants.Media.MediatorIdentifiers.DocIdSectionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var docidValue = sectionCode[AppConstants.Media.MediatorIdentifiers.DocIdSectionPrefix.Length..];
            return $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.DocId}={docidValue}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={fileFormat}&{AppConstants.Media.GetPubQueryAllLangsOff}&{AppConstants.Media.GetPubQueryParamLangWritten}={languageCode}";
        }

        return $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={sectionCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={fileFormat}&{AppConstants.Media.GetPubQueryAllLangsOff}&{AppConstants.Media.GetPubQueryParamLangWritten}={languageCode}";
    }

    private static bool TryExtractTrackUrl(JsonElement trackFile, out string? url)
    {
        url = null;
        if (!trackFile.TryGetProperty(AppConstants.Media.PubMediaJson.File, out var fileElement))
        {
            return false;
        }

        if (fileElement.ValueKind == JsonValueKind.String)
        {
            url = fileElement.GetString();
            return true;
        }

        if (fileElement.ValueKind == JsonValueKind.Object &&
            fileElement.TryGetProperty(AppConstants.Media.PubMediaJson.Url, out var urlElement))
        {
            url = urlElement.GetString();
            return true;
        }

        return false;
    }

    private static string ResolveTrackTitle(JsonElement trackFile)
    {
        if (!trackFile.TryGetProperty(AppConstants.Media.PubMediaJson.Title, out var titleElement))
        {
            return MediaTrackTitleHelper.UnknownTitle;
        }

        if (titleElement.ValueKind == JsonValueKind.String)
        {
            return MediaTrackTitleHelper.DecodeHtmlTitle(titleElement.GetString());
        }

        if (titleElement.ValueKind == JsonValueKind.Object &&
            titleElement.TryGetProperty(AppConstants.Media.PubMediaJson.Text, out var titleTextElement))
        {
            return MediaTrackTitleHelper.DecodeHtmlTitle(titleTextElement.GetString());
        }

        return MediaTrackTitleHelper.UnknownTitle;
    }

    private static string ResolveMediatorTrackCode(string sectionCode, int? trackNumber, bool useDocidParam)
    {
        if (useDocidParam && sectionCode.StartsWith(AppConstants.Media.MediatorIdentifiers.DocIdSectionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var docidSuffix = sectionCode[AppConstants.Media.MediatorIdentifiers.DocIdSectionPrefix.Length..];
            return trackNumber.HasValue ? $"{docidSuffix}-{trackNumber.Value}" : docidSuffix;
        }

        return trackNumber.HasValue ? $"{sectionCode}-{trackNumber.Value}" : sectionCode;
    }
}

