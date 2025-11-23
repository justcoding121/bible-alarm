using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Audio.Links.Harvestor.Models.Bible;
using Bible.Alarm.Audio.Links.Harvestor.Utility;
using Bible.Alarm.Shared.Constants;
using System.Text.Json;

namespace Bible.Alarm.Audio.Links.Harvestor.Harvestors.Bible
{
    internal class JwBibleHarvester
    {
        // Maximum concurrent language downloads to avoid overwhelming network/CPU
        private const int MaxConcurrentLanguageDownloads = 8;

        internal async static Task Harvest_Bible_Links(
            Dictionary<string, string> biblePublicationCodeToNameMappings,
            ConcurrentDictionary<string, string> languageCodeToNameMappings,
            ConcurrentDictionary<string, List<string>> languageCodeToEditionsMapping,
            bool isTestRun = false)
        {
            foreach (var publication in biblePublicationCodeToNameMappings)
            {
                var publicationCode = publication.Key;
                Console.WriteLine($"Starting harvest for publication: {publicationCode} ({publication.Value})");

                Dictionary<string, string> discoveredLanguages = await DiscoverLanguagesFromApi(publicationCode);
                if (discoveredLanguages.Count == 0)
                {
                    continue;
                }

                // Filter out sign languages and songs
                var filteredLanguages = discoveredLanguages
                    .Where(lang => !ShouldSkipLanguage(lang.Value))
                    .ToDictionary(x => x.Key, x => x.Value);

                if (filteredLanguages.Count == 0)
                {
                    Console.WriteLine($"No valid languages found for publication {publicationCode} after filtering.");
                    continue;
                }

                // In test run mode, only process English ("E") language
                if (isTestRun)
                {
                    if (filteredLanguages.TryGetValue("E", out var englishName))
                    {
                        Console.WriteLine($"TEST RUN: Processing only English language for publication {publicationCode}");
                        filteredLanguages = new Dictionary<string, string> { ["E"] = englishName };
                    }
                    else
                    {
                        Console.WriteLine($"TEST RUN: English language not found for publication {publicationCode}. Skipping.");
                        continue;
                    }
                }

                // Process discovered languages in parallel with throttling
                using var semaphore = new SemaphoreSlim(MaxConcurrentLanguageDownloads, MaxConcurrentLanguageDownloads);
                var languageTasks = filteredLanguages.Select(async langEntry =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var languageCode = langEntry.Key;
                        var language = langEntry.Value;

                        languageCodeToNameMappings.TryAdd(languageCode, language);

                        Console.WriteLine($"Harvesting Bible chapter links for {publication.Value} of {language} language.");
                        await HarvestBibleLinks(languageCode, publicationCode);

                        if (!languageCodeToEditionsMapping.TryAdd(languageCode, new List<string>([publication.Key])))
                        {
                            languageCodeToEditionsMapping[languageCode].Add(publication.Key);
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(languageTasks);
            }
        }

        private static bool ShouldSkipLanguage(string languageName)
        {
            if (string.IsNullOrWhiteSpace(languageName))
                return false;

            var lowerName = languageName.ToLowerInvariant();
            return lowerName.Contains("sign language");
        }


        private static async Task<Dictionary<string, string>> DiscoverLanguagesFromApi(string publicationCode)
        {
            var discoveredLanguages = new Dictionary<string, string>();
            var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&fileformat=MP3&alllangs=1&langwritten=E&txtCMSLang=E";

            var jsonString = await DownloadUtility.GetAsync(harvestLink);
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return discoveredLanguages;
            }

            if (!root.TryGetProperty("languages", out var languages))
            {
                return discoveredLanguages;
            }

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

                discoveredLanguages[languageCode] = language;
            }

            return discoveredLanguages;
        }

        private static async Task<bool> HarvestBibleLinks(string languageCode, string publicationCode)
        {
            var booksDirectory = $"{DirectoryHelper.IndexDirectory}/media/Audio/Bible/{languageCode}/{publicationCode}";
            var booksIndex = $"{booksDirectory}/books.json";

            var bookNumberBookMap = new Dictionary<int, BibleBook>();
            var bookNumberChapterMap = new Dictionary<int, Dictionary<int, BibleChapter>>();

            var bookNumber = 1;
            var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&booknum={bookNumber}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang=E";

            while (bookNumber <= 66)
            {
                var jsonString = await DownloadUtility.GetAsync(harvestLink);
                JsonDocument doc = null;
                JsonElement files = default;

                try
                {
                    doc = JsonDocument.Parse(jsonString);
                    var root = doc.RootElement;

                    if (root.ValueKind != JsonValueKind.Object)
                    {
                        AdvanceToNextBook(ref bookNumber, ref harvestLink, publicationCode, languageCode);
                        doc?.Dispose();
                        continue;
                    }

                    if (!root.TryGetProperty("files", out files))
                    {
                        AdvanceToNextBook(ref bookNumber, ref harvestLink, publicationCode, languageCode);
                        doc?.Dispose();
                        continue;
                    }
                }
                catch (Exception e)
                {
                    if (e is JsonException or ArgumentException or KeyNotFoundException or InvalidOperationException)
                    {
                        AdvanceToNextBook(ref bookNumber, ref harvestLink, publicationCode, languageCode);
                        doc?.Dispose();
                        continue;
                    }
                    throw;
                }

                try
                {
                    if (!files.TryGetProperty(languageCode, out var languageFiles))
                    {
                        AdvanceToNextBook(ref bookNumber, ref harvestLink, publicationCode, languageCode);
                        continue;
                    }

                    if (!languageFiles.TryGetProperty("MP3", out var bookFiles))
                    {
                        AdvanceToNextBook(ref bookNumber, ref harvestLink, publicationCode, languageCode);
                        continue;
                    }

                    ProcessBookFiles(bookFiles, doc.RootElement, harvestLink, bookNumberBookMap, bookNumberChapterMap, ref bookNumber);
                }
                catch (KeyNotFoundException)
                {
                    AdvanceToNextBook(ref bookNumber, ref harvestLink, publicationCode, languageCode);
                }
                finally
                {
                    doc?.Dispose();
                }

                if (harvestLink.Contains("booknum="))
                {
                    bookNumber++;
                }

                harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&booknum={bookNumber}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang=E";
            }

            if (bookNumberBookMap.Count > 0)
            {
                SaveBooksAndChapters(booksDirectory, booksIndex, bookNumberBookMap, bookNumberChapterMap);
                return true;
            }

            return false;
        }

        private static void ProcessBookFiles(
            JsonElement bookFiles,
            JsonElement rootElement,
            string harvestLink,
            Dictionary<int, BibleBook> bookNumberBookMap,
            Dictionary<int, Dictionary<int, BibleChapter>> bookNumberChapterMap,
            ref int bookNumber)
        {
            foreach (var bookFile in bookFiles.EnumerateArray())
            {
                if (!bookFile.TryGetProperty("file", out var fileElement) ||
                    !fileElement.TryGetProperty("url", out var urlElement))
                {
                    continue;
                }

                string url = urlElement.GetString();
                if (string.IsNullOrEmpty(url))
                {
                    continue;
                }

                if (!bookFile.TryGetProperty("track", out var trackElement))
                {
                    continue;
                }

                var track = trackElement.GetInt32();
                if (track == 0 || url.EndsWith(".zip"))
                {
                    continue;
                }

                if (!bookFile.TryGetProperty("booknum", out var bookNumElement))
                {
                    continue;
                }

                bookNumber = bookNumElement.GetInt32();

                if (!bookNumberBookMap.ContainsKey(bookNumber))
                {
                    var bookName = GetBookName(bookFile, rootElement, harvestLink);
                    if (string.IsNullOrEmpty(bookName))
                    {
                        continue;
                    }

                    bookNumberBookMap[bookNumber] = new BibleBook
                    {
                        Number = bookNumber,
                        Name = bookName
                    };
                }

                var trackNumber = track;
                if (!bookNumberChapterMap.ContainsKey(bookNumber))
                {
                    bookNumberChapterMap[bookNumber] = new Dictionary<int, BibleChapter>();
                }

                if (!bookNumberChapterMap[bookNumber].ContainsKey(trackNumber))
                {
                    bookNumberChapterMap[bookNumber].Add(trackNumber, new BibleChapter
                    {
                        Number = trackNumber,
                        Url = url,
                    });
                }
            }
        }

        private static string GetBookName(JsonElement bookFile, JsonElement rootElement, string harvestLink)
        {
            string name;

            if (harvestLink.Contains("booknum="))
            {
                if (!rootElement.TryGetProperty("pubName", out var pubNameElement))
                {
                    return null;
                }
                name = pubNameElement.GetString()!;
            }
            else
            {
                if (!bookFile.TryGetProperty("title", out var titleElement))
                {
                    return null;
                }
                name = titleElement.GetString()!.Split('-')[0].Trim();
            }

            return name == "Psalm 1" ? "Psalms" : name;
        }

        private static void AdvanceToNextBook(ref int bookNumber, ref string harvestLink, string publicationCode, string languageCode)
        {
            bookNumber++;
            harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&booknum={bookNumber}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang=E";
        }

        private static void SaveBooksAndChapters(
            string booksDirectory,
            string booksIndex,
            Dictionary<int, BibleBook> bookNumberBookMap,
            Dictionary<int, Dictionary<int, BibleChapter>> bookNumberChapterMap)
        {
            if (!Directory.Exists(booksDirectory))
            {
                Directory.CreateDirectory(booksDirectory);
            }

            File.WriteAllText(booksIndex, JsonSerializer.Serialize(bookNumberBookMap.Select(x =>
                new BibleBook
                {
                    Number = x.Key,
                    Name = x.Value.Name
                }).OrderBy(x => x.Number)));

            foreach (var book in bookNumberBookMap)
            {
                var directory = $"{booksDirectory}/{book.Value.Number}";
                DirectoryHelper.Ensure(directory);

                var chapterIndex = $"{directory}/chapters.json";
                File.WriteAllText(chapterIndex, JsonSerializer.Serialize(
                    bookNumberChapterMap[book.Key]
                        .Select(x => x.Value)
                        .OrderBy(x => x.Number)
                        .ToList()));
            }
        }
    }
}
