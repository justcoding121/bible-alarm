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

            if (root.TryGetProperty(AppConstants.Media.PubMediaJson.PubName, out var pubNameElement))
            {
                var rawName = pubNameElement.GetString();
                sectionName = MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawName);
            }

            if (!filesElement.TryGetProperty(languageCode, out var languageFiles) ||
                !languageFiles.TryGetProperty(formatKey, out var formatFiles))
            {
                return (null, sectionName);
            }

            var fileFormat = isVideo ? AppConstants.Media.MediaStreamFormatMp4 : AppConstants.Media.MediaStreamFormatMp3;
            string lookUpPathBase;
            if (useDocidParam && sectionCode.StartsWith(AppConstants.Media.MediatorIdentifiers.DocIdSectionPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var docidValue = sectionCode[AppConstants.Media.MediatorIdentifiers.DocIdSectionPrefix.Length..];
                lookUpPathBase = $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.DocId}={docidValue}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={fileFormat}&{AppConstants.Media.GetPubQueryAllLangsOff}&{AppConstants.Media.GetPubQueryParamLangWritten}={languageCode}";
            }
            else
            {
                lookUpPathBase = $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={sectionCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={fileFormat}&{AppConstants.Media.GetPubQueryAllLangsOff}&{AppConstants.Media.GetPubQueryParamLangWritten}={languageCode}";
            }

            foreach (var trackFile in formatFiles.EnumerateArray())
            {
                if (!trackFile.TryGetProperty(AppConstants.Media.PubMediaJson.File, out var fileElement))
                {
                    continue;
                }

                string? url = null;
                if (fileElement.ValueKind == JsonValueKind.String)
                {
                    url = fileElement.GetString();
                }
                else if (fileElement.ValueKind == JsonValueKind.Object && fileElement.TryGetProperty(AppConstants.Media.PubMediaJson.Url, out var urlElement))
                {
                    url = urlElement.GetString();
                }

                if (string.IsNullOrEmpty(url))
                {
                    continue;
                }

                var title = MediaTrackTitleHelper.UnknownTitle;
                if (trackFile.TryGetProperty(AppConstants.Media.PubMediaJson.Title, out var titleElement))
                {
                    if (titleElement.ValueKind == JsonValueKind.String)
                    {
                        title = MediaTrackTitleHelper.DecodeHtmlTitle(titleElement.GetString());
                    }
                    else if (titleElement.ValueKind == JsonValueKind.Object && titleElement.TryGetProperty(AppConstants.Media.PubMediaJson.Text, out var titleTextElement))
                    {
                        title = MediaTrackTitleHelper.DecodeHtmlTitle(titleTextElement.GetString());
                    }
                }

                if (AudioDescriptionTitlePhrases.ContainsAudioDescriptionPhrase(languageCode, title))
                {
                    continue;
                }

                string trackCode;
                if (useDocidParam && sectionCode.StartsWith(AppConstants.Media.MediatorIdentifiers.DocIdSectionPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var docidSuffix = sectionCode[AppConstants.Media.MediatorIdentifiers.DocIdSectionPrefix.Length..];
                    trackCode = trackNumber.HasValue ? $"{docidSuffix}-{trackNumber.Value}" : docidSuffix;
                }
                else
                    trackCode = trackNumber.HasValue ? $"{sectionCode}-{trackNumber.Value}" : sectionCode;
                var lookUpPath = lookUpPathBase;

                tracks.Add(new MediatorTrack
                {
                    TrackCode = trackCode,
                    Title = title,
                    Url = url,
                    LookUpPath = lookUpPath
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
}

