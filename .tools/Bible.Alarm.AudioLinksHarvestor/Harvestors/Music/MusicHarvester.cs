using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.AudioLinksHarvestor.Models;
using Bible.Alarm.AudioLinksHarvestor.Models.Music;
using Bible.Alarm.AudioLinksHarvestor.Utility;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.AudioLinksHarvestor.Harvestors.Music;

internal class MusicHarvester(ILogger logger, DownloadUtility downloadUtility)
{
    private const int MaxConcurrentLanguageDownloads = 8;

    private static Dictionary<string, string> vocalsPublicationCodeToNameMappings = new([
        new KeyValuePair<string, string>("osg","Original Songs"),
        new KeyValuePair<string, string>("sjjc","\"Sing Out Joyfully\" to Jehovah (2016)"),
        new KeyValuePair<string, string>("snv","Sing to Jehovah (2014) ")
    ]);

    internal async Task HarvestVocalMusicLinks(bool isTestRun = false)
    {
        var languageCodeToNames = new Dictionary<string, string>();
        var languageCodeToPublications = new Dictionary<string, List<string>>();

        foreach (var publication in vocalsPublicationCodeToNameMappings)
        {
            string jsonString;
            try
            {
                var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?booknum=0&output=json&pub={publication.Key}&fileformat=MP3&alllangs=1&langwritten=E&txtCMSLang=E";
                jsonString = await downloadUtility.GetAsync(harvestLink);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
            {
                continue;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to fetch languages for publication {PublicationCode} ({PublicationName}). Skipping.", publication.Key, publication.Value);
                continue;
            }

            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!root.TryGetProperty("languages", out var languages))
            {
                continue;
            }

            var languageEntries = new List<(string Code, string Name)>();
            foreach (var item in languages.EnumerateObject())
            {
                var languageCode = item.Name;

                if (!item.Value.TryGetProperty("name", out var nameElement))
                {
                    continue;
                }

                var language = nameElement.GetString();
                if (string.IsNullOrEmpty(language))
                {
                    continue;
                }

                languageEntries.Add((languageCode, language));
            }

            if (isTestRun)
            {
                var englishEntry = languageEntries.FirstOrDefault(e => e.Code == "E");
                if (englishEntry.Code == "E")
                {
                    logger.Information("TEST RUN: Processing only English language for publication {PublicationCode}", publication.Key);
                    languageEntries = [englishEntry];
                }
                else
                {
                    continue;
                }
            }

            using var semaphore = new SemaphoreSlim(MaxConcurrentLanguageDownloads, MaxConcurrentLanguageDownloads);
            var languageTasks = languageEntries.Select(async entry =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var (languageCode, language) = entry;
                    logger.Information("Harvesting Music track links for {PublicationName} of {Language} language.", publication.Value, language);

                    try
                    {
                        await HarvestMusicLinks(publication.Key, [publication.Key], languageCode);
                        languageCodeToNames[languageCode] = language;

                        lock (languageCodeToPublications)
                        {
                            if (languageCodeToPublications.ContainsKey(languageCode))
                            {
                                languageCodeToPublications[languageCode].Add(publication.Key);
                            }
                            else
                            {
                                languageCodeToPublications[languageCode] = new List<string>([publication.Key]);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Error(e, "Failed: Harvesting Music track links for {PublicationName} of {Language} language.", publication.Value, language);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(languageTasks);

        }

        foreach (var languagePublication in languageCodeToPublications)
        {
            if (!Directory.Exists($"{DirectoryHelper.IndexDirectory}/media/Music/Vocals/{languagePublication.Key}"))
            {
                Directory.CreateDirectory($"{DirectoryHelper.IndexDirectory}/media/Music/Vocals/{languagePublication.Key}");
            }

            File.WriteAllText($"{DirectoryHelper.IndexDirectory}/media/Music/Vocals/{languagePublication.Key}/publications.json", JsonSerializer.Serialize(
             languagePublication.Value.Select(x => new
             Publication
             {
                 Code = x,
                 Name = vocalsPublicationCodeToNameMappings[x]
             }).OrderBy(x => x.Code)));
        }

        File.WriteAllText($"{DirectoryHelper.IndexDirectory}/media/Music/Vocals/languages.json",
        JsonSerializer.Serialize(languageCodeToPublications.Select(x =>
        new Language
        {
            Code = x.Key,
            Name = languageCodeToNames[x.Key]
        }).OrderBy(x => x.Code)));

    }

    private static Dictionary<string, string> melodyPublicationCodeToNameMappings = new([
        new KeyValuePair<string, string>("iam","Sing Praises to Jehovah (1984)")
    ]);

    internal async Task HarvestMusicMelodyLinks(bool isTestRun = false)
    {
        var discs = new List<string>();
        var downloadCodes = new List<string>();

        foreach (var publication in melodyPublicationCodeToNameMappings)
        {
            downloadCodes.Clear();
            if (publication.Key == "iam")
            {
                for (var i = 1; i <= 9; i++)
                {
                    if (i is 7 or 8)
                    {
                        continue;
                    }

                    downloadCodes.Add($"{publication.Key}-{i}");
                }
            }
            else
            {
                downloadCodes.Add(publication.Key);
            }

            logger.Information("Harvesting Music track links for {PublicationName}.", publication.Value);
            await HarvestMusicLinks(publication.Key, downloadCodes);
        }

        File.WriteAllText($"{DirectoryHelper.IndexDirectory}/media/Music/Melodies/publications.json", JsonSerializer.Serialize(
        melodyPublicationCodeToNameMappings.Select(x => new
        Publication
        {
            Code = x.Key,
            Name = x.Value
        }).OrderBy(x => x.Code)));
    }

    private async Task<bool> HarvestMusicLinks(string publicationCode, List<string> publicationDownloadCodes, string languageCode = null)
    {
        var dir = languageCode == null ? $"{DirectoryHelper.IndexDirectory}/media/Music/Melodies/{publicationCode}" :
                                         $"{DirectoryHelper.IndexDirectory}/media/Music/Vocals/{languageCode}/{publicationCode}";
        var file = $"{dir}/tracks.json";

        var trackNumber = 1;
        var musicTracks = new List<MusicTrack>();

        foreach (var publicationDownloadCode in publicationDownloadCodes)
        {
            string jsonString;
            try
            {
                var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationDownloadCode}&fileformat=MP3&alllangs=0{(languageCode == null ? "&langwritten=E" : $"&langwritten={languageCode}")}&txtCMSLang=E";
                jsonString = await downloadUtility.GetAsync(harvestLink);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
            {
                continue;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to fetch tracks for publication {PublicationCode}. Skipping.", publicationDownloadCode);
                continue;
            }

            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var lc = languageCode ?? "E";

            if (!root.TryGetProperty("files", out var filesElement))
            {
                continue;
            }

            if (!filesElement.TryGetProperty(lc, out var languageFiles))
            {
                continue;
            }

            if (!languageFiles.TryGetProperty("MP3", out var musicFiles))
            {
                continue;
            }

            foreach (var musicFile in musicFiles.EnumerateArray())
            {
                if (!musicFile.TryGetProperty("file", out var fileElement) ||
                    !fileElement.TryGetProperty("url", out var urlElement))
                {
                    continue;
                }

                string url = urlElement.GetString();
                if (string.IsNullOrEmpty(url))
                {
                    continue;
                }

                if (!musicFile.TryGetProperty("track", out var trackElement))
                {
                    continue;
                }

                var track = trackElement.GetInt32();

                if (track == 0 || url.EndsWith(".zip"))
                {
                    continue;
                }

                if (!musicFile.TryGetProperty("duration", out var durationElement))
                {
                }
                var duration = durationElement.ValueKind != JsonValueKind.Undefined ? durationElement.GetDouble() : 0.0;

                if (!musicFile.TryGetProperty("title", out var titleElement))
                {
                }
                var title = titleElement.ValueKind != JsonValueKind.Undefined ? titleElement.GetString()! : "Unknown";

                musicTracks.Add(new MusicTrack
                {
                    Number = trackNumber,
                    Title = title,
                    Url = url,
                    LookUpPath = $"?output=json&pub={publicationDownloadCode}&fileformat=MP3" +
                                $"{(languageCode == null ? "&langwritten=E" : $"&langwritten={languageCode}")}" +
                                $"&txtCMSLang=E&track={track}"
                });

                trackNumber++;
            }

        }

        if (musicTracks.Count > 0)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(file, JsonSerializer.Serialize(musicTracks.OrderBy(x => x.Number)));
            return true;
        }

        return false;
    }

}
