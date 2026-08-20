#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Cataloger.Catalogers;

internal static class MusicTrackCatalogParsing
{
    internal static string BuildMusicCatalogLink(string publicationDownloadCode, string? languageCode)
    {
        var langParam = languageCode == null ? $"&{AppConstants.Media.GetPubQueryParamLangWritten}={AppConstants.Media.DefaultLanguageCode}" : $"&{AppConstants.Media.GetPubQueryParamLangWritten}={languageCode}";
        return $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={publicationDownloadCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatMp3}&{AppConstants.Media.GetPubQueryAllLangsOff}{langParam}";
    }

    internal static int ProcessMusicFiles(
        JsonElement musicFiles,
        string publicationDownloadCode,
        string? languageCode,
        int trackCode,
        List<MusicTrack> musicTracks)
    {
        foreach (var musicFile in musicFiles.EnumerateArray())
        {
            if (!TryExtractMusicTrackData(musicFile, out var url, out var track, out var title))
            {
                continue;
            }

            if (track == 0 || url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || AudioDescriptionTitlePhrases.ContainsAudioDescriptionPhrase(languageCode, title))
            {
                continue;
            }

            var musicTrack = CreateMusicTrack(trackCode.ToString(System.Globalization.CultureInfo.InvariantCulture), track, title, url, publicationDownloadCode, languageCode);
            musicTracks.Add(musicTrack);
            trackCode++;
        }

        return trackCode;
    }

    internal static void SaveMusicTracks(string dir, string file, List<MusicTrack> musicTracks)
    {
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tracksJson = JsonSerializer.Serialize(musicTracks.OrderBy(x => x.Number));
        File.WriteAllText(file, tracksJson);
    }

    private static bool TryExtractMusicTrackData(JsonElement musicFile, out string url, out int track, out string title)
    {
        url = string.Empty;
        track = 0;
        title = "Unknown";

        if (!musicFile.TryGetProperty(AppConstants.Media.PubMediaJson.File, out var fileElement) ||
            !fileElement.TryGetProperty(AppConstants.Media.PubMediaJson.Url, out var urlElement))
        {
            return false;
        }

        url = urlElement.GetString() ?? string.Empty;
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        if (!musicFile.TryGetProperty(AppConstants.Media.PubMediaJson.Track, out var trackElement))
        {
            return false;
        }

        track = trackElement.GetInt32();

        if (musicFile.TryGetProperty(AppConstants.Media.PubMediaJson.Title, out var titleElement))
        {
            var rawTitle = titleElement.ValueKind != JsonValueKind.Undefined ? titleElement.GetString() : null;
            // Decode HTML entities like &nbsp; to proper characters and replace non-breaking spaces with regular spaces
            title = MediaTrackTitleHelper.DecodeHtmlTitle(rawTitle);
        }

        return true;
    }

    private static MusicTrack CreateMusicTrack(
        string trackCode,
        int track,
        string title,
        string url,
        string publicationDownloadCode,
        string? languageCode)
    {
        // LookUpPath is no longer stored in the database - it's computed at runtime
        // Store DownloadCode for melody music that uses disc codes (e.g., "iam-1", "iam-2")
        // Store OriginalTrackCode for melody music - the API expects the track number within that disc
        _ = languageCode; // Reserved for future use (keeps method signature aligned with callers)

        return new MusicTrack
        {
            Number = int.TryParse(trackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsedNum) ? parsedNum : track,
            Title = title,
            Url = url,
            DownloadCode = publicationDownloadCode,
            OriginalTrackCode = track
        };
    }
}

