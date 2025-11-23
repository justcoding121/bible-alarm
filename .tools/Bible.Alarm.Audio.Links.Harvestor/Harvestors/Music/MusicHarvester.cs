using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Audio.Links.Harvestor.Models;
using Bible.Alarm.Audio.Links.Harvestor.Models.Music;
using Bible.Alarm.Audio.Links.Harvestor.Utility;
using Bible.Alarm.Shared.Constants;
using System.Text.Json;

namespace Bible.Alarm.Audio.Links.Harvestor.Harvestors.Music
{
    internal class MusicHarverster
    {
        // Maximum concurrent language downloads to avoid overwhelming network/CPU
        private const int MaxConcurrentLanguageDownloads = 8;

        private static Dictionary<string, string> vocalsPublicationCodeToNameMappings = new Dictionary<string, string>(new[]{
            new KeyValuePair<string, string>("osg","Original Songs"),
            new KeyValuePair<string, string>("sjjc","\"Sing Out Joyfully\" to Jehovah (2016)"),
            new KeyValuePair<string, string>("snv","Sing to Jehovah (2014) ")
        });

        internal async static Task Harvest_Vocal_Music_Links(bool isTestRun = false)
        {
            var languageCodeToNames = new Dictionary<string, string>();
            var languageCodeToPublications = new Dictionary<string, List<string>>();

            foreach (var publication in vocalsPublicationCodeToNameMappings)
            {
                var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?booknum=0&output=json&pub={publication.Key}&fileformat=MP3&alllangs=1&langwritten=E&txtCMSLang=E";

                var jsonString = await DownloadUtility.GetAsync(harvestLink);
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;
                
                // Check if root is an object, not an array
                if (root.ValueKind != JsonValueKind.Object)
                {
                    Console.WriteLine($"Warning: Root element is not an object (type: {root.ValueKind}) for publication {publication.Key}. Skipping.");
                    continue;
                }
                
                if (!root.TryGetProperty("languages", out var languages))
                {
                    Console.WriteLine($"Warning: 'languages' property not found in response for publication {publication.Key}. Skipping.");
                    continue;
                }

                // Collect all languages first
                var languageEntries = new List<(string Code, string Name)>();
                foreach (var item in languages.EnumerateObject())
                {
                    var languageCode = item.Name;
                    
                    if (!item.Value.TryGetProperty("name", out var nameElement))
                    {
                        Console.WriteLine($"Warning: 'name' property not found for language {languageCode}. Skipping.");
                        continue;
                    }
                    
                    var language = nameElement.GetString();
                    if (string.IsNullOrEmpty(language))
                    {
                        Console.WriteLine($"Warning: Language name is null or empty for language {languageCode}. Skipping.");
                        continue;
                    }

                    languageEntries.Add((languageCode, language));
                }

                // In test run mode, only process English ("E") language
                if (isTestRun)
                {
                    var englishEntry = languageEntries.FirstOrDefault(e => e.Code == "E");
                    if (englishEntry.Code == "E")
                    {
                        Console.WriteLine($"TEST RUN: Processing only English language for publication {publication.Key}");
                        languageEntries = new List<(string Code, string Name)> { englishEntry };
                    }
                    else
                    {
                        Console.WriteLine($"TEST RUN: English language not found for publication {publication.Key}. Skipping.");
                        continue;
                    }
                }

                // Process languages in parallel with throttling
                using var semaphore = new SemaphoreSlim(MaxConcurrentLanguageDownloads, MaxConcurrentLanguageDownloads);
                var languageTasks = languageEntries.Select(async entry =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var (languageCode, language) = entry;
                        Console.WriteLine($"Harvesting Music track links for {publication.Value} of {language} language.");

                        try
                        {
                            await harvestMusicLinks(publication.Key, new List<string>([publication.Key]), languageCode);
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
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Failed: Harvesting Music track links for {publication.Value} of {language} language. Exception: {e}");
                            Console.ForegroundColor = ConsoleColor.White;
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

        private static Dictionary<string, string> melodyPublicationCodeToNameMappings = new Dictionary<string, string>(new[]{
            new KeyValuePair<string, string>("iam","Sing Praises to Jehovah (1984)")
        });

        internal async static Task Harvest_Music_Melody_Links(bool isTestRun = false)
        {
            var discs = new List<string>();
            var downloadCodes = new List<string>();

            foreach (var publication in melodyPublicationCodeToNameMappings)
            {
                downloadCodes.Clear();
                //multiple discs for 1984 melodies
                if (publication.Key == "iam")
                {
                    for (var i = 1; i <= 9; i++)
                    {
                        //we don't have these discs
                        if (i is 7 or 8)
                            continue;

                        downloadCodes.Add($"{publication.Key}-{i}");
                    }
                }
                else
                {
                    downloadCodes.Add(publication.Key);
                }

                Console.WriteLine($"Harvesting Music track links for {publication.Value}.");
                await harvestMusicLinks(publication.Key, downloadCodes);
            }

            File.WriteAllText($"{DirectoryHelper.IndexDirectory}/media/Music/Melodies/publications.json", JsonSerializer.Serialize(
            melodyPublicationCodeToNameMappings.Select(x => new
            Publication
            {
                Code = x.Key,
                Name = x.Value
            }).OrderBy(x => x.Code)));
        }

        private static async Task<bool> harvestMusicLinks(string publicationCode, List<string> publicationDownloadCodes, string languageCode = null)
        {
            var dir = languageCode == null ? $"{DirectoryHelper.IndexDirectory}/media/Music/Melodies/{publicationCode}" :
                                             $"{DirectoryHelper.IndexDirectory}/media/Music/Vocals/{languageCode}/{publicationCode}";
            var file = $"{dir}/tracks.json";

            var trackNumber = 1;
            var musicTracks = new List<MusicTrack>();

            foreach (var publicationDownloadCode in publicationDownloadCodes)
            {
                var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationDownloadCode}&fileformat=MP3&alllangs=0{(languageCode == null ? "&langwritten=E" : $"&langwritten={languageCode}")}&txtCMSLang=E";

                var jsonString = await DownloadUtility.GetAsync(harvestLink);

                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;

                // Check if root is an object, not an array
                if (root.ValueKind != JsonValueKind.Object)
                {
                    Console.WriteLine($"Warning: Root element is not an object (type: {root.ValueKind}) for publication {publicationDownloadCode}. Skipping.");
                    continue;
                }

                var lc = languageCode ?? "E";

                if (!root.TryGetProperty("files", out var filesElement))
                {
                    Console.WriteLine($"Warning: 'files' property not found for publication {publicationDownloadCode}. Skipping.");
                    continue;
                }

                if (!filesElement.TryGetProperty(lc, out var languageFiles))
                {
                    Console.WriteLine($"Warning: Language '{lc}' not found in files for publication {publicationDownloadCode}. Skipping.");
                    continue;
                }

                if (!languageFiles.TryGetProperty("MP3", out var musicFiles))
                {
                    Console.WriteLine($"Warning: 'MP3' property not found for language '{lc}' in publication {publicationDownloadCode}. Skipping.");
                    continue;
                }

                foreach (var musicFile in musicFiles.EnumerateArray())
                {
                    if (!musicFile.TryGetProperty("file", out var fileElement) ||
                        !fileElement.TryGetProperty("url", out var urlElement))
                    {
                        Console.WriteLine($"Warning: Missing 'file.url' property in music file. Skipping.");
                        continue;
                    }

                    string url = urlElement.GetString();
                    if (string.IsNullOrEmpty(url))
                    {
                        Console.WriteLine($"Warning: URL is null or empty. Skipping.");
                        continue;
                    }

                    if (!musicFile.TryGetProperty("track", out var trackElement))
                    {
                        Console.WriteLine($"Warning: Missing 'track' property in music file. Skipping.");
                        continue;
                    }

                    var track = trackElement.GetInt32();

                    if (track == 0 || url.EndsWith(".zip"))
                        continue;

                    if (!musicFile.TryGetProperty("duration", out var durationElement))
                    {
                        Console.WriteLine($"Warning: Missing 'duration' property. Using default value.");
                    }
                    var duration = durationElement.ValueKind != JsonValueKind.Undefined ? durationElement.GetDouble() : 0.0;

                    if (!musicFile.TryGetProperty("title", out var titleElement))
                    {
                        Console.WriteLine($"Warning: Missing 'title' property. Using default value.");
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
}
